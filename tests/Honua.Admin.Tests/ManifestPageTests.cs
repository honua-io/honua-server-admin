using Bunit;
using NSubstitute;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class ManifestPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public ManifestPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ManifestPage_Renders_Title()
    {
        var cut = _ctx.Render<ManifestPage>();

        Assert.Contains("Manifests", cut.Markup);
    }

    [Fact]
    public void ManifestPage_HasExportButton()
    {
        var cut = _ctx.Render<ManifestPage>();

        Assert.Contains("Export Manifest", cut.Markup);
    }

    [Fact]
    public void ManifestPage_ShowsExportSection()
    {
        var cut = _ctx.Render<ManifestPage>();

        Assert.Contains("Export Manifest", cut.Markup);
        Assert.Contains("Namespace", cut.Markup);
    }

    [Fact]
    public void ManifestPage_ShowsApplySection()
    {
        var cut = _ctx.Render<ManifestPage>();

        Assert.Contains("Apply Manifest", cut.Markup);
        Assert.Contains("Dry Run", cut.Markup);
        Assert.Contains("Prune", cut.Markup);
    }

    [Fact]
    public void ManifestPage_HasApplyButton()
    {
        var cut = _ctx.Render<ManifestPage>();

        // The Apply Manifest button is present
        var buttons = cut.FindAll("button");
        Assert.True(buttons.Count > 0);
        Assert.Contains("Apply Manifest", cut.Markup);
    }
}
