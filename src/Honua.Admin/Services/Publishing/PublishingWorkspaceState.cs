using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;

namespace Honua.Admin.Services.Publishing;

public enum PublishingWorkspaceStatus
{
    Idle,
    Loading,
    Validating,
    Reconciling,
    Publishing,
    Saving,
    Error
}

public sealed record PublishingValidationCheck(
    string Key,
    string Label,
    bool Passed,
    string Message);

public sealed record PublishingEnvironmentState(
    string Name,
    string DesiredState,
    string ActualState,
    string DriftStatus,
    string Evidence);

public sealed class PublishingWorkspaceState
{
    private readonly IHonuaAdminClient _client;
    private readonly HashSet<string> _desiredProtocols = new(StringComparer.OrdinalIgnoreCase);

    public PublishingWorkspaceState(IHonuaAdminClient client)
    {
        _client = client;
    }

    public PublishingWorkspaceStatus Status { get; private set; } = PublishingWorkspaceStatus.Idle;

    public string? LastError { get; private set; }

    public IReadOnlyList<ConnectionSummary> Connections { get; private set; } = Array.Empty<ConnectionSummary>();

    public IReadOnlyList<DiscoveredTable> Tables { get; private set; } = Array.Empty<DiscoveredTable>();

    public IReadOnlyList<LayerSummary> Layers { get; private set; } = Array.Empty<LayerSummary>();

    public IReadOnlyList<ServiceSummary> Services { get; private set; } = Array.Empty<ServiceSummary>();

    public ServiceSettings? ServiceSettings { get; private set; }

    public DeployPreflightResult? DeployPreflight { get; private set; }

    public ManifestDriftReport? ManifestDrift { get; private set; }

    public IReadOnlyList<ManifestVersionResponse> ManifestVersions { get; private set; } = Array.Empty<ManifestVersionResponse>();

    public IReadOnlyList<ManifestPendingChangeResponse> PendingManifestChanges { get; private set; } = Array.Empty<ManifestPendingChangeResponse>();

    public IReadOnlyList<ManifestPendingChangeResponse> ManifestApprovalHistory { get; private set; } = Array.Empty<ManifestPendingChangeResponse>();

    public GitOpsWatchConfigResponse? GitOpsWatch { get; private set; }

    public IReadOnlyList<GitOpsChangeRecordResponse> GitOpsChanges { get; private set; } = Array.Empty<GitOpsChangeRecordResponse>();

    public ManifestApplyResult? LastManifestApplyResult { get; private set; }

    public string? ManifestError { get; private set; }

    public string? ManifestApprovalError { get; private set; }

    public string? GitOpsError { get; private set; }

    public LayerSummary? LastPublishedLayer { get; private set; }

    public string? SelectedConnectionId { get; private set; }

    public string ServiceName { get; private set; } = "default";

    public string DraftSchema { get; private set; } = "public";

    public string DraftTable { get; private set; } = string.Empty;

    public string DraftLayerName { get; private set; } = string.Empty;

    public string? DraftDescription { get; private set; }

    public string? DraftGeometryColumn { get; private set; } = "geom";

    public string? DraftGeometryType { get; private set; }

    public int? DraftSrid { get; private set; } = 4326;

    public string? DraftPrimaryKey { get; private set; }

    public bool DraftEnabled { get; private set; } = true;

    public string GitOpsRepositoryUrl { get; private set; } = string.Empty;

    public string GitOpsBranch { get; private set; } = "main";

    public string GitOpsManifestPath { get; private set; } = "manifests/";

    public int GitOpsPollIntervalSeconds { get; private set; } = 60;

    public bool GitOpsApprovalRequired { get; private set; } = true;

    public bool GitOpsPruneEnabled { get; private set; }

    public bool GitOpsEnabled { get; private set; } = true;

    public event Action? OnChanged;

