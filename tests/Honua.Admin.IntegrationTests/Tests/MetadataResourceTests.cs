using System.Net;
using System.Text.Json;
using Honua.Admin.IntegrationTests.Fixtures;
using Honua.Admin.IntegrationTests.Helpers;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.IntegrationTests.Tests;

[Collection("HonuaServer")]
public class MetadataResourceTests
{
    private readonly HonuaServerFixture _fixture;

    public MetadataResourceTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateMetadataResource_Succeeds()
    {
        // Arrange
        var resourceName = TestConstants.UniqueName("meta-create");
        var resource = CreateTestResource(resourceName);

        // Act
        var created = await _fixture.Client.CreateMetadataResourceAsync(resource);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(TestConstants.TestMetadataKind, created.Kind);
        Assert.NotNull(created.Metadata);
        Assert.Equal(resourceName, created.Metadata!.Name);
        Assert.Equal(TestConstants.TestMetadataNamespace, created.Metadata.Namespace);

        // Cleanup
        await _fixture.Client.DeleteMetadataResourceAsync(
            TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);
    }

    [Fact]
    public async Task GetMetadataResource_WithETag_ReturnsResource()
    {
        // Arrange
        var resourceName = TestConstants.UniqueName("meta-etag");
        var resource = CreateTestResource(resourceName);
        await _fixture.Client.CreateMetadataResourceAsync(resource);

        try
        {
            // Act
            var (fetched, etag) = await _fixture.Client.GetMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);

            // Assert
            Assert.NotNull(fetched);
            Assert.Equal(resourceName, fetched.Metadata?.Name);
            // ETag may or may not be present depending on server configuration
        }
        finally
        {
            await _fixture.Client.DeleteMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);
        }
    }

    [Fact]
    public async Task UpdateMetadataResource_WithCorrectETag_Succeeds()
    {
        // Arrange
        var resourceName = TestConstants.UniqueName("meta-update");
        var resource = CreateTestResource(resourceName);
        await _fixture.Client.CreateMetadataResourceAsync(resource);

        try
        {
            // Get the resource to obtain the ETag
            var (fetched, etag) = await _fixture.Client.GetMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);

            // Create updated resource with modified spec
            var updatedResource = new MetadataResource
            {
                ApiVersion = TestConstants.TestApiVersion,
                Kind = TestConstants.TestMetadataKind,
                Metadata = new ResourceMetadata
                {
                    Name = resourceName,
                    Namespace = TestConstants.TestMetadataNamespace,
                    Labels = new Dictionary<string, string> { { "updated", "true" } }
                },
                Spec = JsonSerializer.SerializeToElement(new { description = "Updated spec", version = 2 })
            };

            // Act
            var updated = await _fixture.Client.UpdateMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName,
                updatedResource, ifMatch: etag);

            // Assert
            Assert.NotNull(updated);
            Assert.Equal(resourceName, updated.Metadata?.Name);
        }
        finally
        {
            await _fixture.Client.DeleteMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);
        }
    }

    [Fact]
    public async Task UpdateMetadataResource_WithStaleETag_ThrowsConflict()
    {
        // Arrange
        var resourceName = TestConstants.UniqueName("meta-stale");
        var resource = CreateTestResource(resourceName);
        await _fixture.Client.CreateMetadataResourceAsync(resource);

        try
        {
            // Get the resource to obtain the ETag
            var (fetched, etag) = await _fixture.Client.GetMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);

            // Perform a first update to make the ETag stale
            var firstUpdate = new MetadataResource
            {
                ApiVersion = TestConstants.TestApiVersion,
                Kind = TestConstants.TestMetadataKind,
                Metadata = new ResourceMetadata
                {
                    Name = resourceName,
                    Namespace = TestConstants.TestMetadataNamespace
                },
                Spec = JsonSerializer.SerializeToElement(new { description = "First update" })
            };
            await _fixture.Client.UpdateMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName,
                firstUpdate, ifMatch: etag);

            // Now use the original (now stale) ETag for a second update
            var secondUpdate = new MetadataResource
            {
                ApiVersion = TestConstants.TestApiVersion,
                Kind = TestConstants.TestMetadataKind,
                Metadata = new ResourceMetadata
                {
                    Name = resourceName,
                    Namespace = TestConstants.TestMetadataNamespace
                },
                Spec = JsonSerializer.SerializeToElement(new { description = "Conflicting update" })
            };

            // Act & Assert - should throw conflict (409 or 412)
            var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(() =>
                _fixture.Client.UpdateMetadataResourceAsync(
                    TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName,
                    secondUpdate, ifMatch: etag));

            Assert.True(
                ex.StatusCode == HttpStatusCode.Conflict || ex.StatusCode == HttpStatusCode.PreconditionFailed,
                $"Expected 409 or 412, got {(int)ex.StatusCode} {ex.StatusCode}");
        }
        finally
        {
            await _fixture.Client.DeleteMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);
        }
    }

    [Fact]
    public async Task DeleteMetadataResource_Succeeds()
    {
        // Arrange
        var resourceName = TestConstants.UniqueName("meta-delete");
        var resource = CreateTestResource(resourceName);
        await _fixture.Client.CreateMetadataResourceAsync(resource);

        // Act
        await _fixture.Client.DeleteMetadataResourceAsync(
            TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);

        // Assert - getting the deleted resource should throw 404
        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(() =>
            _fixture.Client.GetMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task ListMetadataResources_WithKindFilter_ReturnsFiltered()
    {
        // Arrange - create a resource to ensure there's at least one
        var resourceName = TestConstants.UniqueName("meta-list");
        var resource = CreateTestResource(resourceName);
        await _fixture.Client.CreateMetadataResourceAsync(resource);

        try
        {
            // Act
            var resources = await _fixture.Client.ListMetadataResourcesAsync(kind: TestConstants.TestMetadataKind);

            // Assert
            Assert.NotNull(resources);
            Assert.All(resources, r => Assert.Equal(TestConstants.TestMetadataKind, r.Kind));
            Assert.Contains(resources, r => r.Metadata?.Name == resourceName);
        }
        finally
        {
            await _fixture.Client.DeleteMetadataResourceAsync(
                TestConstants.TestMetadataKind, TestConstants.TestMetadataNamespace, resourceName);
        }
    }

    /// <summary>
    /// Creates a test MetadataResource with a given name.
    /// </summary>
    private static MetadataResource CreateTestResource(string name)
    {
        return new MetadataResource
        {
            ApiVersion = TestConstants.TestApiVersion,
            Kind = TestConstants.TestMetadataKind,
            Metadata = new ResourceMetadata
            {
                Name = name,
                Namespace = TestConstants.TestMetadataNamespace,
                Labels = new Dictionary<string, string>
                {
                    { "test", "true" },
                    { "suite", "integration" }
                }
            },
            Spec = JsonSerializer.SerializeToElement(new
            {
                description = $"Test resource {name}",
                version = 1
            })
        };
    }
}
