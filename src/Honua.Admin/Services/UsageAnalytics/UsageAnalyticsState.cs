// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.UsageAnalytics;

namespace Honua.Admin.Services.UsageAnalytics;

public enum UsageAnalyticsStatus
{
    Idle,
    Loading,
    Ready,
    Error
}

public sealed record UsageAnalyticsRangeOption(string Key, string Label, TimeSpan Duration);

public sealed record QueryThroughputBucket(
    DateTimeOffset Timestamp,
    long QueryCount,
    double QueriesPerSecond,
    double AverageLatencyMs,
    int SlowQueryCount,
    double PercentOfPeak);

public sealed record QueryThroughputAggregate(
    string ServiceName,
    string LayerName,
    string Protocol,
    long QueryCount,
    double AverageQueriesPerSecond,
    double AverageLatencyMs,
    int SlowQueryCount);

public static class UsageAnalyticsRanges
{
    public const string LastHour = "1h";
    public const string Last24Hours = "24h";
    public const string Last7Days = "7d";
    public const string Last30Days = "30d";

    public static IReadOnlyList<UsageAnalyticsRangeOption> Options { get; } =
    [
        new(LastHour, "1 hour", TimeSpan.FromHours(1)),
        new(Last24Hours, "24 hours", TimeSpan.FromHours(24)),
        new(Last7Days, "7 days", TimeSpan.FromDays(7)),
        new(Last30Days, "30 days", TimeSpan.FromDays(30)),
    ];

