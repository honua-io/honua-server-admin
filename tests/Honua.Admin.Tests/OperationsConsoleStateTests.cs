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

    [Fact]
    public async Task ApplyRecentError_updates_troubleshooting_state_from_realtime_event()
    {
        var state = new OperationsConsoleState(new RecentErrorsUnavailableClient());
        await state.RefreshAsync();

        state.ApplyRecentError(new RecentErrorEntry
        {
            Timestamp = DateTimeOffset.Parse("2026-04-28T08:40:00Z"),
            CorrelationId = "corr-realtime",
            Path = "/api/v1/admin/services",
            StatusCode = 503,
            Message = "backend unavailable"
        });

        Assert.DoesNotContain("Recent errors", state.SectionErrors.Keys);
        Assert.NotNull(state.RecentErrors);
        Assert.Single(state.RecentErrors!.Errors);
        Assert.Contains("1 recent error", state.TroubleshootingLabel);
        Assert.Equal(OperationsConsoleStatus.Idle, state.Status);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task ApplyMigrationStatus_updates_release_evidence_from_realtime_event()
    {
        var state = new OperationsConsoleState(new MigrationUnavailableClient());
        await state.RefreshAsync();

        state.ApplyMigrationStatus(new MigrationObservabilityResponse
        {
            Status = "Applying",
            Message = "Running migration 002",
            UpgradeRequired = true,
            GeneratedAt = DateTimeOffset.Parse("2026-04-28T08:42:00Z")
        });

        Assert.Equal("Applying", state.MigrationStatus?.Status);
        Assert.Equal("Applying - upgrade required", state.MigrationStatus is null
            ? "missing"
            : $"{state.MigrationStatus.Status} - {(state.MigrationStatus.UpgradeRequired ? "upgrade required" : "no upgrade")}");
        Assert.DoesNotContain("Migrations", state.SectionErrors.Keys);
        Assert.Equal(OperationsConsoleStatus.Idle, state.Status);
        Assert.Null(state.LastError);
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

    private sealed class MigrationUnavailableClient : StubHonuaAdminClient
    {
        public override Task<MigrationObservabilityResponse> GetMigrationStatusAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("migrations unavailable");
    }
}
