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

public class LayerStylePageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public LayerStylePageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static LayerStyleResponse CreateTestStyleResponse() => new()
    {
        MapLibreStyle = JsonSerializer.Deserialize<JsonElement>("{\"version\": 8, \"layers\": []}"),
        DrawingInfo = JsonSerializer.Deserialize<JsonElement>("{\"renderer\": {\"type\": \"simple\"}}")
    };

    [Fact]
    public void LayerStylePage_Renders_Title()
    {
        _client.GetLayerStyleAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestStyleResponse()));

        var cut = _ctx.Render<LayerStylePage>(parameters =>
            parameters.Add(p => p.LayerId, 42));

        Assert.Contains("Layer Style", cut.Markup);
    }

    [Fact]
    public void LayerStylePage_ShowsLayerIdChip()
    {
        _client.GetLayerStyleAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestStyleResponse()));

        var cut = _ctx.Render<LayerStylePage>(parameters =>
            parameters.Add(p => p.LayerId, 42));

        Assert.Contains("Layer 42", cut.Markup);
    }

    [Fact]
    public void LayerStylePage_ShowsStyleTabs_AfterLoading()
    {
        _client.GetLayerStyleAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestStyleResponse()));

        var cut = _ctx.Render<LayerStylePage>(parameters =>
            parameters.Add(p => p.LayerId, 42));

        cut.WaitForState(() => cut.Markup.Contains("MapLibre Style"));
        Assert.Contains("MapLibre Style", cut.Markup);
        Assert.Contains("Drawing Info", cut.Markup);
    }

    [Fact]
    public void LayerStylePage_CallsGetLayerStyleOnInit()
    {
        _client.GetLayerStyleAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestStyleResponse()));

        var cut = _ctx.Render<LayerStylePage>(parameters =>
            parameters.Add(p => p.LayerId, 42));

        _client.Received().GetLayerStyleAsync(42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LayerStylePage_ShowsError_OnApiFailure()
    {
        _client.GetLayerStyleAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new HonuaAdminApiException(HttpStatusCode.NotFound, "Style not found"));

        var cut = _ctx.Render<LayerStylePage>(parameters =>
            parameters.Add(p => p.LayerId, 99));

        cut.WaitForState(() => cut.Markup.Contains("Style not found"));
        Assert.Contains("Style not found", cut.Markup);
    }
}
