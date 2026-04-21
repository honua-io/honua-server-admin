using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpecWorkspace;

/// <summary>
/// Source-generated serializer context for all spec-workspace DTOs. Required because
/// the admin project is AOT/trim friendly; reflection-based <c>JsonSerializer</c> calls
/// would break WebAssembly publishing.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SpecDocument))]
[JsonSerializable(typeof(SpecGrammar))]
[JsonSerializable(typeof(WorkspaceSnapshot))]
[JsonSerializable(typeof(PlanResult))]
[JsonSerializable(typeof(ApplyEvent))]
[JsonSerializable(typeof(ConversationTurn))]
[JsonSerializable(typeof(IntentOutcome))]
[JsonSerializable(typeof(IntentRequest))]
[JsonSerializable(typeof(LayoutWidths))]
[JsonSerializable(typeof(CatalogCandidate))]
[JsonSerializable(typeof(ResolveQuery))]
public sealed partial class SpecWorkspaceJsonContext : JsonSerializerContext
{
}
