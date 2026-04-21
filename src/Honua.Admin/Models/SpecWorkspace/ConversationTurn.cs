using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

/// <summary>
/// One round-trip in the operator/grounding dialogue. Rendered as a card in the
/// NL pane. The <see cref="Response"/> discriminator determines which body the UI
/// shows (mutation diff, clarification picker, or error chip).
/// </summary>
public sealed record ConversationTurn
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("ts")]
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("response")]
    public required IntentOutcome Response { get; init; }
}

public enum IntentResponseKind
{
    Mutation,
    Clarification,
    Unsupported,
    Error
}

/// <summary>
/// Result of submitting an operator intent to the grounding service. Callers
/// branch on <see cref="Kind"/>; only one of <see cref="Mutation"/>,
/// <see cref="Clarification"/> or <see cref="Message"/> will be populated.
/// </summary>
public sealed record IntentOutcome
{
    [JsonPropertyName("kind")]
    public required IntentResponseKind Kind { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("mutation")]
    public SpecMutation? Mutation { get; init; }

    [JsonPropertyName("clarification")]
    public ClarificationRequest? Clarification { get; init; }
}

public sealed record SpecMutation
{
    [JsonPropertyName("section")]
    public required SpecSectionId Section { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("beforeJson")]
    public required string BeforeJson { get; init; }

    [JsonPropertyName("afterJson")]
    public required string AfterJson { get; init; }

    [JsonPropertyName("next")]
    public SpecDocument? NextDocument { get; init; }
}

public enum ClarificationKind
{
    PickDataset,
    PickColumn,
    PickValue,
    SpecifyUnit,
    SpecifyCrs,
    ChooseOp
}

public sealed record ClarificationRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("kind")]
    public required ClarificationKind Kind { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }

    [JsonPropertyName("options")]
    public IReadOnlyList<ClarificationOption> Options { get; init; } = System.Array.Empty<ClarificationOption>();

    [JsonPropertyName("field")]
    public string? Field { get; init; }
}

public sealed record ClarificationOption(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("hint")] string? Hint = null);

public sealed record IntentRequest
{
    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("spec")]
    public required SpecDocument CurrentSpec { get; init; }

    [JsonPropertyName("clarificationId")]
    public string? ClarificationId { get; init; }

    [JsonPropertyName("clarificationValue")]
    public string? ClarificationValue { get; init; }
}
