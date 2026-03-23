using Honua.Admin.IntegrationTests.Fixtures;
using Honua.Admin.IntegrationTests.Helpers;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class ConnectionCrudTests
{
    private readonly HonuaServerFixture _fixture;

    public ConnectionCrudTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ListConnections_ReturnsListWithoutError()
    {
        // Act
        var connections = await _fixture.Client.ListConnectionsAsync();

        // Assert
        Assert.NotNull(connections);
        // The list may be empty initially or contain bootstrap connections
    }

    [Fact]
    public async Task TestDraftConnection_WithValidPostGis_Succeeds()
    {
        // Arrange
        var request = CreatePostGisConnectionRequest("draft-test");

        // Act
        var result = await _fixture.Client.TestDraftConnectionAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsHealthy, $"Draft connection test should be healthy. Message: {result.Message}");
    }

    [Fact]
    public async Task CreateConnection_ThenGet_ThenUpdate_ThenDelete_FullLifecycle()
    {
        // Arrange
        var uniqueName = TestConstants.UniqueName("lifecycle");
        var createRequest = CreatePostGisConnectionRequest(uniqueName);

        // Act - Create
        var created = await _fixture.Client.CreateConnectionAsync(createRequest);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.ConnectionId);
        Assert.Equal(uniqueName, created.Name);

        var connectionId = created.ConnectionId.ToString();

        try
        {
            // Act - Get
            var detail = await _fixture.Client.GetConnectionAsync(connectionId);
            Assert.NotNull(detail);
            Assert.Equal(created.ConnectionId, detail.ConnectionId);
            Assert.Equal(uniqueName, detail.Name);

            // Act - Update
            var updateRequest = new UpdateSecureConnectionRequest
            {
                Description = "Updated description for lifecycle test"
            };
            var updated = await _fixture.Client.UpdateConnectionAsync(connectionId, updateRequest);
            Assert.NotNull(updated);
            Assert.Equal(created.ConnectionId, updated.ConnectionId);

            // Verify the update took effect
            var detailAfterUpdate = await _fixture.Client.GetConnectionAsync(connectionId);
            Assert.Equal("Updated description for lifecycle test", detailAfterUpdate.Description);
        }
        finally
        {
            // Act - Delete (cleanup)
            await _fixture.Client.DeleteConnectionAsync(connectionId);
        }

        // Verify deletion: listing should no longer contain the deleted connection
        var remaining = await _fixture.Client.ListConnectionsAsync();
        Assert.DoesNotContain(remaining, c => c.ConnectionId == created.ConnectionId);
    }

    [Fact]
    public async Task TestConnection_OnExistingConnection_ReturnsResult()
    {
        // Arrange - create a connection first
        var uniqueName = TestConstants.UniqueName("test-conn");
        var createRequest = CreatePostGisConnectionRequest(uniqueName);
        var created = await _fixture.Client.CreateConnectionAsync(createRequest);
        var connectionId = created.ConnectionId.ToString();

        try
        {
            // Act
            var result = await _fixture.Client.TestConnectionAsync(connectionId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsHealthy, $"Existing connection test should be healthy. Message: {result.Message}");
        }
        finally
        {
            await _fixture.Client.DeleteConnectionAsync(connectionId);
        }
    }

    [Fact]
    public async Task ValidateEncryption_ReturnsResult()
    {
        // Act
        var result = await _fixture.Client.ValidateEncryptionAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsValid, $"Encryption should be valid. Message: {result.Message}");
    }

    [Fact]
    public async Task DiscoverTables_FindsSeededTables()
    {
        // Arrange - create a connection first
        var uniqueName = TestConstants.UniqueName("discover");
        var createRequest = CreatePostGisConnectionRequest(uniqueName);
        var created = await _fixture.Client.CreateConnectionAsync(createRequest);
        var connectionId = created.ConnectionId.ToString();

        try
        {
            // Act
            var discovery = await _fixture.Client.DiscoverTablesAsync(connectionId);

            // Assert
            Assert.NotNull(discovery);
            Assert.NotEmpty(discovery.Tables);

            var tableNames = discovery.Tables.Select(t => t.Table).ToList();
            Assert.Contains(TestConstants.ParksTable, tableNames);
            Assert.Contains(TestConstants.RoadsTable, tableNames);
        }
        finally
        {
            await _fixture.Client.DeleteConnectionAsync(connectionId);
        }
    }

    [Fact]
    public async Task ListConnections_AfterCreation_ContainsNewConnection()
    {
        // Arrange
        var uniqueName = TestConstants.UniqueName("list-check");
        var createRequest = CreatePostGisConnectionRequest(uniqueName);
        var created = await _fixture.Client.CreateConnectionAsync(createRequest);
        var connectionId = created.ConnectionId.ToString();

        try
        {
            // Act
            var connections = await _fixture.Client.ListConnectionsAsync();

            // Assert
            Assert.Contains(connections, c => c.ConnectionId == created.ConnectionId);
        }
        finally
        {
            await _fixture.Client.DeleteConnectionAsync(connectionId);
        }
    }

    [Fact]
    public async Task CreateConnection_ThenDelete_RoundTrip()
    {
        // Arrange
        var uniqueName = TestConstants.UniqueName("roundtrip");
        var createRequest = CreatePostGisConnectionRequest(uniqueName);

        // Act
        var created = await _fixture.Client.CreateConnectionAsync(createRequest);
        Assert.NotNull(created);

        await _fixture.Client.DeleteConnectionAsync(created.ConnectionId.ToString());

        // Assert - verify it's gone
        var connections = await _fixture.Client.ListConnectionsAsync();
        Assert.DoesNotContain(connections, c => c.ConnectionId == created.ConnectionId);
    }

    /// <summary>
    /// Helper to create a connection request pointing at the fixture's PostGIS instance
    /// using the internal Docker network address.
    /// </summary>
    private CreateSecureConnectionRequest CreatePostGisConnectionRequest(string name)
    {
        return new CreateSecureConnectionRequest
        {
            Name = name,
            Description = $"Integration test connection: {name}",
            Host = _fixture.PostGisInternalHost,
            Port = _fixture.PostGisInternalPort,
            DatabaseName = TestConstants.TestDatabase,
            Username = TestConstants.TestDbUser,
            Password = TestConstants.TestDbPassword,
            SslRequired = false,
            SslMode = "Disable"
        };
    }
}
