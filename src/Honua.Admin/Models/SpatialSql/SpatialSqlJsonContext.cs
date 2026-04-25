using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpatialSql;

/// <summary>
/// Source-generated serializer context for spatial-SQL DTOs. Mirrors the
/// SpecWorkspace approach so admin can publish AOT/trim-friendly without
/// reflection-based JSON.
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
public sealed partial class SpatialSqlJsonContext : JsonSerializerContext
{
}
