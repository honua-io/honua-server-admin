// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Honua.Admin.AuditTools;

/// <summary>
/// Renders matrix.md from the join of <see cref="EndpointInventory"/> and
/// <see cref="CoverageMatrix"/>. The output is grouped by feature, sorted
/// by priority then by route, and includes the snapshot date + commit
/// header so reviewers can pinpoint inventory drift.
/// </summary>
public static class MatrixRenderer
{
    public static string Render(EndpointInventory inventory, CoverageMatrix coverage)
    {
        var lookup = coverage.Rows.ToDictionary(r => r.Key, System.StringComparer.Ordinal);
        var sb = new StringBuilder();

        sb.AppendLine("# Admin UI ↔ Honua-Server API Coverage Matrix");
        sb.AppendLine();
        sb.AppendLine("> **Generated** — do not edit by hand. Run `dotnet run --project tools/audit-api-surface -- render` to regenerate.");
        sb.AppendLine();
        sb.Append("- snapshot_date: ").AppendLine(coverage.SnapshotDate);
        sb.Append("- honua_server_commit: `").Append(inventory.HonuaServerCommit).AppendLine("`");
        sb.Append("- endpoints_total: ").AppendLine(inventory.Endpoints.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine();

        var (supported, partial, missing, oos, unclassified) = SummariseCoverage(inventory, lookup);
        sb.AppendLine("## Coverage Summary");
        sb.AppendLine();
        sb.AppendLine("| coverage | count |");
        sb.AppendLine("| -------- | ----- |");
        sb.Append("| supported | ").Append(supported).AppendLine(" |");
        sb.Append("| partial | ").Append(partial).AppendLine(" |");
        sb.Append("| missing | ").Append(missing).AppendLine(" |");
        sb.Append("| out-of-scope | ").Append(oos).AppendLine(" |");
        if (unclassified > 0)
        {
            sb.Append("| **unclassified (drift)** | **").Append(unclassified).AppendLine("** |");
        }
        sb.AppendLine();

        var byFeature = inventory.Endpoints
            .GroupBy(e => e.Feature, System.StringComparer.Ordinal)
            .OrderBy(g => g.Key, System.StringComparer.Ordinal);

        foreach (var group in byFeature)
        {
            sb.Append("## ").AppendLine(group.Key);
            sb.AppendLine();
            sb.AppendLine("| key | priority | coverage | admin page | notes |");
            sb.AppendLine("| --- | -------- | -------- | ---------- | ----- |");

            var ordered = group
                .OrderBy(e => PriorityRank(lookup.GetValueOrDefault(e.Key)?.Priority))
                .ThenBy(e => e.Route, System.StringComparer.Ordinal)
                .ThenBy(e => e.Verb, System.StringComparer.Ordinal);

            foreach (var endpoint in ordered)
            {
                var row = lookup.GetValueOrDefault(endpoint.Key);
                var priority = row?.Priority ?? "(unclassified)";
                var coverageState = row?.Coverage ?? "(unclassified)";
                var adminPage = row?.AdminPage ?? string.Empty;
                var notes = row?.Notes;
                if (string.IsNullOrEmpty(notes) && !string.IsNullOrEmpty(row?.OutOfScopeReason))
                {
                    notes = row.OutOfScopeReason;
                }
                if (string.IsNullOrEmpty(notes) && !string.IsNullOrEmpty(row?.FollowUpTicket))
                {
                    notes = $"follow-up: {row.FollowUpTicket}";
                }

                sb.Append("| `").Append(endpoint.Key).Append("` | ")
                  .Append(priority).Append(" | ")
                  .Append(coverageState).Append(" | ")
                  .Append(EscapeMarkdown(adminPage)).Append(" | ")
                  .Append(EscapeMarkdown(notes ?? string.Empty)).AppendLine(" |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static (int supported, int partial, int missing, int outOfScope, int unclassified) SummariseCoverage(
        EndpointInventory inventory,
        IReadOnlyDictionary<string, CoverageRow> lookup)
    {
        var supported = 0;
        var partial = 0;
        var missing = 0;
        var oos = 0;
        var unclassified = 0;
        foreach (var endpoint in inventory.Endpoints)
        {
            if (!lookup.TryGetValue(endpoint.Key, out var row))
            {
                unclassified++;
                continue;
            }
            switch (row.Coverage)
            {
                case "supported": supported++; break;
                case "partial": partial++; break;
                case "missing": missing++; break;
                case "out-of-scope": oos++; break;
                default: unclassified++; break;
            }
        }
        return (supported, partial, missing, oos, unclassified);
    }

    private static int PriorityRank(string? priority) => priority switch
    {
        "P0" => 0,
        "P1" => 1,
        "P2" => 2,
        "deferred" => 3,
        "n/a" => 4,
        _ => 5,
    };

    private static string EscapeMarkdown(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }
        return value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", string.Empty);
    }
}
