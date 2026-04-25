using System;
using Honua.Admin.Models.LicenseWorkspace;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// Single source of truth for translating server license-status responses (or
/// transport failures) into the operator-actionable
/// <see cref="LicenseDiagnostic"/> set. The string-pattern match on
/// <c>ValidationState</c> is intentionally contained here so the gap-ticket
/// fix on the server side (a stable enum) replaces it cleanly — see design
/// risk #2 and the gap report.
/// </summary>
public static class LicenseDiagnosticClassifier
{
    public static LicenseDiagnostic Classify(LicenseClientError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error.Kind switch
        {
            LicenseClientErrorKind.Authentication => LicenseDiagnostic.AuthenticationFailure,
            LicenseClientErrorKind.Transport => LicenseDiagnostic.EndpointUnreachable,
            LicenseClientErrorKind.Server => LicenseDiagnostic.EndpointUnreachable,
            LicenseClientErrorKind.BadRequest => LicenseDiagnostic.Unknown,
            LicenseClientErrorKind.Protocol => LicenseDiagnostic.Unknown,
            _ => LicenseDiagnostic.Unknown
        };
    }

    public static LicenseDiagnostic Classify(LicenseStatusDto status)
    {
        ArgumentNullException.ThrowIfNull(status);

        if (status.IsValid)
        {
            return ExpiryBandClassifier.Classify(status.ExpiresAt) == ExpiryBand.Expired
                ? LicenseDiagnostic.Expired
                : LicenseDiagnostic.Valid;
        }

        var state = (status.ValidationState ?? string.Empty).Trim();

        if (ContainsAny(state, "expired", "expiry"))
        {
            return LicenseDiagnostic.Expired;
        }

        if (ContainsAny(state, "signature", "signed", "tamper", "verification"))
        {
            return LicenseDiagnostic.InvalidSignature;
        }

        // A server that reports IsValid=false but ExpiresAt in the past with no
        // explicit string still counts as expired for operator action.
        if (ExpiryBandClassifier.Classify(status.ExpiresAt) == ExpiryBand.Expired)
        {
            return LicenseDiagnostic.Expired;
        }

        return LicenseDiagnostic.Unknown;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
