using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.SpecWorkspace;

namespace Honua.Admin.Services.SpecWorkspace;

public enum WorkspaceStatus
{
    Idle,
    Planning,
    Applying,
    Cancelling,
    Error
}

/// <summary>
/// Scoped observable store that backs the three panes. All mutations funnel through
/// explicit methods so persistence, telemetry, and preview updates stay single-origin.
/// </summary>
public sealed class SpecWorkspaceState : IAsyncDisposable
{
    private const string StorageKeyPrefix = "spec-workspace:";

    private readonly ISpecWorkspaceClient _client;
    private readonly IBrowserStorageService _storage;
    private readonly ISpecWorkspaceTelemetry _telemetry;
    private readonly CatalogCache _catalog;

    private readonly List<ConversationTurn> _conversation = new();
    private readonly List<ApplyEvent> _applyEvents = new();
    private readonly Dictionary<string, string> _sectionSummaries = new(StringComparer.Ordinal);
    private readonly Dictionary<SpecSectionId, string> _sectionTexts = new();
    private readonly Dictionary<SpecSectionId, TextSelectionState> _dslSelections = new();
    private readonly Dictionary<SpecSectionId, IReadOnlyList<ValidationDiagnostic>> _editorDiagnosticsBySection = new();
    private readonly List<ValidationDiagnostic> _diagnostics = new();
    private readonly SemaphoreSlim _workGate = new(1, 1);

    private CancellationTokenSource? _applyCts;
    private string? _activeJobId;
    private Task? _applyTask;
    private Task? _planTask;
    private int _specRevision;
    private bool _disposed;
    private bool _rehydrated;

    public SpecWorkspaceState(
        ISpecWorkspaceClient client,
        IBrowserStorageService storage,
        ISpecWorkspaceTelemetry telemetry,
        CatalogCache catalog)
    {
        _client = client;
        _storage = storage;
        _telemetry = telemetry;
        _catalog = catalog;
        SyncSectionTextsFromSpec();
    }

    public string PrincipalId { get; private set; } = "operator";

    public SpecDocument Spec { get; private set; } = SpecDocument.Empty;

    public string TextView { get; private set; } = string.Empty;

    public IReadOnlyList<ConversationTurn> Conversation => _conversation;

    public PlanResult? PlanResult { get; private set; }

    public IReadOnlyList<ApplyEvent> ApplyEvents => _applyEvents;

    public WorkspaceStatus Status { get; private set; } = WorkspaceStatus.Idle;

    public LayoutWidths Layout { get; private set; } = LayoutWidths.Default;

    public bool IsJsonView { get; private set; }

    public SpecGrammar? Grammar { get; private set; }

    public string PromptDraft { get; private set; } = string.Empty;

    public string? ActiveJobId => _activeJobId;

    public SpecSectionId ActiveDslSection { get; private set; } = SpecSectionId.Compute;

    public IReadOnlyList<ValidationDiagnostic> Diagnostics => _diagnostics;

    public event Action? OnChanged;

    public string GetSectionSummary(SpecSectionId section) =>
        _sectionSummaries.TryGetValue(section.ToString(), out var summary) ? summary : string.Empty;

    public IReadOnlyList<ValidationDiagnostic> GetDiagnostics(SpecSectionId section) =>
        _diagnostics.Where(d => d.Section == section).ToArray();

    public string GetSectionText(SpecSectionId section) =>
        _sectionTexts.TryGetValue(section, out var value) ? value : string.Empty;

    public async Task InitializeAsync(string principalId, CancellationToken cancellationToken = default)
    {
        PrincipalId = principalId;
        _catalog.SetPrincipal(principalId);
        Grammar = await _client.LoadGrammarAsync(cancellationToken).ConfigureAwait(false);

        if (!_rehydrated)
        {
            await RehydrateFromStorageAsync(cancellationToken).ConfigureAwait(false);
            _rehydrated = true;
        }

        if (_sectionTexts.Count == 0)
        {
            SyncSectionTextsFromSpec();
        }

        await RefreshSectionSummariesAsync(cancellationToken).ConfigureAwait(false);
        RefreshTextView();
        RefreshDiagnostics();
        Notify();
    }

    public void SetPromptDraft(string? value)
    {
        PromptDraft = value ?? string.Empty;
        _ = PersistAsync(CancellationToken.None);
        Notify();
    }

    public void SetActiveDslSection(SpecSectionId section)
    {
        ActiveDslSection = section;
    }

    public void SetDslSelection(SpecSectionId section, int start, int end)
    {
        ActiveDslSection = section;
        _dslSelections[section] = new TextSelectionState(start, end);
    }

