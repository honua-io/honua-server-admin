namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Visual band the operator sees for license expiry. Computed in UTC from
/// <see cref="LicenseStatusDto.ExpiresAt"/> by
/// <see cref="Services.LicenseWorkspace.ExpiryBandClassifier"/>.
/// </summary>
public enum ExpiryBand
{
    /// <summary>
    /// More than 30 days remaining. Informational only.
    /// </summary>
    Healthy,

    /// <summary>
    /// 30 days or less remaining. First warning band.
    /// </summary>
    Warn30,

    /// <summary>
    /// 14 days or less remaining.
    /// </summary>
    Warn14,

    /// <summary>
    /// 7 days or less remaining.
    /// </summary>
    Warn7,

    /// <summary>
    /// 1 day or less remaining. Critical.
    /// </summary>
    Warn1,

    /// <summary>
    /// Expiry has passed.
    /// </summary>
    Expired,

    /// <summary>
    /// License has no expiry (perpetual / community edition).
    /// </summary>
    Perpetual
}
