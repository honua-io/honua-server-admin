namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Single entitlement row mirroring honua-server <c>EntitlementResponse</c>.
/// </summary>
public sealed class EntitlementDto
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}
