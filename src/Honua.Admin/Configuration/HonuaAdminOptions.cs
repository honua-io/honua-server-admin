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
    /// Relative or absolute endpoint used for form deployment requests.
    /// </summary>
    public string DeployEndpoint { get; set; } = "/api/admin/forms/deploy";
}
