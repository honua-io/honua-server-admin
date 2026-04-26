using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpatialSql;

/// <summary>
/// Schema snapshot fed to the SQL editor for autocomplete. Tables and columns come
/// from the server's published-layer view; PostGIS functions/operators come from a
/// curated reference list shipped alongside the snapshot.
/// </summary>
public sealed record SchemaSnapshot
{
    [JsonPropertyName("tables")]
    public IReadOnlyList<SchemaTable> Tables { get; init; } = System.Array.Empty<SchemaTable>();

    [JsonPropertyName("functions")]
    public IReadOnlyList<PostGisFunction> Functions { get; init; } = System.Array.Empty<PostGisFunction>();

    [JsonPropertyName("operators")]
    public IReadOnlyList<PostGisOperator> Operators { get; init; } = System.Array.Empty<PostGisOperator>();

    [JsonPropertyName("fetchedAt")]
    public string FetchedAt { get; init; } = string.Empty;
}

public sealed record SchemaTable
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("geometryColumn")]
    public string? GeometryColumn { get; init; }

    [JsonPropertyName("srid")]
    public int? Srid { get; init; }

    [JsonPropertyName("columns")]
    public IReadOnlyList<SchemaColumn> Columns { get; init; } = System.Array.Empty<SchemaColumn>();
}

public sealed record SchemaColumn(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string? Description = null);

public sealed record PostGisFunction(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("signature")] string Signature,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("documentation")] string Documentation);

public sealed record PostGisOperator(
    [property: JsonPropertyName("symbol")] string Symbol,
    [property: JsonPropertyName("description")] string Description);
