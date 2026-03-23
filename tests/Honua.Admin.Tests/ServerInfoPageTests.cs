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

public class ServerInfoPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public ServerInfoPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ServerInfoPage_Renders_Title()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ServerInfoPage>();

        Assert.Contains("Server Information", cut.Markup);
    }

    [Fact]
    public void ServerInfoPage_ShowsVersionInfo_AfterLoading()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ServerInfoPage>();

        cut.WaitForState(() => cut.Markup.Contains("2.1.0"));
        Assert.Contains("2.1.0", cut.Markup);
        Assert.Contains("v1alpha1", cut.Markup);
    }

    [Fact]
    public void ServerInfoPage_ShowsCompatibilityInfo_AfterLoading()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ServerInfoPage>();

        cut.WaitForState(() => cut.Markup.Contains("compatible"));
        Assert.Contains("compatible", cut.Markup);
    }

    [Fact]
    public void ServerInfoPage_ShowsErrorBanner_OnApiFailure()
    {
        _client.GetVersionAsync(default)
            .ThrowsAsyncForAnyArgs(new HonuaAdminApiException(HttpStatusCode.InternalServerError, "Server error"));
        _client.GetCapabilitiesAsync(default)
            .ThrowsAsyncForAnyArgs(new HonuaAdminApiException(HttpStatusCode.InternalServerError, "Server error"));
        _client.CheckCompatibilityAsync(default)
            .ThrowsAsyncForAnyArgs(new HonuaAdminApiException(HttpStatusCode.InternalServerError, "Server error"));
        _client.GetConfigAsync(default)
            .ThrowsAsyncForAnyArgs(new HonuaAdminApiException(HttpStatusCode.InternalServerError, "Server error"));

        var cut = _ctx.Render<ServerInfoPage>();

        cut.WaitForState(() => cut.Markup.Contains("Failed to load"));
        Assert.Contains("Failed to load", cut.Markup);
    }

    [Fact]
    public void ServerInfoPage_ShowsCapabilitiesSection()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ServerInfoPage>();

        cut.WaitForState(() => cut.Markup.Contains("Capabilities"));
        Assert.Contains("Capabilities", cut.Markup);
    }

    [Fact]
    public void ServerInfoPage_ShowsConfigurationSection()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ServerInfoPage>();

        cut.WaitForState(() => cut.Markup.Contains("Configuration"));
        Assert.Contains("Configuration", cut.Markup);
    }

    private void SetupSuccessfulMocks()
    {
        _client.GetVersionAsync(default)
            .ReturnsForAnyArgs(Task.FromResult(new AdminVersionResponse
            {
                Version = "2.1.0",
                MetadataApiVersion = "v1alpha1",
                ServerTime = DateTimeOffset.UtcNow
            }));

        _client.CheckCompatibilityAsync(default)
            .ReturnsForAnyArgs(Task.FromResult(new ServerCompatibilityResult
            {
                IsSupported = true,
                Capabilities = new AdminCapabilitiesResponse
                {
                    MetadataApiVersions = new[] { "v1alpha1" },
                    ResourceKinds = new[] { "Layer", "Service" },
                    ManifestSupported = true,
                    ManifestDryRunSupported = true,
                    ManifestPruneSupported = false,
                    Compatibility = new AdminCompatibilityInfo
                    {
                        ServerVersion = "2.1.0",
                        ReleaseChannel = "stable",
                        Features = new AdminFeatureCompatibility
                        {
                            MetadataResources = true,
                            ManifestExport = true,
                            ManifestApply = true,
                            ManifestDryRun = true,
                            ManifestPrune = false
                        }
                    }
                }
            }));

        _client.GetCapabilitiesAsync(default)
            .ReturnsForAnyArgs(Task.FromResult(new AdminCapabilitiesResponse
            {
                MetadataApiVersions = new[] { "v1alpha1" },
                ResourceKinds = new[] { "Layer", "Service" },
                ManifestSupported = true,
                ManifestDryRunSupported = true,
                ManifestPruneSupported = false
            }));

        _client.GetConfigAsync(default)
            .ReturnsForAnyArgs(Task.FromResult(JsonSerializer.Deserialize<JsonElement>("{\"server\": {\"port\": 5001}}")));
    }
}
