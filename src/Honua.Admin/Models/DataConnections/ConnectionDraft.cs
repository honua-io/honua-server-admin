using System;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// Working copy of a connection being created or edited. Credentials live here
/// only for the duration of a single submission; the state store clears the
/// draft as soon as the create / update returns. Never persisted to local
/// storage and never round-tripped from the server.
/// </summary>
public sealed class ConnectionDraft
{
    public Guid? ConnectionId { get; init; }

    public string ProviderId { get; init; } = "postgres";

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5432;

    public string DatabaseName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string? Password { get; set; }

    public string? SecretReference { get; set; }

    public string? SecretType { get; set; }

    public bool SslRequired { get; set; } = true;

    public string SslMode { get; set; } = "Require";

    public bool? IsActive { get; set; }

    public CredentialMode CredentialMode { get; set; } = CredentialMode.Managed;
}

public enum CredentialMode
{
    Managed,
    External
}

/// <summary>
/// Wire shape for create. Mirrors <c>CreateSecureConnectionRequest</c> on the
/// server bytes-for-bytes.
/// </summary>
public sealed class CreateConnectionRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Host { get; init; }

    public int Port { get; init; } = 5432;

    public required string DatabaseName { get; init; }

    public required string Username { get; init; }

    public string? Password { get; init; }

    public string? SecretReference { get; init; }

    public string? SecretType { get; init; }

    public bool SslRequired { get; init; } = true;

    public string SslMode { get; init; } = "Require";
}

/// <summary>
/// Wire shape for update. Every field is nullable so the caller can pass a
/// partial body — the server keeps prior values for fields left null.
/// </summary>
public sealed class UpdateConnectionRequest
{
    public string? Description { get; init; }

    public string? Host { get; init; }

    public int? Port { get; init; }

    public string? DatabaseName { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public bool? SslRequired { get; init; }

    public string? SslMode { get; init; }

    public bool? IsActive { get; init; }
}
