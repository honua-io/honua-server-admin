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

public sealed class StubUsageAnalyticsClient : IUsageAnalyticsClient
{
    private static readonly DateTimeOffset BaselineEnd = DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture);

    public Task<UsageAnalyticsReport> GetReportAsync(UsageAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var range = UsageAnalyticsRanges.Resolve(query.RangeKey);
        var rangeEnd = BaselineEnd;
        var rangeStart = rangeEnd - range.Duration;
        var scale = range.Duration.TotalHours / 24d;

        var series = BuildSeries(rangeStart, rangeEnd, scale);
        var popularLayers = Scale(BuildPopularLayers(), scale).ToArray();
        var endpoints = Scale(BuildEndpoints(), scale).ToArray();
        var slowQueries = Scale(BuildSlowQueries(rangeEnd), scale).ToArray();
        var storage = BuildStorage(scale).ToArray();
        var users = Scale(BuildUsers(rangeEnd), scale).ToArray();

        var report = new UsageAnalyticsReport
        {
            GeneratedAt = rangeEnd,
            RangeStart = rangeStart,
            RangeEnd = rangeEnd,
            RangeLabel = range.Label,
            DataSource = "preview-analytics",
            QuerySeries = series,
            PopularLayers = popularLayers,
            PopularEndpoints = endpoints,
            SlowQueries = slowQueries,
            StorageGrowth = storage,
            UserActivity = users,
            Totals = new UsageAnalyticsTotals
            {
                TotalQueries = popularLayers.Sum(layer => layer.QueryCount),
                QueriesPerSecond = Math.Round(series.GroupBy(point => point.Timestamp).Average(group => group.Sum(point => point.QueriesPerSecond)), 2),
                UniqueUsers = users.Count(user => user.QueryCount > 0),
                SlowQueryCount = slowQueries.Sum(query => query.Count),
                P95LatencyMs = Math.Round(popularLayers.Max(layer => layer.P95LatencyMs), 1),
                StorageBytes = storage.Sum(item => item.CurrentBytes),
                StorageDeltaBytes = storage.Sum(item => item.DeltaBytes),
            },
        };

        return Task.FromResult(report);
    }

    private static IReadOnlyList<QueryThroughputPoint> BuildSeries(DateTimeOffset rangeStart, DateTimeOffset rangeEnd, double scale)
    {
        var buckets = 12;
        var step = TimeSpan.FromTicks((rangeEnd - rangeStart).Ticks / buckets);
        var dimensions = new[]
        {
            new SeriesSeed("default", "Parcels", "FeatureServer", 9.2, 84, 3),
            new SeriesSeed("default", "Parcels", "OGC Features", 4.7, 112, 2),
            new SeriesSeed("imagery", "Orthophoto mosaic", "ImageServer", 3.1, 188, 4),
            new SeriesSeed("planning", "Zoning districts", "MapServer", 1.8, 146, 1),
            new SeriesSeed("basemap", "Address points", "OData", 1.2, 96, 1),
        };

        var points = new List<QueryThroughputPoint>(buckets * dimensions.Length);
        for (var i = 0; i < buckets; i++)
        {
            var timestamp = rangeStart + TimeSpan.FromTicks(step.Ticks * (i + 1));
            var rhythm = 0.82 + (Math.Sin(i / 1.8) + 1d) * 0.16;
            var peak = i is >= 5 and <= 8 ? 1.22 : 1d;
            foreach (var seed in dimensions)
            {
                var qps = seed.BaseQps * rhythm * peak * Math.Clamp(scale, 0.35, 8d);
                points.Add(new QueryThroughputPoint
                {
                    Timestamp = timestamp,
                    ServiceName = seed.ServiceName,
                    LayerName = seed.LayerName,
                    Protocol = seed.Protocol,
                    QueryCount = Math.Max(1, (long)Math.Round(qps * step.TotalSeconds)),
                    QueriesPerSecond = Math.Round(qps, 2),
                    AverageLatencyMs = Math.Round(seed.AverageLatencyMs * (1 + (i % 4) * 0.04), 1),
                    SlowQueryCount = Math.Max(0, (int)Math.Round(seed.BaseSlowCount * rhythm * Math.Max(1, scale / 2d))),
                });
            }
        }

        return points;
    }

    private static IEnumerable<PopularLayerMetric> BuildPopularLayers()
    {
        yield return new PopularLayerMetric { ServiceName = "default", LayerName = "Parcels", Protocol = "FeatureServer", QueryCount = 796_000, QueriesPerSecond = 9.21, UniqueUsers = 82, P95LatencyMs = 214, ErrorRate = 0.003 };
        yield return new PopularLayerMetric { ServiceName = "default", LayerName = "Parcels", Protocol = "OGC Features", QueryCount = 405_000, QueriesPerSecond = 4.69, UniqueUsers = 44, P95LatencyMs = 248, ErrorRate = 0.006 };
        yield return new PopularLayerMetric { ServiceName = "imagery", LayerName = "Orthophoto mosaic", Protocol = "ImageServer", QueryCount = 268_000, QueriesPerSecond = 3.10, UniqueUsers = 27, P95LatencyMs = 436, ErrorRate = 0.011 };
        yield return new PopularLayerMetric { ServiceName = "planning", LayerName = "Zoning districts", Protocol = "MapServer", QueryCount = 156_000, QueriesPerSecond = 1.81, UniqueUsers = 19, P95LatencyMs = 326, ErrorRate = 0.004 };
        yield return new PopularLayerMetric { ServiceName = "basemap", LayerName = "Address points", Protocol = "OData", QueryCount = 104_000, QueriesPerSecond = 1.20, UniqueUsers = 13, P95LatencyMs = 184, ErrorRate = 0.001 };
    }

    private static IEnumerable<EndpointUsageMetric> BuildEndpoints()
    {
        yield return new EndpointUsageMetric { ServiceName = "default", LayerName = "Parcels", Path = "/rest/services/default/FeatureServer/101/query", Protocol = "FeatureServer", QueryCount = 668_000, QueriesPerSecond = 7.73, P95LatencyMs = 220, ErrorRate = 0.002 };
        yield return new EndpointUsageMetric { ServiceName = "default", LayerName = "Parcels", Path = "/ogc/features/collections/parcels/items", Protocol = "OGC Features", QueryCount = 405_000, QueriesPerSecond = 4.69, P95LatencyMs = 248, ErrorRate = 0.006 };
        yield return new EndpointUsageMetric { ServiceName = "imagery", LayerName = "Orthophoto mosaic", Path = "/rest/services/imagery/ImageServer/query", Protocol = "ImageServer", QueryCount = 198_000, QueriesPerSecond = 2.29, P95LatencyMs = 436, ErrorRate = 0.011 };
        yield return new EndpointUsageMetric { ServiceName = "planning", LayerName = "Zoning districts", Path = "/rest/services/planning/MapServer/export", Protocol = "MapServer", QueryCount = 131_000, QueriesPerSecond = 1.52, P95LatencyMs = 326, ErrorRate = 0.004 };
        yield return new EndpointUsageMetric { ServiceName = "basemap", LayerName = "Address points", Path = "/odata/basemap/AddressPoints", Protocol = "OData", QueryCount = 104_000, QueriesPerSecond = 1.20, P95LatencyMs = 184, ErrorRate = 0.001 };
    }

    private static IEnumerable<SlowQueryMetric> BuildSlowQueries(DateTimeOffset rangeEnd)
    {
        yield return new SlowQueryMetric
        {
            ServiceName = "imagery",
            LayerName = "Orthophoto mosaic",
            Protocol = "ImageServer",
            Endpoint = "/rest/services/imagery/ImageServer/query",
            QueryPattern = "large mosaic query without bbox",
            Count = 47,
            P95DurationMs = 2_840,
            MaxDurationMs = 5_904,
            LastSeen = rangeEnd.AddMinutes(-17),
            ExampleCorrelationId = "corr-img-7041",
        };
        yield return new SlowQueryMetric
        {
            ServiceName = "default",
            LayerName = "Parcels",
            Protocol = "OGC Features",
            Endpoint = "/ogc/features/collections/parcels/items",
            QueryPattern = "contains filter over unbounded geometry",
            Count = 31,
            P95DurationMs = 1_720,
            MaxDurationMs = 3_210,
            LastSeen = rangeEnd.AddMinutes(-43),
            ExampleCorrelationId = "corr-ogc-1882",
        };
        yield return new SlowQueryMetric
        {
            ServiceName = "planning",
            LayerName = "Zoning districts",
            Protocol = "MapServer",
            Endpoint = "/rest/services/planning/MapServer/export",
            QueryPattern = "high-DPI export over full extent",
            Count = 18,
            P95DurationMs = 1_420,
            MaxDurationMs = 2_160,
            LastSeen = rangeEnd.AddHours(-3),
            ExampleCorrelationId = "corr-map-5097",
        };
    }

    private static IEnumerable<StorageGrowthMetric> BuildStorage(double scale)
    {
        yield return new StorageGrowthMetric
        {
            Tenant = "enterprise",
            ServiceName = "default",
            CurrentBytes = 824_633_720_832,
            DeltaBytes = (long)(19_327_352_832 * scale),
            GrowthPercent = 2.4 * scale,
            LayerCount = 9,
            LayerNames = ["Parcels"],
            Protocols = ["FeatureServer", "OGC Features"],
        };
        yield return new StorageGrowthMetric
        {
            Tenant = "enterprise",
            ServiceName = "imagery",
            CurrentBytes = 1_942_337_060_864,
            DeltaBytes = (long)(54_706_470_912 * scale),
            GrowthPercent = 2.9 * scale,
            LayerCount = 3,
            LayerNames = ["Orthophoto mosaic"],
            Protocols = ["ImageServer"],
        };
        yield return new StorageGrowthMetric
        {
            Tenant = "planning",
            ServiceName = "planning",
            CurrentBytes = 218_405_060_096,
            DeltaBytes = (long)(4_831_838_208 * scale),
            GrowthPercent = 2.3 * scale,
            LayerCount = 5,
            LayerNames = ["Zoning districts"],
            Protocols = ["MapServer"],
        };
    }

    private static IEnumerable<UserActivityMetric> BuildUsers(DateTimeOffset rangeEnd)
    {
        yield return new UserActivityMetric { PrincipalId = "aad:1738", DisplayName = "Kai Ito", Tenant = "enterprise", TopService = "default", TopLayer = "Parcels", MostUsedProtocol = "FeatureServer", QueryCount = 142_000, DistinctLayers = 8, P95LatencyMs = 221, LastSeen = rangeEnd.AddMinutes(-4) };
        yield return new UserActivityMetric { PrincipalId = "aad:9914", DisplayName = "Malia Santos", Tenant = "enterprise", TopService = "imagery", TopLayer = "Orthophoto mosaic", MostUsedProtocol = "ImageServer", QueryCount = 88_000, DistinctLayers = 4, P95LatencyMs = 462, LastSeen = rangeEnd.AddMinutes(-28) };
        yield return new UserActivityMetric { PrincipalId = "svc:tiles-worker", DisplayName = "Tiles worker", Tenant = "enterprise", TopService = "default", TopLayer = "Parcels", MostUsedProtocol = "OGC Features", QueryCount = 72_000, DistinctLayers = 2, P95LatencyMs = 248, LastSeen = rangeEnd.AddMinutes(-11) };
        yield return new UserActivityMetric { PrincipalId = "aad:4417", DisplayName = "Noel Park", Tenant = "planning", TopService = "planning", TopLayer = "Zoning districts", MostUsedProtocol = "MapServer", QueryCount = 39_000, DistinctLayers = 5, P95LatencyMs = 338, LastSeen = rangeEnd.AddHours(-1) };
    }

    private static IEnumerable<PopularLayerMetric> Scale(IEnumerable<PopularLayerMetric> metrics, double scale)
        => metrics.Select(metric => metric with
        {
            QueryCount = Math.Max(1, (long)Math.Round(metric.QueryCount * scale)),
            QueriesPerSecond = Math.Round(metric.QueriesPerSecond * Math.Clamp(scale, 0.35, 8d), 2),
            UniqueUsers = Math.Max(1, (int)Math.Round(metric.UniqueUsers * Math.Clamp(scale, 0.5, 4d))),
        });

    private static IEnumerable<EndpointUsageMetric> Scale(IEnumerable<EndpointUsageMetric> metrics, double scale)
        => metrics.Select(metric => metric with
        {
            QueryCount = Math.Max(1, (long)Math.Round(metric.QueryCount * scale)),
            QueriesPerSecond = Math.Round(metric.QueriesPerSecond * Math.Clamp(scale, 0.35, 8d), 2),
        });

    private static IEnumerable<SlowQueryMetric> Scale(IEnumerable<SlowQueryMetric> metrics, double scale)
        => metrics.Select(metric => metric with { Count = Math.Max(1, (int)Math.Round(metric.Count * scale)) });

    private static IEnumerable<UserActivityMetric> Scale(IEnumerable<UserActivityMetric> metrics, double scale)
        => metrics.Select(metric => metric with { QueryCount = Math.Max(1, (long)Math.Round(metric.QueryCount * scale)) });

    private sealed record SeriesSeed(
        string ServiceName,
        string LayerName,
        string Protocol,
        double BaseQps,
        double AverageLatencyMs,
        int BaseSlowCount);
}
