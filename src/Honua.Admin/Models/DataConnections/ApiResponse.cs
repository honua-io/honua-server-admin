using System;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// Mirrors <c>Honua.Server.Features.Infrastructure.Models.ApiResponse&lt;T&gt;</c>.
/// honua-server admin endpoints wrap every payload in this envelope; the admin UI
/// unwraps via <see cref="Data"/>. Duplicated locally to keep the data-connection
/// module self-contained — the identity workspace owns its own mirror under
/// <c>Models/Identity/IdentityModels.cs</c>; consolidate when a shared
/// <c>Models/Common</c> namespace is introduced.
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
