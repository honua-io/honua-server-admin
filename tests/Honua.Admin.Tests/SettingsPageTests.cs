using Bunit;
using Xunit;
using Honua.Admin.Pages;
using Honua.Admin.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Honua.Admin.Tests;

public class SettingsPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly AdminAuthStateProvider _auth;

    public SettingsPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _auth = _ctx.Services.GetRequiredService<AdminAuthStateProvider>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void SettingsPage_Renders_Title()
    {
        var cut = _ctx.Render<SettingsPage>();

        Assert.Contains("Settings", cut.Markup);
    }

    [Fact]
    public void SettingsPage_ShowsConnectionSection()
    {
        var cut = _ctx.Render<SettingsPage>();

        Assert.Contains("Connection", cut.Markup);
        Assert.Contains("Server URL", cut.Markup);
        Assert.Contains("API Key", cut.Markup);
    }

    [Fact]
    public void SettingsPage_HasDisconnectButton()
    {
        var cut = _ctx.Render<SettingsPage>();

        Assert.Contains("Disconnect", cut.Markup);
    }

    [Fact]
    public void SettingsPage_HasChangeConnectionButton()
    {
        var cut = _ctx.Render<SettingsPage>();

        Assert.Contains("Change Connection", cut.Markup);
    }

    [Fact]
    public void SettingsPage_ShowsAboutSection()
    {
        var cut = _ctx.Render<SettingsPage>();

        Assert.Contains("About", cut.Markup);
        Assert.Contains("Honua Server Admin", cut.Markup);
        Assert.Contains("SDK Version", cut.Markup);
    }

    [Fact]
    public void SettingsPage_HasShowApiKeyCheckbox()
    {
        var cut = _ctx.Render<SettingsPage>();

        Assert.Contains("Show API Key", cut.Markup);
    }
}
