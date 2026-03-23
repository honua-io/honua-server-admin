using Bunit;
using NSubstitute;
using Xunit;
using Honua.Admin.Pages;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Tests;

public class ConnectionListPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public ConnectionListPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    [Fact]
    public void ConnectionListPage_Renders_Title()
    {
        _client.ListConnectionsAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(new List<SecureConnectionSummary>()));

        var cut = _ctx.Render<ConnectionListPage>();

        Assert.Contains("Connections", cut.Markup);
    }

    [Fact]
    public void ConnectionListPage_ShowsEmptyState_WhenNoConnections()
    {
        _client.ListConnectionsAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(new List<SecureConnectionSummary>()));

        var cut = _ctx.Render<ConnectionListPage>();

        cut.WaitForState(() => cut.Markup.Contains("No connections"));
        Assert.Contains("No connections", cut.Markup);
        Assert.Contains("Add a database connection to get started", cut.Markup);
    }

    [Fact]
    public void ConnectionListPage_HasAddConnectionButton()
    {
        _client.ListConnectionsAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(new List<SecureConnectionSummary>()));

        var cut = _ctx.Render<ConnectionListPage>();

        Assert.Contains("Add Connection", cut.Markup);
    }

    [Fact]
    public void ConnectionListPage_HasValidateEncryptionButton()
    {
        _client.ListConnectionsAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(new List<SecureConnectionSummary>()));

        var cut = _ctx.Render<ConnectionListPage>();

        Assert.Contains("Validate Encryption", cut.Markup);
    }

    [Fact]
    public void ConnectionListPage_HasRotateKeyButton()
    {
        _client.ListConnectionsAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(new List<SecureConnectionSummary>()));

        var cut = _ctx.Render<ConnectionListPage>();

        Assert.Contains("Rotate Key", cut.Markup);
    }

    [Fact]
    public void ConnectionListPage_CallsListConnectionsOnInit()
    {
        _client.ListConnectionsAsync(default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(new List<SecureConnectionSummary>()));

        var cut = _ctx.Render<ConnectionListPage>();

        _client.ReceivedWithAnyArgs().ListConnectionsAsync(default);
    }
}
