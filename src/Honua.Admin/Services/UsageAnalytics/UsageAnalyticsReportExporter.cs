// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Honua.Admin.Models.UsageAnalytics;

namespace Honua.Admin.Services.UsageAnalytics;

public static class UsageAnalyticsReportExporter
{
    public static UsageAnalyticsExportPayload ToCsv(UsageAnalyticsExportView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var builder = new StringBuilder();
        AppendRow(builder, "Honua usage analytics", view.Report.RangeLabel);
        AppendRow(builder, "Generated", view.Report.GeneratedAt.ToString("u", CultureInfo.InvariantCulture));
        AppendRow(builder, "Filters", view.FilterLabel);
        builder.AppendLine();

        AppendRow(builder, "Summary");
        AppendRow(builder, "Total queries", view.Totals.TotalQueries.ToString(CultureInfo.InvariantCulture));
        AppendRow(builder, "Average QPS", view.Totals.QueriesPerSecond.ToString("0.##", CultureInfo.InvariantCulture));
        AppendRow(builder, "Unique users", view.Totals.UniqueUsers.ToString(CultureInfo.InvariantCulture));
        AppendRow(builder, "Slow queries", view.Totals.SlowQueryCount.ToString(CultureInfo.InvariantCulture));
        AppendRow(builder, "P95 latency ms", view.Totals.P95LatencyMs.ToString("0.#", CultureInfo.InvariantCulture));
        AppendRow(builder, "Storage", FormatBytes(view.Totals.StorageBytes));
        AppendRow(builder, "Storage growth", FormatBytes(view.Totals.StorageDeltaBytes));
        builder.AppendLine();

        AppendRow(builder, "Popular layers");
        AppendRow(builder, "Service", "Layer", "Protocol", "Queries", "QPS", "Users", "P95 ms", "Error rate");
        foreach (var layer in view.PopularLayers)
        {
            AppendRow(
                builder,
                layer.ServiceName,
                layer.LayerName,
                layer.Protocol,
                layer.QueryCount.ToString(CultureInfo.InvariantCulture),
                layer.QueriesPerSecond.ToString("0.##", CultureInfo.InvariantCulture),
                layer.UniqueUsers.ToString(CultureInfo.InvariantCulture),
                layer.P95LatencyMs.ToString("0.#", CultureInfo.InvariantCulture),
                FormatPercent(layer.ErrorRate));
        }
        builder.AppendLine();

        AppendRow(builder, "Popular endpoints");
        AppendRow(builder, "Service", "Layer", "Path", "Protocol", "Queries", "QPS", "P95 ms", "Error rate");
        foreach (var endpoint in view.PopularEndpoints)
        {
            AppendRow(
                builder,
                endpoint.ServiceName,
                endpoint.LayerName,
                endpoint.Path,
                endpoint.Protocol,
                endpoint.QueryCount.ToString(CultureInfo.InvariantCulture),
                endpoint.QueriesPerSecond.ToString("0.##", CultureInfo.InvariantCulture),
                endpoint.P95LatencyMs.ToString("0.#", CultureInfo.InvariantCulture),
                FormatPercent(endpoint.ErrorRate));
        }
        builder.AppendLine();

        AppendRow(builder, "Slow queries");
        AppendRow(builder, "Service", "Layer", "Protocol", "Endpoint", "Pattern", "Count", "P95 ms", "Max ms", "Last seen", "Correlation");
        foreach (var query in view.SlowQueries)
        {
            AppendRow(
                builder,
                query.ServiceName,
                query.LayerName,
                query.Protocol,
                query.Endpoint,
                query.QueryPattern,
                query.Count.ToString(CultureInfo.InvariantCulture),
                query.P95DurationMs.ToString("0.#", CultureInfo.InvariantCulture),
                query.MaxDurationMs.ToString("0.#", CultureInfo.InvariantCulture),
                query.LastSeen.ToString("u", CultureInfo.InvariantCulture),
                query.ExampleCorrelationId);
        }
        builder.AppendLine();

        AppendRow(builder, "Storage growth");
        AppendRow(builder, "Tenant", "Service", "Current", "Delta", "Growth", "Layers");
        foreach (var storage in view.StorageGrowth)
        {
            AppendRow(
                builder,
                storage.Tenant,
                storage.ServiceName,
                FormatBytes(storage.CurrentBytes),
                FormatBytes(storage.DeltaBytes),
                FormatPercent(storage.GrowthPercent / 100d),
                storage.LayerCount.ToString(CultureInfo.InvariantCulture));
        }
        builder.AppendLine();

        AppendRow(builder, "User activity");
        AppendRow(builder, "Principal", "Display name", "Tenant", "Service", "Layer", "Protocol", "Queries", "Distinct layers", "P95 ms", "Last seen");
        foreach (var user in view.UserActivity)
        {
            AppendRow(
                builder,
                user.PrincipalId,
                user.DisplayName,
                user.Tenant,
                user.TopService,
                user.TopLayer,
                user.MostUsedProtocol,
                user.QueryCount.ToString(CultureInfo.InvariantCulture),
                user.DistinctLayers.ToString(CultureInfo.InvariantCulture),
                user.P95LatencyMs.ToString("0.#", CultureInfo.InvariantCulture),
                user.LastSeen.ToString("u", CultureInfo.InvariantCulture));
        }

        return new UsageAnalyticsExportPayload(
            $"honua-usage-analytics-{view.Report.RangeEnd:yyyyMMddHHmm}.csv",
            "text/csv",
            builder.ToString());
    }

