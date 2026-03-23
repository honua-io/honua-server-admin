using Bunit;
using NSubstitute;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class CreateMetadataResourcePageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public CreateMetadataResourcePageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void CreateMetadataResourcePage_Renders_Title()
    {
        var cut = _ctx.Render<CreateMetadataResourcePage>();

        Assert.Contains("Create Metadata Resource", cut.Markup);
    }

    [Fact]
    public void CreateMetadataResourcePage_Renders_FormFields()
    {
        var cut = _ctx.Render<CreateMetadataResourcePage>();

        Assert.Contains("API Version", cut.Markup);
        Assert.Contains("Kind", cut.Markup);
        Assert.Contains("Namespace", cut.Markup);
        Assert.Contains("Name", cut.Markup);
    }

    [Fact]
    public void CreateMetadataResourcePage_HasCancelButton()
    {
        var cut = _ctx.Render<CreateMetadataResourcePage>();

        Assert.Contains("Cancel", cut.Markup);
    }

    [Fact]
    public void CreateMetadataResourcePage_HasCreateButton()
    {
        var cut = _ctx.Render<CreateMetadataResourcePage>();

        Assert.Contains("Create Resource", cut.Markup);
    }

    [Fact]
    public void CreateMetadataResourcePage_ShowsLabelsSection()
    {
        var cut = _ctx.Render<CreateMetadataResourcePage>();

        Assert.Contains("Labels", cut.Markup);
        Assert.Contains("Add Label", cut.Markup);
    }

    [Fact]
    public void CreateMetadataResourcePage_ShowsSpecJsonSection()
    {
        var cut = _ctx.Render<CreateMetadataResourcePage>();

        Assert.Contains("Spec (JSON)", cut.Markup);
    }
}
