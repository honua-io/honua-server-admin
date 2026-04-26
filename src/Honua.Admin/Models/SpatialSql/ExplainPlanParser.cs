using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Honua.Admin.Models.SpatialSql;

/// <summary>
/// Projects PostgreSQL's <c>EXPLAIN (ANALYZE, FORMAT JSON)</c> document into the
/// admin's <see cref="ExplainPlan"/> tree. The server returns the plan unmodified;
/// this parser is reflection-free so it stays AOT/trim friendly.
/// </summary>
public static class ExplainPlanParser
{
    /// <summary>
    /// Parse the full Postgres EXPLAIN JSON envelope (an array containing one object
    /// with a top-level "Plan" property).
    /// </summary>
    public static ExplainPlan Parse(string explainJson)
    {
        if (string.IsNullOrWhiteSpace(explainJson))
        {
            throw new ArgumentException("EXPLAIN JSON cannot be empty.", nameof(explainJson));
        }

        using var doc = JsonDocument.Parse(explainJson);
        return ParseInternal(doc.RootElement);
    }

    /// <summary>
    /// Parse from a pre-deserialized <see cref="JsonElement"/>. Useful when the
    /// server hands back the EXPLAIN payload nested in a larger envelope.
    /// </summary>
    public static ExplainPlan Parse(JsonElement element) => ParseInternal(element);

    private static ExplainPlan ParseInternal(JsonElement element)
    {
        JsonElement payload = element;
        if (payload.ValueKind == JsonValueKind.Array)
        {
            var found = false;
            foreach (var entry in payload.EnumerateArray())
            {
                payload = entry;
                found = true;
                break;
            }

            if (!found)
            {
                throw new InvalidOperationException("EXPLAIN JSON array was empty.");
            }
        }

        if (payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("EXPLAIN JSON envelope must be an object.");
        }

        if (!payload.TryGetProperty("Plan", out var plan) || plan.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("EXPLAIN JSON envelope is missing a Plan node.");
        }

        var totalElapsedMs = ReadDouble(payload, "Execution Time");
        var planningMs = ReadDouble(payload, "Planning Time");

        return new ExplainPlan
        {
            Root = ParseNode(plan),
            TotalElapsedMs = totalElapsedMs,
            PlanningMs = planningMs
        };
    }

    private static ExplainNode ParseNode(JsonElement node)
    {
        var nodeType = node.TryGetProperty("Node Type", out var nt) && nt.ValueKind == JsonValueKind.String
            ? nt.GetString() ?? "Unknown"
            : "Unknown";

        var relation = node.TryGetProperty("Relation Name", out var rel) && rel.ValueKind == JsonValueKind.String
            ? rel.GetString()
            : null;

        var children = new List<ExplainNode>();
        if (node.TryGetProperty("Plans", out var plans) && plans.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in plans.EnumerateArray())
            {
                if (child.ValueKind == JsonValueKind.Object)
                {
                    children.Add(ParseNode(child));
                }
            }
        }

        return new ExplainNode
        {
            NodeType = nodeType,
            Relation = relation,
            ActualRows = ReadDouble(node, "Actual Rows"),
            PlanRows = ReadDouble(node, "Plan Rows"),
            ActualTotalMs = ReadDouble(node, "Actual Total Time"),
            TotalCost = ReadDouble(node, "Total Cost"),
            Children = children
        };
    }

    private static double ReadDouble(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return 0d;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) => s,
            _ => 0d
        };
    }
}
