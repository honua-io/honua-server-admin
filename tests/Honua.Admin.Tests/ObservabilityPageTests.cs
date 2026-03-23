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

public class ObservabilityPageTests : IAsyncLifetime
{
    private readonly BunitContext _ctx;
    private readonly IHonuaAdminClient _client;

    public ObservabilityPageTests()
    {
        _ctx = new BunitContext();
        TestHelpers.ConfigureTestServices(_ctx);
        _client = TestHelpers.GetMockClient(_ctx);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _ctx.DisposeAsync();

    private void SetupSuccessfulMocks()
    {
        _client.GetRecentErrorsAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<RecentError>>(new List<RecentError>()));

        _client.GetTelemetryStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TelemetryStatus
            {
                Enabled = true,
                Provider = "OpenTelemetry",
                Endpoint = "http://otel-collector:4317",
                MetricsEnabled = true,
                TracesEnabled = true,
                LogsEnabled = false,
                LastExportAt = DateTimeOffset.UtcNow
            }));

        _client.GetMigrationStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new MigrationStatus
            {
                CurrentVersion = "2.1.0",
                TargetVersion = null,
                PendingMigrations = new List<string>(),
                AppliedMigrations = new List<AppliedMigration>
                {
                    new() { Version = "2.0.0", Name = "InitialSchema", AppliedAt = DateTimeOffset.UtcNow.AddDays(-30) },
                    new() { Version = "2.1.0", Name = "AddIndexes", AppliedAt = DateTimeOffset.UtcNow.AddDays(-10) }
                },
                IsUpToDate = true
            }));

        _client.ListConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<SecureConnectionSummary>>(new List<SecureConnectionSummary>()));
    }

    [Fact]
    public void ObservabilityPage_Renders_Title()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ObservabilityPage>();

        Assert.Contains("Observability", cut.Markup);
    }

    [Fact]
    public void ObservabilityPage_ShowsTabHeaders()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ObservabilityPage>();

        // MudTabs renders all tab headers
        Assert.Contains("Errors", cut.Markup);
        Assert.Contains("Telemetry", cut.Markup);
        Assert.Contains("Migrations", cut.Markup);
        Assert.Contains("Connections", cut.Markup);
        Assert.Contains("Encryption", cut.Markup);
    }

    [Fact]
    public void ObservabilityPage_CallsLoadDataOnInit()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ObservabilityPage>();

        _client.Received().GetRecentErrorsAsync(Arg.Any<int?>(), Arg.Any<CancellationToken>());
        _client.Received().GetTelemetryStatusAsync(Arg.Any<CancellationToken>());
        _client.Received().GetMigrationStatusAsync(Arg.Any<CancellationToken>());
        _client.Received().ListConnectionsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ObservabilityPage_ShowsEmptyErrorState_WhenNoErrors()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ObservabilityPage>();

        // Errors is the first (active) tab, so its content is rendered
        cut.WaitForState(() => cut.Markup.Contains("No recent errors"));
        Assert.Contains("No recent errors", cut.Markup);
    }

    [Fact]
    public void ObservabilityPage_ShowsRefreshButton_OnErrorsTab()
    {
        SetupSuccessfulMocks();

        var cut = _ctx.Render<ObservabilityPage>();

        // The Errors tab (active) should have a Refresh button
        cut.WaitForState(() => cut.Markup.Contains("Refresh"));
        Assert.Contains("Refresh", cut.Markup);
    }
}
