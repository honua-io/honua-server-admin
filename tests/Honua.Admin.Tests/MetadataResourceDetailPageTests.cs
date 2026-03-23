using System.Net;
using System.Text.Json;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class MetadataResourceDetailPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public MetadataResourceDetailPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static MetadataResource CreateTestResource() => new()
    {
        ApiVersion = "honua.io/v1alpha1",
        Kind = "Layer",
        Metadata = new ResourceMetadata
        {
            Id = "abc-123",
            Name = "parcels",
            Namespace = "default",
            ResourceVersion = "1",
            Generation = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            UpdatedAt = DateTimeOffset.UtcNow,
            Labels = new Dictionary<string, string> { { "env", "prod" } }
        },
        Spec = JsonSerializer.Deserialize<JsonElement>("{\"schema\": \"public\", \"table\": \"parcels\"}")
    };

    [Fact]
    public void MetadataResourceDetailPage_Renders_ResourcePath()
    {
        var resource = CreateTestResource();
        _client.GetMetadataResourceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(MetadataResource, string?)>((resource, "etag-1")));

        var cut = _ctx.Render<MetadataResourceDetailPage>(parameters => parameters
            .Add(p => p.Kind, "Layer")
            .Add(p => p.Ns, "default")
            .Add(p => p.Name, "parcels"));

        cut.WaitForState(() => cut.Markup.Contains("Layer / default / parcels"));
        Assert.Contains("Layer / default / parcels", cut.Markup);
    }

    [Fact]
    public void MetadataResourceDetailPage_ShowsResourceMetadata()
    {
        var resource = CreateTestResource();
        _client.GetMetadataResourceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(MetadataResource, string?)>((resource, "etag-1")));

        var cut = _ctx.Render<MetadataResourceDetailPage>(parameters => parameters
            .Add(p => p.Kind, "Layer")
            .Add(p => p.Ns, "default")
            .Add(p => p.Name, "parcels"));

        cut.WaitForState(() => cut.Markup.Contains("Resource Metadata"));
        Assert.Contains("Resource Metadata", cut.Markup);
        Assert.Contains("abc-123", cut.Markup);
        Assert.Contains("Resource Version", cut.Markup);
    }

    [Fact]
    public void MetadataResourceDetailPage_ShowsLabelsSection()
    {
        var resource = CreateTestResource();
        _client.GetMetadataResourceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(MetadataResource, string?)>((resource, "etag-1")));

        var cut = _ctx.Render<MetadataResourceDetailPage>(parameters => parameters
            .Add(p => p.Kind, "Layer")
            .Add(p => p.Ns, "default")
            .Add(p => p.Name, "parcels"));

        cut.WaitForState(() => cut.Markup.Contains("Labels"));
        Assert.Contains("Labels", cut.Markup);
        Assert.Contains("Add Label", cut.Markup);
    }

    [Fact]
    public void MetadataResourceDetailPage_HasDeleteButton()
    {
        var resource = CreateTestResource();
        _client.GetMetadataResourceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(MetadataResource, string?)>((resource, "etag-1")));

        var cut = _ctx.Render<MetadataResourceDetailPage>(parameters => parameters
            .Add(p => p.Kind, "Layer")
            .Add(p => p.Ns, "default")
            .Add(p => p.Name, "parcels"));

        cut.WaitForState(() => cut.Markup.Contains("Delete"));
        Assert.Contains("Delete", cut.Markup);
    }

    [Fact]
    public void MetadataResourceDetailPage_CallsGetMetadataResourceOnInit()
    {
        var resource = CreateTestResource();
        _client.GetMetadataResourceAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(MetadataResource, string?)>((resource, "etag-1")));

        var cut = _ctx.Render<MetadataResourceDetailPage>(parameters => parameters
            .Add(p => p.Kind, "Layer")
            .Add(p => p.Ns, "default")
            .Add(p => p.Name, "parcels"));

        _client.Received().GetMetadataResourceAsync("Layer", "default", "parcels", Arg.Any<CancellationToken>());
    }
}
