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

    [Fact]
    public void Same_day_earlier_than_now_classifies_as_expired()
    {
        // Expiry earlier today (UTC) — date-truncated days remaining is 0,
        // but the precise instant has already passed.
        Assert.Equal(ExpiryBand.Expired, ExpiryBandClassifier.Classify(Now.AddHours(-2), Now));
    }

    [Fact]
    public void Exact_now_instant_classifies_as_expired()
    {
        Assert.Equal(ExpiryBand.Expired, ExpiryBandClassifier.Classify(Now, Now));
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
        // Anchor expiry one hour after Now so daysRemaining=0 means "later
        // today" (a future same-UTC-day instant), without colliding with the
        // same-day-already-expired short-circuit.
        var expiry = Now.AddDays(daysRemaining).AddHours(1);
        Assert.Equal(expected, ExpiryBandClassifier.Classify(expiry, Now));
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

    [Fact]
    public void Format_relative_day_expired_same_utc_day_says_earlier_today()
    {
        // Date-truncated days = 0 paired with the Expired band must render as
        // "earlier today" so the headline ("License expired") and the detail
        // copy stay consistent.
        Assert.Equal("earlier today", ExpiryBandClassifier.FormatRelativeDay(ExpiryBand.Expired, 0));
    }

    [Fact]
    public void Format_relative_day_expired_prior_day_renders_days_ago()
    {
        Assert.Equal("3 day(s) ago", ExpiryBandClassifier.FormatRelativeDay(ExpiryBand.Expired, -3));
    }

    [Fact]
    public void Format_relative_day_future_same_utc_day_says_later_today()
    {
        Assert.Equal("later today", ExpiryBandClassifier.FormatRelativeDay(ExpiryBand.Warn1, 0));
    }

    [Theory]
    [InlineData(ExpiryBand.Warn1, 1, "in 1 day(s)")]
    [InlineData(ExpiryBand.Warn7, 5, "in 5 day(s)")]
    [InlineData(ExpiryBand.Healthy, 180, "in 180 day(s)")]
    public void Format_relative_day_future_renders_in_n_days(ExpiryBand band, int days, string expected)
    {
        Assert.Equal(expected, ExpiryBandClassifier.FormatRelativeDay(band, days));
    }
}
