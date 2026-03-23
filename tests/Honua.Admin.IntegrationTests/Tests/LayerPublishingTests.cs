using Honua.Admin.IntegrationTests.Fixtures;
using Honua.Admin.IntegrationTests.Helpers;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class LayerPublishingTests : IAsyncLifetime
{
    private readonly HonuaServerFixture _fixture;
    private string _connectionId = string.Empty;

    public LayerPublishingTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Create a connection for layer publishing tests
        var uniqueName = TestConstants.UniqueName("layer-pub");
        var request = new CreateSecureConnectionRequest
        {
            Name = uniqueName,
            Description = "Connection for layer publishing tests",
            Host = _fixture.PostGisInternalHost,
            Port = _fixture.PostGisInternalPort,
            DatabaseName = TestConstants.TestDatabase,
            Username = TestConstants.TestDbUser,
            Password = TestConstants.TestDbPassword,
            SslRequired = false,
            SslMode = "Disable"
        };

        var created = await _fixture.Client.CreateConnectionAsync(request);
        _connectionId = created.ConnectionId.ToString();
    }

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(_connectionId))
        {
            await _fixture.Client.DeleteConnectionAsync(_connectionId);
        }
    }

    [Fact]
    public async Task DiscoverTables_FindsSeededParksAndRoads()
    {
        // Act
        var discovery = await _fixture.Client.DiscoverTablesAsync(_connectionId);

        // Assert
        Assert.NotNull(discovery);
        Assert.NotEmpty(discovery.Tables);

        var parksTable = discovery.Tables.FirstOrDefault(t => t.Table == TestConstants.ParksTable);
        Assert.NotNull(parksTable);
        Assert.Equal(TestConstants.PublicSchema, parksTable.Schema);

        var roadsTable = discovery.Tables.FirstOrDefault(t => t.Table == TestConstants.RoadsTable);
        Assert.NotNull(roadsTable);
    }

    [Fact]
    public async Task PublishLayer_CreatesLayer()
    {
        // Arrange
        var layerName = TestConstants.UniqueName("parks-layer");
        var request = new PublishLayerRequest
        {
            Schema = TestConstants.PublicSchema,
            Table = TestConstants.ParksTable,
            LayerName = layerName,
            Description = "Published parks layer for integration test",
            GeometryColumn = "geom",
            GeometryType = "Point",
            Srid = TestConstants.TestSrid,
            Enabled = true
        };

        // Act
        var published = await _fixture.Client.PublishLayerAsync(_connectionId, request);

        // Assert
        Assert.NotNull(published);
        Assert.Equal(layerName, published.LayerName);
        Assert.True(published.Enabled);
        Assert.True(published.LayerId > 0);
    }

    [Fact]
    public async Task ListLayers_ContainsPublishedLayer()
    {
        // Arrange - publish a layer first
        var layerName = TestConstants.UniqueName("list-layer");
        var request = new PublishLayerRequest
        {
            Schema = TestConstants.PublicSchema,
            Table = TestConstants.ParksTable,
            LayerName = layerName,
            GeometryColumn = "geom",
            GeometryType = "Point",
            Srid = TestConstants.TestSrid,
            Enabled = true
        };
        var published = await _fixture.Client.PublishLayerAsync(_connectionId, request);

        // Act
        var layers = await _fixture.Client.ListLayersAsync(_connectionId);

        // Assert
        Assert.NotNull(layers);
        Assert.Contains(layers, l => l.LayerId == published.LayerId);
    }

    [Fact]
    public async Task SetLayerEnabled_TogglesEnableDisable()
    {
        // Arrange - publish a layer first
        var layerName = TestConstants.UniqueName("toggle-layer");
        var request = new PublishLayerRequest
        {
            Schema = TestConstants.PublicSchema,
            Table = TestConstants.RoadsTable,
            LayerName = layerName,
            GeometryColumn = "geom",
            GeometryType = "LineString",
            Srid = TestConstants.TestSrid,
            Enabled = true
        };
        var published = await _fixture.Client.PublishLayerAsync(_connectionId, request);
        Assert.True(published.Enabled);

        // Act - disable
        var disabled = await _fixture.Client.SetLayerEnabledAsync(
            _connectionId, published.LayerId, enabled: false);
        Assert.False(disabled.Enabled);

        // Act - re-enable
        var reEnabled = await _fixture.Client.SetLayerEnabledAsync(
            _connectionId, published.LayerId, enabled: true);
        Assert.True(reEnabled.Enabled);
    }

    [Fact]
    public async Task PublishLayer_RoadsTable_Succeeds()
    {
        // Arrange
        var layerName = TestConstants.UniqueName("roads-layer");
        var request = new PublishLayerRequest
        {
            Schema = TestConstants.PublicSchema,
            Table = TestConstants.RoadsTable,
            LayerName = layerName,
            Description = "Published roads layer for integration test",
            GeometryColumn = "geom",
            GeometryType = "LineString",
            Srid = TestConstants.TestSrid,
            Enabled = true
        };

        // Act
        var published = await _fixture.Client.PublishLayerAsync(_connectionId, request);

        // Assert
        Assert.NotNull(published);
        Assert.Equal(layerName, published.LayerName);
        Assert.True(published.LayerId > 0);
    }
}
