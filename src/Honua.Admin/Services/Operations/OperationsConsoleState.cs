// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;

namespace Honua.Admin.Services.Operations;

public enum OperationsConsoleStatus
{
    Idle,
    Loading,
    Partial,
    Error
}

public sealed class OperationsConsoleState
{
    private readonly IHonuaAdminClient _client;
    private readonly Dictionary<string, string> _sectionErrors = new(StringComparer.OrdinalIgnoreCase);

    public OperationsConsoleState(IHonuaAdminClient client)
    {
        _client = client;
    }

    public OperationsConsoleStatus Status { get; private set; } = OperationsConsoleStatus.Idle;

    public string? LastError { get; private set; }

    public DeployPreflightResult? DeployPreflight { get; private set; }

    public ManifestDriftReport? ManifestDrift { get; private set; }

    public IReadOnlyList<ManifestVersionResponse> ManifestVersions { get; private set; } = Array.Empty<ManifestVersionResponse>();

    public IReadOnlyList<ManifestPendingChangeResponse> PendingApprovals { get; private set; } = Array.Empty<ManifestPendingChangeResponse>();

    public IReadOnlyList<GitOpsChangeRecordResponse> GitOpsChanges { get; private set; } = Array.Empty<GitOpsChangeRecordResponse>();

    public RecentErrorsResponse? RecentErrors { get; private set; }

    public ObservabilityStatusResponse? TelemetryStatus { get; private set; }

    public MigrationObservabilityResponse? MigrationStatus { get; private set; }

    public IReadOnlyDictionary<string, string> SectionErrors => _sectionErrors;

    public ManifestVersionResponse? LatestManifestVersion => ManifestVersions.FirstOrDefault();

    public GitOpsChangeRecordResponse? LatestGitOpsChange => GitOpsChanges.FirstOrDefault();

    public int RecentErrorCount => RecentErrors?.Errors.Count ?? 0;

    public string ReleaseHealthLabel => DeployPreflight is null
        ? "Unknown"
        : DeployPreflight.ReadyForCoordinatedDeploy
            ? "Ready"
            : "Needs attention";

    public string ReleaseEvidenceLabel => LatestManifestVersion is null
        ? "No manifest version loaded"
        : $"{LatestManifestVersion.VersionId} · {LatestManifestVersion.ResourceCount} resource(s)";

    public string DriftLabel
    {
        get
        {
            if (ManifestDrift is null)
            {
                return "Unknown";
            }

            if (ManifestDrift.HasDrift)
            {
                return $"{ManifestDrift.Resources.Count} drifted resource(s)";
            }

            return PendingApprovals.Count == 0
                ? "In sync"
                : $"{PendingApprovals.Count} approval(s) pending";
        }
    }

    public string TroubleshootingLabel
    {
        get
        {
            if (RecentErrorCount > 0)
            {
                return $"{RecentErrorCount} recent error(s)";
            }

            if (TelemetryStatus?.TracingEnabled == true && TelemetryStatus.OtlpConfigured)
            {
                return "Telemetry ready";
            }

            return "Telemetry incomplete";
        }
    }

    public event Action? OnChanged;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Status = OperationsConsoleStatus.Loading;
        LastError = null;
        _sectionErrors.Clear();
        ClearSectionData();
        Notify();

        try
        {
            await CaptureAsync("Deploy preflight", async () =>
            {
                DeployPreflight = await _client.GetDeployPreflightAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CaptureAsync("Manifest drift", async () =>
            {
                ManifestDrift = await _client.GetManifestDriftAsync(verbose: false, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CaptureAsync("Manifest versions", async () =>
            {
                ManifestVersions = (await _client.ListManifestVersionsAsync(5, 0, cancellationToken).ConfigureAwait(false)).Versions;
            }).ConfigureAwait(false);

            await CaptureAsync("Manifest approvals", async () =>
            {
                PendingApprovals = await _client.ListPendingManifestChangesAsync("pending", cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CaptureAsync("GitOps changes", async () =>
            {
                GitOpsChanges = await _client.ListGitOpsChangesAsync(5, 0, cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CaptureAsync("Recent errors", async () =>
            {
                RecentErrors = await _client.GetRecentErrorsAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CaptureAsync("Telemetry", async () =>
            {
                TelemetryStatus = await _client.GetTelemetryStatusAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            await CaptureAsync("Migrations", async () =>
            {
                MigrationStatus = await _client.GetMigrationStatusAsync(cancellationToken).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Status = _sectionErrors.Count == 0 ? OperationsConsoleStatus.Idle : OperationsConsoleStatus.Partial;
            LastError = _sectionErrors.Count == 0 ? null : "Some operations console data could not be loaded.";
        }
        catch (OperationCanceledException)
        {
            Status = OperationsConsoleStatus.Idle;
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = OperationsConsoleStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    private async Task CaptureAsync(string section, Func<Task> load)
    {
        try
        {
            await load().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _sectionErrors[section] = ex.Message;
        }
    }

    private void ClearSectionData()
    {
        DeployPreflight = null;
        ManifestDrift = null;
        ManifestVersions = Array.Empty<ManifestVersionResponse>();
        PendingApprovals = Array.Empty<ManifestPendingChangeResponse>();
        GitOpsChanges = Array.Empty<GitOpsChangeRecordResponse>();
        RecentErrors = null;
        TelemetryStatus = null;
        MigrationStatus = null;
    }

    private void Notify() => OnChanged?.Invoke();
}
