using Bunit;
using NSubstitute;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class ServiceListPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public ServiceListPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ServiceListPage_Renders_Title()
    {
        _client.ListServicesAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<ServiceSummary>>(new List<ServiceSummary>()));

        var cut = _ctx.Render<ServiceListPage>();

        Assert.Contains("Services", cut.Markup);
    }

    [Fact]
    public void ServiceListPage_ShowsEmptyState_WhenNoServices()
    {
        _client.ListServicesAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<ServiceSummary>>(new List<ServiceSummary>()));

        var cut = _ctx.Render<ServiceListPage>();

        cut.WaitForState(() => cut.Markup.Contains("No services found"));
        Assert.Contains("No services found", cut.Markup);
        Assert.Contains("No services have been registered yet", cut.Markup);
    }

    [Fact]
    public void ServiceListPage_CallsListServicesOnInit()
    {
        _client.ListServicesAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<ServiceSummary>>(new List<ServiceSummary>()));

        var cut = _ctx.Render<ServiceListPage>();

        _client.ReceivedWithAnyArgs().ListServicesAsync(default);
    }

    [Fact]
    public void ServiceListPage_HasRefreshButton()
    {
        _client.ListServicesAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<ServiceSummary>>(new List<ServiceSummary>()));

        var cut = _ctx.Render<ServiceListPage>();

        // The refresh icon button should be present
        var buttons = cut.FindAll("button");
        Assert.True(buttons.Count > 0);
    }
}
