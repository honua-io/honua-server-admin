using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpatialSql;

public sealed record SqlExecuteRequest
{
    [JsonPropertyName("sql")]
    public required string Sql { get; init; }

    [JsonPropertyName("parameters")]
    public IReadOnlyList<SqlParameter> Parameters { get; init; } = System.Array.Empty<SqlParameter>();

    [JsonPropertyName("allowMutation")]
    public bool AllowMutation { get; init; }

    [JsonPropertyName("rowLimit")]
    public int? RowLimit { get; init; }

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; init; }
}

public sealed record SqlParameter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value);

/// <summary>
/// Result of a SQL execution. <see cref="Truncated"/> reflects the server-applied row
/// cap; <see cref="RowLimit"/> echoes the cap in effect so the UI can show it. Mutation
/// requests that succeed populate <see cref="AuditEntryId"/>.
/// </summary>
public sealed record SqlExecuteResult
{
    [JsonPropertyName("columns")]
    public IReadOnlyList<SqlColumn> Columns { get; init; } = System.Array.Empty<SqlColumn>();

    [JsonPropertyName("rows")]
    public IReadOnlyList<SqlRow> Rows { get; init; } = System.Array.Empty<SqlRow>();

    [JsonPropertyName("truncated")]
    public bool Truncated { get; init; }

    [JsonPropertyName("rowLimit")]
    public int RowLimit { get; init; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; init; }

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; init; }

    [JsonPropertyName("geometryColumnIndex")]
    public int? GeometryColumnIndex { get; init; }

    [JsonPropertyName("geometrySrid")]
    public int? GeometrySrid { get; init; }

    [JsonPropertyName("auditEntryId")]
    public string? AuditEntryId { get; init; }

    [JsonPropertyName("error")]
    public SqlExecuteError? Error { get; init; }

    [JsonIgnore]
    public bool HasGeometry => GeometryColumnIndex is int idx && idx >= 0 && idx < Columns.Count;

    [JsonIgnore]
    public bool IsError => Error is not null;
}

public sealed record SqlColumn(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("isGeometry")] bool IsGeometry = false);

/// <summary>
/// One result row. Cells are stored as a nullable-string list so SQL <c>NULL</c>s
/// stay distinguishable from empty strings, while the wrapper itself is a
/// non-nullable type — keeping it usable as a Razor generic parameter where
/// <c>IReadOnlyList&lt;string?&gt;</c> would trip nullable-context source-generation.
/// </summary>
public sealed record SqlRow([property: JsonPropertyName("cells")] IReadOnlyList<string?> Cells)
{
    public int Count => Cells.Count;

    public string? this[int index] => Cells[index];
}

public sealed record SqlExecuteError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record SqlExplainRequest
{
    [JsonPropertyName("sql")]
    public required string Sql { get; init; }

    [JsonPropertyName("parameters")]
    public IReadOnlyList<SqlParameter> Parameters { get; init; } = System.Array.Empty<SqlParameter>();
}
