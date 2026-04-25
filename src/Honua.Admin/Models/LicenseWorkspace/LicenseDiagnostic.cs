namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Operator-actionable license failure mode. Each value maps to a distinct
/// remediation copy block in the diagnostics surface — see
/// <see cref="Services.LicenseWorkspace.LicenseDiagnosticClassifier"/>.
/// </summary>
public enum LicenseDiagnostic
{
    /// <summary>
    /// License loaded, signature verified, not expired. No banner.
    /// </summary>
    Valid,

    /// <summary>
    /// License previously loaded but the expiry date has passed.
    /// </summary>
    Expired,

    /// <summary>
    /// Server rejected the license signature. Operator must re-download the
    /// canonical file rather than edit it.
    /// </summary>
    InvalidSignature,

    /// <summary>
    /// Transport error reaching the licensing endpoint (timeout / 5xx /
    /// network). Operator verifies the server is reachable.
    /// </summary>
    EndpointUnreachable,

    /// <summary>
    /// Server is reachable but rejected the admin credentials (401/403).
    /// Distinct sub-state of the unreachable family with different remediation.
    /// </summary>
    AuthenticationFailure,

    /// <summary>
    /// A specific feature is gated by edition / entitlement. Surfaces alongside
    /// the entitlement row that triggered the diagnostic.
    /// </summary>
    FeatureNotEntitled,

    /// <summary>
    /// Server returned a non-Valid state we could not classify. Generic copy
    /// invites the operator to file a support ticket.
    /// </summary>
    Unknown
}