    public static UsageAnalyticsExportPayload ToPdf(UsageAnalyticsExportView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var lines = new List<string>
        {
            "Honua Usage Analytics",
            $"Range: {view.Report.RangeLabel}",
            $"Generated: {view.Report.GeneratedAt:u}",
            $"Filters: {view.FilterLabel}",
            string.Empty,
            $"Total queries: {view.Totals.TotalQueries:N0}",
            $"Average QPS: {view.Totals.QueriesPerSecond:0.##}",
            $"Unique users: {view.Totals.UniqueUsers:N0}",
            $"Slow queries: {view.Totals.SlowQueryCount:N0}",
            $"P95 latency: {view.Totals.P95LatencyMs:0.#} ms",
            $"Storage: {FormatBytes(view.Totals.StorageBytes)} ({FormatBytes(view.Totals.StorageDeltaBytes)} growth)",
            string.Empty,
            "Top layers",
        };

        lines.AddRange(view.PopularLayers.Take(8).Select(layer =>
            $"{layer.ServiceName}/{layer.LayerName} {layer.Protocol}: {layer.QueryCount:N0} queries, {layer.P95LatencyMs:0.#} ms p95"));

        lines.Add(string.Empty);
        lines.Add("Slow queries");
        lines.AddRange(view.SlowQueries.Take(6).Select(query =>
            $"{query.ServiceName}/{query.LayerName} {query.Protocol}: {query.Count:N0} hits, {query.P95DurationMs:0.#} ms p95"));

        lines.Add(string.Empty);
        lines.Add("User activity");
        lines.AddRange(view.UserActivity.Take(6).Select(user =>
            $"{user.DisplayName} ({user.PrincipalId}): {user.QueryCount:N0} queries, {user.DistinctLayers:N0} layers"));

        return new UsageAnalyticsExportPayload(
            $"honua-usage-analytics-{view.Report.RangeEnd:yyyyMMddHHmm}.pdf",
            "application/pdf",
            BuildPdf(lines));
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB"];
        double value = bytes;
        var index = 0;
        while (Math.Abs(value) >= 1024 && index < units.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.#} {units[index]}";
    }

    public static string FormatPercent(double ratio)
        => $"{ratio * 100:0.##}%";

    private static void AppendRow(StringBuilder builder, params string[] values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(EscapeCsv(values[i]));
        }

        builder.AppendLine();
    }

