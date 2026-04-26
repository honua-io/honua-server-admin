using System;
using System.Threading.Tasks;
using Bunit;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Pages.Operator.DataConnections;
using Honua.Admin.Pages.Operator.DataConnections.Providers;
using Honua.Admin.Services.DataConnections;
using Honua.Admin.Services.DataConnections.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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

        // Production list endpoint filters out disabled connections (server's
        // GetActiveConnectionsAsync uses `WHERE is_active = true`). After a
        // refresh the row should disappear from the workspace list, but…
        await state.RefreshListAsync();
        Assert.DoesNotContain(state.Connections, c => c.ConnectionId == newId);

        // …it remains retrievable by id (GetConnectionAsync does not filter
        // on IsActive), so direct-URL navigation still resolves the detail.
        await state.LoadDetailAsync(newId);
        Assert.NotNull(state.SelectedDetail);
        Assert.False(state.SelectedDetail!.IsActive);
    }

    [Fact]
    public void PostgresConnectionForm_in_edit_mode_locks_fields_outside_the_update_contract()
    {
        // honua-server's UpdateSecureConnectionRequest has no Name,
        // SecretReference, or SecretType slots. The form must render those
        // controls read-only / hidden in edit mode so the operator never
        // believes a Save will persist them.
        var draft = new ConnectionDraft
        {
            ConnectionId = Guid.NewGuid(),
            ProviderId = "postgres",
            Name = "primary",
            Host = "db.example.com",
            DatabaseName = "honua",
            Username = "honua",
            CredentialMode = CredentialMode.External,
            SecretReference = "aws:secretsmanager:prod-db-creds",
            SecretType = "aws"
        };

        var cut = RenderFormWithPopoverHost(draft, isEdit: true);

        // Display name input is read-only / disabled — MudTextField puts
        // data-testid on the wrapping element, so look at the markup
        // around the testid.
        var nameMarkup = cut.Find("[data-testid='conn-name']").OuterHtml;
        Assert.Contains("readonly", nameMarkup, StringComparison.OrdinalIgnoreCase);

        // Credential-mode helper appears only in edit mode.
        cut.Find("[data-testid='conn-mode-edit-help']");

        // External-mode secret reference is rendered read-only; the secret
        // type input must not be present (rotating the type is impossible
        // through the current update contract).
        var secretRefMarkup = cut.Find("[data-testid='conn-secret-ref']").OuterHtml;
        Assert.Contains("readonly", secretRefMarkup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(cut.FindAll("[data-testid='conn-secret-type']"));
    }

    [Fact]
    public void PostgresConnectionForm_in_create_mode_keeps_credential_mode_editable()
    {
        // Sanity check the IsEdit gate cuts only the edit path: a fresh
        // create-mode draft starting in External mode must keep the
        // secret-type input live (operator can rotate before save).
        var draft = new ConnectionDraft
        {
            ProviderId = "postgres",
            CredentialMode = CredentialMode.External
        };

        var cut = RenderFormWithPopoverHost(draft, isEdit: false);

        Assert.Empty(cut.FindAll("[data-testid='conn-mode-edit-help']"));
        cut.Find("[data-testid='conn-secret-type']");
    }

    private IRenderedFragment RenderFormWithPopoverHost(ConnectionDraft draft, bool isEdit)
    {
        // MudSelect (used for SSL mode) requires a MudPopoverProvider in
        // the render tree. Wrap the form in a host that supplies one.
        return _ctx.Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<PostgresConnectionForm>(1);
            builder.AddAttribute(2, nameof(PostgresConnectionForm.Draft), draft);
            builder.AddAttribute(3, nameof(PostgresConnectionForm.IsEdit), isEdit);
            builder.CloseComponent();
        });
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }
}
