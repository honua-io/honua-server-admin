using System.Text.Json.Serialization;
using Honua.Admin.Services.SpatialSql;

namespace Honua.Admin.Models.SpatialSql;

/// <summary>
/// Source-generated serializer context for spatial-SQL DTOs. Mirrors the
/// SpecWorkspace approach so admin can publish AOT/trim-friendly without
/// reflection-based JSON. Includes <see cref="MapPreviewFeature"/> so the
/// JS interop boundary stays trim-safe alongside the network DTOs.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SchemaSnapshot))]
[JsonSerializable(typeof(SqlExecuteRequest))]
[JsonSerializable(typeof(SqlExecuteResult))]
[JsonSerializable(typeof(SqlExplainRequest))]
[JsonSerializable(typeof(ExplainPlan))]
[JsonSerializable(typeof(ExplainNode))]
[JsonSerializable(typeof(SaveViewRequest))]
[JsonSerializable(typeof(NamedViewRegistration))]
[JsonSerializable(typeof(MapPreviewFeature))]
[JsonSerializable(typeof(System.Collections.Generic.IReadOnlyList<MapPreviewFeature>))]
public sealed partial class SpatialSqlJsonContext : JsonSerializerContext
{
}
