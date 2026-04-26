// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Honua.Admin.AuditTools;

/// <summary>
/// Models the hand-authored coverage.yaml: one row per endpoint key,
/// with classification, optional admin page reference, priority, and
/// (when out of scope) a written reason + follow-up ticket.
/// </summary>
public sealed record CoverageRow(
    string Key,
    string Coverage,
    string? AdminPage,
    string Priority,
    string? OutOfScopeReason,
    string? FollowUpTicket,
    string? Notes);

public sealed record CoverageMatrix(
    string SnapshotDate,
    string HonuaServerCommit,
    IReadOnlyList<CoverageRow> Rows)
{
    public static readonly IReadOnlyCollection<string> ValidCoverage = new HashSet<string>(System.StringComparer.Ordinal)
    {
        "supported",
        "partial",
        "missing",
        "out-of-scope",
    };

    public static readonly IReadOnlyCollection<string> ValidPriorities = new HashSet<string>(System.StringComparer.Ordinal)
    {
        "P0",
        "P1",
        "P2",
        "deferred",
        "n/a",
    };
}

/// <summary>
/// Minimal YAML reader/writer for the bounded shape coverage.yaml uses.
/// We deliberately avoid a YAML package dependency: the schema is a flat
/// list of maps with simple scalar values, so a hand-written reader keeps
/// the audit tooling self-contained.
/// </summary>
public static class CoverageYamlSerializer
{
    public static CoverageMatrix Load(string yamlPath)
    {
        var text = File.ReadAllText(yamlPath);
        var lines = text.Replace("\r\n", "\n").Split('\n');

        string snapshotDate = string.Empty;
        string commit = string.Empty;
        var rows = new List<CoverageRow>();

        // Document is structured as:
        //   snapshot_date: 2026-04-24
        //   honua_server_commit: <sha>
        //   rows:
        //     - key: ...
        //       coverage: ...
        //       admin_page: ...
        //       priority: ...
        //       out_of_scope_reason: ...
        //       follow_up_ticket: ...
        //       notes: ...
        Dictionary<string, string?>? current = null;
        var inRows = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (!inRows)
            {
                if (line.StartsWith("snapshot_date:", System.StringComparison.Ordinal))
                {
                    snapshotDate = line[("snapshot_date:".Length)..].Trim().Trim('"');
                }
                else if (line.StartsWith("honua_server_commit:", System.StringComparison.Ordinal))
                {
                    commit = line[("honua_server_commit:".Length)..].Trim().Trim('"');
                }
                else if (line.StartsWith("rows:", System.StringComparison.Ordinal))
                {
                    inRows = true;
                }
                continue;
            }

            if (line.TrimStart().StartsWith("- ", System.StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    rows.Add(BuildRow(current));
                }
                current = new Dictionary<string, string?>(System.StringComparer.Ordinal);
                ParseInlineKeyValue(line.TrimStart()[2..], current);
            }
            else if (current is not null)
            {
                ParseInlineKeyValue(line.TrimStart(), current);
            }
        }

        if (current is not null)
        {
            rows.Add(BuildRow(current));
        }

        return new CoverageMatrix(snapshotDate, commit, rows);
    }

    public static string SerializeRows(IEnumerable<CoverageRow> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows)
        {
            sb.Append("  - key: ").AppendLine(QuoteIfNeeded(row.Key));
            sb.Append("    coverage: ").AppendLine(QuoteIfNeeded(row.Coverage));
            sb.Append("    admin_page: ").AppendLine(QuoteIfNeeded(row.AdminPage ?? string.Empty));
            sb.Append("    priority: ").AppendLine(QuoteIfNeeded(row.Priority));
            sb.Append("    out_of_scope_reason: ").AppendLine(QuoteIfNeeded(row.OutOfScopeReason ?? string.Empty));
            sb.Append("    follow_up_ticket: ").AppendLine(QuoteIfNeeded(row.FollowUpTicket ?? string.Empty));
            sb.Append("    notes: ").AppendLine(QuoteIfNeeded(row.Notes ?? string.Empty));
        }
        return sb.ToString();
    }

    private static void ParseInlineKeyValue(string segment, Dictionary<string, string?> bag)
    {
        // segment is "key: value" — value may be empty.
        var colon = segment.IndexOf(':');
        if (colon <= 0)
        {
            return;
        }
        var key = segment[..colon].Trim();
        var value = segment[(colon + 1)..].Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = UnescapeYamlString(value[1..^1]);
        }
        bag[key] = value.Length == 0 ? null : value;
    }

    private static string UnescapeYamlString(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '\\' && i + 1 < value.Length)
            {
                var next = value[i + 1];
                if (next == '\\' || next == '"')
                {
                    sb.Append(next);
                    i++;
                    continue;
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static CoverageRow BuildRow(Dictionary<string, string?> bag)
    {
        return new CoverageRow(
            Key: bag.GetValueOrDefault("key") ?? string.Empty,
            Coverage: bag.GetValueOrDefault("coverage") ?? "missing",
            AdminPage: bag.GetValueOrDefault("admin_page"),
            Priority: bag.GetValueOrDefault("priority") ?? "deferred",
            OutOfScopeReason: bag.GetValueOrDefault("out_of_scope_reason"),
            FollowUpTicket: bag.GetValueOrDefault("follow_up_ticket"),
            Notes: bag.GetValueOrDefault("notes"));
    }

    private static string QuoteIfNeeded(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }
        if (Regex.IsMatch(value, @"^[A-Za-z0-9_./:#@-]+$"))
        {
            return value;
        }
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
