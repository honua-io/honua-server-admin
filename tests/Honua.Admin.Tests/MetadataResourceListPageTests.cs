using Bunit;
using NSubstitute;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class MetadataResourceListPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public MetadataResourceListPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void MetadataResourceListPage_Renders_Title()
    {
        _client.ListMetadataResourcesAsync(Arg.Any<string?>(), Arg.Any<string?>(), default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<MetadataResource>>(new List<MetadataResource>()));

        var cut = _ctx.Render<MetadataResourceListPage>();

        Assert.Contains("Metadata Resources", cut.Markup);
    }

    [Fact]
    public void MetadataResourceListPage_Renders_FilterControls()
    {
        _client.ListMetadataResourcesAsync(Arg.Any<string?>(), Arg.Any<string?>(), default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<MetadataResource>>(new List<MetadataResource>()));

        var cut = _ctx.Render<MetadataResourceListPage>();

        Assert.Contains("Kind", cut.Markup);
        Assert.Contains("Namespace", cut.Markup);
        Assert.Contains("Apply Filter", cut.Markup);
    }

    [Fact]
    public void MetadataResourceListPage_ShowsEmptyState_WhenNoResources()
    {
        _client.ListMetadataResourcesAsync(Arg.Any<string?>(), Arg.Any<string?>(), default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<MetadataResource>>(new List<MetadataResource>()));

        var cut = _ctx.Render<MetadataResourceListPage>();

        cut.WaitForState(() => cut.Markup.Contains("No metadata resources found"));
        Assert.Contains("No metadata resources found", cut.Markup);
        Assert.Contains("No resources match the current filter criteria", cut.Markup);
    }

    [Fact]
    public void MetadataResourceListPage_HasCreateResourceButton()
    {
        _client.ListMetadataResourcesAsync(Arg.Any<string?>(), Arg.Any<string?>(), default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<MetadataResource>>(new List<MetadataResource>()));

        var cut = _ctx.Render<MetadataResourceListPage>();

        Assert.Contains("Create Resource", cut.Markup);
    }

    [Fact]
    public void MetadataResourceListPage_CallsListMetadataResourcesOnInit()
    {
        _client.ListMetadataResourcesAsync(Arg.Any<string?>(), Arg.Any<string?>(), default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<MetadataResource>>(new List<MetadataResource>()));

        var cut = _ctx.Render<MetadataResourceListPage>();

        _client.ReceivedWithAnyArgs().ListMetadataResourcesAsync(default, default, default);
    }
}
