using Honua.Admin.Auth;

namespace Honua.Admin.Tests;

public sealed class AnonymousAuthenticationStateProviderTests
{
    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAuthenticatedPrincipal()
    {
        var provider = new AnonymousAuthenticationStateProvider();

        var state = await provider.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("LocalDevelopment", state.User.Identity?.AuthenticationType);
    }
}
