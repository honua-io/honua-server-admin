// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Admin;

/// <summary>
/// Full connection record returned by the secure connection detail endpoint.
/// </summary>
public sealed record ConnectionDetail : ConnectionSummary
{
    [JsonPropertyName("credentialReference")] public string? CredentialReference { get; init; }
    [JsonPropertyName("encryptionVersion")] public int EncryptionVersion { get; init; }
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Discovered table from a connection's tables endpoint
/// (<c>Admin/AdminEndpoints :: GET /admin/connections/{id}/tables</c>).
/// </summary>
public sealed record DiscoveredTable
{
    [JsonPropertyName("schema")] public string Schema { get; init; } = string.Empty;
    [JsonPropertyName("table")] public string Table { get; init; } = string.Empty;
    [JsonPropertyName("geometryColumn")] public string? GeometryColumn { get; init; }
    [JsonPropertyName("geometryType")] public string? GeometryType { get; init; }
    [JsonPropertyName("srid")] public int? Srid { get; init; }
    [JsonPropertyName("estimatedRows")] public long? EstimatedRows { get; init; }
    [JsonPropertyName("columns")] public IReadOnlyList<TableColumn> Columns { get; init; } = Array.Empty<TableColumn>();
}

/// <summary>
/// Response from the table-discovery endpoint.
/// </summary>
public sealed record TableDiscoveryResponse
{
    [JsonPropertyName("tables")] public IReadOnlyList<DiscoveredTable> Tables { get; init; } = Array.Empty<DiscoveredTable>();
}

public sealed record TableColumn
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("dataType")] public string DataType { get; init; } = string.Empty;
    [JsonPropertyName("isNullable")] public bool IsNullable { get; init; }
    [JsonPropertyName("isPrimaryKey")] public bool IsPrimaryKey { get; init; }
    [JsonPropertyName("maxLength")] public int? MaxLength { get; init; }
}

/// <summary>
/// Payload for creating or testing a connection draft.
/// </summary>
public sealed record CreateConnectionRequest
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("host")] public string Host { get; init; } = string.Empty;
    [JsonPropertyName("port")] public int Port { get; init; } = 5432;
    [JsonPropertyName("databaseName")] public string DatabaseName { get; init; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; init; } = string.Empty;
    [JsonPropertyName("password")] public string? Password { get; init; }
    [JsonPropertyName("secretReference")] public string? SecretReference { get; init; }
    [JsonPropertyName("secretType")] public string? SecretType { get; init; }
    [JsonPropertyName("sslRequired")] public bool SslRequired { get; init; } = true;
    [JsonPropertyName("sslMode")] public string SslMode { get; init; } = "Require";
}

public sealed record UpdateConnectionRequest
{
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("host")] public string? Host { get; init; }
    [JsonPropertyName("port")] public int? Port { get; init; }
    [JsonPropertyName("databaseName")] public string? DatabaseName { get; init; }
    [JsonPropertyName("username")] public string? Username { get; init; }
    [JsonPropertyName("password")] public string? Password { get; init; }
    [JsonPropertyName("sslRequired")] public bool? SslRequired { get; init; }
    [JsonPropertyName("sslMode")] public string? SslMode { get; init; }
    [JsonPropertyName("isActive")] public bool? IsActive { get; init; }
}

public sealed record TestConnectionResult
{
    [JsonPropertyName("connectionId")] public Guid ConnectionId { get; init; }
    [JsonPropertyName("connectionName")] public string ConnectionName { get; init; } = string.Empty;
    [JsonPropertyName("isHealthy")] public bool IsHealthy { get; init; }
    [JsonPropertyName("testedAt")] public DateTimeOffset TestedAt { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed record EncryptionValidationResult
{
    [JsonPropertyName("isValid")] public bool IsValid { get; init; }
    [JsonPropertyName("currentKeyVersion")] public int CurrentKeyVersion { get; init; }
    [JsonPropertyName("validatedAt")] public DateTimeOffset ValidatedAt { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed record KeyRotationResult
{
    [JsonPropertyName("previousKeyVersion")] public int PreviousKeyVersion { get; init; }
    [JsonPropertyName("newKeyVersion")] public int NewKeyVersion { get; init; }
    [JsonPropertyName("rotatedAt")] public DateTimeOffset RotatedAt { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
