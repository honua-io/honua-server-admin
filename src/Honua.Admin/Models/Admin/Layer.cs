// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

/// <summary>
/// Layer summary from the
/// <c>Admin/LayerPublishingEndpoints</c> list endpoint.
/// </summary>
public sealed record LayerSummary
{
    [JsonPropertyName("layerId")] public int LayerId { get; init; }
    [JsonPropertyName("layerName")] public string LayerName { get; init; } = string.Empty;
    [JsonPropertyName("schema")] public string Schema { get; init; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("geometryType")] public string GeometryType { get; init; } = string.Empty;
    [JsonPropertyName("srid")] public int Srid { get; init; }
    [JsonPropertyName("primaryKey")] public string? PrimaryKey { get; init; }
    [JsonPropertyName("fieldCount")] public int FieldCount { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("serviceName")] public string ServiceName { get; init; } = "default";

    [JsonIgnore] public int Id => LayerId;
    [JsonIgnore] public string Name => LayerName;
}

/// <summary>
/// Style payload for the
/// <c>Admin/AdminLayerStyleEndpoints :: GET/PUT /metadata/layers/{id}/style</c>
/// endpoint.
/// </summary>
public sealed record LayerStyle
{
    [JsonPropertyName("mapLibreStyle")] public JsonElement? MapLibreStyle { get; init; }
    [JsonPropertyName("drawingInfo")] public JsonElement? DrawingInfo { get; init; }

    [JsonIgnore] public string MapLibreStyleText => FormatJson(MapLibreStyle);
    [JsonIgnore] public string DrawingInfoText => FormatJson(DrawingInfo);

    private static string FormatJson(JsonElement? element)
    {
        if (!element.HasValue ||
            element.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(element.Value, AdminJsonContext.Default.JsonElement);
    }
}

/// <summary>
/// Request payload for publishing a new layer
/// (<c>POST /admin/connections/{id}/layers</c>).
/// </summary>
public sealed record PublishLayerRequest
{
    [JsonPropertyName("schema")] public string Schema { get; init; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; init; } = string.Empty;
    [JsonPropertyName("layerName")] public string LayerName { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("geometryColumn")] public string? GeometryColumn { get; init; }
    [JsonPropertyName("geometryType")] public string? GeometryType { get; init; }
    [JsonPropertyName("srid")] public int? Srid { get; init; }
    [JsonPropertyName("primaryKey")] public string? PrimaryKey { get; init; }
    [JsonPropertyName("fields")] public IReadOnlyList<string> Fields { get; init; } = System.Array.Empty<string>();
    [JsonPropertyName("serviceName")] public string? ServiceName { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
}

public sealed record LayerEnabledRequest
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
}

public sealed record LayerStyleUpdateRequest
{
    [JsonPropertyName("mapLibreStyle")] public JsonElement? MapLibreStyle { get; init; }
    [JsonPropertyName("drawingInfo")] public JsonElement? DrawingInfo { get; init; }
}
