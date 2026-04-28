// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;

namespace Honua.Admin.Models.UsageAnalytics;

public sealed record UsageAnalyticsQuery
{
    public string RangeKey { get; init; } = "24h";
    public string? ServiceName { get; init; }
    public string? LayerName { get; init; }
    public string? Protocol { get; init; }
}

public sealed record UsageAnalyticsReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public DateTimeOffset RangeStart { get; init; }
    public DateTimeOffset RangeEnd { get; init; }
    public string RangeLabel { get; init; } = string.Empty;
    public string DataSource { get; init; } = string.Empty;
    public UsageAnalyticsTotals Totals { get; init; } = new();
    public IReadOnlyList<QueryThroughputPoint> QuerySeries { get; init; } = Array.Empty<QueryThroughputPoint>();
    public IReadOnlyList<PopularLayerMetric> PopularLayers { get; init; } = Array.Empty<PopularLayerMetric>();
    public IReadOnlyList<EndpointUsageMetric> PopularEndpoints { get; init; } = Array.Empty<EndpointUsageMetric>();
    public IReadOnlyList<SlowQueryMetric> SlowQueries { get; init; } = Array.Empty<SlowQueryMetric>();
    public IReadOnlyList<StorageGrowthMetric> StorageGrowth { get; init; } = Array.Empty<StorageGrowthMetric>();
    public IReadOnlyList<UserActivityMetric> UserActivity { get; init; } = Array.Empty<UserActivityMetric>();
}

public sealed record UsageAnalyticsTotals
{
    public long TotalQueries { get; init; }
    public double QueriesPerSecond { get; init; }
    public int UniqueUsers { get; init; }
    public int SlowQueryCount { get; init; }
    public double P95LatencyMs { get; init; }
    public long StorageBytes { get; init; }
    public long StorageDeltaBytes { get; init; }
}

public sealed record QueryThroughputPoint
{
    public DateTimeOffset Timestamp { get; init; }
    public string ServiceName { get; init; } = string.Empty;
    public string LayerName { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public long QueryCount { get; init; }
    public double QueriesPerSecond { get; init; }
    public double AverageLatencyMs { get; init; }
    public int SlowQueryCount { get; init; }
}

public sealed record PopularLayerMetric
{
    public string ServiceName { get; init; } = string.Empty;
    public string LayerName { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public long QueryCount { get; init; }
    public double QueriesPerSecond { get; init; }
    public int UniqueUsers { get; init; }
    public double P95LatencyMs { get; init; }
    public double ErrorRate { get; init; }
}

public sealed record EndpointUsageMetric
{
    public string ServiceName { get; init; } = string.Empty;
    public string LayerName { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public long QueryCount { get; init; }
    public double QueriesPerSecond { get; init; }
    public double P95LatencyMs { get; init; }
    public double ErrorRate { get; init; }
}

public sealed record SlowQueryMetric
{
    public string ServiceName { get; init; } = string.Empty;
    public string LayerName { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string QueryPattern { get; init; } = string.Empty;
    public int Count { get; init; }
    public double P95DurationMs { get; init; }
    public double MaxDurationMs { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public string ExampleCorrelationId { get; init; } = string.Empty;
}

public sealed record StorageGrowthMetric
{
    public string Tenant { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public long CurrentBytes { get; init; }
    public long DeltaBytes { get; init; }
    public double GrowthPercent { get; init; }
    public int LayerCount { get; init; }
    public IReadOnlyList<string> LayerNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Protocols { get; init; } = Array.Empty<string>();
}

public sealed record UserActivityMetric
{
    public string PrincipalId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Tenant { get; init; } = string.Empty;
    public string TopService { get; init; } = string.Empty;
    public string TopLayer { get; init; } = string.Empty;
    public string MostUsedProtocol { get; init; } = string.Empty;
    public long QueryCount { get; init; }
    public int DistinctLayers { get; init; }
    public double P95LatencyMs { get; init; }
    public DateTimeOffset LastSeen { get; init; }
}

public sealed record UsageAnalyticsExportPayload(
    string FileName,
    string MimeType,
    string Content);