    private static string EscapeCsv(string value)
    {
        value = NeutralizeSpreadsheetFormula(value);

        if (!value.Contains('"', StringComparison.Ordinal) &&
            !value.Contains(',', StringComparison.Ordinal) &&
            !value.Contains('\n', StringComparison.Ordinal) &&
            !value.Contains('\r', StringComparison.Ordinal))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string NeutralizeSpreadsheetFormula(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var trimmed = value.TrimStart();
        if (trimmed.Length == 0)
        {
            return value;
        }

        return trimmed[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;
    }

    private static string BuildPdf(IReadOnlyList<string> lines)
    {
        var text = new StringBuilder();
        text.AppendLine("BT");
        text.AppendLine("/F1 11 Tf");
        text.AppendLine("50 760 Td");
        text.AppendLine("14 TL");
        foreach (var line in lines.Take(44))
        {
            text.Append(EncodePdfText(line)).AppendLine(" Tj");
            text.AppendLine("T*");
        }

        text.AppendLine("ET");
        var content = text.ToString();

        var toUnicode = BuildToUnicodeMap(lines);
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type0 /BaseFont /ArialUnicodeMS /Encoding /Identity-H /DescendantFonts [6 0 R] /ToUnicode 8 0 R >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream",
            "<< /Type /Font /Subtype /CIDFontType2 /BaseFont /ArialUnicodeMS /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 7 0 R /CIDToGIDMap /Identity /DW 600 >>",
            "<< /Type /FontDescriptor /FontName /ArialUnicodeMS /Flags 32 /FontBBox [-1000 -300 2000 1100] /ItalicAngle 0 /Ascent 1000 /Descent -300 /CapHeight 700 /StemV 80 >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(toUnicode)} >>\nstream\n{toUnicode}endstream",
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        foreach (var (obj, index) in objects.Select((obj, index) => (obj, index)))
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index + 1)
                .Append(" 0 obj\n")
                .Append(obj)
                .Append("\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n")
            .Append("0 ")
            .Append(objects.Length + 1)
            .Append('\n')
            .Append("0000000000 65535 f \n");

        foreach (var offset in offsets.Skip(1))
        {
            pdf.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        pdf.Append("trailer\n")
            .Append("<< /Size ")
            .Append(objects.Length + 1)
            .Append(" /Root 1 0 R >>\n")
            .Append("startxref\n")
            .Append(xrefOffset)
            .Append("\n%%EOF\n");

        return pdf.ToString();
    }

    private static string EncodePdfText(string value)
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes(value);
        var encoded = new StringBuilder(2 + bytes.Length * 2);
        encoded.Append('<');
        foreach (var valueByte in bytes)
        {
            encoded.Append(valueByte.ToString("X2", CultureInfo.InvariantCulture));
        }

        encoded.Append('>');
        return encoded.ToString();
    }

    private static string BuildToUnicodeMap(IReadOnlyList<string> lines)
    {
        var chars = lines
            .SelectMany(line => line)
            .Distinct()
            .OrderBy(ch => ch)
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("/CIDInit /ProcSet findresource begin");
        builder.AppendLine("12 dict begin");
        builder.AppendLine("begincmap");
        builder.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def");
        builder.AppendLine("/CMapName /HonuaUsageAnalytics def");
        builder.AppendLine("/CMapType 2 def");
        builder.AppendLine("1 begincodespacerange");
        builder.AppendLine("<0000> <FFFF>");
        builder.AppendLine("endcodespacerange");

        foreach (var chunk in chars.Chunk(100))
        {
            builder.Append(chunk.Length.ToString(CultureInfo.InvariantCulture)).AppendLine(" beginbfchar");
            foreach (var ch in chunk)
            {
                var code = ((int)ch).ToString("X4", CultureInfo.InvariantCulture);
                builder.Append('<').Append(code).Append("> <").Append(code).AppendLine(">");
            }

            builder.AppendLine("endbfchar");
        }

        builder.AppendLine("endcmap");
        builder.AppendLine("CMapName currentdict /CMap defineresource pop");
        builder.AppendLine("end");
        builder.AppendLine("end");
        return builder.ToString();
    }
}

public sealed record UsageAnalyticsExportView(
    UsageAnalyticsReport Report,
    UsageAnalyticsTotals Totals,
    IReadOnlyList<PopularLayerMetric> PopularLayers,
    IReadOnlyList<EndpointUsageMetric> PopularEndpoints,
    IReadOnlyList<SlowQueryMetric> SlowQueries,
    IReadOnlyList<StorageGrowthMetric> StorageGrowth,
    IReadOnlyList<UserActivityMetric> UserActivity,
    string FilterLabel);
