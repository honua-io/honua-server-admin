using System;
using Honua.Admin.Models.LicenseWorkspace;

namespace Honua.Admin.Services.LicenseWorkspace;

/// <summary>
/// Pure UI computation of the expiry warning band. Operates in UTC against
/// <see cref="DateTimeOffset.UtcNow"/> so the band stays correct regardless of
/// the operator's browser timezone — see design risk #7.
/// </summary>
public static class ExpiryBandClassifier
{
    public static ExpiryBand Classify(DateTimeOffset? expiresAt) =>
        Classify(expiresAt, DateTimeOffset.UtcNow);

    public static ExpiryBand Classify(DateTimeOffset? expiresAt, DateTimeOffset nowUtc)
    {
        if (expiresAt is null)
        {
            return ExpiryBand.Perpetual;
        }

        // Precise-instant check first: a license that already expired earlier
        // today (same UTC day) would otherwise truncate to days=0 and be
        // misreported as Warn1.
        if (expiresAt.Value <= nowUtc)
        {
            return ExpiryBand.Expired;
        }

        var days = ComputeDaysRemaining(expiresAt.Value, nowUtc);

        if (days <= 1)
        {
            return ExpiryBand.Warn1;
        }
        if (days <= 7)
        {
            return ExpiryBand.Warn7;
        }
        if (days <= 14)
        {
            return ExpiryBand.Warn14;
        }
        if (days <= 30)
        {
            return ExpiryBand.Warn30;
        }
        return ExpiryBand.Healthy;
    }

    /// <summary>
    /// Days remaining truncated to whole days in UTC. The truncation prevents
    /// off-by-one banding across timezone boundaries (a license that expires
    /// "tomorrow at noon UTC" should always read as 1 day remaining for every
    /// operator, regardless of where they are).
    /// </summary>
    public static int ComputeDaysRemaining(DateTimeOffset expiresAt, DateTimeOffset nowUtc)
    {
        var expiryUtcDate = expiresAt.UtcDateTime.Date;
        var todayUtcDate = nowUtc.UtcDateTime.Date;
        return (int)(expiryUtcDate - todayUtcDate).TotalDays;
    }
}
