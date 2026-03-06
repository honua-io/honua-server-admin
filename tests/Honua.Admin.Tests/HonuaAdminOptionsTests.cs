using Honua.Admin.Configuration;

namespace Honua.Admin.Tests;

public sealed class HonuaAdminOptionsTests
{
    [Fact]
    public void ResolveApiBaseAddress_UsesConfiguredAbsoluteApiBaseUrl()
    {
        var options = new HonuaAdminOptions
        {
            ApiBaseUrl = "https://api.example.test/"
        };

        var resolved = options.ResolveApiBaseAddress(new Uri("https://app.example.test/"));

        Assert.Equal(new Uri("https://api.example.test/"), resolved);
    }

    [Fact]
    public void ResolveApiBaseAddress_UsesHostBaseAddress_WhenApiBaseUrlMissing()
    {
        var options = new HonuaAdminOptions();
        var hostBaseAddress = new Uri("https://app.example.test/");

        var resolved = options.ResolveApiBaseAddress(hostBaseAddress);

        Assert.Equal(hostBaseAddress, resolved);
    }

    [Fact]
    public void ResolveApiBaseAddress_SupportsRelativeApiBaseUrl()
    {
        var options = new HonuaAdminOptions
        {
            ApiBaseUrl = "/api/"
        };

        var resolved = options.ResolveApiBaseAddress(new Uri("https://app.example.test/admin/"));

        Assert.Equal(new Uri("https://app.example.test/api/"), resolved);
    }
}
