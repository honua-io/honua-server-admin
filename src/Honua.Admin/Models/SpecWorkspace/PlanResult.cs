using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

/// <summary>
/// Output of a plan call: the topologically-ordered DAG the server would execute
/// plus the estimated cost of each node.
/// </summary>
public sealed record PlanResult
{
    [JsonPropertyName("jobId")]
    public required string JobId { get; init; }

    [JsonPropertyName("nodes")]
    public IReadOnlyList<PlanNode> Nodes { get; init; } = System.Array.Empty<PlanNode>();

    [JsonPropertyName("warnings")]
    public IReadOnlyList<PlanWarning> Warnings { get; init; } = System.Array.Empty<PlanWarning>();

    [JsonPropertyName("parameters")]
    public IReadOnlyList<PlanParameterBinding> Parameters { get; init; } = System.Array.Empty<PlanParameterBinding>();

    [JsonPropertyName("failed")]
    public bool Failed { get; init; }

    [JsonPropertyName("failureMessage")]
    public string? FailureMessage { get; init; }
}

public sealed record PlanNode
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("inputs")]
    public IReadOnlyList<string> Inputs { get; init; } = System.Array.Empty<string>();

    [JsonPropertyName("depth")]
    public int Depth { get; init; }

    [JsonPropertyName("estimatedRows")]
    public long EstimatedRows { get; init; }

    [JsonPropertyName("estimatedBytes")]
    public long EstimatedBytes { get; init; }

    [JsonPropertyName("estimatedMillis")]
    public long EstimatedMillis { get; init; }

    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = System.Array.Empty<string>();

    [JsonPropertyName("contentHash")]
    public string? ContentHash { get; init; }

    [JsonPropertyName("cachePolicy")]
    public PlanCachePolicy CachePolicy { get; init; } = PlanCachePolicy.None;

    [JsonPropertyName("materialization")]
    public PlanMaterializationKind Materialization { get; init; } = PlanMaterializationKind.Ephemeral;
}

public sealed record PlanWarning(
    [property: JsonPropertyName("nodeId")] string? NodeId,
    [property: JsonPropertyName("severity")] PlanWarningSeverity Severity,
    [property: JsonPropertyName("message")] string Message);

public sealed record PlanParameterBinding(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("default")] string? Default,
    [property: JsonPropertyName("required")] bool Required,
    [property: JsonPropertyName("contentHash")] string ContentHash);

public enum PlanWarningSeverity
{
    Yellow,
    Red
}

public enum PlanCachePolicy
{
    None,
    MetadataOnly,
    ContentHash
}

public enum PlanMaterializationKind
{
    Ephemeral,
    PreviewOnly,
    DurableDataset,
    DurableService,
    DurableApp
}
