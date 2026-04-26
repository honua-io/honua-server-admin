using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpatialSql;

/// <summary>
/// EXPLAIN (ANALYZE, FORMAT JSON) projection. <see cref="Root"/> mirrors the server's
/// top-level plan node; the admin renders the tree under <see cref="ExplainNode.Children"/>
/// without re-deriving timing or row counts.
/// </summary>
public sealed record ExplainPlan
{
    [JsonPropertyName("root")]
    public required ExplainNode Root { get; init; }

    [JsonPropertyName("totalElapsedMs")]
    public double TotalElapsedMs { get; init; }

    [JsonPropertyName("planningMs")]
    public double PlanningMs { get; init; }

    [JsonPropertyName("error")]
    public SqlExecuteError? Error { get; init; }

    [JsonIgnore]
    public bool IsError => Error is not null;
}

public sealed record ExplainNode
{
    [JsonPropertyName("nodeType")]
    public required string NodeType { get; init; }

    [JsonPropertyName("relation")]
    public string? Relation { get; init; }

    [JsonPropertyName("actualRows")]
    public double ActualRows { get; init; }

    [JsonPropertyName("planRows")]
    public double PlanRows { get; init; }

    [JsonPropertyName("actualTotalMs")]
    public double ActualTotalMs { get; init; }

    [JsonPropertyName("totalCost")]
    public double TotalCost { get; init; }

    [JsonPropertyName("children")]
    public IReadOnlyList<ExplainNode> Children { get; init; } = System.Array.Empty<ExplainNode>();

    /// <summary>
    /// True when the planner's row estimate differs from the actual row count by 10x
    /// or more in either direction. Drives the misestimate badge in the tree view.
    /// </summary>
    [JsonIgnore]
    public bool RowEstimateOff =>
        PlanRows > 0
        && ActualRows > 0
        && (ActualRows / PlanRows >= 10d || PlanRows / ActualRows >= 10d);
}
