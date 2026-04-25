using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

/// <summary>
/// What gets serialized to localStorage for "refresh preserves state". Captures the
/// current spec draft, conversation, layout widths, and principal so different
/// operators sharing a browser get isolated drafts.
/// </summary>
public sealed record WorkspaceSnapshot
{
    [JsonPropertyName("principalId")]
    public string PrincipalId { get; init; } = "operator";

    [JsonPropertyName("spec")]
    public SpecDocument Spec { get; init; } = SpecDocument.Empty;

    [JsonPropertyName("conversation")]
    public IReadOnlyList<ConversationTurn> Conversation { get; init; } = System.Array.Empty<ConversationTurn>();

    [JsonPropertyName("layout")]
    public LayoutWidths Layout { get; init; } = LayoutWidths.Default;

    [JsonPropertyName("promptDraft")]
    public string PromptDraft { get; init; } = string.Empty;

    [JsonPropertyName("isJsonView")]
    public bool IsJsonView { get; init; }

    [JsonPropertyName("sectionTexts")]
    public Dictionary<string, string> SectionTexts { get; init; } = new(StringComparer.Ordinal);
}

public sealed record LayoutWidths(
    [property: JsonPropertyName("nl")] double Nl,
    [property: JsonPropertyName("dsl")] double Dsl,
    [property: JsonPropertyName("preview")] double Preview)
{
    public static LayoutWidths Default { get; } = new(28, 36, 36);
}
