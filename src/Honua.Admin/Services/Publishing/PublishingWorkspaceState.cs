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
    string DesiredRevision,
    string ActualRevision,
    string DesiredProtocols,
    string ActualProtocols,
    string DriftStatus);

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
            var desiredProtocols = string.Join(", ", DesiredProtocols);
            var actualProtocols = string.Join(", ", ServiceSettings?.EnabledProtocols ?? Array.Empty<string>());
            return
            [
                new PublishingEnvironmentState("dev", "service-intent", "service-intent", desiredProtocols, actualProtocols, ProtocolDriftLabel),
                new PublishingEnvironmentState("staging", "service-intent", "awaiting promotion", desiredProtocols, "FeatureServer", "Promotion pending"),
                new PublishingEnvironmentState("production", "service-intent", "stable", desiredProtocols, "FeatureServer, OgcFeatures", "Review required")
            ];
        }
    }

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
            Status = PublishingWorkspaceStatus.Idle;
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

    private void Notify() => OnChanged?.Invoke();
}
