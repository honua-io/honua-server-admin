using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

/// <summary>
/// Canonical in-memory projection of a spec draft. Named sections mirror the
/// walking-skeleton resource set (<c>sources</c>, <c>scope</c>, <c>parameters</c>,
/// <c>compute</c>, <c>map</c>, <c>output</c>). JSON is the authoritative form; text projections
/// are derived.
/// </summary>
public sealed record SpecDocument
{
    [JsonPropertyName("sources")]
    public IReadOnlyList<SpecSourceEntry> Sources { get; init; } = System.Array.Empty<SpecSourceEntry>();

    [JsonPropertyName("scope")]
    public SpecScope Scope { get; init; } = new();

    [JsonPropertyName("parameters")]
    public IReadOnlyList<SpecParameterEntry> Parameters { get; init; } = System.Array.Empty<SpecParameterEntry>();

    [JsonPropertyName("compute")]
    public IReadOnlyList<SpecComputeStep> Compute { get; init; } = System.Array.Empty<SpecComputeStep>();

    [JsonPropertyName("map")]
    public SpecMap Map { get; init; } = new();

    [JsonPropertyName("output")]
    public SpecOutput Output { get; init; } = new();

    public static SpecDocument Empty { get; } = new();

    public SpecDocument WithSection(SpecSectionId section, SpecDocument source) => section switch
    {
        SpecSectionId.Sources => this with { Sources = source.Sources },
        SpecSectionId.Scope => this with { Scope = source.Scope },
        SpecSectionId.Parameters => this with { Parameters = source.Parameters },
        SpecSectionId.Compute => this with { Compute = source.Compute },
        SpecSectionId.Map => this with { Map = source.Map },
        SpecSectionId.Output => this with { Output = source.Output },
        _ => this
    };
}

public sealed record SpecSourceEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("dataset")] string Dataset,
    [property: JsonPropertyName("pin")] string? Pin = null);

public sealed record SpecScope
{
    [JsonPropertyName("bbox")]
    public double[]? Bbox { get; init; }

    [JsonPropertyName("crs")]
    public string? Crs { get; init; }
}

public sealed record SpecParameterEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("default")] string? Default = null,
    [property: JsonPropertyName("required")] bool Required = false);

public sealed record SpecComputeStep(
    [property: JsonPropertyName("op")] string Op,
    [property: JsonPropertyName("inputs")] IReadOnlyList<string> Inputs,
    [property: JsonPropertyName("args")] IReadOnlyDictionary<string, string> Args);

public sealed record SpecMap
{
    [JsonPropertyName("layers")]
    public IReadOnlyList<SpecMapLayer> Layers { get; init; } = System.Array.Empty<SpecMapLayer>();
}

public sealed record SpecMapLayer(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("symbology")] string Symbology);

public sealed record SpecOutput
{
    [JsonPropertyName("kind")]
    public SpecOutputKind Kind { get; init; } = SpecOutputKind.None;

    [JsonPropertyName("target")]
    public string? Target { get; init; }
}

public enum SpecOutputKind
{
    None,
    Analysis,
    Map,
    AppScaffold
}

public enum SpecSectionId
{
    Sources = 0,
    Scope = 1,
    Compute = 2,
    Map = 3,
    Output = 4,
    Parameters = 5
}
