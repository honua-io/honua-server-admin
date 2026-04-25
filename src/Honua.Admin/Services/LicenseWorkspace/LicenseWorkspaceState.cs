using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.LicenseWorkspace;

namespace Honua.Admin.Services.LicenseWorkspace;

public enum LicenseWorkspaceStatus
{
    Idle,
    Loading,
    Uploading,
    Error
}

/// <summary>
/// Scoped observable store backing the License workspace. All mutations funnel
/// through explicit methods so the diagnostic classifier, telemetry, and
/// upload-redaction invariant stay single-origin.
/// </summary>
public sealed class LicenseWorkspaceState
{
    private static readonly IReadOnlyList<EntitlementDto> EmptyEntitlements = Array.Empty<EntitlementDto>();

    private readonly ILicenseWorkspaceClient _client;
    private readonly ILicenseWorkspaceTelemetry _telemetry;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LicenseWorkspaceState(ILicenseWorkspaceClient client, ILicenseWorkspaceTelemetry telemetry)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    public LicenseStatusDto? Status { get; private set; }

    public IReadOnlyList<EntitlementDto> Entitlements { get; private set; } = EmptyEntitlements;

    public LicenseWorkspaceStatus WorkflowStatus { get; private set; } = LicenseWorkspaceStatus.Idle;

    public LicenseDiagnostic Diagnostic { get; private set; } = LicenseDiagnostic.Unknown;

    public ExpiryBand ExpiryBand { get; private set; } = ExpiryBand.Perpetual;

    /// <summary>
    /// Last error returned by the client (if any). Cleared on the next
    /// successful refresh.
    /// </summary>
    public LicenseClientError? LastError { get; private set; }

    public DateTimeOffset? LastRefreshedUtc { get; private set; }

    public bool IsBusy => WorkflowStatus is LicenseWorkspaceStatus.Loading or LicenseWorkspaceStatus.Uploading;

    public event Action? OnChanged;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Replace the active license. The byte buffer is consumed by the client
    /// and is NOT retained on the state — the upload action owns the only
    /// reference, and the workspace re-fetches status from the server rather
    /// than trusting the upload response alone.
    /// </summary>
    public async Task UploadAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            // Treat empty selection as a no-op so the dialog can display a
            // validation message without flipping the state into Error.
            _telemetry.Record("upload_attempted", new Dictionary<string, object?>
            {
                ["bytes"] = 0,
                ["result"] = "empty"
            });
            return;
        }

        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            WorkflowStatus = LicenseWorkspaceStatus.Uploading;
            Notify();

            _telemetry.Record("upload_attempted", new Dictionary<string, object?>
            {
                ["bytes"] = bytes.Length
            });

            var watch = Stopwatch.StartNew();
            var uploadResult = await _client.UploadLicenseAsync(bytes, cancellationToken).ConfigureAwait(false);
            watch.Stop();

            if (!uploadResult.IsSuccess)
            {
                LastError = uploadResult.Error;
                Diagnostic = LicenseDiagnosticClassifier.Classify(uploadResult.Error!);
                WorkflowStatus = LicenseWorkspaceStatus.Error;
                _telemetry.Record("upload_failed", new Dictionary<string, object?>
                {
                    ["kind"] = uploadResult.Error!.Kind.ToString(),
                    ["status_code"] = uploadResult.Error.StatusCode
                });
                _telemetry.Record("diagnostic_observed", new Dictionary<string, object?>
                {
                    ["kind"] = Diagnostic.ToString()
                });
                Notify();
                return;
            }

            _telemetry.RecordLatency("upload_succeeded", watch.ElapsedMilliseconds);

            // Refresh from GET so we never rely on the upload response as the
            // sole source of truth. The refresh runs under the same gate slot.
            await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        WorkflowStatus = LicenseWorkspaceStatus.Loading;
        Notify();

        var watch = Stopwatch.StartNew();
        var result = await _client.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        watch.Stop();

        ApplyStatusResult(result, "status_loaded", watch.ElapsedMilliseconds);
    }

    private void ApplyStatusResult(LicenseClientResult<LicenseStatusDto> result, string telemetryName, long elapsedMs)
    {
        if (!result.IsSuccess)
        {
            LastError = result.Error;
            Status = null;
            Entitlements = EmptyEntitlements;
            ExpiryBand = ExpiryBand.Perpetual;
            Diagnostic = LicenseDiagnosticClassifier.Classify(result.Error!);
            WorkflowStatus = LicenseWorkspaceStatus.Error;
            _telemetry.Record("status_load_failed", new Dictionary<string, object?>
            {
                ["kind"] = result.Error!.Kind.ToString(),
                ["status_code"] = result.Error.StatusCode
            });
            _telemetry.Record("diagnostic_observed", new Dictionary<string, object?>
            {
                ["kind"] = Diagnostic.ToString()
            });
            Notify();
            return;
        }

        Status = result.Value!;
        Entitlements = Status.Entitlements ?? EmptyEntitlements;
        ExpiryBand = ExpiryBandClassifier.Classify(Status.ExpiresAt);
        Diagnostic = LicenseDiagnosticClassifier.Classify(Status);
        LastError = null;
        LastRefreshedUtc = DateTimeOffset.UtcNow;
        WorkflowStatus = LicenseWorkspaceStatus.Idle;
        _telemetry.RecordLatency(telemetryName, elapsedMs, new Dictionary<string, object?>
        {
            ["edition"] = Status.Edition,
            ["entitlements"] = Entitlements.Count,
            ["band"] = ExpiryBand.ToString()
        });
        _telemetry.Record("diagnostic_observed", new Dictionary<string, object?>
        {
            ["kind"] = Diagnostic.ToString()
        });
        Notify();
    }

    /// <summary>
    /// Entitlement-detail surface: the workspace exposes a stable lookup so
    /// "feature not entitled" callers can land on the row that triggered them.
    /// </summary>
    public EntitlementDto? FindEntitlement(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }
        for (var i = 0; i < Entitlements.Count; i++)
        {
            if (string.Equals(Entitlements[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return Entitlements[i];
            }
        }
        return null;
    }

    private void Notify() => OnChanged?.Invoke();
}
