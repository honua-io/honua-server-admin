// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

/// <summary>
/// Service summary from <c>Admin/ServiceSettingsEndpoints</c> list.
/// </summary>
public sealed record ServiceSummary
{
    [JsonPropertyName("serviceName")] public string ServiceName { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("layerCount")] public int LayerCount { get; init; }
    [JsonPropertyName("enabledProtocols")] public IReadOnlyList<string> EnabledProtocols { get; init; } = System.Array.Empty<string>();

    [JsonIgnore] public string Name => ServiceName;
    [JsonIgnore] public bool Enabled => EnabledProtocols.Count > 0;
}

/// <summary>
/// Service settings detail from
/// <c>Admin/ServiceSettingsEndpoints :: GET /services/{name}/settings</c>.
/// </summary>
public sealed record ServiceSettings
{
    [JsonPropertyName("serviceName")] public string ServiceName { get; init; } = string.Empty;
    [JsonPropertyName("enabledProtocols")] public IReadOnlyList<string> EnabledProtocols { get; init; } = System.Array.Empty<string>();
    [JsonPropertyName("availableProtocols")] public IReadOnlyList<string> AvailableProtocols { get; init; } = System.Array.Empty<string>();
    [JsonPropertyName("accessPolicy")] public AccessPolicySettings? AccessPolicy { get; init; }
    [JsonPropertyName("timeInfo")] public TimeInfoSettings? TimeInfo { get; init; }
    [JsonPropertyName("mapServer")] public MapServerSettings MapServer { get; init; } = new();

    [JsonIgnore] public string Name => ServiceName;
}

public sealed record AccessPolicySettings
{
    [JsonPropertyName("allowAnonymous")] public bool? AllowAnonymous { get; init; }
    [JsonPropertyName("allowAnonymousWrite")] public bool? AllowAnonymousWrite { get; init; }
    [JsonPropertyName("allowedRoles")] public IReadOnlyList<string>? AllowedRoles { get; init; }
    [JsonPropertyName("allowedWriteRoles")] public IReadOnlyList<string>? AllowedWriteRoles { get; init; }
}

public sealed record TimeInfoSettings
{
    [JsonPropertyName("startTimeField")] public string? StartTimeField { get; init; }
    [JsonPropertyName("endTimeField")] public string? EndTimeField { get; init; }
    [JsonPropertyName("trackIdField")] public string? TrackIdField { get; init; }
}

public sealed record MapServerSettings
{
    [JsonPropertyName("maxImageWidth")] public int? MaxImageWidth { get; init; }
    [JsonPropertyName("maxImageHeight")] public int? MaxImageHeight { get; init; }
    [JsonPropertyName("defaultImageWidth")] public int? DefaultImageWidth { get; init; }
    [JsonPropertyName("defaultImageHeight")] public int? DefaultImageHeight { get; init; }
    [JsonPropertyName("defaultDpi")] public int? DefaultDpi { get; init; }
    [JsonPropertyName("defaultFormat")] public string? DefaultFormat { get; init; }
    [JsonPropertyName("defaultTransparent")] public bool? DefaultTransparent { get; init; }
    [JsonPropertyName("maxFeaturesPerLayer")] public int? MaxFeaturesPerLayer { get; init; }
}

public sealed record UpdateProtocolsRequest
{
    [JsonPropertyName("enabledProtocols")] public IReadOnlyList<string> EnabledProtocols { get; init; } = System.Array.Empty<string>();
}

public sealed record UpdateLayerMetadataRequest
{
    [JsonPropertyName("accessPolicy")] public AccessPolicySettings? AccessPolicy { get; init; }
    [JsonPropertyName("timeInfo")] public TimeInfoSettings? TimeInfo { get; init; }
}

public sealed record LayerMetadataResponse
{
    [JsonPropertyName("layerId")] public int LayerId { get; init; }
    [JsonPropertyName("layerName")] public string LayerName { get; init; } = string.Empty;
    [JsonPropertyName("accessPolicy")] public AccessPolicySettings? AccessPolicy { get; init; }
    [JsonPropertyName("timeInfo")] public TimeInfoSettings? TimeInfo { get; init; }
}
