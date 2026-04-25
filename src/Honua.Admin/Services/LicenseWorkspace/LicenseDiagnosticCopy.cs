using Honua.Admin.Models.LicenseWorkspace;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// Title + remediation copy per <see cref="LicenseDiagnostic"/>. Centralised so
/// the status banner, upload-failure surface, and entitlement detail view all
/// render the same operator-action text.
/// </summary>
public static class LicenseDiagnosticCopy
{
    public sealed record Copy(string Title, string Action);

    public static Copy For(LicenseDiagnostic diagnostic) => diagnostic switch
    {
        LicenseDiagnostic.Valid => new Copy(
            "License valid",
            "No action required."),
        LicenseDiagnostic.Expired => new Copy(
            "License expired",
            "Upload a renewed license file or contact your account owner to issue a new one."),
        LicenseDiagnostic.InvalidSignature => new Copy(
            "Invalid license signature",
            "Re-download the license file from your portal — do not edit the file. If the problem persists, contact support with the issued-to value below."),
        LicenseDiagnostic.EndpointUnreachable => new Copy(
            "Licensing endpoint unreachable",
            "Verify the server is running and reachable from this admin host, then retry. If the issue continues, check server logs for licensing service errors."),
        LicenseDiagnostic.AuthenticationFailure => new Copy(
            "Admin credentials rejected",
            "Confirm the admin API key is configured and has the licensing scope, then refresh."),
        LicenseDiagnostic.FeatureNotEntitled => new Copy(
            "Feature not entitled under current edition",
            "Upgrade the server edition or activate the entitlement before retrying the operation that triggered this."),
        LicenseDiagnostic.Unknown => new Copy(
            "License is not currently valid",
            "Refresh, then file a support ticket including the validation state shown below if the issue persists."),
        _ => new Copy("License status", "Refresh to retry.")
    };
}
