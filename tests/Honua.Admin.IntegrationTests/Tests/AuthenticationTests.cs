using System.Net;
using Honua.Admin.IntegrationTests.Fixtures;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class AuthenticationTests
{
    private readonly HonuaServerFixture _fixture;

    public AuthenticationTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ValidApiKey_Succeeds()
    {
        // The fixture client is configured with a valid API key.
        // If this call succeeds without throwing, authentication worked.
        var version = await _fixture.Client.GetVersionAsync();

        Assert.NotNull(version);
        Assert.NotEmpty(version.Version);
    }

    [Fact]
    public async Task NoApiKey_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _fixture.CreateUnauthenticatedHttpClient();

        // Act
        var response = await client.GetAsync("/api/v1/admin/version");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidApiKey_ReturnsUnauthorized()
    {
        // Arrange
        using var client = _fixture.CreateHttpClientWithApiKey("completely-wrong-key");

        // Act
        var response = await client.GetAsync("/api/v1/admin/version");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