    public ConnectionSummary? SelectedConnection =>
        Connections.FirstOrDefault(connection => string.Equals(connection.Id, SelectedConnectionId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> DesiredProtocols => _desiredProtocols.Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<string> AvailableProtocols =>
        ServiceSettings?.AvailableProtocols.Count > 0
            ? ServiceSettings.AvailableProtocols
            : ["FeatureServer", "MapServer", "OgcFeatures", "OData", "Grpc"];

    public bool HasBlockingValidation => ValidationChecks.Any(check => !check.Passed);

    public IReadOnlyList<PublishingValidationCheck> ValidationChecks
    {
        get
        {
            var selectedConnection = SelectedConnection;
            var tableSelected = !string.IsNullOrWhiteSpace(DraftTable);
            return
            [
                new PublishingValidationCheck(
                    "connection",
                    "Connection",
                    selectedConnection?.IsActive == true,
                    selectedConnection is null
                        ? "Select a data connection."
                        : selectedConnection.IsActive
                            ? $"{selectedConnection.Name} is active."
                            : $"{selectedConnection.Name} is inactive."),
                new PublishingValidationCheck(
                    "health",
                    "Health",
                    string.Equals(selectedConnection?.HealthStatus, "Healthy", StringComparison.OrdinalIgnoreCase),
                    selectedConnection is null
                        ? "No connection selected."
                        : $"{selectedConnection.HealthStatus} connection health."),
                new PublishingValidationCheck(
                    "table",
                    "Table",
                    tableSelected,
                    tableSelected ? $"{DraftSchema}.{DraftTable} selected." : "Select a discovered table."),
                new PublishingValidationCheck(
                    "geometry",
                    "Geometry",
                    !tableSelected || !string.IsNullOrWhiteSpace(DraftGeometryColumn),
                    string.IsNullOrWhiteSpace(DraftGeometryColumn) ? "Geometry column is missing." : $"{DraftGeometryColumn} geometry column."),
                new PublishingValidationCheck(
                    "service",
                    "Service",
                    !string.IsNullOrWhiteSpace(ServiceName),
                    string.IsNullOrWhiteSpace(ServiceName) ? "Service name is required." : $"{ServiceName} service target."),
                new PublishingValidationCheck(
                    "protocols",
                    "Protocols",
                    _desiredProtocols.Count > 0,
                    _desiredProtocols.Count > 0 ? string.Join(", ", DesiredProtocols) : "Enable at least one protocol."),
                new PublishingValidationCheck(
                    "deploy",
                    "Preflight",
                    DeployPreflight?.ReadyForCoordinatedDeploy == true,
                    DeployPreflight?.Message ?? "Run deploy preflight.")
            ];
        }
    }

    public IReadOnlyList<PublishingEnvironmentState> EnvironmentStates
    {
        get
        {
            var states = new List<PublishingEnvironmentState>();
            var desiredProtocols = JoinOrPlaceholder(DesiredProtocols, "No desired protocols");
            var actualProtocols = JoinOrPlaceholder(ServiceSettings?.EnabledProtocols ?? Array.Empty<string>(), "No enabled protocols");
            var runtimeName = string.IsNullOrWhiteSpace(DeployPreflight?.Environment)
                ? "current"
                : DeployPreflight.Environment!;
            var baseline = ManifestDrift?.BaselineVersionId
                ?? ManifestVersions.FirstOrDefault()?.VersionId
                ?? "No manifest baseline";
            var driftState = ManifestDrift is null
                ? "Manifest drift not loaded"
                : ManifestDrift.HasDrift
                    ? $"{ManifestDrift.Resources.Count} drifted resource(s)"
                    : ProtocolDriftLabel;

            states.Add(new PublishingEnvironmentState(
                runtimeName,
                $"{baseline}; protocols {desiredProtocols}",
                $"{DeployPreflight?.InstanceName ?? "current instance"}; protocols {actualProtocols}",
                driftState,
                ManifestDrift is null ? ManifestError ?? "No drift report" : $"Generated {ManifestDrift.GeneratedAt:yyyy-MM-dd HH:mm} UTC"));

            if (GitOpsWatch is not null)
            {
                states.Add(new PublishingEnvironmentState(
                    "gitops",
                    $"{GitOpsWatch.RepositoryUrl}#{GitOpsWatch.Branch}:{GitOpsWatch.ManifestPath}",
                    ShortSha(GitOpsWatch.LastKnownCommitSha) ?? "No commit observed",
                    GitOpsWatch.Enabled ? "Watching" : "Paused",
                    GitOpsWatch.LastPolledAt is null ? "Never polled" : $"Last poll {GitOpsWatch.LastPolledAt:yyyy-MM-dd HH:mm} UTC"));
            }
            else if (!string.IsNullOrWhiteSpace(GitOpsError))
            {
                states.Add(new PublishingEnvironmentState(
                    "gitops",
                    "Watch not configured",
                    "Unavailable",
                    "Not available",
                    GitOpsError!));
            }

            if (PendingManifestChanges.Count > 0)
            {
                states.Add(new PublishingEnvironmentState(
                    "approval",
                    $"{PendingManifestChanges.Count} pending manifest change(s)",
                    LatestGitOpsStatus(),
                    "Review required",
                    PendingManifestChanges[0].RequestedReason ?? PendingManifestChanges[0].ManifestHash));
            }

            return states;
        }
    }

    public IReadOnlyList<ManifestDriftRecord> DriftResources => ManifestDrift?.Resources ?? Array.Empty<ManifestDriftRecord>();

    public bool HasManifestDrift => ManifestDrift?.HasDrift == true || !string.Equals(ProtocolDriftLabel, "In sync", StringComparison.OrdinalIgnoreCase);

    public string ManifestDriftLabel => ManifestDrift is null
        ? ManifestError ?? "Manifest drift not loaded"
        : ManifestDrift.HasDrift
            ? $"{ManifestDrift.Resources.Count} drifted resource(s)"
            : "Manifest baseline in sync";

    public string ProtocolDriftLabel
    {
        get
        {
            var actual = new HashSet<string>(ServiceSettings?.EnabledProtocols ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return actual.SetEquals(_desiredProtocols) ? "In sync" : "Drift";
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Status = PublishingWorkspaceStatus.Loading;
        LastError = null;
        Notify();

        try
        {
            Connections = await _client.ListConnectionsAsync(cancellationToken).ConfigureAwait(false);
            Services = await _client.ListServicesAsync(cancellationToken).ConfigureAwait(false);
            SelectedConnectionId = Connections.FirstOrDefault()?.Id;
            ServiceName = Services.FirstOrDefault()?.ServiceName ?? "default";
            await LoadServiceAsync(cancellationToken).ConfigureAwait(false);
            await DiscoverTablesAsync(cancellationToken).ConfigureAwait(false);
            await LoadLayersAsync(cancellationToken).ConfigureAwait(false);
            await RunPreflightAsync(cancellationToken).ConfigureAwait(false);
            await LoadReconciliationCoreAsync(cancellationToken).ConfigureAwait(false);
            Status = PublishingWorkspaceStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Idle;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public async Task SelectConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        SelectedConnectionId = connectionId;
        LastError = null;
        Tables = Array.Empty<DiscoveredTable>();
        Layers = Array.Empty<LayerSummary>();
        await DiscoverTablesAsync(cancellationToken).ConfigureAwait(false);
        await LoadLayersAsync(cancellationToken).ConfigureAwait(false);
        Notify();
    }

    public void SetServiceName(string? serviceName)
    {
        ServiceName = string.IsNullOrWhiteSpace(serviceName) ? "default" : serviceName.Trim();
        LastError = null;
        Notify();
    }

    public void SetDraftLayerName(string? value)
    {
        DraftLayerName = value ?? string.Empty;
        Notify();
    }

    public void SetDraftDescription(string? value)
    {
        DraftDescription = value;
        Notify();
    }

    public void SetDraftEnabled(bool value)
    {
        DraftEnabled = value;
        Notify();
    }

    public void SetGitOpsRepositoryUrl(string? value)
    {
        GitOpsRepositoryUrl = value?.Trim() ?? string.Empty;
        Notify();
    }

    public void SetGitOpsBranch(string? value)
    {
        GitOpsBranch = string.IsNullOrWhiteSpace(value) ? "main" : value.Trim();
        Notify();
    }

    public void SetGitOpsManifestPath(string? value)
    {
        GitOpsManifestPath = string.IsNullOrWhiteSpace(value) ? "manifests/" : value.Trim();
        Notify();
    }

    public void SetGitOpsPollIntervalSeconds(int value)
    {
        GitOpsPollIntervalSeconds = Math.Max(30, value);
        Notify();
    }

    public void SetGitOpsApprovalRequired(bool value)
    {
        GitOpsApprovalRequired = value;
        Notify();
    }

    public void SetGitOpsPruneEnabled(bool value)
    {
        GitOpsPruneEnabled = value;
        Notify();
    }

    public void SetGitOpsEnabled(bool value)
    {
        GitOpsEnabled = value;
        Notify();
    }

    public void ToggleProtocol(string protocol, bool enabled)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            return;
        }

        if (enabled)
        {
            _desiredProtocols.Add(protocol);
        }
        else
        {
            _desiredProtocols.Remove(protocol);
        }
        LastError = null;
        Notify();
    }

    public void UseTable(DiscoveredTable table)
    {
        DraftSchema = table.Schema;
        DraftTable = table.Table;
        DraftLayerName = table.Table;
        DraftGeometryColumn = table.GeometryColumn;
        DraftGeometryType = table.GeometryType;
        DraftSrid = table.Srid;
        DraftPrimaryKey = table.Columns.FirstOrDefault(column => column.IsPrimaryKey)?.Name;
        LastError = null;
        Notify();
    }

    public async Task DiscoverTablesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedConnectionId))
        {
            Tables = Array.Empty<DiscoveredTable>();
            return;
        }

        Tables = await _client.DiscoverConnectionTablesAsync(SelectedConnectionId, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(DraftTable) && Tables.Count > 0)
        {
            UseTable(Tables[0]);
        }
    }

