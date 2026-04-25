using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

/// <summary>
/// Projection of the section/operator grammar the server publishes. S1 ships an
/// embedded copy in <c>Resources/spec-grammar.v1.json</c>; once the real server
/// grammar endpoint lands, the deserializer here is unchanged.
/// </summary>
public sealed record SpecGrammar
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "v1";

    [JsonPropertyName("sections")]
    public IReadOnlyList<SpecGrammarSection> Sections { get; init; } = System.Array.Empty<SpecGrammarSection>();

    [JsonPropertyName("operators")]
    public IReadOnlyList<SpecGrammarOperator> Operators { get; init; } = System.Array.Empty<SpecGrammarOperator>();

    [JsonPropertyName("symbology")]
    public SpecGrammarSymbology Symbology { get; init; } = new();
}

public sealed record SpecGrammarSection
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("order")]
    public int Order { get; init; }
}

public sealed record SpecGrammarOperator
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("section")]
    public string Section { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public IReadOnlyList<SpecGrammarParam> Params { get; init; } = System.Array.Empty<SpecGrammarParam>();
}

public sealed record SpecGrammarParam
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("default")]
    public string? Default { get; init; }

    [JsonPropertyName("doc")]
    public string Doc { get; init; } = string.Empty;
}

public sealed record SpecGrammarSymbology
{
    [JsonPropertyName("ramps")]
    public IReadOnlyList<string> Ramps { get; init; } = System.Array.Empty<string>();

    [JsonPropertyName("classifiers")]
    public IReadOnlyList<string> Classifiers { get; init; } = System.Array.Empty<string>();
}
