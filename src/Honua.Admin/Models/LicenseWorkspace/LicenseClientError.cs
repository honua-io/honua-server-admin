namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Why a license-client call did not yield usable data. Mapped to a
/// <see cref="LicenseDiagnostic"/> by the classifier so the upload + status
/// flows can render the same operator-action copy.
/// </summary>
public enum LicenseClientErrorKind
{
    /// <summary>
    /// Network / DNS / TLS / timeout — the licensing endpoint did not respond.
    /// </summary>
    Transport,

    /// <summary>
    /// Server returned 401/403. Admin credentials need attention.
    /// </summary>
    Authentication,

    /// <summary>
    /// Server returned a 4xx other than auth (e.g. 400 on a malformed upload).
    /// </summary>
    BadRequest,

    /// <summary>
    /// Server returned 5xx (or any other unexpected status).
    /// </summary>
    Server,

    /// <summary>
    /// Response decoded but failed local validation (e.g. empty edition).
    /// </summary>
    Protocol
}

/// <summary>
/// Structured error returned from the license client in lieu of throwing.
/// Carries enough context for the diagnostics surface to render
/// operator-actionable copy without reflecting raw exceptions to the UI.
/// </summary>
public sealed record LicenseClientError(
    LicenseClientErrorKind Kind,
    string Message,
    int? StatusCode = null);
