using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Services.DataConnections.Providers;

namespace Honua.Admin.Services.DataConnections;

public enum WorkspaceListStatus
{
    Idle,
    Loading,
    Mutating,
    Testing,
    Error
}

/// <summary>
/// Scoped MVU-style store for the data-connection workspace. Mirrors the
/// shape of <c>SpecWorkspaceState</c> — every observable mutation funnels
/// through an explicit method so persistence (none, by design — credentials
/// must not survive a navigation), telemetry, and notifications stay
/// single-origin. Failures from the client become a state transition, never
/// an exception that escapes to Razor.
/// </summary>
public sealed class DataConnectionsState
{
    private readonly IDataConnectionClient _client;
    private readonly IDataConnectionTelemetry _telemetry;
    private readonly IProviderRegistry _registry;

    private readonly List<DataConnectionSummary> _connections = new();

    public DataConnectionsState(IDataConnectionClient client, IDataConnectionTelemetry telemetry, IProviderRegistry registry)
    {
        _client = client;
        _telemetry = telemetry;
        _registry = registry;
    }

    public IReadOnlyList<DataConnectionSummary> Connections => _connections;

    public WorkspaceListStatus Status { get; private set; } = WorkspaceListStatus.Idle;

    public ConnectionOperationError? LastError { get; private set; }

    public DataConnectionDetail? SelectedDetail { get; private set; }

    public ConnectionDiagnostic? LatestDiagnostic { get; private set; }

    public ConnectionDraft? Draft { get; private set; }

    public IProviderRegistry ProviderRegistry => _registry;

    public event Action? OnChanged;

    public async Task RefreshListAsync(CancellationToken cancellationToken = default)
    {
        Status = WorkspaceListStatus.Loading;
        LastError = null;
        Notify();

        var watch = Stopwatch.StartNew();
        var result = await _client.ListAsync(cancellationToken).ConfigureAwait(false);
        watch.Stop();

        if (!result.IsSuccess)
        {
            Status = WorkspaceListStatus.Error;
            LastError = result.Error;
            _telemetry.Record("data_connections.list_failed", new Dictionary<string, object?>
            {
                ["error_kind"] = result.Error!.Kind.ToString()
            });
            Notify();
            return;
        }

        _connections.Clear();
        _connections.AddRange(result.Value!);
        Status = WorkspaceListStatus.Idle;
        _telemetry.RecordLatency("data_connections.list_loaded", watch.ElapsedMilliseconds, new Dictionary<string, object?>
        {
            ["count"] = _connections.Count
        });
        Notify();
    }

    public ConnectionDraft BeginCreateDraft(string providerId)
    {
        if (!_registry.TryGet(providerId, out var registration))
        {
            throw new InvalidOperationException($"Provider '{providerId}' is not registered.");
        }

        Draft = new ConnectionDraft
        {
            ProviderId = registration.ProviderId,
            Port = registration.DefaultPort
        };
        SelectedDetail = null;
        LatestDiagnostic = null;
        Notify();

        if (registration.IsStub)
        {
            _telemetry.Record("data_connections.provider_stub_viewed", new Dictionary<string, object?>
            {
                ["provider_id"] = registration.ProviderId
            });
        }

        return Draft;
    }

    public void UpdateDraft(Action<ConnectionDraft> mutate)
    {
        if (Draft is null)
        {
            return;
        }
        mutate(Draft);
        Notify();
    }

    public void ClearDraft()
    {
        Draft = null;
        LatestDiagnostic = null;
        Notify();
    }

