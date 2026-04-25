using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Source-generated serializer context for license-workspace DTOs. The admin
/// project is AOT/trim friendly; reflection-based <c>JsonSerializer</c> calls
/// would break WebAssembly publishing.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LicenseStatusDto))]
[JsonSerializable(typeof(EntitlementDto))]
[JsonSerializable(typeof(IReadOnlyList<EntitlementDto>))]
[JsonSerializable(typeof(LicenseApiEnvelope<LicenseStatusDto>))]
[JsonSerializable(typeof(LicenseApiEnvelope<IReadOnlyList<EntitlementDto>>))]
public sealed partial class LicenseWorkspaceJsonContext : JsonSerializerContext
{
}
