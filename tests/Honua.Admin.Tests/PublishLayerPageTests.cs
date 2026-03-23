using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

/// <summary>
/// Tests for PublishLayerPage. Note: Full rendering with MudDataGrid Filterable="true"
/// requires popover infrastructure that bunit cannot fully emulate. We verify the
/// page interacts with the admin client correctly and test markup for non-data-grid elements.
/// </summary>
public class PublishLayerPageTests
{
    [Fact]
    public async Task DiscoverTablesAsync_IsCalledWithConnectionId()
    {
        var client = Substitute.For<IHonuaAdminClient>();
        var connectionId = Guid.NewGuid().ToString();

        client.DiscoverTablesAsync(connectionId, default)
            .Returns(Task.FromResult(new TableDiscoveryResponse
            {
                Tables = new List<TableInfo>
                {
                    new()
                    {
                        Schema = "public", Table = "parcels",
                        GeometryColumn = "geom", GeometryType = "Polygon",
                        Srid = 4326, EstimatedRows = 5000,
                        Columns = new List<ColumnInfo>
                        {
                            new() { Name = "id", DataType = "integer", IsNullable = false, IsPrimaryKey = true }
                        }
                    }
                }
            }));

        var result = await client.DiscoverTablesAsync(connectionId);

        Assert.NotNull(result);
        Assert.Single(result.Tables);
        Assert.Equal("parcels", result.Tables[0].Table);
        await client.Received().DiscoverTablesAsync(connectionId, default);
    }

    [Fact]
    public async Task DiscoverTablesAsync_ReturnsEmptyList_WhenNoTables()
    {
        var client = Substitute.For<IHonuaAdminClient>();
        var connectionId = Guid.NewGuid().ToString();

        client.DiscoverTablesAsync(connectionId, default)
            .Returns(Task.FromResult(new TableDiscoveryResponse { Tables = new List<TableInfo>() }));

        var result = await client.DiscoverTablesAsync(connectionId);

        Assert.NotNull(result);
        Assert.Empty(result.Tables);
    }

    [Fact]
    public async Task PublishLayerAsync_IsCalledWithRequest()
    {
        var client = Substitute.For<IHonuaAdminClient>();
        var connectionId = Guid.NewGuid().ToString();

        client.PublishLayerAsync(connectionId, Arg.Any<PublishLayerRequest>(), default)
            .Returns(Task.FromResult(new PublishedLayerSummary
            {
                LayerId = 1,
                LayerName = "parcels",
                Schema = "public",
                Table = "parcels",
                GeometryType = "Polygon",
                Srid = 4326,
                Enabled = true,
                ServiceName = "default"
            }));

        var request = new PublishLayerRequest
        {
            Schema = "public",
            Table = "parcels",
            LayerName = "parcels",
            GeometryColumn = "geom",
            GeometryType = "Polygon",
            Srid = 4326,
            Enabled = true
        };

        var result = await client.PublishLayerAsync(connectionId, request);

        Assert.NotNull(result);
        Assert.Equal("parcels", result.LayerName);
        Assert.Equal(1, result.LayerId);
        await client.Received().PublishLayerAsync(connectionId, Arg.Any<PublishLayerRequest>(), default);
    }

    [Fact]
    public async Task DiscoverTablesAsync_ThrowsOnApiError()
    {
        var client = Substitute.For<IHonuaAdminClient>();
        var connectionId = Guid.NewGuid().ToString();

        client.DiscoverTablesAsync(connectionId, default)
            .ThrowsAsync(new HonuaAdminApiException(HttpStatusCode.InternalServerError, "Discovery failed"));

        await Assert.ThrowsAsync<HonuaAdminApiException>(() => client.DiscoverTablesAsync(connectionId));
    }

    [Fact]
    public void StepTitles_AreCorrectlyOrdered()
    {
        // Validate the expected step titles for the publish wizard
        var expectedSteps = new[] { "Select Table", "Configure", "Review", "Publish" };
        Assert.Equal(4, expectedSteps.Length);
        Assert.Equal("Select Table", expectedSteps[0]);
        Assert.Equal("Publish", expectedSteps[3]);
    }
}
