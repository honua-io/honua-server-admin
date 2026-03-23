using Bunit;
using Xunit;
using Honua.Admin.Pages;
using Microsoft.JSInterop;

namespace Honua.Admin.Tests;

public class LoginPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;

    public LoginPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void LoginPage_Renders_ServerUrlField()
    {
        var cut = _ctx.Render<LoginPage>();

        Assert.Contains("Server URL", cut.Markup);
    }

    [Fact]
    public void LoginPage_Renders_ApiKeyField()
    {
        var cut = _ctx.Render<LoginPage>();

        Assert.Contains("API Key", cut.Markup);
    }

    [Fact]
    public void LoginPage_Renders_ConnectButton()
    {
        var cut = _ctx.Render<LoginPage>();

        Assert.Contains("Connect", cut.Markup);
    }

    [Fact]
    public void LoginPage_ConnectButton_IsDisabledWhenFieldsAreEmpty()
    {
        var cut = _ctx.Render<LoginPage>();

        // The Connect button should be disabled when server URL and API key are empty
        var buttons = cut.FindAll("button");
        var connectButton = buttons.FirstOrDefault(b => b.TextContent.Contains("Connect") && !b.TextContent.Contains("Test"));

        Assert.NotNull(connectButton);
        Assert.True(connectButton.HasAttribute("disabled"));
    }

    [Fact]
    public void LoginPage_Renders_TestConnectionButton()
    {
        var cut = _ctx.Render<LoginPage>();

        Assert.Contains("Test Connection", cut.Markup);
    }

    [Fact]
    public void LoginPage_Renders_PageTitle()
    {
        var cut = _ctx.Render<LoginPage>();

        Assert.Contains("Connect to Honua Server", cut.Markup);
    }

    [Fact]
    public void LoginPage_Renders_ShowApiKeyCheckbox()
    {
        var cut = _ctx.Render<LoginPage>();

        Assert.Contains("Show API Key", cut.Markup);
    }
}
