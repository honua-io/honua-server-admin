using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;
using Honua.Admin.Services.Operations;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class OperationsConsoleStateTests
{
    [Fact]
    public async Task RefreshAsync_loads_release_drift_evidence_and_troubleshooting_state()
    {
        var state = new OperationsConsoleState(new StubHonuaAdminClient());

        await state.RefreshAsync();

        Assert.Equal(OperationsConsoleStatus.Idle, state.Status);
        Assert.Null(state.LastError);
        Assert.NotNull(state.DeployPreflight);
        Assert.NotNull(state.ManifestDrift);
        Assert.NotEmpty(state.ManifestVersions);
        Assert.NotEmpty(state.PendingApprovals);
        Assert.NotEmpty(state.GitOpsChanges);
        Assert.NotNull(state.RecentErrors);
        Assert.NotNull(state.TelemetryStatus);
        Assert.NotNull(state.MigrationStatus);
        Assert.Equal("Ready", state.ReleaseHealthLabel);
        Assert.Contains("recent error", state.TroubleshootingLabel, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_keeps_console_usable_when_optional_sections_fail()
    {
        var state = new OperationsConsoleState(new PartialOperationsUnavailableClient());

        await state.RefreshAsync();

        Assert.Equal(OperationsConsoleStatus.Partial, state.Status);
        Assert.NotNull(state.LastError);
        Assert.NotNull(state.DeployPreflight);
        Assert.NotNull(state.RecentErrors);
        Assert.Null(state.ManifestDrift);
        Assert.Empty(state.GitOpsChanges);
        Assert.Contains("Manifest drift", state.SectionErrors.Keys);
        Assert.Contains("GitOps changes", state.SectionErrors.Keys);
    }

    [Fact]
    public async Task RefreshAsync_clears_stale_section_data_when_later_refresh_fails()
    {
        var state = new OperationsConsoleState(new FailingSecondPreflightClient());

        await state.RefreshAsync();

        Assert.NotNull(state.DeployPreflight);

        await state.RefreshAsync();

        Assert.Equal(OperationsConsoleStatus.Partial, state.Status);
        Assert.Null(state.DeployPreflight);
        Assert.Equal("Preflight unavailable", state.ReleaseHealthLabel);
        Assert.Contains("Deploy preflight", state.SectionErrors.Keys);
    }

    [Fact]
    public async Task RefreshAsync_reports_unknown_drift_when_approvals_fail()
    {
        var state = new OperationsConsoleState(new ApprovalsUnavailableNoDriftClient());

        await state.RefreshAsync();

        Assert.Equal(OperationsConsoleStatus.Partial, state.Status);
        Assert.Empty(state.PendingApprovals);
        Assert.Equal("Approval state unknown", state.DriftLabel);
        Assert.Contains("Manifest approvals", state.SectionErrors.Keys);
    }

    [Fact]
    public async Task RefreshAsync_reports_recent_errors_unavailable_when_error_buffer_fails()
    {
        var state = new OperationsConsoleState(new RecentErrorsUnavailableClient());

        await state.RefreshAsync();

        Assert.Equal(OperationsConsoleStatus.Partial, state.Status);
        Assert.Null(state.RecentErrors);
        Assert.Equal("Recent errors unavailable", state.TroubleshootingLabel);
        Assert.Contains("Recent errors", state.SectionErrors.Keys);
    }

    private sealed class PartialOperationsUnavailableClient : StubHonuaAdminClient
    {
        public override Task<ManifestDriftReport> GetManifestDriftAsync(bool verbose, CancellationToken cancellationToken)
            => throw new InvalidOperationException("drift unavailable");

        public override Task<IReadOnlyList<GitOpsChangeRecordResponse>> ListGitOpsChangesAsync(int limit, int offset, CancellationToken cancellationToken)
            => throw new InvalidOperationException("gitops unavailable");
    }

    private sealed class FailingSecondPreflightClient : StubHonuaAdminClient
    {
        private int _preflightRequests;

        public override Task<DeployPreflightResult> GetDeployPreflightAsync(CancellationToken cancellationToken)
        {
            _preflightRequests++;

            return _preflightRequests == 1
                ? base.GetDeployPreflightAsync(cancellationToken)
                : throw new InvalidOperationException("preflight unavailable");
        }
    }

    private sealed class ApprovalsUnavailableNoDriftClient : StubHonuaAdminClient
    {
        public override Task<ManifestDriftReport> GetManifestDriftAsync(bool verbose, CancellationToken cancellationToken)
            => Task.FromResult(ManifestDrift with { HasDrift = false, Resources = Array.Empty<ManifestDriftRecord>() });

        public override Task<IReadOnlyList<ManifestPendingChangeResponse>> ListPendingManifestChangesAsync(string? status, CancellationToken cancellationToken)
            => throw new InvalidOperationException("approvals unavailable");
    }

    private sealed class RecentErrorsUnavailableClient : StubHonuaAdminClient
    {
        public override Task<RecentErrorsResponse> GetRecentErrorsAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("recent errors unavailable");
    }
}
