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
