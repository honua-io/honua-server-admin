// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.UsageAnalytics;
using Honua.Admin.Services.UsageAnalytics;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class UsageAnalyticsStateTests
{
    [Fact]
    public async Task LoadAsync_populates_default_dashboard_metrics()
    {
        var state = new UsageAnalyticsState(new StubUsageAnalyticsClient());

        await state.LoadAsync();

        Assert.Equal(UsageAnalyticsStatus.Ready, state.Status);
        Assert.NotNull(state.Report);
        Assert.True(state.FilteredTotals.TotalQueries > 0);
        Assert.Contains("default", state.ServiceOptions);
        Assert.Contains("FeatureServer", state.ProtocolOptions);
        Assert.Contains("Parcels", state.LayerOptions);
        Assert.NotEmpty(state.ThroughputBuckets);
        Assert.NotEmpty(state.ThroughputRows);
    }

    [Fact]
    public async Task Filters_scope_layers_endpoints_slow_queries_and_users()
    {
        var state = new UsageAnalyticsState(new StubUsageAnalyticsClient());
        await state.LoadAsync();

        state.SetServiceFilter("imagery");
        state.SetLayerFilter("Orthophoto mosaic");
        state.SetProtocolFilter("ImageServer");

        Assert.All(state.FilteredPopularLayers, layer =>
        {
            Assert.Equal("imagery", layer.ServiceName);
            Assert.Equal("ImageServer", layer.Protocol);
        });
        Assert.All(state.FilteredPopularEndpoints, endpoint =>
        {
            Assert.Equal("imagery", endpoint.ServiceName);
            Assert.Equal("Orthophoto mosaic", endpoint.LayerName);
        });
        Assert.All(state.FilteredSlowQueries, query => Assert.Equal("imagery", query.ServiceName));
        Assert.All(state.FilteredUserActivity, user => Assert.Equal("imagery", user.TopService));
        var storage = Assert.Single(state.FilteredStorageGrowth);
        Assert.Equal("imagery", storage.ServiceName);
        Assert.Contains("Orthophoto mosaic", storage.LayerNames);
        Assert.Contains("ImageServer", storage.Protocols);
        Assert.Equal(storage.CurrentBytes, state.FilteredTotals.StorageBytes);
        Assert.Equal(storage.DeltaBytes, state.FilteredTotals.StorageDeltaBytes);
        Assert.All(state.ThroughputRows, row => Assert.Equal("ImageServer", row.Protocol));
    }

    [Fact]
    public async Task Storage_filters_honor_layer_and_protocol_membership()
    {
        var state = new UsageAnalyticsState(new StubUsageAnalyticsClient());
        await state.LoadAsync();

        state.SetProtocolFilter("ImageServer");

        var imageStorage = Assert.Single(state.FilteredStorageGrowth);
        Assert.Equal("imagery", imageStorage.ServiceName);
        Assert.Equal(imageStorage.CurrentBytes, state.FilteredTotals.StorageBytes);

        state.SetProtocolFilter(UsageAnalyticsState.AllFilter);
        state.SetLayerFilter("Parcels");

        var parcelStorage = Assert.Single(state.FilteredStorageGrowth);
        Assert.Equal("default", parcelStorage.ServiceName);
        Assert.Contains("FeatureServer", parcelStorage.Protocols);
        Assert.Equal(parcelStorage.CurrentBytes, state.FilteredTotals.StorageBytes);
    }

    [Fact]
    public async Task FilteredTotals_query_count_uses_filtered_aggregate_counts()
    {
        var state = new UsageAnalyticsState(new StubUsageAnalyticsClient());
        state.SetRange(UsageAnalyticsRanges.Last7Days);
        await state.LoadAsync();

        Assert.Equal(
            state.FilteredPopularLayers.Sum(layer => layer.QueryCount),
            state.FilteredTotals.TotalQueries);
        Assert.NotEqual(
            state.ThroughputSeries.Sum(point => point.QueryCount),
            state.FilteredTotals.TotalQueries);

        state.SetServiceFilter("imagery");

        Assert.Equal(
            state.FilteredPopularLayers.Sum(layer => layer.QueryCount),
            state.FilteredTotals.TotalQueries);
    }

    [Fact]
    public async Task ShortRange_slow_query_counts_scale_down_with_the_selected_window()
    {
        var client = new StubUsageAnalyticsClient();

        var lastHour = await client.GetReportAsync(new UsageAnalyticsQuery { RangeKey = UsageAnalyticsRanges.LastHour }, CancellationToken.None);
        var lastDay = await client.GetReportAsync(new UsageAnalyticsQuery { RangeKey = UsageAnalyticsRanges.Last24Hours }, CancellationToken.None);

        Assert.True(
            lastHour.SlowQueries.Sum(query => query.Count) < lastDay.SlowQueries.Sum(query => query.Count),
            "Short analytics windows should not keep 24-hour slow-query counts.");
    }

    [Fact]
    public async Task ExportCsvAndPdf_use_filtered_report_payloads()
    {
        var state = new UsageAnalyticsState(new StubUsageAnalyticsClient());
        await state.LoadAsync();

        state.SetServiceFilter("planning");

        var csv = state.ExportCsv();
        var pdf = state.ExportPdf();

        Assert.Equal("text/csv", csv.MimeType);
        Assert.Contains("service: planning", csv.Content);
        Assert.Contains("Zoning districts", csv.Content);
        Assert.DoesNotContain("Orthophoto mosaic", csv.Content);

        Assert.Equal("application/pdf", pdf.MimeType);
        Assert.StartsWith("%PDF-1.4", pdf.Content, StringComparison.Ordinal);
        Assert.Contains(PdfHex("Filters: service: planning"), pdf.Content);
        Assert.Contains("/Subtype /Type0", pdf.Content);
        Assert.Contains("/Encoding /Identity-H", pdf.Content);
        Assert.Contains("/ToUnicode", pdf.Content);
    }

    [Fact]
    public async Task ServiceAndLayerFilters_clear_incompatible_protocol_filter()
    {
        var state = new UsageAnalyticsState(new StubUsageAnalyticsClient());
        await state.LoadAsync();

        state.SetProtocolFilter("FeatureServer");
        state.SetServiceFilter("imagery");

        Assert.Equal(UsageAnalyticsState.AllFilter, state.ProtocolFilter);
        Assert.Contains("ImageServer", state.ProtocolOptions);
        Assert.DoesNotContain("FeatureServer", state.ProtocolOptions);

        state.SetProtocolFilter("ImageServer");
        state.SetLayerFilter("Parcels");

        Assert.Equal(UsageAnalyticsState.AllFilter, state.ProtocolFilter);
    }

    [Fact]
    public async Task LiteralAllValues_are_filterable_data_values_not_wildcards()
    {
        var state = new UsageAnalyticsState(new FixedUsageAnalyticsClient(new UsageAnalyticsReport
        {
            QuerySeries =
            [
                new QueryThroughputPoint
                {
                    Timestamp = DateTimeOffset.Parse("2026-04-25T01:00:00Z"),
                    ServiceName = "all",
                    LayerName = "all",
                    Protocol = "all",
                    QueryCount = 7,
                    QueriesPerSecond = 1,
                },
                new QueryThroughputPoint
                {
                    Timestamp = DateTimeOffset.Parse("2026-04-25T01:00:00Z"),
                    ServiceName = "default",
                    LayerName = "Parcels",
                    Protocol = "FeatureServer",
                    QueryCount = 11,
                    QueriesPerSecond = 2,
                },
            ],
        }));

        await state.LoadAsync();
        state.SetServiceFilter("all");
        state.SetLayerFilter("all");
        state.SetProtocolFilter("all");

        var point = Assert.Single(state.ThroughputSeries);
        Assert.Equal("all", point.ServiceName);
        Assert.Equal(7, point.QueryCount);
    }

    [Fact]
    public void ExportCsv_escapes_management_report_fields()
    {
        var report = new UsageAnalyticsReport
        {
            GeneratedAt = DateTimeOffset.Parse("2026-04-25T12:00:00Z"),
            RangeStart = DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            RangeEnd = DateTimeOffset.Parse("2026-04-25T12:00:00Z"),
            RangeLabel = "24 hours",
        };
        var view = new UsageAnalyticsExportView(
            report,
            new UsageAnalyticsTotals { TotalQueries = 12 },
            new[]
            {
                new PopularLayerMetric
                {
                    ServiceName = "default",
                    LayerName = "Parcels, historical",
                    Protocol = "FeatureServer",
                    QueryCount = 12,
                },
            },
            Array.Empty<EndpointUsageMetric>(),
            Array.Empty<SlowQueryMetric>(),
            Array.Empty<StorageGrowthMetric>(),
            new[]
            {
                new UserActivityMetric
                {
                    PrincipalId = "@external",
                    DisplayName = "=cmd|' /C calc'!A0",
                    Tenant = "default",
                },
            },
            "all services");

        var csv = UsageAnalyticsReportExporter.ToCsv(view);

        Assert.Contains("\"Parcels, historical\"", csv.Content);
        Assert.Contains("'=cmd|' /C calc'!A0", csv.Content);
        Assert.Contains("'@external", csv.Content);
    }

    [Fact]
    public void ExportPdf_preserves_non_ascii_report_text()
    {
        var displayName = string.Concat("M", "\u0101", "lia ", "\u5C71", "\u7530");
        var report = new UsageAnalyticsReport
        {
            GeneratedAt = DateTimeOffset.Parse("2026-04-25T12:00:00Z"),
            RangeStart = DateTimeOffset.Parse("2026-04-24T12:00:00Z"),
            RangeEnd = DateTimeOffset.Parse("2026-04-25T12:00:00Z"),
            RangeLabel = "24 hours",
        };
        var view = new UsageAnalyticsExportView(
            report,
            new UsageAnalyticsTotals(),
            Array.Empty<PopularLayerMetric>(),
            Array.Empty<EndpointUsageMetric>(),
            Array.Empty<SlowQueryMetric>(),
            Array.Empty<StorageGrowthMetric>(),
            new[]
            {
                new UserActivityMetric
                {
                    PrincipalId = "aad:9914",
                    DisplayName = displayName,
                },
            },
            "all services");

        var pdf = UsageAnalyticsReportExporter.ToPdf(view);

        Assert.Contains(PdfHexBody(displayName), pdf.Content);
    }

    [Fact]
    public async Task LoadAsync_does_not_allow_stale_result_to_overwrite_newer_load()
    {
        var first = new TaskCompletionSource<UsageAnalyticsReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new GatedUsageAnalyticsClient(first.Task, Task.FromResult(Report("new")));
        var state = new UsageAnalyticsState(client);

        var staleLoad = state.LoadAsync();
        await state.LoadAsync();
        first.SetResult(Report("old"));
        await staleLoad;

        Assert.Equal("new", state.Report?.RangeLabel);
        Assert.Equal(UsageAnalyticsStatus.Ready, state.Status);
    }

    private static UsageAnalyticsReport Report(string label)
        => new()
        {
            RangeLabel = label,
            RangeStart = DateTimeOffset.Parse("2026-04-25T00:00:00Z"),
            RangeEnd = DateTimeOffset.Parse("2026-04-25T01:00:00Z"),
            GeneratedAt = DateTimeOffset.Parse("2026-04-25T01:00:00Z"),
            QuerySeries =
            [
                new QueryThroughputPoint
                {
                    Timestamp = DateTimeOffset.Parse("2026-04-25T01:00:00Z"),
                    ServiceName = "default",
                    LayerName = "Parcels",
                    Protocol = "FeatureServer",
                    QueryCount = 10,
                    QueriesPerSecond = 1,
                },
            ],
        };

    private sealed class GatedUsageAnalyticsClient : IUsageAnalyticsClient
    {
        private readonly Queue<Task<UsageAnalyticsReport>> _responses;

        public GatedUsageAnalyticsClient(params Task<UsageAnalyticsReport>[] responses)
        {
            _responses = new Queue<Task<UsageAnalyticsReport>>(responses);
        }

        public Task<UsageAnalyticsReport> GetReportAsync(UsageAnalyticsQuery query, CancellationToken cancellationToken)
            => _responses.Dequeue();
    }

    private sealed class FixedUsageAnalyticsClient : IUsageAnalyticsClient
    {
        private readonly UsageAnalyticsReport _report;

        public FixedUsageAnalyticsClient(UsageAnalyticsReport report)
        {
            _report = report;
        }

        public Task<UsageAnalyticsReport> GetReportAsync(UsageAnalyticsQuery query, CancellationToken cancellationToken)
            => Task.FromResult(_report);
    }

    private static string PdfHex(string value)
        => "<" + PdfHexBody(value) + ">";

    private static string PdfHexBody(string value)
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes(value);
        return string.Concat(bytes.Select(valueByte => valueByte.ToString("X2")));
    }
}
