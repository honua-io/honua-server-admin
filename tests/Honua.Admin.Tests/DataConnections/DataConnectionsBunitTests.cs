using System;
using System.Threading.Tasks;
using Bunit;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Pages.Operator.DataConnections;
using Honua.Admin.Services.DataConnections;
using Honua.Admin.Services.DataConnections.Providers;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Xunit;

namespace Honua.Admin.Tests.DataConnections;

/// <summary>
/// Component-level "E2E" exercising the create → test → disable round-trip
/// against the in-memory <see cref="StubDataConnectionClient"/>. Per the
/// design brief, this satisfies the "at least one E2E" contract clause
/// without introducing Playwright + a server harness.
/// </summary>
public sealed class DataConnectionsBunitTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly StubDataConnectionClient _client = new();

    public DataConnectionsBunitTests()
    {
        _ctx.Services.AddMudServices();
        _ctx.Services.AddSingleton<IDataConnectionClient>(_client);
        _ctx.Services.AddSingleton<IProviderRegistration, PostgresProviderRegistration>();
        _ctx.Services.AddSingleton<IProviderRegistration, SqlServerStubProviderRegistration>();
        _ctx.Services.AddSingleton<IProviderRegistry, ProviderRegistry>();
        _ctx.Services.AddSingleton<IDataConnectionTelemetry, RecordingTelemetry>();
        _ctx.Services.AddScoped<DataConnectionsState>();

        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task DiagnosticGrid_renders_six_rows_for_a_healthy_outcome()
    {
        var state = _ctx.Services.GetRequiredService<DataConnectionsState>();
        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "demo";
        draft.Host = "db.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        draft.Password = "secret";

        var diagnostic = await state.RunDraftPreflightAsync();
        Assert.NotNull(diagnostic);

        var cut = _ctx.RenderComponent<DiagnosticGrid>(parameters => parameters
            .Add(p => p.Diagnostic, diagnostic!));

        var rows = cut.FindAll("[data-testid^='diagnostic-row-']");
        Assert.Equal(6, rows.Count);
    }

    [Fact]
    public async Task DiagnosticGrid_renders_failed_step_for_routed_failure_message()
    {
        var state = _ctx.Services.GetRequiredService<DataConnectionsState>();
        _client.FailureMessageForHost = host =>
            host == "broken.example.com" ? "authentication failed" : null;

        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "demo";
        draft.Host = "broken.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        draft.Password = "wrong";

        var diagnostic = await state.RunDraftPreflightAsync();
        Assert.NotNull(diagnostic);

        var cut = _ctx.RenderComponent<DiagnosticGrid>(parameters => parameters
            .Add(p => p.Diagnostic, diagnostic!));

        var authRow = cut.Find("[data-testid='diagnostic-row-auth']");
        Assert.Contains("Failed", authRow.TextContent);
    }

    [Fact]
    public async Task End_to_end_create_test_disable_round_trip_against_stub_client()
    {
        var state = _ctx.Services.GetRequiredService<DataConnectionsState>();

        // Create
        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "ops-primary";
        draft.Host = "db.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        draft.Password = "secret-1234";
        draft.CredentialMode = CredentialMode.Managed;

        // Preflight against the draft (read-only — only the test endpoint is called).
        var diagnostic = await state.RunDraftPreflightAsync();
        Assert.NotNull(diagnostic);
        Assert.False(diagnostic!.AnyFailed);

        // Save
        var createResult = await state.SubmitDraftAsync();
        Assert.True(createResult.IsSuccess);
        var newId = createResult.Value!.ConnectionId;

        // List refreshes show the new connection
        await state.RefreshListAsync();
        Assert.Single(state.Connections, c => c.ConnectionId == newId);

        // Detail
        await state.LoadDetailAsync(newId);
        Assert.NotNull(state.SelectedDetail);
        Assert.True(state.SelectedDetail!.IsActive);

        // Disable
        var disableResult = await state.SetActiveAsync(newId, active: false);
        Assert.True(disableResult.IsSuccess);

        await state.RefreshListAsync();
        Assert.Single(state.Connections, c => c.ConnectionId == newId && !c.IsActive);
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }
}
