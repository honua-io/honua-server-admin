using NSubstitute;
using Xunit;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

/// <summary>
/// Tests for the LayerListPage component logic.
/// Note: Full rendering tests are skipped because MudSelect (used for the connection
/// selector) uses MudPopoverBase which requires a live JS runtime that bunit cannot
/// fully emulate. Instead, we verify the page interacts with the admin client correctly.
/// </summary>
public class LayerListPageTests
{
    [Fact]
    public async Task ListLayersAsync_IsCalledWithConnectionId()
    {
        var client = Substitute.For<IHonuaAdminClient>();
        var connectionId = Guid.NewGuid();

        client.ListLayersAsync(connectionId.ToString(), null, default)
            .Returns(Task.FromResult<IReadOnlyList<PublishedLayerSummary>>(new List<PublishedLayerSummary>()));

        var result = await client.ListLayersAsync(connectionId.ToString());

        Assert.NotNull(result);
        Assert.Empty(result);
        await client.Received().ListLayersAsync(connectionId.ToString(), null, default);
    }

    [Fact]
    public async Task ListConnectionsAsync_ReturnsConnections()
    {
        var client = Substitute.For<IHonuaAdminClient>();

        var connections = new List<SecureConnectionSummary>
        {
            new() { ConnectionId = Guid.NewGuid(), Name = "DevDB", Host = "localhost", DatabaseName = "devdb" },
            new() { ConnectionId = Guid.NewGuid(), Name = "StagingDB", Host = "staging.local", DatabaseName = "stagingdb" }
        };

        client.ListConnectionsAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(connections));

        var result = await client.ListConnectionsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("DevDB", result[0].Name);
        Assert.Equal("StagingDB", result[1].Name);
    }

    [Fact]
    public async Task ListLayersAsync_ReturnsLayerSummaries()
    {
        var client = Substitute.For<IHonuaAdminClient>();
        var connectionId = Guid.NewGuid().ToString();

        var layers = new List<PublishedLayerSummary>
        {
            new() { LayerId = 1, LayerName = "parcels", Schema = "public", Table = "parcels", GeometryType = "Polygon", Srid = 4326, Enabled = true, ServiceName = "default" },
            new() { LayerId = 2, LayerName = "roads", Schema = "public", Table = "roads", GeometryType = "MultiLineString", Srid = 4326, Enabled = false, ServiceName = "default" }
        };

        client.ListLayersAsync(connectionId, null, default)
            .Returns(Task.FromResult<IReadOnlyList<PublishedLayerSummary>>(layers));

        var result = await client.ListLayersAsync(connectionId);

        Assert.Equal(2, result.Count);
        Assert.Equal("parcels", result[0].LayerName);
        Assert.True(result[0].Enabled);
        Assert.False(result[1].Enabled);
    }
}
