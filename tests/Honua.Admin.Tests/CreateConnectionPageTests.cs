using Bunit;
using NSubstitute;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class CreateConnectionPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public CreateConnectionPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void CreateConnectionPage_Renders_Title()
    {
        var cut = _ctx.Render<CreateConnectionPage>();

        Assert.Contains("New Connection", cut.Markup);
    }

    [Fact]
    public void CreateConnectionPage_Renders_FormFields()
    {
        var cut = _ctx.Render<CreateConnectionPage>();

        Assert.Contains("Name", cut.Markup);
        Assert.Contains("Host", cut.Markup);
        Assert.Contains("Port", cut.Markup);
        Assert.Contains("Database Name", cut.Markup);
        Assert.Contains("Username", cut.Markup);
    }

    [Fact]
    public void CreateConnectionPage_HasCancelButton()
    {
        var cut = _ctx.Render<CreateConnectionPage>();

        Assert.Contains("Cancel", cut.Markup);
    }

    [Fact]
    public void CreateConnectionPage_HasSaveButton()
    {
        var cut = _ctx.Render<CreateConnectionPage>();

        Assert.Contains("Save", cut.Markup);
    }

    [Fact]
    public void CreateConnectionPage_HasTestDraftButton()
    {
        var cut = _ctx.Render<CreateConnectionPage>();

        Assert.Contains("Test Draft", cut.Markup);
    }

    [Fact]
    public void CreateConnectionPage_ShowsCredentialModeToggle()
    {
        var cut = _ctx.Render<CreateConnectionPage>();

        Assert.Contains("Managed Password", cut.Markup);
        Assert.Contains("External Secret Reference", cut.Markup);
    }

    [Fact]
    public void CreateConnectionPage_ShowsSslSection()
    {
        var cut = _ctx.Render<CreateConnectionPage>();

        Assert.Contains("SSL", cut.Markup);
        Assert.Contains("Require SSL", cut.Markup);
    }
}
