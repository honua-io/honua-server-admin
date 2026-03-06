namespace Honua.Admin.Configuration;

/// <summary>
/// Configuration options for the admin client.
/// </summary>
public sealed class HonuaAdminOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "HonuaServer";

    /// <summary>
    /// Optional API base URL. Supports absolute or host-relative values.
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// Relative or absolute endpoint used for form deployment requests.
    /// </summary>
    public string DeployEndpoint { get; set; } = "/api/admin/forms/deploy";

    /// <summary>
    /// Resolves the effective API base address using configuration or host fallback.
    /// </summary>
    /// <param name="hostBaseAddress">The browser host base address.</param>
    /// <returns>The API base address used by HTTP clients.</returns>
    public Uri ResolveApiBaseAddress(Uri hostBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(hostBaseAddress);

        if (!string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            if (Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var absoluteBaseAddress)
                && (absoluteBaseAddress.Scheme == Uri.UriSchemeHttp || absoluteBaseAddress.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteBaseAddress;
            }

            if (Uri.TryCreate(hostBaseAddress, ApiBaseUrl, out var relativeBaseAddress))
            {
                return relativeBaseAddress;
            }
        }

        return hostBaseAddress;
    }
}
