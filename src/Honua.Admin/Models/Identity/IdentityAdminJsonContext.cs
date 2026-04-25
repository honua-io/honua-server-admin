using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Admin.Models.Identity;

/// <summary>
/// Source-generated serializer context for identity admin DTOs. Required to keep
/// the WASM build trim-friendly, mirroring honua-server's
/// <c>IdentityAdminJsonContext</c> / <c>OidcProviderJsonContext</c>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ApiResponse<OidcProviderResponse>))]
[JsonSerializable(typeof(ApiResponse<OidcProviderTestResponse>))]
[JsonSerializable(typeof(ApiResponse<IdentityProvidersResponse>))]
[JsonSerializable(typeof(ApiResponse<IdentityProviderTestResult>))]
[JsonSerializable(typeof(ApiResponse<IReadOnlyList<OidcProviderResponse>>))]
[JsonSerializable(typeof(ApiResponse<List<OidcProviderResponse>>))]
[JsonSerializable(typeof(ApiResponse<object>))]
[JsonSerializable(typeof(CreateOidcProviderRequest))]
[JsonSerializable(typeof(UpdateOidcProviderRequest))]
[JsonSerializable(typeof(OidcProviderResponse))]
[JsonSerializable(typeof(OidcProviderTestResponse))]
[JsonSerializable(typeof(IdentityProvidersResponse))]
[JsonSerializable(typeof(IdentityProviderTestResult))]
[JsonSerializable(typeof(IdentityProviderStatus))]
public sealed partial class IdentityAdminJsonContext : JsonSerializerContext
{
}
