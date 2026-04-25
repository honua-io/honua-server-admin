using System;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Identity;

/// <summary>
/// Mirrors <c>Honua.Server.Features.Infrastructure.Models.ApiResponse&lt;T&gt;</c>. The
/// honua-server admin endpoints wrap every payload in this envelope; the admin UI
/// unwraps via <see cref="Data"/>.
/// </summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Mirrors honua-server's <c>OidcProviderResponse</c>. Secrets are intentionally
/// excluded — the server never round-trips <c>ClientSecret</c>.
/// </summary>
public sealed class OidcProviderResponse
{
    [JsonPropertyName("providerId")]
    public Guid ProviderId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("providerType")]
    public string ProviderType { get; init; } = string.Empty;

    [JsonPropertyName("authority")]
    public string Authority { get; init; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("isHealthy")]
    public bool IsHealthy { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    [JsonPropertyName("lastHealthCheck")]
    public DateTimeOffset? LastHealthCheck { get; init; }
}

/// <summary>
/// Mirrors honua-server's <c>CreateOidcProviderRequest</c>. <see cref="ClientSecret"/>
/// is the only place a plaintext secret ever lives in admin-UI memory; callers must
/// clear the field after the request resolves.
/// </summary>
public sealed class CreateOidcProviderRequest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("providerType")]
    public string ProviderType { get; init; } = string.Empty;

    [JsonPropertyName("authority")]
    public string Authority { get; init; } = string.Empty;

    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Mirrors honua-server's <c>UpdateOidcProviderRequest</c>. Properties stay nullable
/// so the admin UI can omit unchanged fields; <see cref="ClientSecret"/> remains
/// write-only and is only sent when the operator opts to rotate it.
/// </summary>
public sealed class UpdateOidcProviderRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("authority")]
    public string? Authority { get; init; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }
}

/// <summary>
/// Mirrors honua-server's <c>OidcProviderTestResponse</c>. The server reports a
/// reachability flag plus a free-form message; the admin UI maps the message
/// through <see cref="Honua.Admin.Services.Identity.IdentityDiagnostics"/>.
/// </summary>
public sealed class OidcProviderTestResponse
{
    [JsonPropertyName("providerId")]
    public Guid ProviderId { get; init; }

    [JsonPropertyName("isReachable")]
    public bool IsReachable { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("testedAt")]
    public DateTimeOffset TestedAt { get; init; }
}

/// <summary>
/// Mirrors honua-server's <c>IdentityProvidersResponse</c>.
/// </summary>
public sealed class IdentityProvidersResponse
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("providers")]
    public IdentityProviderStatus[] Providers { get; init; } = Array.Empty<IdentityProviderStatus>();
}

/// <summary>
/// Mirrors honua-server's <c>IdentityProviderStatus</c>.
/// </summary>
public sealed class IdentityProviderStatus
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("authority")]
    public string? Authority { get; init; }

    [JsonPropertyName("callbackPath")]
    public string? CallbackPath { get; init; }

    [JsonPropertyName("scopes")]
    public string[]? Scopes { get; init; }

    [JsonPropertyName("isConfigurationValid")]
    public bool IsConfigurationValid { get; init; }
}

/// <summary>
/// Mirrors honua-server's <c>IdentityProviderTestResult</c>.
/// </summary>
public sealed class IdentityProviderTestResult
{
    [JsonPropertyName("providerType")]
    public string ProviderType { get; init; } = string.Empty;

    [JsonPropertyName("isReachable")]
    public bool IsReachable { get; init; }

    [JsonPropertyName("responseTimeMs")]
    public double? ResponseTimeMs { get; init; }

    [JsonPropertyName("discoveryUrl")]
    public string? DiscoveryUrl { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; }
}
