using Honua.Admin.IntegrationTests.Fixtures;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class ObservabilityTests
{
    private readonly HonuaServerFixture _fixture;

    public ObservabilityTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetRecentErrors_ReturnsWithoutError()
    {
        // Act
        var errors = await _fixture.Client.GetRecentErrorsAsync();

        // Assert
        Assert.NotNull(errors);
        // Errors list may be empty on a fresh server, but should not throw
    }

    [Fact]
    public async Task GetTelemetryStatus_ReturnsStatus()
    {
        // Act
        var status = await _fixture.Client.GetTelemetryStatusAsync();

        // Assert
        Assert.NotNull(status);
        // Provider should have a value (could be "none", "otlp", etc.)
        Assert.NotNull(status.Provider);
    }

    [Fact]
    public async Task GetMigrationStatus_ReturnsStatus()
    {
        // Act
        var status = await _fixture.Client.GetMigrationStatusAsync();

        // Assert
        Assert.NotNull(status);
        Assert.NotEmpty(status.CurrentVersion);
    }
}
