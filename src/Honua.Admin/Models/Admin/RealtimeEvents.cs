// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

public sealed record DataConnectionHealthChangedEvent
{
    [JsonPropertyName("connectionId")] public Guid ConnectionId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("healthStatus")] public string HealthStatus { get; init; } = string.Empty;
    [JsonPropertyName("lastHealthCheck")] public DateTimeOffset? LastHealthCheck { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}
