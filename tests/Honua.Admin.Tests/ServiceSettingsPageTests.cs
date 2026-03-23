using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class ServiceSettingsPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public ServiceSettingsPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static ServiceSettingsResponse CreateTestSettings() => new()
    {
        ServiceName = "default",
        EnabledProtocols = new[] { "FeatureServer", "MapServer" },
        AvailableProtocols = new[] { "FeatureServer", "MapServer", "OgcFeatures" },
        MapServer = new MapServerSettingsResponse
        {
            MaxImageWidth = 4096,
            MaxImageHeight = 4096,
            DefaultImageWidth = 800,
            DefaultImageHeight = 600,
            DefaultDpi = 96,
            DefaultFormat = "png",
            DefaultTransparent = true,
            MaxFeaturesPerLayer = 1000
        },
        AccessPolicy = new AccessPolicyResponse
        {
            AllowAnonymous = true,
            AllowAnonymousWrite = false,
            AllowedRoles = new[] { "admin" },
            AllowedWriteRoles = new[] { "admin" }
        },
        TimeInfo = new TimeInfoResponse
        {
            StartTimeField = "start_date",
            EndTimeField = "end_date",
            TrackIdField = "track_id"
        }
    };

    [Fact]
    public void ServiceSettingsPage_Renders_ServiceName()
    {
        _client.GetServiceSettingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestSettings()));

        var cut = _ctx.Render<ServiceSettingsPage>(parameters =>
            parameters.Add(p => p.ServiceName, "default"));

        cut.WaitForState(() => cut.Markup.Contains("default"));
        Assert.Contains("default", cut.Markup);
    }

    [Fact]
    public void ServiceSettingsPage_ShowsTabPanels()
    {
        _client.GetServiceSettingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestSettings()));

        var cut = _ctx.Render<ServiceSettingsPage>(parameters =>
            parameters.Add(p => p.ServiceName, "default"));

        cut.WaitForState(() => cut.Markup.Contains("Protocols"));
        Assert.Contains("Protocols", cut.Markup);
        Assert.Contains("MapServer", cut.Markup);
        Assert.Contains("Access Policy", cut.Markup);
        Assert.Contains("Time Info", cut.Markup);
        Assert.Contains("Layer Metadata", cut.Markup);
    }

    [Fact]
    public void ServiceSettingsPage_CallsGetServiceSettingsOnInit()
    {
        _client.GetServiceSettingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestSettings()));

        var cut = _ctx.Render<ServiceSettingsPage>(parameters =>
            parameters.Add(p => p.ServiceName, "default"));

        _client.Received().GetServiceSettingsAsync("default", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ServiceSettingsPage_ShowsError_OnApiFailure()
    {
        _client.GetServiceSettingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new HonuaAdminApiException(HttpStatusCode.InternalServerError, "Settings load failed"));

        var cut = _ctx.Render<ServiceSettingsPage>(parameters =>
            parameters.Add(p => p.ServiceName, "default"));

        cut.WaitForState(() => cut.Markup.Contains("500"));
        Assert.Contains("Settings load failed", cut.Markup);
    }
}
