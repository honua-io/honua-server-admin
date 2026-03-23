using Bunit;
using NSubstitute;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class DeployControlPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public DeployControlPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void DeployControlPage_Renders_Title()
    {
        var cut = _ctx.Render<DeployControlPage>();

        Assert.Contains("Deploy Control", cut.Markup);
    }

    [Fact]
    public void DeployControlPage_ShowsTabHeaders()
    {
        var cut = _ctx.Render<DeployControlPage>();

        // MudTabs renders all tab headers but only the active tab content
        Assert.Contains("Preflight", cut.Markup);
        Assert.Contains("Plan", cut.Markup);
        Assert.Contains("Operations", cut.Markup);
        Assert.Contains("Manifest", cut.Markup);
    }

    [Fact]
    public void DeployControlPage_ShowsPreflightContent_ByDefault()
    {
        var cut = _ctx.Render<DeployControlPage>();

        // Preflight is the first (active) tab, so its content should be rendered
        Assert.Contains("Run preflight checks to verify deployment readiness", cut.Markup);
        Assert.Contains("Run Preflight", cut.Markup);
    }

    [Fact]
    public void DeployControlPage_HasRunPreflightButton()
    {
        var cut = _ctx.Render<DeployControlPage>();

        var buttons = cut.FindAll("button");
        Assert.True(buttons.Count > 0);
        Assert.Contains("Run Preflight", cut.Markup);
    }

    [Fact]
    public void DeployControlPage_HasPreflightDescription()
    {
        var cut = _ctx.Render<DeployControlPage>();

        Assert.Contains("Run preflight checks to verify deployment readiness", cut.Markup);
    }
}
