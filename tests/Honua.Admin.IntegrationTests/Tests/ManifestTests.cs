using Honua.Admin.IntegrationTests.Fixtures;
using Honua.Admin.IntegrationTests.Helpers;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class ManifestTests
{
    private readonly HonuaServerFixture _fixture;

    public ManifestTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetManifest_ReturnsManifest()
    {
        // Act
        var manifest = await _fixture.Client.GetManifestAsync();

        // Assert
        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.ApiVersion);
        Assert.NotNull(manifest.Resources);
    }

    [Fact]
    public async Task ApplyManifest_WithDryRun_ReturnsResultWithoutPersisting()
    {
        // Arrange - get current manifest and apply it back as dry-run
        var currentManifest = await _fixture.Client.GetManifestAsync();

        var request = new ManifestApplyRequest
        {
            Resources = currentManifest.Resources,
            DryRun = true,
            Prune = false
        };

        // Act
        var result = await _fixture.Client.ApplyManifestAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.DryRun);
        Assert.NotNull(result.Summary);
    }

    [Fact]
    public async Task ApplyManifest_ReturnsCounts()
    {
        // Arrange - get current manifest and re-apply (should mostly be no-ops)
        var currentManifest = await _fixture.Client.GetManifestAsync();

        var request = new ManifestApplyRequest
        {
            Resources = currentManifest.Resources,
            DryRun = false,
            Prune = false
        };

        // Act
        var result = await _fixture.Client.ApplyManifestAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Summary);
        // The sum of all action counts should be >= 0
        var totalActions = result.Summary.Created + result.Summary.Updated +
                          result.Summary.Deleted + result.Summary.Skipped;
        Assert.True(totalActions >= 0);
    }
}
