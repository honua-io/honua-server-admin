// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

/// <summary>
/// Lightweight projection of a honua-server secure connection. This mirrors
/// <c>SecureConnectionSummary</c> from <c>Admin/SecureConnectionEndpoints</c>
/// and intentionally excludes credential material.
/// </summary>
public record ConnectionSummary
{
    [JsonPropertyName("connectionId")] public Guid ConnectionId { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("host")] public string Host { get; init; } = string.Empty;
    [JsonPropertyName("port")] public int Port { get; init; }
    [JsonPropertyName("databaseName")] public string DatabaseName { get; init; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("sslRequired")] public bool SslRequired { get; init; }
    [JsonPropertyName("sslMode")] public string SslMode { get; init; } = string.Empty;
    [JsonPropertyName("storageType")] public string StorageType { get; init; } = string.Empty;
    [JsonPropertyName("isActive")] public bool IsActive { get; init; }
    [JsonPropertyName("healthStatus")] public string HealthStatus { get; init; } = "unknown";
    [JsonPropertyName("lastHealthCheck")] public DateTimeOffset? LastHealthCheck { get; init; }
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("createdBy")] public string CreatedBy { get; init; } = string.Empty;

    [JsonIgnore] public string Id => ConnectionId == Guid.Empty ? Name : ConnectionId.ToString("D");
    [JsonIgnore] public string Provider => StorageType;
    [JsonIgnore] public string Status => HealthStatus;
}
