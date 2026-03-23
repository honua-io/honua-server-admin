using Honua.Admin.IntegrationTests.Fixtures;
using Honua.Admin.IntegrationTests.Helpers;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class ServiceSettingsTests
{
    private readonly HonuaServerFixture _fixture;

    public ServiceSettingsTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListServices_ReturnsServices()
    {
        // Act
        var services = await _fixture.Client.ListServicesAsync();

        // Assert
        Assert.NotNull(services);
        // Server should have at least a default service
        Assert.NotEmpty(services);
    }

    [Fact]
    public async Task GetServiceSettings_ReturnsSettings()
    {
        // Arrange - find a service name from the list
        var services = await _fixture.Client.ListServicesAsync();
        Assert.NotEmpty(services);
        var serviceName = services[0].ServiceName;

        // Act
        var settings = await _fixture.Client.GetServiceSettingsAsync(serviceName);

        // Assert
        Assert.NotNull(settings);
        Assert.Equal(serviceName, settings.ServiceName);
        Assert.NotNull(settings.EnabledProtocols);
        Assert.NotNull(settings.AvailableProtocols);
    }

    [Fact]
    public async Task UpdateProtocols_ChangesEnabledProtocols()
    {
        // Arrange
        var services = await _fixture.Client.ListServicesAsync();
        Assert.NotEmpty(services);
        var serviceName = services[0].ServiceName;

        // Get current settings to determine available protocols
        var currentSettings = await _fixture.Client.GetServiceSettingsAsync(serviceName);
        var availableProtocols = currentSettings.AvailableProtocols;
        Assert.NotEmpty(availableProtocols);

        // Pick a subset of available protocols
        var protocols = new List<string> { availableProtocols[0] };

        // Act
        var updated = await _fixture.Client.UpdateProtocolsAsync(serviceName, protocols);

        // Assert
        Assert.NotNull(updated);
        Assert.Contains(protocols[0], updated.EnabledProtocols);
    }

    [Fact]
    public async Task UpdateMapServerSettings_UpdatesSettings()
    {
        // Arrange
        var services = await _fixture.Client.ListServicesAsync();
        Assert.NotEmpty(services);
        var serviceName = services[0].ServiceName;

        var request = new UpdateMapServerSettingsRequest
        {
            MaxImageWidth = 4096,
            MaxImageHeight = 4096,
            DefaultDpi = 96
        };

        // Act
        var updated = await _fixture.Client.UpdateMapServerSettingsAsync(serviceName, request);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(4096, updated.MapServer.MaxImageWidth);
        Assert.Equal(4096, updated.MapServer.MaxImageHeight);
        Assert.Equal(96, updated.MapServer.DefaultDpi);
    }

    [Fact]
    public async Task UpdateAccessPolicy_UpdatesPolicy()
    {
        // Arrange
        var services = await _fixture.Client.ListServicesAsync();
        Assert.NotEmpty(services);
        var serviceName = services[0].ServiceName;

        var request = new UpdateAccessPolicyRequest
        {
            AllowAnonymous = true,
            AllowAnonymousWrite = false
        };

        // Act
        var updated = await _fixture.Client.UpdateAccessPolicyAsync(serviceName, request);

        // Assert
        Assert.NotNull(updated);
        Assert.NotNull(updated.AccessPolicy);
        Assert.True(updated.AccessPolicy.AllowAnonymous);
        Assert.False(updated.AccessPolicy.AllowAnonymousWrite);
    }
}
