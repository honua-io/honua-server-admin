using System.Text.Json;
using Honua.Admin.IntegrationTests.Fixtures;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class VersionAndCapabilitiesTests
{
    private readonly HonuaServerFixture _fixture;

    public VersionAndCapabilitiesTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetVersion_ReturnsNonEmptyVersion()
    {
        // Act
        var version = await _fixture.Client.GetVersionAsync();

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version.Version);
        Assert.NotEmpty(version.MetadataApiVersion);
    }

    [Fact]
    public async Task GetCapabilities_ReturnsResourceKinds()
    {
        // Act
        var capabilities = await _fixture.Client.GetCapabilitiesAsync();

        // Assert
        Assert.NotNull(capabilities);
        Assert.NotEmpty(capabilities.ResourceKinds);
    }

    [Fact]
    public async Task CheckCompatibility_ReturnsSupported()
    {
        // Act
        var result = await _fixture.Client.CheckCompatibilityAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSupported, $"Server should be supported. Reason: {result.UnsupportedReason}");
        Assert.NotNull(result.Capabilities);
    }

    [Fact]
    public async Task GetConfig_ReturnsJsonElementObject()
    {
        // Act
        var config = await _fixture.Client.GetConfigAsync();

        // Assert
        Assert.True(
            config.ValueKind == JsonValueKind.Object || config.ValueKind == JsonValueKind.Array,
            $"Expected config to be an Object or Array, got {config.ValueKind}");
    }
}
