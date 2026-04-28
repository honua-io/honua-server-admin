using Honua.Admin.Models.SpecWorkspace;
using Honua.Admin.Services.SpecWorkspace;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class SpecWorkspaceStateTests
{
    [Fact]
    public async Task InsertDslTokenAsync_replaces_the_active_editor_selection()
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels");
        await state.UpdateSectionTextAsync(SpecSectionId.Compute, "aggregate inputs=@parcels by= metric=count");

        state.SetActiveDslSection(SpecSectionId.Compute);
        state.SetDslSelection(SpecSectionId.Compute, "aggregate inputs=@parcels by=".Length, "aggregate inputs=@parcels by=".Length);

        await state.InsertDslTokenAsync("@parcels.county");

        Assert.Contains("@parcels.county", state.GetSectionText(SpecSectionId.Compute), StringComparison.Ordinal);
        Assert.Single(state.Spec.Compute);
        Assert.Equal("@parcels.county", state.Spec.Compute[0].Args["by"]);
    }

    [Fact]
    public async Task Compute_section_round_trips_filter_values_containing_whitespace()
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");

        var outcome = await state.AnswerClarificationAsync(
            "pick-value:filter:parcels:county",
            "Big Island");

        Assert.Equal(IntentResponseKind.Mutation, outcome.Kind);
        Assert.Single(state.Spec.Compute);
        Assert.Equal("@parcels.county=Big Island", state.Spec.Compute[0].Args["where"]);

        var serialized = state.GetSectionText(SpecSectionId.Compute);
        await state.UpdateSectionTextAsync(SpecSectionId.Compute, serialized);

        Assert.Single(state.Spec.Compute);
        Assert.Equal("@parcels.county=Big Island", state.Spec.Compute[0].Args["where"]);
        Assert.DoesNotContain(state.Diagnostics, d => d.Code == "invalid-compute-token");
    }

    [Fact]
    public async Task ClearDraftAsync_cancels_in_flight_apply_and_leaves_idle_empty_state()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new BlockingApplyClient();
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();

        var applyTask = state.RunApplyAsync();
        await client.FirstEventEmitted.Task;

        Assert.Equal(WorkspaceStatus.Applying, state.Status);

        await state.ClearDraftAsync();
        await applyTask;

        Assert.Equal(WorkspaceStatus.Idle, state.Status);
        Assert.Empty(state.ApplyEvents);
        Assert.Null(state.ActiveJobId);
        Assert.Equal(SpecDocument.Empty, state.Spec);
    }

    [Fact]
    public async Task RunApplyAsync_ignores_second_start_while_apply_is_active()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new BlockingApplyClient();
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();

        var firstApplyTask = state.RunApplyAsync();
        await client.FirstEventEmitted.Task;

        var secondApplyTask = state.RunApplyAsync();
        var secondReturned = await Task.WhenAny(secondApplyTask, Task.Delay(250)) == secondApplyTask;

        try
        {
            Assert.True(secondReturned);
            Assert.Equal(1, client.ApplyStartCount);
            Assert.Equal(WorkspaceStatus.Applying, state.Status);
            Assert.Single(state.ApplyEvents, e => e.Kind == ApplyEventKind.Started);
        }
        finally
        {
            client.ReleaseApplies();
            await Task.WhenAll(firstApplyTask, secondApplyTask);
        }
    }

    [Fact]
    public async Task RunPlanAsync_ignores_plan_start_while_apply_is_active()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new BlockingApplyClient();
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();

        var planCallsBeforeApply = client.PlanCallCount;
        var applyTask = state.RunApplyAsync();
        await client.FirstEventEmitted.Task;

        try
        {
            await state.RunPlanAsync();

            Assert.Equal(planCallsBeforeApply, client.PlanCallCount);
            Assert.Equal(WorkspaceStatus.Applying, state.Status);
        }
        finally
        {
            client.ReleaseApplies();
            await applyTask;
        }
    }

    [Fact]
    public async Task RunPlanAsync_surfaces_client_failure_and_returns_to_error_state()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new ThrowingClient { PlanFailure = new InvalidOperationException("plan rpc down") };
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");

        await state.RunPlanAsync();

        Assert.Equal(WorkspaceStatus.Error, state.Status);
        Assert.NotNull(state.PlanResult);
        Assert.True(state.PlanResult!.Failed);
        Assert.Equal("plan rpc down", state.PlanResult.FailureMessage);

        // A subsequent plan must not be blocked by the stuck status.
        client.PlanFailure = null;
        await state.RunPlanAsync();
        Assert.Equal(WorkspaceStatus.Idle, state.Status);
        Assert.False(state.PlanResult!.Failed);
    }

    [Fact]
    public async Task RunApplyAsync_surfaces_stream_failure_as_failed_event_and_error_status()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new ThrowingClient { ApplyStreamFailure = new InvalidOperationException("apply stream blew up") };
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();

        await state.RunApplyAsync();

        Assert.Equal(WorkspaceStatus.Error, state.Status);
        Assert.Contains(state.ApplyEvents, e => e.Kind == ApplyEventKind.Failed && e.Message == "apply stream blew up");
        Assert.Null(state.ActiveJobId);
    }

    [Fact]
    public async Task CancelApplyAsync_terminates_local_stream_even_when_remote_cancel_throws()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new BlockingApplyClient { CancelFailure = new InvalidOperationException("cancel rpc down") };
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();

        var applyTask = state.RunApplyAsync();
        await client.FirstEventEmitted.Task;
        Assert.Equal(WorkspaceStatus.Applying, state.Status);

        await state.CancelApplyAsync();
        await applyTask;

        Assert.Equal(WorkspaceStatus.Idle, state.Status);
        Assert.Null(state.ActiveJobId);
        Assert.Contains(state.ApplyEvents, e => e.Kind == ApplyEventKind.Cancelled);
    }

    [Fact]
    public async Task Spec_edit_during_in_flight_plan_discards_stale_plan_result()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new BlockingPlanClient();
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");

        var planTask = state.RunPlanAsync();
        await client.PlanEntered.Task;

        Assert.Equal(WorkspaceStatus.Planning, state.Status);

        // Mutate the spec while the plan client is still blocked — any result it
        // returns is for the superseded spec and must be discarded.
        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels pin=v2");

        client.ReleasePlan();
        await planTask;

        Assert.Null(state.PlanResult);
        Assert.Equal(WorkspaceStatus.Idle, state.Status);
    }

    [Fact]
    public async Task Spec_edit_during_in_flight_apply_supersedes_apply_and_returns_to_idle()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new BlockingApplyClient();
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();

        var applyTask = state.RunApplyAsync();
        await client.FirstEventEmitted.Task;
        Assert.Equal(WorkspaceStatus.Applying, state.Status);

        // Mutate the spec mid-stream — subsequent events from the still-running
        // apply are for the superseded spec and should not land.
        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels pin=v2");

        // Releasing the apply lets it produce a Completed event; the revision check
        // in DriveApplyAsync must cancel the producer before that lands.
        client.ReleaseApplies();
        await applyTask;

        Assert.Equal(WorkspaceStatus.Idle, state.Status);
        Assert.Null(state.ActiveJobId);
        Assert.Empty(state.ApplyEvents);
    }

    [Fact]
    public async Task ClearDraftAsync_waits_for_in_flight_plan_and_leaves_idle_empty_state()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new BlockingPlanClient();
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");

        var planTask = state.RunPlanAsync();
        await client.PlanEntered.Task;

        Assert.Equal(WorkspaceStatus.Planning, state.Status);

        var clearTask = state.ClearDraftAsync();
        client.ReleasePlan();
        await planTask;
        await clearTask;

        Assert.Null(state.PlanResult);
        Assert.Equal(WorkspaceStatus.Idle, state.Status);
        Assert.Equal(SpecDocument.Empty, state.Spec);
        Assert.Empty(state.Conversation);
    }

    [Theory]
    [InlineData("county", "@parcels.county")]
    [InlineData("@parcels.county", "@parcels.county")]
    public async Task InsertColumnTokenAsync_prefixes_bare_columns_and_leaves_qualified_tokens_alone(string column, string expectedToken)
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.UpdateSectionTextAsync(SpecSectionId.Compute, string.Empty);

        state.SetActiveDslSection(SpecSectionId.Compute);
        state.SetDslSelection(SpecSectionId.Compute, 0, 0);

        await state.InsertColumnTokenAsync(column);

        Assert.Equal(expectedToken, state.GetSectionText(SpecSectionId.Compute));
    }

    [Fact]
    public async Task Map_section_with_duplicate_field_emits_diagnostic_without_throwing()
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels");

        // Repeated `source=` used to crash ParseMap via ToDictionary; the editor
        // must stay alive and surface a red diagnostic instead.
        await state.UpdateSectionTextAsync(SpecSectionId.Map, "layer source=@parcels source=@wells symbology=viridis");

        Assert.Contains(state.GetDiagnostics(SpecSectionId.Map), d =>
            d.Code == "duplicate-map-field" && d.Severity == ValidationSeverity.Red);
    }

    [Fact]
    public async Task Spec_mutation_via_text_edit_invalidates_prior_plan_and_apply_results()
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();
        await state.RunApplyAsync();

        Assert.NotNull(state.PlanResult);
        Assert.NotEmpty(state.ApplyEvents);
        Assert.Contains(state.ApplyEvents, e => e.Kind == ApplyEventKind.Completed);

        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels pin=v2");

        Assert.Null(state.PlanResult);
        Assert.Empty(state.ApplyEvents);
    }

    [Fact]
    public async Task DraftChanges_track_edits_against_the_last_successful_plan()
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels");

        Assert.Equal(SpecChangeStatus.Added, ChangeFor(state, SpecSectionId.Sources).Status);
        Assert.True(state.HasDraftChanges);

        await state.RunPlanAsync();

        Assert.Equal(SpecChangeStatus.Unchanged, ChangeFor(state, SpecSectionId.Sources).Status);
        Assert.False(state.HasDraftChanges);

        await state.UpdateSectionTextAsync(SpecSectionId.Compute, "aggregate inputs=@parcels by=@parcels.county metric=count");
        await state.UpdateSectionTextAsync(SpecSectionId.Sources, string.Empty);

        Assert.Equal(SpecChangeStatus.Added, ChangeFor(state, SpecSectionId.Compute).Status);
        Assert.Equal(SpecChangeStatus.Removed, ChangeFor(state, SpecSectionId.Sources).Status);
        Assert.True(state.HasDraftChanges);
    }

    [Fact]
    public async Task RunApplyAsync_replans_after_spec_edit_invalidates_prior_plan()
    {
        var storage = new MemoryBrowserStorageService();
        var client = new CountingPlanClient();
        var state = new SpecWorkspaceState(
            client,
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();
        var plansBeforeEdit = client.PlanCallCount;

        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels pin=v2");
        Assert.Null(state.PlanResult);

        await state.RunApplyAsync();

        Assert.Equal(plansBeforeEdit + 1, client.PlanCallCount);
        Assert.NotNull(state.PlanResult);
    }

    [Fact]
    public async Task Spec_mutation_via_clarification_invalidates_prior_plan_and_apply_results()
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.SubmitPromptAsync("aggregate count of @parcels by county");
        await state.RunPlanAsync();
        await state.RunApplyAsync();

        Assert.NotNull(state.PlanResult);
        Assert.NotEmpty(state.ApplyEvents);

        await state.AnswerClarificationAsync("pick-value:filter:parcels:county", "Alpha");

        Assert.Null(state.PlanResult);
        Assert.Empty(state.ApplyEvents);
    }

    [Fact]
    public async Task InitializeAsync_rehydrates_persisted_workspace_snapshot()
    {
        var storage = new MemoryBrowserStorageService();
        var first = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await first.InitializeAsync("operator");
        first.SetPromptDraft("aggregate count of @parcels by county");
        first.SetLayout(new LayoutWidths(30, 35, 35));
        await first.SubmitPromptAsync("aggregate count of @parcels by county");

        var second = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await second.InitializeAsync("operator");

        Assert.Equal("aggregate count of @parcels by county", second.PromptDraft);
        Assert.Equal(new LayoutWidths(30, 35, 35), second.Layout);
        Assert.Single(second.Conversation);
        Assert.Contains("@parcels = parcels", second.GetSectionText(SpecSectionId.Sources), StringComparison.Ordinal);
    }

    private static SpecSectionChange ChangeFor(SpecWorkspaceState state, SpecSectionId section) =>
        state.DraftChanges.Single(change => change.Section == section);

    private sealed class BlockingApplyClient : ISpecWorkspaceClient
    {
        private readonly StubSpecWorkspaceClient _inner = new();
        private readonly TaskCompletionSource _releaseApplies = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _applyStartCount;
        private int _planCallCount;

        public TaskCompletionSource FirstEventEmitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ApplyStartCount => _applyStartCount;

        public int PlanCallCount => _planCallCount;

        public void ReleaseApplies() => _releaseApplies.TrySetResult();

        public Task<IntentOutcome> SubmitIntentAsync(IntentRequest request, CancellationToken cancellationToken) =>
            _inner.SubmitIntentAsync(request, cancellationToken);

        public Task<IReadOnlyList<CatalogCandidate>> ResolveCatalogAsync(ResolveQuery query, CancellationToken cancellationToken) =>
            _inner.ResolveCatalogAsync(query, cancellationToken);

        public Task<PlanResult> PlanAsync(SpecDocument document, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _planCallCount);
            return _inner.PlanAsync(document, cancellationToken);
        }

        public async IAsyncEnumerable<ApplyEvent> ApplyAsync(
            SpecDocument document,
            string jobId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _applyStartCount);
            yield return new ApplyEvent { Kind = ApplyEventKind.Started, JobId = jobId };
            FirstEventEmitted.TrySetResult();

            var cancelled = false;
            try
            {
                await _releaseApplies.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            if (cancelled)
            {
                yield return new ApplyEvent
                {
                    Kind = ApplyEventKind.Cancelled,
                    JobId = jobId,
                    Message = "cancelled"
                };
            }
            else
            {
                yield return new ApplyEvent
                {
                    Kind = ApplyEventKind.Completed,
                    JobId = jobId
                };
            }
        }

        public Exception? CancelFailure { get; set; }

        public Task CancelAsync(string jobId, CancellationToken cancellationToken)
        {
            if (CancelFailure is not null)
            {
                return Task.FromException(CancelFailure);
            }

            return _inner.CancelAsync(jobId, cancellationToken);
        }

        public Task<string> SummarizeSectionAsync(SpecDocument document, SpecSectionId section, CancellationToken cancellationToken) =>
            _inner.SummarizeSectionAsync(document, section, cancellationToken);

        public Task<SpecGrammar> LoadGrammarAsync(CancellationToken cancellationToken) =>
            _inner.LoadGrammarAsync(cancellationToken);

        public IReadOnlyList<ValidationDiagnostic> Validate(SpecDocument document) => _inner.Validate(document);
    }

    private sealed class ThrowingClient : ISpecWorkspaceClient
    {
        private readonly StubSpecWorkspaceClient _inner = new();

        public Exception? PlanFailure { get; set; }

        public Exception? ApplyStreamFailure { get; set; }

        public Task<IntentOutcome> SubmitIntentAsync(IntentRequest request, CancellationToken cancellationToken) =>
            _inner.SubmitIntentAsync(request, cancellationToken);

        public Task<IReadOnlyList<CatalogCandidate>> ResolveCatalogAsync(ResolveQuery query, CancellationToken cancellationToken) =>
            _inner.ResolveCatalogAsync(query, cancellationToken);

        public Task<PlanResult> PlanAsync(SpecDocument document, CancellationToken cancellationToken)
        {
            if (PlanFailure is not null)
            {
                return Task.FromException<PlanResult>(PlanFailure);
            }

            return _inner.PlanAsync(document, cancellationToken);
        }

        public async IAsyncEnumerable<ApplyEvent> ApplyAsync(
            SpecDocument document,
            string jobId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new ApplyEvent { Kind = ApplyEventKind.Started, JobId = jobId };

            if (ApplyStreamFailure is not null)
            {
                throw ApplyStreamFailure;
            }

            await foreach (var evt in _inner.ApplyAsync(document, jobId, cancellationToken).ConfigureAwait(false))
            {
                yield return evt;
            }
        }

        public Task CancelAsync(string jobId, CancellationToken cancellationToken) =>
            _inner.CancelAsync(jobId, cancellationToken);

        public Task<string> SummarizeSectionAsync(SpecDocument document, SpecSectionId section, CancellationToken cancellationToken) =>
            _inner.SummarizeSectionAsync(document, section, cancellationToken);

        public Task<SpecGrammar> LoadGrammarAsync(CancellationToken cancellationToken) =>
            _inner.LoadGrammarAsync(cancellationToken);

        public IReadOnlyList<ValidationDiagnostic> Validate(SpecDocument document) => _inner.Validate(document);
    }

    private sealed class BlockingPlanClient : ISpecWorkspaceClient
    {
        private readonly StubSpecWorkspaceClient _inner = new();
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource PlanEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleasePlan() => _release.TrySetResult();

        public Task<IntentOutcome> SubmitIntentAsync(IntentRequest request, CancellationToken cancellationToken) =>
            _inner.SubmitIntentAsync(request, cancellationToken);

        public Task<IReadOnlyList<CatalogCandidate>> ResolveCatalogAsync(ResolveQuery query, CancellationToken cancellationToken) =>
            _inner.ResolveCatalogAsync(query, cancellationToken);

        public async Task<PlanResult> PlanAsync(SpecDocument document, CancellationToken cancellationToken)
        {
            PlanEntered.TrySetResult();
            await _release.Task.ConfigureAwait(false);
            return await _inner.PlanAsync(document, cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<ApplyEvent> ApplyAsync(SpecDocument document, string jobId, CancellationToken cancellationToken) =>
            _inner.ApplyAsync(document, jobId, cancellationToken);

        public Task CancelAsync(string jobId, CancellationToken cancellationToken) =>
            _inner.CancelAsync(jobId, cancellationToken);

        public Task<string> SummarizeSectionAsync(SpecDocument document, SpecSectionId section, CancellationToken cancellationToken) =>
            _inner.SummarizeSectionAsync(document, section, cancellationToken);

        public Task<SpecGrammar> LoadGrammarAsync(CancellationToken cancellationToken) =>
            _inner.LoadGrammarAsync(cancellationToken);

        public IReadOnlyList<ValidationDiagnostic> Validate(SpecDocument document) => _inner.Validate(document);
    }

    private sealed class CountingPlanClient : ISpecWorkspaceClient
    {
        private readonly StubSpecWorkspaceClient _inner = new();
        private int _planCallCount;

        public int PlanCallCount => _planCallCount;

        public Task<IntentOutcome> SubmitIntentAsync(IntentRequest request, CancellationToken cancellationToken) =>
            _inner.SubmitIntentAsync(request, cancellationToken);

        public Task<IReadOnlyList<CatalogCandidate>> ResolveCatalogAsync(ResolveQuery query, CancellationToken cancellationToken) =>
            _inner.ResolveCatalogAsync(query, cancellationToken);

        public Task<PlanResult> PlanAsync(SpecDocument document, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _planCallCount);
            return _inner.PlanAsync(document, cancellationToken);
        }

        public IAsyncEnumerable<ApplyEvent> ApplyAsync(SpecDocument document, string jobId, CancellationToken cancellationToken) =>
            _inner.ApplyAsync(document, jobId, cancellationToken);

        public Task CancelAsync(string jobId, CancellationToken cancellationToken) =>
            _inner.CancelAsync(jobId, cancellationToken);

        public Task<string> SummarizeSectionAsync(SpecDocument document, SpecSectionId section, CancellationToken cancellationToken) =>
            _inner.SummarizeSectionAsync(document, section, cancellationToken);

        public Task<SpecGrammar> LoadGrammarAsync(CancellationToken cancellationToken) =>
            _inner.LoadGrammarAsync(cancellationToken);

        public IReadOnlyList<ValidationDiagnostic> Validate(SpecDocument document) => _inner.Validate(document);
    }

    private sealed class MemoryBrowserStorageService : IBrowserStorageService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NullSpecWorkspaceTelemetry : ISpecWorkspaceTelemetry
    {
        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }
}
