using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.LicenseWorkspace;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// Deterministic in-memory client used by tests and offline preview. Lets
/// every <see cref="LicenseDiagnostic"/> outcome and every
/// <see cref="ExpiryBand"/> drive the workspace without a live server. The
/// stub never inspects uploaded bytes — the redaction invariant lives at the
/// state layer, the stub just records that an upload happened.
/// </summary>
public sealed class StubLicenseWorkspaceClient : ILicenseWorkspaceClient
{
    private LicenseStatusResponse _status;
    private LicenseClientError? _statusError;
    private LicenseClientError? _uploadError;

    public StubLicenseWorkspaceClient()
        : this(BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(180)))
    {
    }

    public StubLicenseWorkspaceClient(LicenseStatusResponse initialStatus)
    {
        _status = initialStatus;
    }

    /// <summary>
    /// Test-only knob: count of upload calls. Lets state-level tests assert
    /// the upload flow refreshed status from <c>GetStatusAsync</c> rather than
    /// trusting the upload response alone.
    /// </summary>
    public int UploadCallCount { get; private set; }

    /// <summary>
    /// Test-only knob: the last byte length the stub saw. The stub does NOT
    /// retain the uploaded bytes — only their length, to assert the workspace
    /// state layer drops references after the call.
    /// </summary>
    public int LastUploadLength { get; private set; }

    public void SetStatus(LicenseStatusResponse status)
    {
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _statusError = null;
    }

    public void SetStatusError(LicenseClientError? error)
    {
        _statusError = error;
    }

    public void SetUploadError(LicenseClientError? error)
    {
        _uploadError = error;
    }

    public Task<LicenseClientResult<LicenseStatusResponse>> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_statusError is not null)
        {
            return Task.FromResult(LicenseClientResult<LicenseStatusResponse>.Failure(_statusError));
        }
        return Task.FromResult(LicenseClientResult<LicenseStatusResponse>.Success(_status));
    }

    public Task<LicenseClientResult<EntitlementListBox>> GetEntitlementsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_statusError is not null)
        {
            return Task.FromResult(LicenseClientResult<EntitlementListBox>.Failure(_statusError));
        }
        var box = new EntitlementListBox { Items = _status.Entitlements };
        return Task.FromResult(LicenseClientResult<EntitlementListBox>.Success(box));
    }

    public Task<LicenseClientResult<LicenseStatusResponse>> UploadLicenseAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        cancellationToken.ThrowIfCancellationRequested();

        UploadCallCount++;
        LastUploadLength = bytes.Length;

        if (_uploadError is not null)
        {
            return Task.FromResult(LicenseClientResult<LicenseStatusResponse>.Failure(_uploadError));
        }

        // Successful uploads in the stub flip the in-memory status to a
        // freshly-issued enterprise license expiring in 365 days. Component
        // tests can override with SetStatus() before/after.
        var refreshed = BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(365));
        _status = refreshed;
        return Task.FromResult(LicenseClientResult<LicenseStatusResponse>.Success(refreshed));
    }

    public static LicenseStatusResponse BuildHealthyEnterprise(DateTimeOffset expiresAt) => new()
    {
        Edition = "Enterprise",
        ExpiresAt = expiresAt,
        IssuedAt = DateTimeOffset.UtcNow.AddDays(-1),
        LicensedTo = "Honua Demo Org",
        IssuanceSource = LicenseStatusResponse.DefaultIssuanceSource,
        IsValid = true,
        ValidationState = "valid",
        Entitlements = BuildSampleEntitlements(allActive: true)
    };

    public static LicenseStatusResponse BuildExpired() => new()
    {
        Edition = "Professional",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(-3),
        IssuedAt = DateTimeOffset.UtcNow.AddDays(-365),
        LicensedTo = "Honua Demo Org",
        IssuanceSource = LicenseStatusResponse.DefaultIssuanceSource,
        IsValid = false,
        ValidationState = "expired",
        Entitlements = BuildSampleEntitlements(allActive: false)
    };

    public static LicenseStatusResponse BuildInvalidSignature() => new()
    {
        Edition = "Unknown",
        ExpiresAt = null,
        IssuedAt = null,
        LicensedTo = null,
        IsValid = false,
        ValidationState = "invalid signature",
        Entitlements = Array.Empty<LicenseEntitlement>()
    };

    public static LicenseStatusResponse BuildPerpetualCommunity() => new()
    {
        Edition = "Community",
        ExpiresAt = null,
        IssuedAt = DateTimeOffset.UtcNow.AddDays(-30),
        LicensedTo = null,
        IssuanceSource = LicenseStatusResponse.DefaultIssuanceSource,
        IsValid = true,
        ValidationState = "valid",
        Entitlements = BuildSampleEntitlements(allActive: false)
    };

    public static IReadOnlyList<LicenseEntitlement> BuildSampleEntitlements(bool allActive) => new[]
    {
        new LicenseEntitlement { Key = "oidc", Name = "OIDC sign-in", IsActive = allActive },
        new LicenseEntitlement { Key = "rbac", Name = "Role-based access control", IsActive = allActive },
        new LicenseEntitlement { Key = "rate-limiting", Name = "Rate limiting", IsActive = allActive },
        new LicenseEntitlement { Key = "audit-export", Name = "Audit export", IsActive = false }
    };
}