    public async Task LoadServiceAsync(CancellationToken cancellationToken = default)
    {
        ServiceSettings = await _client.GetServiceSettingsAsync(ServiceName, cancellationToken).ConfigureAwait(false);
        _desiredProtocols.Clear();
        foreach (var protocol in ServiceSettings.EnabledProtocols)
        {
            _desiredProtocols.Add(protocol);
        }
    }

    public async Task LoadLayersAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedConnectionId))
        {
            Layers = Array.Empty<LayerSummary>();
            return;
        }

        Layers = await _client.ListLayersAsync(SelectedConnectionId, ServiceName, cancellationToken).ConfigureAwait(false);
    }

    public async Task RunPreflightAsync(CancellationToken cancellationToken = default)
    {
        DeployPreflight = await _client.GetDeployPreflightAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RefreshReconciliationAsync(CancellationToken cancellationToken = default)
    {
        Status = PublishingWorkspaceStatus.Reconciling;
        LastError = null;
        Notify();

        try
        {
            await LoadReconciliationCoreAsync(cancellationToken).ConfigureAwait(false);
            Status = PublishingWorkspaceStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Idle;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public async Task ConfigureGitOpsWatchAsync(string? configuredBy = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(GitOpsRepositoryUrl))
        {
            LastError = "GitOps repository URL is required.";
            Notify();
            return;
        }

        Status = PublishingWorkspaceStatus.Saving;
        LastError = null;
        GitOpsError = null;
        Notify();

        try
        {
            GitOpsWatch = await _client.ConfigureGitOpsWatchAsync(
                new GitOpsWatchConfigRequest
                {
                    RepositoryUrl = GitOpsRepositoryUrl,
                    Branch = GitOpsBranch,
                    ManifestPath = GitOpsManifestPath,
                    PollIntervalSeconds = GitOpsPollIntervalSeconds,
                    ApprovalRequired = GitOpsApprovalRequired,
                    PruneEnabled = GitOpsPruneEnabled,
                    Enabled = GitOpsEnabled,
                    ConfiguredBy = configuredBy
                },
                cancellationToken).ConfigureAwait(false);
            HydrateGitOpsDraft(GitOpsWatch);
            await LoadGitOpsChangesAsync(cancellationToken).ConfigureAwait(false);
            Status = PublishingWorkspaceStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Idle;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
            GitOpsError = ex.Message;
        }

        Notify();
    }

    public async Task DeleteGitOpsWatchAsync(CancellationToken cancellationToken = default)
    {
        Status = PublishingWorkspaceStatus.Saving;
        LastError = null;
        Notify();

        try
        {
            await _client.DeleteGitOpsWatchAsync(cancellationToken).ConfigureAwait(false);
            GitOpsWatch = null;
            GitOpsChanges = Array.Empty<GitOpsChangeRecordResponse>();
            GitOpsError = "No git repository watch is configured.";
            Status = PublishingWorkspaceStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Idle;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
            GitOpsError = ex.Message;
        }

        Notify();
    }

    public async Task ApprovePendingManifestAsync(Guid pendingId, string? reason = null, string? approvedBy = null, CancellationToken cancellationToken = default)
    {
        Status = PublishingWorkspaceStatus.Saving;
        LastError = null;
        LastManifestApplyResult = null;
        Notify();

        try
        {
            LastManifestApplyResult = await _client.ApprovePendingManifestChangeAsync(
                pendingId,
                new ManifestApproveRequest { ApprovedBy = approvedBy, Reason = reason },
                cancellationToken).ConfigureAwait(false);
            await LoadReconciliationCoreAsync(cancellationToken).ConfigureAwait(false);
            Status = PublishingWorkspaceStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Idle;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public async Task RejectPendingManifestAsync(Guid pendingId, string? reason = null, string? rejectedBy = null, CancellationToken cancellationToken = default)
    {
        Status = PublishingWorkspaceStatus.Saving;
        LastError = null;
        Notify();

        try
        {
            _ = await _client.RejectPendingManifestChangeAsync(
                pendingId,
                new ManifestRejectRequest { RejectedBy = rejectedBy, Reason = reason },
                cancellationToken).ConfigureAwait(false);
            await LoadReconciliationCoreAsync(cancellationToken).ConfigureAwait(false);
            Status = PublishingWorkspaceStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Idle;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public async Task SaveProtocolsAsync(CancellationToken cancellationToken = default)
    {
        Status = PublishingWorkspaceStatus.Saving;
        LastError = null;
        Notify();

        try
        {
            ServiceSettings = await _client.UpdateServiceProtocolsAsync(
                ServiceName,
                new UpdateProtocolsRequest { EnabledProtocols = DesiredProtocols },
                cancellationToken).ConfigureAwait(false);
            Status = PublishingWorkspaceStatus.Idle;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public async Task<LayerSummary?> PublishAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedConnectionId))
        {
            LastError = "Select a connection before publishing.";
            Notify();
            return null;
        }

        if (string.IsNullOrWhiteSpace(DraftTable))
        {
            LastError = "Select a table before publishing.";
            Notify();
            return null;
        }

        Status = PublishingWorkspaceStatus.Publishing;
        LastError = null;
        Notify();

        try
        {
            LastPublishedLayer = await _client.PublishLayerAsync(
                SelectedConnectionId,
                new PublishLayerRequest
                {
                    Schema = DraftSchema,
                    Table = DraftTable,
                    LayerName = string.IsNullOrWhiteSpace(DraftLayerName) ? DraftTable : DraftLayerName,
                    Description = DraftDescription,
                    GeometryColumn = DraftGeometryColumn,
                    GeometryType = DraftGeometryType,
                    Srid = DraftSrid,
                    PrimaryKey = DraftPrimaryKey,
                    ServiceName = ServiceName,
                    Enabled = DraftEnabled
                },
                cancellationToken).ConfigureAwait(false);
            await LoadLayersAsync(cancellationToken).ConfigureAwait(false);
            Status = PublishingWorkspaceStatus.Idle;
            Notify();
            return LastPublishedLayer;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = PublishingWorkspaceStatus.Error;
            LastError = ex.Message;
            Notify();
            return null;
        }
    }

    public async Task ToggleLayerAsync(LayerSummary layer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(SelectedConnectionId))
        {
            return;
        }

        await _client.SetLayerEnabledAsync(
            SelectedConnectionId,
            layer.LayerId,
            !layer.Enabled,
            ServiceName,
            cancellationToken).ConfigureAwait(false);
        await LoadLayersAsync(cancellationToken).ConfigureAwait(false);
        Notify();
    }

    private async Task LoadReconciliationCoreAsync(CancellationToken cancellationToken)
    {
        await LoadManifestStateAsync(cancellationToken).ConfigureAwait(false);
        await LoadManifestApprovalStateAsync(cancellationToken).ConfigureAwait(false);
        await LoadGitOpsStateAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task LoadManifestStateAsync(CancellationToken cancellationToken)
    {
        ManifestError = null;

        try
        {
            ManifestDrift = await _client.GetManifestDriftAsync(verbose: false, cancellationToken).ConfigureAwait(false);
            ManifestVersions = (await _client.ListManifestVersionsAsync(5, 0, cancellationToken).ConfigureAwait(false)).Versions;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ManifestDrift = null;
            ManifestVersions = Array.Empty<ManifestVersionResponse>();
            ManifestError = ex.Message;
        }
    }

    private async Task LoadManifestApprovalStateAsync(CancellationToken cancellationToken)
    {
        ManifestApprovalError = null;

        try
        {
            PendingManifestChanges = await _client.ListPendingManifestChangesAsync("pending", cancellationToken).ConfigureAwait(false);
            ManifestApprovalHistory = await _client.ListManifestApprovalHistoryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PendingManifestChanges = Array.Empty<ManifestPendingChangeResponse>();
            ManifestApprovalHistory = Array.Empty<ManifestPendingChangeResponse>();
            ManifestApprovalError = ex.Message;
        }
    }

    private async Task LoadGitOpsStateAsync(CancellationToken cancellationToken)
    {
        GitOpsError = null;

        try
        {
            GitOpsWatch = await _client.GetGitOpsWatchAsync(cancellationToken).ConfigureAwait(false);
            HydrateGitOpsDraft(GitOpsWatch);
            await LoadGitOpsChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            GitOpsWatch = null;
            GitOpsChanges = Array.Empty<GitOpsChangeRecordResponse>();
            GitOpsError = ex.Message;
        }
    }

    private async Task LoadGitOpsChangesAsync(CancellationToken cancellationToken)
    {
        GitOpsChanges = await _client.ListGitOpsChangesAsync(5, 0, cancellationToken).ConfigureAwait(false);
    }

    private void HydrateGitOpsDraft(GitOpsWatchConfigResponse watch)
    {
        GitOpsRepositoryUrl = watch.RepositoryUrl;
        GitOpsBranch = string.IsNullOrWhiteSpace(watch.Branch) ? "main" : watch.Branch;
        GitOpsManifestPath = string.IsNullOrWhiteSpace(watch.ManifestPath) ? "manifests/" : watch.ManifestPath;
        GitOpsPollIntervalSeconds = watch.PollIntervalSeconds <= 0 ? 60 : watch.PollIntervalSeconds;
        GitOpsApprovalRequired = watch.ApprovalRequired;
        GitOpsPruneEnabled = watch.PruneEnabled;
        GitOpsEnabled = watch.Enabled;
    }

    private string LatestGitOpsStatus()
    {
        var latest = GitOpsChanges.FirstOrDefault();
        if (latest is null)
        {
            return "No GitOps changes loaded";
        }

        var commit = ShortSha(latest.CommitSha) ?? latest.CommitSha;
        return string.IsNullOrWhiteSpace(latest.Status)
            ? commit
            : $"{latest.Status} at {commit}";
    }

    private static string JoinOrPlaceholder(IEnumerable<string> values, string placeholder)
    {
        var materialized = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return materialized.Length == 0 ? placeholder : string.Join(", ", materialized);
    }

    private static string? ShortSha(string? sha)
        => string.IsNullOrWhiteSpace(sha)
            ? null
            : sha.Length <= 12
                ? sha
                : sha[..12];

    private void Notify() => OnChanged?.Invoke();
}
