using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.SpatialSql;

public sealed record SaveViewRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("sql")]
    public required string Sql { get; init; }

    [JsonPropertyName("parameters")]
    public IReadOnlyList<SqlParameter> Parameters { get; init; } = System.Array.Empty<SqlParameter>();

    [JsonPropertyName("geometryColumn")]
    public string? GeometryColumn { get; init; }

    [JsonPropertyName("srid")]
    public int? Srid { get; init; }
}

/// <summary>
/// Server response for a saved query. The three protocol URLs come back as the
/// server has them — the admin treats them as opaque copy-to-clipboard chips.
/// </summary>
public sealed record NamedViewRegistration
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("featureServerUrl")]
    public string? FeatureServerUrl { get; init; }

    [JsonPropertyName("ogcFeaturesUrl")]
    public string? OgcFeaturesUrl { get; init; }

    [JsonPropertyName("oDataUrl")]
    public string? ODataUrl { get; init; }

    [JsonPropertyName("error")]
    public SqlExecuteError? Error { get; init; }

    [JsonIgnore]
    public bool IsError => Error is not null;
}
