using Honua.Admin.Models.LicenseWorkspace;
using Honua.Admin.Services.LicenseWorkspace;
using Xunit;

namespace Honua.Admin.Tests.LicenseWorkspace;

public sealed class ExpiryBandClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Null_expiry_classifies_as_perpetual()
    {
        Assert.Equal(ExpiryBand.Perpetual, ExpiryBandClassifier.Classify(null, Now));
    }

    [Fact]
    public void Past_expiry_classifies_as_expired()
    {
        Assert.Equal(ExpiryBand.Expired, ExpiryBandClassifier.Classify(Now.AddDays(-1), Now));
    }

    [Theory]
    [InlineData(0, ExpiryBand.Warn1)]
    [InlineData(1, ExpiryBand.Warn1)]
    [InlineData(2, ExpiryBand.Warn7)]
    [InlineData(7, ExpiryBand.Warn7)]
    [InlineData(8, ExpiryBand.Warn14)]
    [InlineData(14, ExpiryBand.Warn14)]
    [InlineData(15, ExpiryBand.Warn30)]
    [InlineData(30, ExpiryBand.Warn30)]
    [InlineData(31, ExpiryBand.Healthy)]
    [InlineData(365, ExpiryBand.Healthy)]
    public void Days_remaining_drives_band(int daysRemaining, ExpiryBand expected)
    {
        // ComputeDaysRemaining truncates to date, so add 1h to keep both
        // sides on the same UTC day after rounding.
        var expiry = Now.UtcDateTime.Date.AddDays(daysRemaining).AddHours(1);
        Assert.Equal(expected, ExpiryBandClassifier.Classify(new DateTimeOffset(expiry, TimeSpan.Zero), Now));
    }

    [Fact]
    public void Banding_uses_utc_dates_so_negative_offsets_do_not_flip_band()
    {
        // Operator in -10:00 timezone; UTC date is 2026-04-25, local is still 2026-04-25.
        // Expiry stamp is 2026-04-26 02:00 UTC (i.e. 16:00 of the same local day).
        // Classifier should still report Warn1 because UTC day delta is exactly 1.
        var expiry = new DateTimeOffset(2026, 4, 26, 2, 0, 0, TimeSpan.Zero);
        var nowInLocal = new DateTimeOffset(2026, 4, 25, 12, 0, 0, TimeSpan.FromHours(-10));
        Assert.Equal(ExpiryBand.Warn1, ExpiryBandClassifier.Classify(expiry, nowInLocal));
    }
}
