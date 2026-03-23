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

public class ConnectionDetailPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public ConnectionDetailPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private static SecureConnectionDetail CreateTestConnection() => new()
    {
        ConnectionId = Guid.NewGuid(),
        Name = "TestDB",
        Description = "A test database",
        Host = "db.example.com",
        Port = 5432,
        DatabaseName = "testdb",
        Username = "admin",
        SslMode = "Require",
        SslRequired = true,
        IsActive = true,
        StorageType = "PostgreSQL",
        HealthStatus = "Healthy",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-10),
        CreatedBy = "admin-user",
        CredentialReference = null,
        EncryptionVersion = 1,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public void ConnectionDetailPage_Renders_Title()
    {
        var connection = CreateTestConnection();
        _client.GetConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        var cut = _ctx.Render<ConnectionDetailPage>(parameters =>
            parameters.Add(p => p.ConnectionId, connection.ConnectionId.ToString()));

        cut.WaitForState(() => cut.Markup.Contains("TestDB"));
        Assert.Contains("TestDB", cut.Markup);
    }

    [Fact]
    public void ConnectionDetailPage_ShowsConnectionDetails_AfterLoading()
    {
        var connection = CreateTestConnection();
        _client.GetConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        var cut = _ctx.Render<ConnectionDetailPage>(parameters =>
            parameters.Add(p => p.ConnectionId, connection.ConnectionId.ToString()));

        cut.WaitForState(() => cut.Markup.Contains("Details"));
        Assert.Contains("Details", cut.Markup);
        Assert.Contains("Connection ID", cut.Markup);
        Assert.Contains("Storage Type", cut.Markup);
    }

    [Fact]
    public void ConnectionDetailPage_ShowsTabPanels()
    {
        var connection = CreateTestConnection();
        _client.GetConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(connection));

        var cut = _ctx.Render<ConnectionDetailPage>(parameters =>
            parameters.Add(p => p.ConnectionId, connection.ConnectionId.ToString()));

        cut.WaitForState(() => cut.Markup.Contains("Details"));
        Assert.Contains("Details", cut.Markup);
        Assert.Contains("Discovery", cut.Markup);
        Assert.Contains("Layers", cut.Markup);
        Assert.Contains("Health", cut.Markup);
    }

    [Fact]
    public void ConnectionDetailPage_CallsGetConnectionOnInit()
    {
        var connectionId = Guid.NewGuid().ToString();
        _client.GetConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(CreateTestConnection()));

        var cut = _ctx.Render<ConnectionDetailPage>(parameters =>
            parameters.Add(p => p.ConnectionId, connectionId));

        _client.Received().GetConnectionAsync(connectionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ConnectionDetailPage_ShowsErrorBanner_OnApiFailure()
    {
        _client.GetConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new HonuaAdminApiException(HttpStatusCode.NotFound, "Connection not found"));

        var cut = _ctx.Render<ConnectionDetailPage>(parameters =>
            parameters.Add(p => p.ConnectionId, Guid.NewGuid().ToString()));

        cut.WaitForState(() => cut.Markup.Contains("Failed to load connection"));
        Assert.Contains("Failed to load connection", cut.Markup);
    }
}
