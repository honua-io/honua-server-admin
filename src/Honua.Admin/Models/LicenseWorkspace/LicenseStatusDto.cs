using System;
using System.Collections.Generic;

namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Read-side projection of the honua-server <c>LicenseStatusResponse</c>. The
/// admin UI never deserializes the signed license file itself — only metadata
/// the server has already extracted and chosen to expose.
/// </summary>
public sealed class LicenseStatusDto
{
    /// <summary>
    /// Default "Issued by" surface value when the server response omits
    /// <see cref="IssuanceSource"/>. Marketplace adapters
    /// (<c>honua-server#804</c>) will populate the field directly; until then
    /// every license is BYOL-issued.
    /// </summary>
    public const string DefaultIssuanceSource = "BYOL portal";

    public string Edition { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? IssuedAt { get; init; }

    public string? LicensedTo { get; init; }

    public bool IsValid { get; init; }

    /// <summary>
    /// Where the license was issued from — "BYOL portal" today, "AWS Marketplace"
    /// or "Azure Marketplace" once the marketplace adapters from
    /// <c>honua-io/honua-server#804</c> land. The admin UI defaults to
    /// "BYOL portal" client-side so existing servers (which do not yet emit
    /// this field) keep displaying a sensible "Issued by" cell. See design
    /// brief decision #7.
    /// </summary>
    public string? IssuanceSource { get; init; }

    /// <summary>
    /// Free-form server validation state (e.g. "valid", "expired",
    /// "invalid signature"). The classifier in
    /// <see cref="Services.LicenseWorkspace.LicenseDiagnosticClassifier"/>
    /// pattern-matches on this until the server publishes a stable
    /// <c>LicenseValidationCode</c> enum (gap report).
    /// </summary>
    public string ValidationState { get; init; } = string.Empty;

    /// <summary>
    /// Server-computed days-until-expiry hint. The UI re-derives expiry banding
    /// in UTC from <see cref="ExpiresAt"/> so behaviour stays correct even when
    /// the server omits the hint.
    /// </summary>
    public int? DaysUntilExpiry { get; init; }

    public bool ExpiryWarning { get; init; }

    public IReadOnlyList<EntitlementDto> Entitlements { get; init; } = Array.Empty<EntitlementDto>();
}
