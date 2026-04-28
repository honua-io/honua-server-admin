using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

public enum ApplyNodeStatus
{
    Pending,
    Running,
    Completed,
    CacheHit,
    Cancelled,
    Failed
}

/// <summary>
/// Single event on the apply stream. The stream begins with <see cref="ApplyEventKind.Started"/>,
/// interleaves <see cref="ApplyEventKind.NodeUpdate"/> per plan node, and ends with either
/// <see cref="ApplyEventKind.Completed"/>, <see cref="ApplyEventKind.Cancelled"/> or
/// <see cref="ApplyEventKind.Failed"/>.
/// </summary>
public sealed record ApplyEvent
{
    [JsonPropertyName("kind")]
    public required ApplyEventKind Kind { get; init; }

    [JsonPropertyName("jobId")]
    public required string JobId { get; init; }

    [JsonPropertyName("nodeId")]
    public string? NodeId { get; init; }

    [JsonPropertyName("nodeOp")]
    public string? NodeOp { get; init; }

    [JsonPropertyName("status")]
    public ApplyNodeStatus? Status { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("payload")]
    public ApplyPayload? Payload { get; init; }

    [JsonPropertyName("cacheKey")]
    public string? CacheKey { get; init; }

    [JsonPropertyName("materializedResource")]
    public MaterializedResource? MaterializedResource { get; init; }
}

public enum ApplyEventKind
{
    Started,
    NodeUpdate,
    Completed,
    Cancelled,
    Failed
}

public sealed record ApplyPayload
{
    [JsonPropertyName("kind")]
    public required SpecOutputKind Kind { get; init; }

    [JsonPropertyName("mapFeatures")]
    public IReadOnlyList<MapFeature> MapFeatures { get; init; } = System.Array.Empty<MapFeature>();

    [JsonPropertyName("tableRows")]
    public IReadOnlyList<IReadOnlyDictionary<string, string>> TableRows { get; init; } = System.Array.Empty<IReadOnlyDictionary<string, string>>();

    [JsonPropertyName("tableColumns")]
    public IReadOnlyList<string> TableColumns { get; init; } = System.Array.Empty<string>();

    [JsonPropertyName("parameterBindings")]
    public IReadOnlyList<PlanParameterBinding> ParameterBindings { get; init; } = System.Array.Empty<PlanParameterBinding>();

    [JsonPropertyName("appScaffold")]
    public AppScaffold? AppScaffold { get; init; }
}

public sealed record MapFeature(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("geometry")] string Geometry,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon);

public sealed record AppScaffold(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("parameters")] IReadOnlyList<AppScaffoldParameter> Parameters,
    [property: JsonPropertyName("layout")] string Layout);

public sealed record AppScaffoldParameter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("default")] string? Default);

public sealed record MaterializedResource(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] PlanMaterializationKind Kind,
    [property: JsonPropertyName("version")] string Version);
