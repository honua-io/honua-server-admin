using System;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// UI projection of <c>Honua.Sdk.Admin.Models.SecureConnectionSummary</c>.
/// The local model keeps the workspace provider metadata out of the reusable SDK
/// wire contract.
/// </summary>
public sealed record DataConnectionSummary
{
    public required Guid ConnectionId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; }

    public required string DatabaseName { get; init; }

    public required string Username { get; init; }

    public bool SslRequired { get; init; }

    public required string SslMode { get; init; }

    public required string StorageType { get; init; }

    public bool IsActive { get; init; }

    public required string HealthStatus { get; init; }

    public DateTimeOffset? LastHealthCheck { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required string CreatedBy { get; init; }

    [JsonIgnore]
    public string ProviderId => "postgres";
}

public sealed record DataConnectionDetail
{
    public required Guid ConnectionId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; }

    public required string DatabaseName { get; init; }

    public required string Username { get; init; }

    public bool SslRequired { get; init; }

    public required string SslMode { get; init; }

    public required string StorageType { get; init; }

    public bool IsActive { get; init; }

    public required string HealthStatus { get; init; }

    public DateTimeOffset? LastHealthCheck { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public required string CreatedBy { get; init; }

    public string? CredentialReference { get; init; }

    public int EncryptionVersion { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    [JsonIgnore]
    public string ProviderId => "postgres";
}
