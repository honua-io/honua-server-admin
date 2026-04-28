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

    private sealed class PartialOperationsUnavailableClient : StubHonuaAdminClient
    {
        public override Task<ManifestDriftReport> GetManifestDriftAsync(bool verbose, CancellationToken cancellationToken)
            => throw new InvalidOperationException("drift unavailable");

        public override Task<IReadOnlyList<GitOpsChangeRecordResponse>> ListGitOpsChangesAsync(int limit, int offset, CancellationToken cancellationToken)
            => throw new InvalidOperationException("gitops unavailable");
    }
}
