using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

public enum CatalogCandidateKind
{
    Dataset,
    Column,
    Operator,
    SymbologyRamp,
    Value
}

public sealed record CatalogCandidate
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("kind")]
    public required CatalogCandidateKind Kind { get; init; }

    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("parent")]
    public string? Parent { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("sampleValues")]
    public IReadOnlyList<string> SampleValues { get; init; } = System.Array.Empty<string>();

    [JsonPropertyName("rbacScope")]
    public string? RbacScope { get; init; }

    [JsonPropertyName("costHint")]
    public string? CostHint { get; init; }

    [JsonPropertyName("documentation")]
    public string? Documentation { get; init; }
}

public sealed record ResolveQuery
{
    [JsonPropertyName("trigger")]
    public required CatalogTrigger Trigger { get; init; }

    [JsonPropertyName("prefix")]
    public string Prefix { get; init; } = string.Empty;

    [JsonPropertyName("parent")]
    public string? Parent { get; init; }

    [JsonPropertyName("principalId")]
    public string PrincipalId { get; init; } = "operator";
}

public enum CatalogTrigger
{
    AtMention,
    DotMember,
    ParamList,
    SymbologyRamp
}