    public async Task<ConnectionResult<DataConnectionSummary>> SubmitDraftAsync(CancellationToken cancellationToken = default)
    {
        if (Draft is null)
        {
            return ConnectionResult<DataConnectionSummary>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Validation, "error.no_draft"));
        }

        var providerId = Draft.ProviderId;

        Status = WorkspaceListStatus.Mutating;
        LastError = null;
        Notify();

        _telemetry.Record("data_connections.create_submitted", new Dictionary<string, object?>
        {
            ["provider"] = providerId
        });

        var request = ToCreateRequest(Draft);
        var result = await _client.CreateAsync(request, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            Status = WorkspaceListStatus.Error;
            LastError = result.Error;
            _telemetry.Record("data_connections.create_failed", new Dictionary<string, object?>
            {
                ["provider"] = providerId,
                ["failure_code"] = result.Error!.Kind.ToString()
            });
            Notify();
            return result;
        }

        // Optimistic insert — replace on next refresh.
        var summary = result.Value!;
        _connections.Add(summary);
        await TryRefreshSelectedDetailAsync(summary.ConnectionId, cancellationToken).ConfigureAwait(false);
        Draft = null;
        LatestDiagnostic = null;
        Status = WorkspaceListStatus.Idle;
        _telemetry.Record("data_connections.create_succeeded", new Dictionary<string, object?>
        {
            ["provider"] = providerId,
            ["connection_id"] = summary.ConnectionId
        });
        Notify();
        return result;
    }

    public async Task LoadDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (SelectedDetail?.ConnectionId != id)
        {
            // Navigating to a different connection — clear the prior preflight
            // grid so we don't render a stale verdict against the new selection.
            LatestDiagnostic = null;
        }
        Status = WorkspaceListStatus.Loading;
        LastError = null;
        Notify();

        var result = await _client.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Status = WorkspaceListStatus.Error;
            LastError = result.Error;
            Notify();
            return;
        }

        SelectedDetail = result.Value;
        Status = WorkspaceListStatus.Idle;
        Notify();
    }

    public async Task<ConnectionResult<DataConnectionSummary>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        Status = WorkspaceListStatus.Mutating;
        LastError = null;
        Notify();

        var result = active
            ? await _client.EnableAsync(id, cancellationToken).ConfigureAwait(false)
            : await _client.DisableAsync(id, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            Status = WorkspaceListStatus.Error;
            LastError = result.Error;
            Notify();
            return result;
        }

        ReplaceInList(result.Value!);
        await TryRefreshSelectedDetailAsync(id, cancellationToken).ConfigureAwait(false);
        LatestDiagnostic = null;
        Status = WorkspaceListStatus.Idle;
        _telemetry.Record(active ? "data_connections.enabled" : "data_connections.disabled", new Dictionary<string, object?>
        {
            ["connection_id"] = id
        });
        Notify();
        return result;
    }

    public ConnectionDraft BeginEditDraft(DataConnectionDetail detail)
    {
        Draft = new ConnectionDraft
        {
            ConnectionId = detail.ConnectionId,
            ProviderId = detail.ProviderId,
            Name = detail.Name,
            Description = detail.Description,
            Host = detail.Host,
            Port = detail.Port,
            DatabaseName = detail.DatabaseName,
            Username = detail.Username,
            SslRequired = detail.SslRequired,
            SslMode = detail.SslMode,
            CredentialMode = string.Equals(detail.StorageType, "external", StringComparison.OrdinalIgnoreCase) ? CredentialMode.External : CredentialMode.Managed,
            SecretReference = detail.CredentialReference,
            IsActive = detail.IsActive
        };
        Notify();
        return Draft;
    }

    public async Task<ConnectionResult<DataConnectionSummary>> SubmitEditAsync(CancellationToken cancellationToken = default)
    {
        if (Draft is null || Draft.ConnectionId is null)
        {
            return ConnectionResult<DataConnectionSummary>.Fail(
                new ConnectionOperationError(ConnectionErrorKind.Validation, "error.no_draft"));
        }

        Status = WorkspaceListStatus.Mutating;
        LastError = null;
        Notify();

        var request = new UpdateConnectionRequest
        {
            Description = Draft.Description,
            Host = Draft.Host,
            Port = Draft.Port,
            DatabaseName = Draft.DatabaseName,
            Username = Draft.Username,
            Password = Draft.CredentialMode == CredentialMode.Managed && !string.IsNullOrEmpty(Draft.Password)
                ? Draft.Password
                : null,
            SslRequired = Draft.SslRequired,
            SslMode = Draft.SslMode,
            IsActive = Draft.IsActive
        };

        var result = await _client.UpdateAsync(Draft.ConnectionId.Value, request, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            Status = WorkspaceListStatus.Error;
            LastError = result.Error;
            _telemetry.Record("data_connections.update_failed", new Dictionary<string, object?>
            {
                ["connection_id"] = Draft.ConnectionId,
                ["failure_code"] = result.Error!.Kind.ToString()
            });
            Notify();
            return result;
        }

        var summary = result.Value!;
        ReplaceInList(summary);
        await TryRefreshSelectedDetailAsync(summary.ConnectionId, cancellationToken).ConfigureAwait(false);
        Draft = null;
        LatestDiagnostic = null;
        Status = WorkspaceListStatus.Idle;
        _telemetry.Record("data_connections.update_succeeded", new Dictionary<string, object?>
        {
            ["connection_id"] = summary.ConnectionId
        });
        Notify();
        return result;
    }

    public async Task<ConnectionResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Status = WorkspaceListStatus.Mutating;
        LastError = null;
        Notify();

        var result = await _client.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            Status = WorkspaceListStatus.Error;
            LastError = result.Error;
            Notify();
            return result;
        }

        _connections.RemoveAll(c => c.ConnectionId == id);
        if (SelectedDetail?.ConnectionId == id)
        {
            SelectedDetail = null;
            LatestDiagnostic = null;
        }
        Status = WorkspaceListStatus.Idle;
        _telemetry.Record("data_connections.deleted", new Dictionary<string, object?>
        {
            ["connection_id"] = id
        });
        Notify();
        return result;
    }

    public async Task<ConnectionDiagnostic?> RunDraftPreflightAsync(CancellationToken cancellationToken = default)
    {
        if (Draft is null)
        {
            return null;
        }

        Status = WorkspaceListStatus.Testing;
        LastError = null;
        Notify();

        _telemetry.Record("data_connections.test_started", new Dictionary<string, object?>
        {
            ["mode"] = "draft",
            ["provider"] = Draft.ProviderId
        });

        var watch = Stopwatch.StartNew();
        var request = ToCreateRequest(Draft);
        var result = await _client.TestDraftAsync(request, cancellationToken).ConfigureAwait(false);
        watch.Stop();

        return CompletePreflight(result, detail: null, watch.ElapsedMilliseconds);
    }

    public async Task<ConnectionDiagnostic?> RunExistingPreflightAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Status = WorkspaceListStatus.Testing;
        LastError = null;
        Notify();

        _telemetry.Record("data_connections.test_started", new Dictionary<string, object?>
        {
            ["mode"] = "existing",
            ["connection_id"] = id
        });

        var watch = Stopwatch.StartNew();
        var result = await _client.TestExistingAsync(id, cancellationToken).ConfigureAwait(false);
        watch.Stop();

        return CompletePreflight(result, SelectedDetail, watch.ElapsedMilliseconds);
    }

    private ConnectionDiagnostic? CompletePreflight(ConnectionResult<ConnectionTestOutcome> result, DataConnectionDetail? detail, long elapsedMs)
    {
        if (!result.IsSuccess)
        {
            Status = WorkspaceListStatus.Error;
            LastError = result.Error;
            LatestDiagnostic = null;
            _telemetry.Record("data_connections.test_completed", new Dictionary<string, object?>
            {
                ["result_kind"] = "error",
                ["error_kind"] = result.Error!.Kind.ToString()
            });
            Notify();
            return null;
        }

        var diagnostic = DiagnosticMapper.Map(result.Value!, detail);
        LatestDiagnostic = diagnostic;
        Status = WorkspaceListStatus.Idle;

        var failedStep = diagnostic.AnyFailed
            ? diagnostic.Cells.First(c => c.Status == DiagnosticStatus.Failed).Step.ToString()
            : null;

        _telemetry.RecordLatency("data_connections.test_completed", elapsedMs, new Dictionary<string, object?>
        {
            ["result_kind"] = result.Value!.IsHealthy ? "healthy" : (failedStep is null ? "not_assessed" : "failed"),
            ["failed_step"] = failedStep
        });
        Notify();
        return diagnostic;
    }

    public ProviderCapabilityMatrix GetCapabilityMatrix(string providerId)
    {
        var registration = _registry.GetById(providerId);
        var checks = registration.ManagedHostingChecks
            .Select(c => c with { Status = DiagnosticStatus.NotAssessed })
            .ToArray();
        return new ProviderCapabilityMatrix
        {
            ProviderId = providerId,
            Checks = checks,
            IsServerSourced = false
        };
    }

    private async Task TryRefreshSelectedDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        // Mutating endpoints (POST/PUT) return a Summary; the page expects a
        // full Detail. Best-effort follow-up: on success, populate SelectedDetail;
        // on failure leave the prior value unchanged so the page does not blank.
        var result = await _client.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            SelectedDetail = result.Value;
        }
    }

    private void ReplaceInList(DataConnectionSummary summary)
    {
        var index = _connections.FindIndex(c => c.ConnectionId == summary.ConnectionId);
        if (index < 0)
        {
            _connections.Add(summary);
        }
        else
        {
            _connections[index] = summary;
        }
    }

    private static CreateConnectionRequest ToCreateRequest(ConnectionDraft draft) => new()
    {
        Name = draft.Name,
        Description = draft.Description,
        Host = draft.Host,
        Port = draft.Port,
        DatabaseName = draft.DatabaseName,
        Username = draft.Username,
        Password = draft.CredentialMode == CredentialMode.Managed ? draft.Password : null,
        SecretReference = draft.CredentialMode == CredentialMode.External ? draft.SecretReference : null,
        SecretType = draft.CredentialMode == CredentialMode.External ? draft.SecretType : null,
        SslRequired = draft.SslRequired,
        SslMode = draft.SslMode
    };

    private void Notify() => OnChanged?.Invoke();
}