    public static UsageAnalyticsRangeOption Resolve(string? key)
        => Options.FirstOrDefault(option => string.Equals(option.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? Options[1];
}

public sealed class UsageAnalyticsState
{
    public const string AllFilter = "\u001Fhonua-all";

    private readonly IUsageAnalyticsClient _client;
    private int _loadVersion;

    public UsageAnalyticsState(IUsageAnalyticsClient client)
    {
        _client = client;
    }

    public UsageAnalyticsStatus Status { get; private set; } = UsageAnalyticsStatus.Idle;

    public string RangeKey { get; private set; } = UsageAnalyticsRanges.Last24Hours;

    public string ServiceFilter { get; private set; } = AllFilter;

    public string LayerFilter { get; private set; } = AllFilter;

    public string ProtocolFilter { get; private set; } = AllFilter;

    public string? LastError { get; private set; }

    public UsageAnalyticsReport? Report { get; private set; }

    public event Action? OnChanged;

    public bool IsLoading => Status == UsageAnalyticsStatus.Loading;

    public UsageAnalyticsTotals FilteredTotals
    {
        get
        {
            if (Report is null)
            {
                return new UsageAnalyticsTotals();
            }

            var buckets = ThroughputBuckets;
            var layers = FilteredPopularLayers;
            var storage = FilteredStorageGrowth;
            var users = FilteredUserActivity;
            var slowQueries = FilteredSlowQueries;

            return new UsageAnalyticsTotals
            {
                TotalQueries = layers.Count == 0
                    ? ThroughputSeries.Sum(point => point.QueryCount)
                    : layers.Sum(layer => layer.QueryCount),
                QueriesPerSecond = buckets.Count == 0 ? 0 : Math.Round(buckets.Average(bucket => bucket.QueriesPerSecond), 2),
                UniqueUsers = users.Count,
                SlowQueryCount = slowQueries.Sum(query => query.Count),
                P95LatencyMs = layers.Count == 0 ? 0 : Math.Round(layers.Max(layer => layer.P95LatencyMs), 1),
                StorageBytes = storage.Sum(item => item.CurrentBytes),
                StorageDeltaBytes = storage.Sum(item => item.DeltaBytes),
            };
        }
    }

    public IReadOnlyList<QueryThroughputPoint> ThroughputSeries
        => Report?.QuerySeries.Where(Matches).ToArray() ?? Array.Empty<QueryThroughputPoint>();

    public IReadOnlyList<QueryThroughputBucket> ThroughputBuckets
    {
        get
        {
            var buckets = ThroughputSeries
                .GroupBy(point => point.Timestamp)
                .OrderBy(group => group.Key)
                .Select(group => new QueryThroughputBucket(
                    group.Key,
                    group.Sum(point => point.QueryCount),
                    Math.Round(group.Sum(point => point.QueriesPerSecond), 2),
                    Math.Round(group.Average(point => point.AverageLatencyMs), 1),
                    group.Sum(point => point.SlowQueryCount),
                    PercentOfPeak: 0))
                .ToArray();

            var peak = buckets.Length == 0 ? 0 : buckets.Max(bucket => bucket.QueriesPerSecond);
            if (peak <= 0)
            {
                return buckets;
            }

            return buckets
                .Select(bucket => bucket with { PercentOfPeak = Math.Round(bucket.QueriesPerSecond / peak * 100d, 1) })
                .ToArray();
        }
    }

    public IReadOnlyList<QueryThroughputAggregate> ThroughputRows
        => ThroughputSeries
            .GroupBy(point => new { point.ServiceName, point.LayerName, point.Protocol })
            .Select(group => new QueryThroughputAggregate(
                group.Key.ServiceName,
                group.Key.LayerName,
                group.Key.Protocol,
                group.Sum(point => point.QueryCount),
                Math.Round(group.Average(point => point.QueriesPerSecond), 2),
                Math.Round(group.Average(point => point.AverageLatencyMs), 1),
                group.Sum(point => point.SlowQueryCount)))
            .OrderByDescending(row => row.QueryCount)
            .ToArray();

    public IReadOnlyList<PopularLayerMetric> FilteredPopularLayers
        => Report?.PopularLayers
            .Where(Matches)
            .OrderByDescending(layer => layer.QueryCount)
            .ToArray() ?? Array.Empty<PopularLayerMetric>();

    public IReadOnlyList<EndpointUsageMetric> FilteredPopularEndpoints
        => Report?.PopularEndpoints
            .Where(Matches)
            .OrderByDescending(endpoint => endpoint.QueryCount)
            .ToArray() ?? Array.Empty<EndpointUsageMetric>();

    public IReadOnlyList<SlowQueryMetric> FilteredSlowQueries
        => Report?.SlowQueries
            .Where(Matches)
            .OrderByDescending(query => query.P95DurationMs)
            .ToArray() ?? Array.Empty<SlowQueryMetric>();

    public IReadOnlyList<StorageGrowthMetric> FilteredStorageGrowth
        => Report?.StorageGrowth
            .Where(Matches)
            .OrderByDescending(storage => storage.DeltaBytes)
            .ToArray() ?? Array.Empty<StorageGrowthMetric>();

    public IReadOnlyList<UserActivityMetric> FilteredUserActivity
        => Report?.UserActivity
            .Where(Matches)
            .OrderByDescending(user => user.QueryCount)
            .ToArray() ?? Array.Empty<UserActivityMetric>();

    public IReadOnlyList<string> ServiceOptions
        => BuildOptions(Report is null
            ? []
            : Report.QuerySeries.Select(point => point.ServiceName)
                .Concat(Report.StorageGrowth.Select(storage => storage.ServiceName))
                .Concat(Report.UserActivity.Select(user => user.TopService)));

    public IReadOnlyList<string> LayerOptions
        => BuildOptions(Report is null
            ? []
            : Report.QuerySeries
                .Where(point => MatchesFilter(ServiceFilter, point.ServiceName) && MatchesFilter(ProtocolFilter, point.Protocol))
                .Select(point => point.LayerName)
                .Concat(Report.UserActivity
                    .Where(user => MatchesFilter(ServiceFilter, user.TopService) && MatchesFilter(ProtocolFilter, user.MostUsedProtocol))
                    .Select(user => user.TopLayer)));

    public IReadOnlyList<string> ProtocolOptions
        => BuildOptions(Report is null
            ? []
            : Report.QuerySeries
                .Where(point => MatchesFilter(ServiceFilter, point.ServiceName) && MatchesFilter(LayerFilter, point.LayerName))
                .Select(point => point.Protocol)
                .Concat(Report.PopularEndpoints
                    .Where(endpoint => MatchesFilter(ServiceFilter, endpoint.ServiceName) && MatchesFilter(LayerFilter, endpoint.LayerName))
                    .Select(endpoint => endpoint.Protocol))
                .Concat(Report.UserActivity
                    .Where(user => MatchesFilter(ServiceFilter, user.TopService) && MatchesFilter(LayerFilter, user.TopLayer))
                    .Select(user => user.MostUsedProtocol)));

    public string FilterLabel
    {
        get
        {
            var parts = new[]
            {
                FormatFilter("service", ServiceFilter),
                FormatFilter("layer", LayerFilter),
                FormatFilter("protocol", ProtocolFilter),
            }.Where(part => part is not null);

            var label = string.Join(", ", parts);
            return string.IsNullOrWhiteSpace(label) ? "all services, layers, protocols" : label;
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        Status = UsageAnalyticsStatus.Loading;
        LastError = null;
        Notify();

        try
        {
            var report = await _client.GetReportAsync(BuildQuery(), cancellationToken).ConfigureAwait(false);
            if (version != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            Report = report;
            Status = UsageAnalyticsStatus.Ready;
        }
        catch (OperationCanceledException)
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                Status = UsageAnalyticsStatus.Idle;
            }

            throw;
        }
        catch (Exception ex)
        {
            if (version != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            Status = UsageAnalyticsStatus.Error;
            LastError = ex.Message;
        }
        finally
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                Notify();
            }
        }
    }

    public void SetRange(string rangeKey)
    {
        RangeKey = UsageAnalyticsRanges.Resolve(rangeKey).Key;
        Notify();
    }

    public void SetServiceFilter(string? serviceName)
    {
        ServiceFilter = NormalizeFilter(serviceName);
        if (!ProtocolOptions.Contains(ProtocolFilter, StringComparer.OrdinalIgnoreCase))
        {
            ProtocolFilter = AllFilter;
        }

        if (!LayerOptions.Contains(LayerFilter, StringComparer.OrdinalIgnoreCase))
        {
            LayerFilter = AllFilter;
        }

        Notify();
    }

    public void SetLayerFilter(string? layerName)
    {
        LayerFilter = NormalizeFilter(layerName);
        if (!ProtocolOptions.Contains(ProtocolFilter, StringComparer.OrdinalIgnoreCase))
        {
            ProtocolFilter = AllFilter;
        }

        Notify();
    }

    public void SetProtocolFilter(string? protocol)
    {
        ProtocolFilter = NormalizeFilter(protocol);
        if (!LayerOptions.Contains(LayerFilter, StringComparer.OrdinalIgnoreCase))
        {
            LayerFilter = AllFilter;
        }

        Notify();
    }

    public UsageAnalyticsExportPayload ExportCsv()
        => UsageAnalyticsReportExporter.ToCsv(BuildExportView());

    public UsageAnalyticsExportPayload ExportPdf()
        => UsageAnalyticsReportExporter.ToPdf(BuildExportView());

    private UsageAnalyticsQuery BuildQuery()
        => new()
        {
            RangeKey = RangeKey,
            ServiceName = FilterOrNull(ServiceFilter),
            LayerName = FilterOrNull(LayerFilter),
            Protocol = FilterOrNull(ProtocolFilter),
        };

    private UsageAnalyticsExportView BuildExportView()
    {
        if (Report is null)
        {
            throw new InvalidOperationException("Usage analytics has not loaded.");
        }

        return new UsageAnalyticsExportView(
            Report,
            FilteredTotals,
            FilteredPopularLayers,
            FilteredPopularEndpoints,
            FilteredSlowQueries,
            FilteredStorageGrowth,
            FilteredUserActivity,
            FilterLabel);
    }

    private bool Matches(QueryThroughputPoint point)
        => MatchesFilter(ServiceFilter, point.ServiceName) &&
            MatchesFilter(LayerFilter, point.LayerName) &&
            MatchesFilter(ProtocolFilter, point.Protocol);

    private bool Matches(PopularLayerMetric layer)
        => MatchesFilter(ServiceFilter, layer.ServiceName) &&
            MatchesFilter(LayerFilter, layer.LayerName) &&
            MatchesFilter(ProtocolFilter, layer.Protocol);

    private bool Matches(EndpointUsageMetric endpoint)
        => MatchesFilter(ServiceFilter, endpoint.ServiceName) &&
            MatchesFilter(LayerFilter, endpoint.LayerName) &&
            MatchesFilter(ProtocolFilter, endpoint.Protocol);

    private bool Matches(SlowQueryMetric query)
        => MatchesFilter(ServiceFilter, query.ServiceName) &&
            MatchesFilter(LayerFilter, query.LayerName) &&
            MatchesFilter(ProtocolFilter, query.Protocol);

    private bool Matches(StorageGrowthMetric storage)
        => MatchesFilter(ServiceFilter, storage.ServiceName) &&
            MatchesFilter(LayerFilter, storage.LayerNames) &&
            MatchesFilter(ProtocolFilter, storage.Protocols);

    private bool Matches(UserActivityMetric user)
        => MatchesFilter(ServiceFilter, user.TopService) &&
            MatchesFilter(LayerFilter, user.TopLayer) &&
            MatchesFilter(ProtocolFilter, user.MostUsedProtocol);

    private static bool MatchesFilter(string filter, string value)
        => string.Equals(filter, AllFilter, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesFilter(string filter, IReadOnlyList<string> values)
        => string.Equals(filter, AllFilter, StringComparison.OrdinalIgnoreCase) ||
            values.Contains(filter, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<string> BuildOptions(IEnumerable<string> values)
    {
        var options = new List<string> { AllFilter };
        options.AddRange(values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        return options;
    }

    private static string NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? AllFilter : value;

    private static string? FilterOrNull(string filter)
        => string.Equals(filter, AllFilter, StringComparison.OrdinalIgnoreCase) ? null : filter;

    private static string? FormatFilter(string name, string value)
        => string.Equals(value, AllFilter, StringComparison.OrdinalIgnoreCase)
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{name}: {value}");

    private void Notify() => OnChanged?.Invoke();
}
