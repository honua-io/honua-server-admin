using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;
using Honua.Admin.Services.Publishing;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class PublishingWorkspaceStateTests
{
    [Fact]
    public async Task InitializeAsync_loads_connection_service_layers_tables_and_preflight()
    {
        var state = new PublishingWorkspaceState(new StubHonuaAdminClient());

        await state.InitializeAsync();

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.NotEmpty(state.Connections);
        Assert.NotEmpty(state.Services);
        Assert.NotEmpty(state.Tables);
        Assert.NotEmpty(state.Layers);
        Assert.NotNull(state.DeployPreflight);
        Assert.NotNull(state.ManifestDrift);
        Assert.NotEmpty(state.ManifestVersions);
        Assert.NotEmpty(state.PendingManifestChanges);
        Assert.NotNull(state.GitOpsWatch);
        Assert.NotEmpty(state.GitOpsChanges);
        Assert.Contains(state.EnvironmentStates, row => row.Name == "gitops");
        Assert.Contains(state.EnvironmentStates, row => row.Name == "approval");
        Assert.All(state.ValidationChecks, check => Assert.True(check.Passed, check.Message));
    }

    [Fact]
    public async Task ToggleProtocol_and_SaveProtocolsAsync_persist_desired_protocols()
    {
        var client = new RecordingPublishingClient();
        var state = new PublishingWorkspaceState(client);
        await state.InitializeAsync();

        state.ToggleProtocol("OData", enabled: true);
        await state.SaveProtocolsAsync();

        Assert.Contains("OData", client.LastProtocols);
        Assert.Equal("In sync", state.ProtocolDriftLabel);
    }

    [Fact]
    public async Task PublishAsync_maps_draft_to_publish_request()
    {
        var client = new RecordingPublishingClient();
        var state = new PublishingWorkspaceState(client);
        await state.InitializeAsync();
        state.UseTable(new DiscoveredTable
        {
            Schema = "gis",
            Table = "trails",
            GeometryColumn = "shape",
            GeometryType = "LineString",
            Srid = 4326
        });
        state.SetDraftLayerName("Trails");
        state.SetDraftDescription("Trail network");

        var layer = await state.PublishAsync();

        Assert.NotNull(layer);
        Assert.NotNull(client.LastPublishRequest);
        Assert.Equal("gis", client.LastPublishRequest!.Schema);
        Assert.Equal("trails", client.LastPublishRequest.Table);
        Assert.Equal("Trails", client.LastPublishRequest.LayerName);
        Assert.Equal("shape", client.LastPublishRequest.GeometryColumn);
        Assert.Equal("default", client.LastPublishRequest.ServiceName);
    }

    [Fact]
    public async Task ConfigureGitOpsWatchAsync_maps_draft_to_request()
    {
        var client = new RecordingPublishingClient();
        var state = new PublishingWorkspaceState(client);
        await state.InitializeAsync();

        state.SetGitOpsRepositoryUrl("https://github.com/honua-io/environments");
        state.SetGitOpsBranch("release");
        state.SetGitOpsManifestPath("env/prod");
        state.SetGitOpsPollIntervalSeconds(45);
        state.SetGitOpsApprovalRequired(true);
        state.SetGitOpsPruneEnabled(true);
        state.SetGitOpsEnabled(false);

        await state.ConfigureGitOpsWatchAsync(configuredBy: "operator");

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.NotNull(client.LastGitOpsRequest);
        Assert.Equal("https://github.com/honua-io/environments", client.LastGitOpsRequest!.RepositoryUrl);
        Assert.Equal("release", client.LastGitOpsRequest.Branch);
        Assert.Equal("env/prod", client.LastGitOpsRequest.ManifestPath);
        Assert.Equal(45, client.LastGitOpsRequest.PollIntervalSeconds);
        Assert.True(client.LastGitOpsRequest.ApprovalRequired);
        Assert.True(client.LastGitOpsRequest.PruneEnabled);
        Assert.False(client.LastGitOpsRequest.Enabled);
        Assert.Equal("operator", client.LastGitOpsRequest.ConfiguredBy);
    }

    [Fact]
    public async Task ConfigureGitOpsWatchAsync_keeps_save_successful_when_change_history_fails()
    {
        var state = new PublishingWorkspaceState(new GitOpsChangesUnavailableClient());
        await state.InitializeAsync();

        state.SetGitOpsRepositoryUrl("https://github.com/honua-io/environments");

        await state.ConfigureGitOpsWatchAsync(configuredBy: "operator");

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.Null(state.LastError);
        Assert.NotNull(state.GitOpsWatch);
        Assert.Equal("https://github.com/honua-io/environments", state.GitOpsWatch.RepositoryUrl);
        Assert.Empty(state.GitOpsChanges);
        Assert.Contains("GitOps changes", state.GitOpsError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovePendingManifestAsync_records_apply_result_and_refreshes_state()
    {
        var client = new RecordingPublishingClient();
        var state = new PublishingWorkspaceState(client);
        await state.InitializeAsync();
        var pendingId = state.PendingManifestChanges[0].PendingId;

        await state.ApprovePendingManifestAsync(pendingId, "looks good", "operator");

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.Equal(pendingId, client.LastApprovedPendingId);
        Assert.Equal("looks good", client.LastApproveRequest?.Reason);
        Assert.NotNull(state.LastManifestApplyResult);
        Assert.Empty(state.PendingManifestChanges);
    }

    [Fact]
    public async Task RejectPendingManifestAsync_records_reject_request_and_refreshes_state()
    {
        var client = new RecordingPublishingClient();
        var state = new PublishingWorkspaceState(client);
        await state.InitializeAsync();
        var pendingId = state.PendingManifestChanges[0].PendingId;

        await state.RejectPendingManifestAsync(pendingId, "needs another pass", "operator");

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.Equal(pendingId, client.LastRejectedPendingId);
        Assert.Equal("needs another pass", client.LastRejectRequest?.Reason);
        Assert.Empty(state.PendingManifestChanges);
    }

    [Fact]
    public async Task PublishAsync_without_table_surfaces_validation_error()
    {
        var state = new PublishingWorkspaceState(new EmptyTableClient());
        await state.InitializeAsync();

        var layer = await state.PublishAsync();

        Assert.Null(layer);
        Assert.Contains("Select a table", state.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitializeAsync_keeps_workspace_usable_when_optional_reconciliation_endpoints_are_unavailable()
    {
        var state = new PublishingWorkspaceState(new OptionalReconciliationUnavailableClient());

        await state.InitializeAsync();

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.Null(state.LastError);
        Assert.NotNull(state.ManifestError);
        Assert.NotNull(state.ManifestApprovalError);
        Assert.NotNull(state.GitOpsError);
        Assert.True(state.HasManifestDrift);
        Assert.Contains(state.EnvironmentStates, row => row.Name == "gitops");
    }

    [Fact]
    public async Task InitializeAsync_preserves_manifest_drift_when_version_history_fails()
    {
        var state = new PublishingWorkspaceState(new ManifestVersionUnavailableClient());

        await state.InitializeAsync();

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.NotNull(state.ManifestDrift);
        Assert.Empty(state.ManifestVersions);
        Assert.Contains("Manifest versions", state.ManifestError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitializeAsync_preserves_pending_approvals_when_approval_history_fails()
    {
        var state = new PublishingWorkspaceState(new ManifestApprovalHistoryUnavailableClient());

        await state.InitializeAsync();

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.NotEmpty(state.PendingManifestChanges);
        Assert.Empty(state.ManifestApprovalHistory);
        Assert.Contains("Approval history", state.ManifestApprovalError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InitializeAsync_preserves_gitops_watch_when_change_history_fails()
    {
        var state = new PublishingWorkspaceState(new GitOpsChangesUnavailableClient());

        await state.InitializeAsync();

        Assert.Equal(PublishingWorkspaceStatus.Idle, state.Status);
        Assert.NotNull(state.GitOpsWatch);
        Assert.Empty(state.GitOpsChanges);
        Assert.Contains("GitOps changes", state.GitOpsError, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingPublishingClient : StubHonuaAdminClient
    {
        public PublishLayerRequest? LastPublishRequest { get; private set; }
        public IReadOnlyList<string> LastProtocols { get; private set; } = Array.Empty<string>();
        public GitOpsWatchConfigRequest? LastGitOpsRequest { get; private set; }
        public Guid? LastApprovedPendingId { get; private set; }
        public ManifestApproveRequest? LastApproveRequest { get; private set; }
        public Guid? LastRejectedPendingId { get; private set; }
        public ManifestRejectRequest? LastRejectRequest { get; private set; }
        private bool _pendingResolved;

        public override Task<LayerSummary> PublishLayerAsync(string connectionId, PublishLayerRequest request, CancellationToken cancellationToken)
        {
            LastPublishRequest = request;
            return Task.FromResult(new LayerSummary
            {
                LayerId = 202,
                LayerName = request.LayerName,
                Schema = request.Schema,
                Table = request.Table,
                GeometryType = request.GeometryType ?? string.Empty,
                Srid = request.Srid ?? 4326,
                Enabled = request.Enabled,
                ServiceName = request.ServiceName ?? "default"
            });
        }

        public override Task<ServiceSettings> UpdateServiceProtocolsAsync(string serviceName, UpdateProtocolsRequest request, CancellationToken cancellationToken)
        {
            LastProtocols = request.EnabledProtocols;
            return Task.FromResult(ServiceSettings with { EnabledProtocols = request.EnabledProtocols });
        }

        public override Task<GitOpsWatchConfigResponse> ConfigureGitOpsWatchAsync(GitOpsWatchConfigRequest request, CancellationToken cancellationToken)
        {
            LastGitOpsRequest = request;
            return base.ConfigureGitOpsWatchAsync(request, cancellationToken);
        }

        public override Task<ManifestApplyResult> ApprovePendingManifestChangeAsync(Guid pendingId, ManifestApproveRequest request, CancellationToken cancellationToken)
        {
            LastApprovedPendingId = pendingId;
            LastApproveRequest = request;
            _pendingResolved = true;
            return base.ApprovePendingManifestChangeAsync(pendingId, request, cancellationToken);
        }

        public override Task<ManifestPendingChangeResponse> RejectPendingManifestChangeAsync(Guid pendingId, ManifestRejectRequest request, CancellationToken cancellationToken)
        {
            LastRejectedPendingId = pendingId;
            LastRejectRequest = request;
            _pendingResolved = true;
            return base.RejectPendingManifestChangeAsync(pendingId, request, cancellationToken);
        }

        public override Task<IReadOnlyList<ManifestPendingChangeResponse>> ListPendingManifestChangesAsync(string? status, CancellationToken cancellationToken)
            => _pendingResolved
                ? Task.FromResult<IReadOnlyList<ManifestPendingChangeResponse>>(Array.Empty<ManifestPendingChangeResponse>())
                : base.ListPendingManifestChangesAsync(status, cancellationToken);
    }

    private sealed class EmptyTableClient : StubHonuaAdminClient
    {
        public override Task<IReadOnlyList<DiscoveredTable>> DiscoverConnectionTablesAsync(string connectionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<DiscoveredTable>>(Array.Empty<DiscoveredTable>());
    }

    private sealed class OptionalReconciliationUnavailableClient : StubHonuaAdminClient
    {
        public override Task<ManifestDriftReport> GetManifestDriftAsync(bool verbose, CancellationToken cancellationToken)
            => throw new InvalidOperationException("manifest drift unavailable");

        public override Task<IReadOnlyList<ManifestPendingChangeResponse>> ListPendingManifestChangesAsync(string? status, CancellationToken cancellationToken)
            => throw new InvalidOperationException("approval unavailable");

        public override Task<GitOpsWatchConfigResponse> GetGitOpsWatchAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("gitops unavailable");
    }

    private sealed class ManifestVersionUnavailableClient : StubHonuaAdminClient
    {
        public override Task<ManifestVersionListResponse> ListManifestVersionsAsync(int limit, int offset, CancellationToken cancellationToken)
            => throw new InvalidOperationException("versions unavailable");
    }

    private sealed class ManifestApprovalHistoryUnavailableClient : StubHonuaAdminClient
    {
        public override Task<IReadOnlyList<ManifestPendingChangeResponse>> ListManifestApprovalHistoryAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("history unavailable");
    }

    private sealed class GitOpsChangesUnavailableClient : StubHonuaAdminClient
    {
        public override Task<IReadOnlyList<GitOpsChangeRecordResponse>> ListGitOpsChangesAsync(int limit, int offset, CancellationToken cancellationToken)
            => throw new InvalidOperationException("changes unavailable");
    }
}
