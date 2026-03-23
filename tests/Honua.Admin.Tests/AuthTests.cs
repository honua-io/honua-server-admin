using NSubstitute;
using Xunit;
using Honua.Admin.Auth;
using Microsoft.JSInterop;

namespace Honua.Admin.Tests;

public class AuthTests
{
    private static IJSRuntime CreateMockJsRuntime()
    {
        var jsRuntime = Substitute.For<IJSRuntime>();
        // Setup default returns for localStorage.getItem calls
        jsRuntime.InvokeAsync<string?>(Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(new ValueTask<string?>((string?)null));
        return jsRuntime;
    }

    [Fact]
    public void AdminAuthStateProvider_StartsUnauthenticated()
    {
        var jsRuntime = CreateMockJsRuntime();
        var auth = new AdminAuthStateProvider(jsRuntime);

        Assert.False(auth.IsAuthenticated);
        Assert.Equal(string.Empty, auth.ServerUrl);
        Assert.Equal(string.Empty, auth.ApiKey);
    }

    [Fact]
    public async Task AdminAuthStateProvider_AfterLogin_IsAuthenticated()
    {
        var jsRuntime = CreateMockJsRuntime();
        var auth = new AdminAuthStateProvider(jsRuntime);

        await auth.LoginAsync("https://server.example.com", "my-api-key");

        Assert.True(auth.IsAuthenticated);
        Assert.Equal("https://server.example.com", auth.ServerUrl);
        Assert.Equal("my-api-key", auth.ApiKey);
    }

    [Fact]
    public async Task AdminAuthStateProvider_AfterLogin_TrimsTrailingSlash()
    {
        var jsRuntime = CreateMockJsRuntime();
        var auth = new AdminAuthStateProvider(jsRuntime);

        await auth.LoginAsync("https://server.example.com/", "my-api-key");

        Assert.Equal("https://server.example.com", auth.ServerUrl);
    }

    [Fact]
    public async Task AdminAuthStateProvider_AfterLogout_IsNotAuthenticated()
    {
        var jsRuntime = CreateMockJsRuntime();
        var auth = new AdminAuthStateProvider(jsRuntime);

        await auth.LoginAsync("https://server.example.com", "my-api-key");
        Assert.True(auth.IsAuthenticated);

        await auth.LogoutAsync();

        Assert.False(auth.IsAuthenticated);
        Assert.Equal(string.Empty, auth.ServerUrl);
        Assert.Equal(string.Empty, auth.ApiKey);
    }

    [Fact]
    public async Task AdminAuthStateProvider_LoginAsync_PersistsToLocalStorage()
    {
        var jsRuntime = CreateMockJsRuntime();
        var auth = new AdminAuthStateProvider(jsRuntime);

        await auth.LoginAsync("https://server.example.com", "my-api-key");

        // Verify localStorage.setItem was called for server URL
        await jsRuntime.Received().InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "localStorage.setItem",
            Arg.Is<object[]>(args =>
                args.Length == 2 &&
                (string)args[0] == "honua_admin_server_url" &&
                (string)args[1] == "https://server.example.com"));

        // Verify localStorage.setItem was called for API key
        await jsRuntime.Received().InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "localStorage.setItem",
            Arg.Is<object[]>(args =>
                args.Length == 2 &&
                (string)args[0] == "honua_admin_api_key" &&
                (string)args[1] == "my-api-key"));
    }

    [Fact]
    public async Task AdminAuthStateProvider_LogoutAsync_RemovesFromLocalStorage()
    {
        var jsRuntime = CreateMockJsRuntime();
        var auth = new AdminAuthStateProvider(jsRuntime);

        await auth.LoginAsync("https://server.example.com", "my-api-key");
        await auth.LogoutAsync();

        // Verify localStorage.removeItem was called for server URL
        await jsRuntime.Received().InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "localStorage.removeItem",
            Arg.Is<object[]>(args =>
                args.Length == 1 &&
                (string)args[0] == "honua_admin_server_url"));

        // Verify localStorage.removeItem was called for API key
        await jsRuntime.Received().InvokeAsync<Microsoft.JSInterop.Infrastructure.IJSVoidResult>(
            "localStorage.removeItem",
            Arg.Is<object[]>(args =>
                args.Length == 1 &&
                (string)args[0] == "honua_admin_api_key"));
    }

    [Fact]
    public async Task AdminAuthStateProvider_InitializeAsync_LoadsFromLocalStorage()
    {
        var jsRuntime = Substitute.For<IJSRuntime>();

        // Return stored values for localStorage.getItem
        jsRuntime.InvokeAsync<string?>("localStorage.getItem",
            Arg.Is<object[]>(args => args.Length > 0 && (string)args[0] == "honua_admin_server_url"))
            .Returns(new ValueTask<string?>("https://stored-server.com"));

        jsRuntime.InvokeAsync<string?>("localStorage.getItem",
            Arg.Is<object[]>(args => args.Length > 0 && (string)args[0] == "honua_admin_api_key"))
            .Returns(new ValueTask<string?>("stored-key"));

        var auth = new AdminAuthStateProvider(jsRuntime);

        Assert.False(auth.IsAuthenticated);

        await auth.InitializeAsync();

        Assert.True(auth.IsAuthenticated);
        Assert.Equal("https://stored-server.com", auth.ServerUrl);
        Assert.Equal("stored-key", auth.ApiKey);
    }
}