    public async Task UpdateSectionTextAsync(SpecSectionId section, string? text, CancellationToken cancellationToken = default)
    {
        ActiveDslSection = section;
        _sectionTexts[section] = text ?? string.Empty;

        var parse = SpecSectionTextTranslator.ParseSection(Spec, section, _sectionTexts[section]);
        Spec = parse.Document;
        InvalidatePlanAndApply();
        _editorDiagnosticsBySection[section] = parse.Diagnostics;

        RefreshTextView();
        RefreshDiagnostics();
        await RefreshSectionSummaryAsync(section, cancellationToken).ConfigureAwait(false);
        await PersistAsync(cancellationToken).ConfigureAwait(false);
        Notify();
    }

    public async Task InsertDslTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var section = ActiveDslSection;
        var current = GetSectionText(section);
        var selection = _dslSelections.TryGetValue(section, out var range)
            ? range
            : new TextSelectionState(current.Length, current.Length);

        var start = Math.Clamp(selection.Start, 0, current.Length);
        var end = Math.Clamp(selection.End, start, current.Length);
        var updated = current[..start] + token + current[end..];

        _dslSelections[section] = new TextSelectionState(start + token.Length, start + token.Length);
        await UpdateSectionTextAsync(section, updated, cancellationToken).ConfigureAwait(false);
    }

    public Task InsertColumnTokenAsync(string column, CancellationToken cancellationToken = default) =>
        InsertDslTokenAsync(QualifyColumnToken(column, Spec), cancellationToken);

    internal static string QualifyColumnToken(string column, SpecDocument spec)
    {
        // Aggregate payloads surface the group-by column as a fully qualified
        // token (e.g. `@parcels.county`). Inserting `@{source}.{column}` on top
        // of that would produce `@parcels.@parcels.county`, so pass an already
        // qualified token through as-is and only prefix bare column names.
        if (column.StartsWith('@'))
        {
            return column;
        }

        var sourceId = spec.Sources.FirstOrDefault()?.Id ?? "source";
        return $"@{sourceId}.{column}";
    }

    public async Task<IntentOutcome> SubmitPromptAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var request = new IntentRequest
        {
            Prompt = prompt,
            CurrentSpec = Spec
        };

        _telemetry.Record("spec_prompt_submitted", new Dictionary<string, object?>
        {
            ["prompt_length"] = prompt.Length
        });

        var outcome = await _client.SubmitIntentAsync(request, cancellationToken).ConfigureAwait(false);
        await RecordTurnAsync(prompt, outcome, cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    public async Task<IntentOutcome> AnswerClarificationAsync(string clarificationId, string value, CancellationToken cancellationToken = default)
    {
        var request = new IntentRequest
        {
            Prompt = $"clarification:{clarificationId}={value}",
            CurrentSpec = Spec,
            ClarificationId = clarificationId,
            ClarificationValue = value
        };

        var outcome = await _client.SubmitIntentAsync(request, cancellationToken).ConfigureAwait(false);
        await RecordTurnAsync(request.Prompt, outcome, cancellationToken).ConfigureAwait(false);
        return outcome;
    }

    public async Task RunPlanAsync(CancellationToken cancellationToken = default)
    {
        if (!await TryEnterActiveWorkAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await RunPlanCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _workGate.Release();
        }
    }

    private async Task RunPlanCoreAsync(CancellationToken cancellationToken)
    {
        var task = DrivePlanAsync(cancellationToken);
        _planTask = task;
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_planTask, task))
            {
                _planTask = null;
            }
        }
    }

    private async Task DrivePlanAsync(CancellationToken cancellationToken)
    {
        Status = WorkspaceStatus.Planning;
        Notify();

        var watch = Stopwatch.StartNew();
        var startRevision = _specRevision;
        _telemetry.Record("spec_plan_started");
        var result = await _client.PlanAsync(Spec, cancellationToken).ConfigureAwait(false);
        watch.Stop();

        if (startRevision != _specRevision)
        {
            // Spec changed while the plan was running — the result targets a superseded
            // spec, so drop it. Flip the status back to Idle so the user can re-plan.
            if (Status == WorkspaceStatus.Planning)
            {
                Status = WorkspaceStatus.Idle;
                Notify();
            }
            _telemetry.Record("spec_plan_superseded");
            return;
        }

        PlanResult = result;
        Status = result.Failed ? WorkspaceStatus.Error : WorkspaceStatus.Idle;
        _applyEvents.Clear();
        _telemetry.RecordLatency("spec_plan_completed", watch.ElapsedMilliseconds, new Dictionary<string, object?>
        {
            ["node_count"] = result.Nodes.Count,
            ["failed"] = result.Failed
        });
        Notify();
    }

    public async Task RunApplyAsync(CancellationToken externalCancellation = default)
    {
        if (!await TryEnterActiveWorkAsync(externalCancellation).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            if (PlanResult is null)
            {
                await RunPlanCoreAsync(externalCancellation).ConfigureAwait(false);
                if (PlanResult is null || PlanResult.Failed)
                {
                    return;
                }
            }

            await RunApplyCoreAsync(externalCancellation).ConfigureAwait(false);
        }
        finally
        {
            _workGate.Release();
        }
    }

    private async Task RunApplyCoreAsync(CancellationToken externalCancellation)
    {
        if (PlanResult is null)
        {
            await RunPlanCoreAsync(externalCancellation).ConfigureAwait(false);
            if (PlanResult is null || PlanResult.Failed)
            {
                return;
            }
        }

        _applyCts?.Dispose();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalCancellation);
        _applyCts = cts;
        _applyEvents.Clear();
        Status = WorkspaceStatus.Applying;
        var jobId = Guid.NewGuid().ToString("n");
        _activeJobId = jobId;

        _telemetry.Record("spec_apply_started", new Dictionary<string, object?>
        {
            ["job_id"] = jobId
        });
        Notify();

        var task = DriveApplyAsync(jobId, cts);
        _applyTask = task;
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            var ownsActiveJob = string.Equals(_activeJobId, jobId, StringComparison.Ordinal);
            if (ReferenceEquals(_applyTask, task))
            {
                _applyTask = null;
            }
            if (ReferenceEquals(_applyCts, cts))
            {
                _applyCts.Dispose();
                _applyCts = null;
            }
            if (ownsActiveJob)
            {
                _activeJobId = null;
                if (Status is WorkspaceStatus.Applying or WorkspaceStatus.Cancelling)
                {
                    Status = WorkspaceStatus.Idle;
                    Notify();
                }
            }
        }
    }

    private async Task DriveApplyAsync(string jobId, CancellationTokenSource cts)
    {
        var startRevision = _specRevision;
        try
        {
            await foreach (var evt in _client.ApplyAsync(Spec, jobId, cts.Token).ConfigureAwait(false))
            {
                if (startRevision != _specRevision)
                {
                    // Spec changed mid-stream — the producer is running against a
                    // superseded snapshot. Cancel it and drop any further events.
                    try
                    {
                        cts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    _telemetry.Record("spec_apply_superseded");
                    return;
                }

                if (!string.Equals(evt.JobId, jobId, StringComparison.Ordinal) || !IsCurrentApply(jobId))
                {
                    continue;
                }

                _applyEvents.Add(evt);
                _telemetry.Record("spec_apply_node_event", new Dictionary<string, object?>
                {
                    ["node_id"] = evt.NodeId,
                    ["status"] = evt.Status?.ToString()
                });

                switch (evt.Kind)
                {
                    case ApplyEventKind.Completed:
                        Status = WorkspaceStatus.Idle;
                        _telemetry.Record("spec_apply_completed");
                        break;

                    case ApplyEventKind.Cancelled:
                        Status = WorkspaceStatus.Idle;
                        _telemetry.Record("spec_apply_cancelled");
                        break;

                    case ApplyEventKind.Failed:
                        Status = WorkspaceStatus.Error;
                        _telemetry.Record("spec_apply_failed", new Dictionary<string, object?>
                        {
                            ["message"] = evt.Message
                        });
                        break;
                }

                Notify();
            }
        }
        catch (OperationCanceledException) when (IsCurrentApply(jobId) && startRevision == _specRevision)
        {
            Status = WorkspaceStatus.Idle;
            _applyEvents.Add(new ApplyEvent
            {
                Kind = ApplyEventKind.Cancelled,
                JobId = jobId,
                Message = "cancelled"
            });
            _telemetry.Record("spec_apply_cancelled");
            Notify();
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task CancelApplyAsync(CancellationToken cancellationToken = default)
    {
        var jobId = _activeJobId;
        var cts = _applyCts;
        if (jobId is null || cts is null)
        {
            return;
        }

        Status = WorkspaceStatus.Cancelling;
        Notify();
        await _client.CancelAsync(jobId, cancellationToken).ConfigureAwait(false);
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Apply already terminated and disposed its CTS; nothing to cancel.
        }
    }

    public async Task<CatalogResolution> ResolveCatalogAsync(ResolveQuery query, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var resolution = await _catalog.GetOrResolveAsync(_client, query, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        var elapsed = resolution.Cached ? resolution.ElapsedMillis : stopwatch.ElapsedMilliseconds;
        _telemetry.RecordLatency("spec_completion_latency_ms", elapsed, new Dictionary<string, object?>
        {
            ["trigger"] = query.Trigger.ToString(),
            ["cached"] = resolution.Cached,
            ["candidates"] = resolution.Candidates.Count
        });

        return resolution with { ElapsedMillis = elapsed };
    }

    public void InsertPromptToken(string token)
    {
        PromptDraft = string.Concat(PromptDraft, token);
        _ = PersistAsync(CancellationToken.None);
        Notify();
    }

    public void ToggleJsonView()
    {
        IsJsonView = !IsJsonView;
        _ = PersistAsync(CancellationToken.None);
        Notify();
    }

    public void SetLayout(LayoutWidths widths)
    {
        Layout = widths;
        _telemetry.Record("spec_layout_changed", new Dictionary<string, object?>
        {
            ["nl"] = widths.Nl,
            ["dsl"] = widths.Dsl,
            ["preview"] = widths.Preview
        });
        _ = PersistAsync(CancellationToken.None);
        Notify();
    }

    public async Task ClearDraftAsync(CancellationToken cancellationToken = default)
    {
        await StopActiveWorkAsync(cancellationToken).ConfigureAwait(false);

        await _workGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Spec = SpecDocument.Empty;
            _conversation.Clear();
            _applyEvents.Clear();
            _sectionSummaries.Clear();
            _editorDiagnosticsBySection.Clear();
            PlanResult = null;
            Status = WorkspaceStatus.Idle;
            PromptDraft = string.Empty;
            IsJsonView = false;
            SyncSectionTextsFromSpec();
            RefreshTextView();
            RefreshDiagnostics();
            await _storage.RemoveAsync(StorageKeyFor(PrincipalId), cancellationToken).ConfigureAwait(false);
            _telemetry.Record("spec_draft_cleared");
            Notify();
        }
        finally
        {
            _workGate.Release();
        }
    }

    private async Task StopActiveWorkAsync(CancellationToken cancellationToken)
    {
        var applyTask = _applyTask;
        if (applyTask is not null)
        {
            await CancelApplyAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await applyTask.ConfigureAwait(false);
            }
            catch
            {
                // The apply loop owns its own exception handling; swallow anything leaking
                // out so the reset can proceed to a clean slate.
            }
        }

        var planTask = _planTask;
        if (planTask is not null)
        {
            try
            {
                await planTask.ConfigureAwait(false);
            }
            catch
            {
                // Plan failures surface through the returned PlanResult, not exceptions;
                // swallow any transport errors so clear still reaches a clean slate.
            }
        }
    }

    public void ReplaceSpec(SpecDocument document)
    {
        Spec = document;
        InvalidatePlanAndApply();
        SyncSectionTextsFromSpec();
        _editorDiagnosticsBySection.Clear();
        RefreshTextView();
        RefreshDiagnostics();
        _ = RefreshSectionSummariesAsync(CancellationToken.None);
        _ = PersistAsync(CancellationToken.None);
        Notify();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _applyCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // CTS already disposed by the terminal-state cleanup in RunApplyAsync.
        }
        _applyCts?.Dispose();

        try
        {
            await PersistAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Browser storage is best-effort during teardown.
        }
    }

    internal async Task RehydrateFromStorageAsync(CancellationToken cancellationToken)
    {
        string? payload;
        try
        {
            payload = await _storage.GetAsync(StorageKeyFor(PrincipalId), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            payload = null;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            SyncSectionTextsFromSpec();
            return;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize(payload, SpecWorkspaceJsonContext.Default.WorkspaceSnapshot);
            if (snapshot is null)
            {
                SyncSectionTextsFromSpec();
                return;
            }

            Spec = snapshot.Spec;
            Layout = snapshot.Layout;
            PromptDraft = snapshot.PromptDraft;
            IsJsonView = snapshot.IsJsonView;

            _conversation.Clear();
            _conversation.AddRange(snapshot.Conversation);

            _sectionTexts.Clear();
            foreach (var (key, value) in snapshot.SectionTexts)
            {
                if (Enum.TryParse<SpecSectionId>(key, true, out var section))
                {
                    _sectionTexts[section] = value;
                }
            }

            _editorDiagnosticsBySection.Clear();

            if (_sectionTexts.Count == 0)
            {
                SyncSectionTextsFromSpec();
            }

            _telemetry.Record("spec_draft_rehydrated", new Dictionary<string, object?>
            {
                ["conversation_turns"] = _conversation.Count
            });
        }
        catch (JsonException)
        {
            await _storage.RemoveAsync(StorageKeyFor(PrincipalId), cancellationToken).ConfigureAwait(false);
            SyncSectionTextsFromSpec();
        }
    }

    internal async Task PersistAsync(CancellationToken cancellationToken)
    {
        var snapshot = new WorkspaceSnapshot
        {
            PrincipalId = PrincipalId,
            Spec = Spec,
            Conversation = _conversation.ToArray(),
            Layout = Layout,
            PromptDraft = PromptDraft,
            IsJsonView = IsJsonView,
            SectionTexts = _sectionTexts.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value, StringComparer.Ordinal)
        };

        var json = JsonSerializer.Serialize(snapshot, SpecWorkspaceJsonContext.Default.WorkspaceSnapshot);

        try
        {
            await _storage.SetAsync(StorageKeyFor(PrincipalId), json, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Storage is best-effort in tests and pre-render.
        }
    }

    private async Task RecordTurnAsync(string prompt, IntentOutcome outcome, CancellationToken cancellationToken)
    {
        _conversation.Add(new ConversationTurn
        {
            Id = Guid.NewGuid().ToString("n"),
            Prompt = prompt,
            Response = outcome
        });

        if (outcome.Kind == IntentResponseKind.Mutation && outcome.Mutation?.NextDocument is { } nextDocument)
        {
            Spec = nextDocument;
            InvalidatePlanAndApply();
            SyncSectionTextsFromSpec();
            _editorDiagnosticsBySection.Clear();
            RefreshTextView();
            RefreshDiagnostics();
            await RefreshSectionSummariesAsync(cancellationToken).ConfigureAwait(false);

            _telemetry.Record("spec_mutation_applied", new Dictionary<string, object?>
            {
                ["section"] = outcome.Mutation.Section.ToString(),
                ["summary"] = outcome.Mutation.Summary
            });
        }

        await PersistAsync(cancellationToken).ConfigureAwait(false);
        Notify();
    }

    private async Task RefreshSectionSummariesAsync(CancellationToken cancellationToken)
    {
        foreach (var section in Enum.GetValues<SpecSectionId>())
        {
            await RefreshSectionSummaryAsync(section, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshSectionSummaryAsync(SpecSectionId section, CancellationToken cancellationToken)
    {
        var summary = await _client.SummarizeSectionAsync(Spec, section, cancellationToken).ConfigureAwait(false);
        _sectionSummaries[section.ToString()] = summary;
    }

    private void SyncSectionTextsFromSpec()
    {
        _sectionTexts.Clear();
        foreach (var (section, text) in SpecSectionTextTranslator.Serialize(Spec))
        {
            _sectionTexts[section] = text;
        }
    }

    private void RefreshTextView()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        TextView = JsonSerializer.Serialize(Spec, options);
    }

    private void RefreshDiagnostics()
    {
        var next = new List<ValidationDiagnostic>();
        foreach (var diagnostics in _editorDiagnosticsBySection.Values)
        {
            next.AddRange(diagnostics);
        }

        next.AddRange(_client.Validate(Spec));

        _diagnostics.Clear();
        _diagnostics.AddRange(next
            .DistinctBy(d => $"{d.Section}:{d.Code}:{d.Identifier}:{d.Message}")
            .OrderBy(d => d.Section)
            .ThenBy(d => d.Severity));
    }

    private void InvalidatePlanAndApply()
    {
        // Any mutation of Spec orphans the previous plan/apply artifacts: they were
        // computed against a spec that no longer matches. Clear them so Preview stops
        // rendering stale payloads and RunApplyAsync re-plans before the next apply.
        // Also bump the revision so in-flight plan/apply tasks can detect that their
        // terminal results are for a superseded spec and discard them.
        _specRevision++;
        PlanResult = null;
        _applyEvents.Clear();
    }

    private static string StorageKeyFor(string principalId) => $"{StorageKeyPrefix}draft:{principalId}";

    private async Task<bool> TryEnterActiveWorkAsync(CancellationToken cancellationToken)
    {
        if (Status is WorkspaceStatus.Planning or WorkspaceStatus.Applying or WorkspaceStatus.Cancelling)
        {
            return false;
        }

        if (!await _workGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (Status is WorkspaceStatus.Planning or WorkspaceStatus.Applying or WorkspaceStatus.Cancelling)
        {
            _workGate.Release();
            return false;
        }

        return true;
    }

    private bool IsCurrentApply(string jobId) =>
        string.Equals(_activeJobId, jobId, StringComparison.Ordinal);

    private void Notify() => OnChanged?.Invoke();

    private sealed record TextSelectionState(int Start, int End);
}
