using System;
using System.Linq;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Services.DataConnections;
using Honua.Admin.Services.DataConnections.Providers;
using Xunit;

namespace Honua.Admin.Tests.DataConnections;

public sealed class DataConnectionsStateTests
{
    [Fact]
    public async Task RefreshListAsync_loads_connections_and_emits_loaded_event()
    {
        var stub = new StubDataConnectionClient(new[] { Sample("primary", isActive: true) });
        var (state, telemetry) = BuildState(stub);

        await state.RefreshListAsync();

        Assert.Equal(WorkspaceListStatus.Idle, state.Status);
        Assert.Single(state.Connections);
        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.list_loaded");
    }

    [Fact]
    public async Task SubmitDraftAsync_creates_connection_and_emits_create_succeeded()
    {
        var stub = new StubDataConnectionClient();
        var (state, telemetry) = BuildState(stub);

        await state.RefreshListAsync();
        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "ops-primary";
        draft.Host = "db.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        draft.Password = "secret-1234";

        var result = await state.SubmitDraftAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(state.Connections);
        Assert.Null(state.Draft);
        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.create_submitted");
        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.create_succeeded");
    }

    [Fact]
    public async Task SubmitDraftAsync_with_missing_credentials_fails_validation_and_keeps_draft()
    {
        var stub = new StubDataConnectionClient();
        var (state, telemetry) = BuildState(stub);

        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "ops-primary";
        draft.Host = "db.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        // No password, no secret reference.

        var result = await state.SubmitDraftAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(WorkspaceListStatus.Error, state.Status);
        Assert.NotNull(state.Draft);
        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.create_failed");
    }

    [Fact]
    public async Task SetActiveAsync_disable_marks_connection_inactive_and_records_event()
    {
        var seed = Sample("primary", isActive: true);
        var stub = new StubDataConnectionClient(new[] { seed });
        var (state, telemetry) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(seed.ConnectionId);
        var result = await state.SetActiveAsync(seed.ConnectionId, active: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.IsActive);
        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.disabled");
    }

    [Fact]
    public async Task DeleteAsync_removes_from_list_and_records_event()
    {
        var seed = Sample("primary", isActive: true);
        var stub = new StubDataConnectionClient(new[] { seed });
        var (state, telemetry) = BuildState(stub);

        await state.RefreshListAsync();
        var result = await state.DeleteAsync(seed.ConnectionId);

        Assert.True(result.IsSuccess);
        Assert.Empty(state.Connections);
        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.deleted");
    }

    [Fact]
    public async Task RunDraftPreflightAsync_healthy_response_lights_all_cells_ok()
    {
        var stub = new StubDataConnectionClient();
        var (state, _) = BuildState(stub);

        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "draft";
        draft.Host = "db.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        draft.Password = "secret";

        var diagnostic = await state.RunDraftPreflightAsync();

        Assert.NotNull(diagnostic);
        Assert.False(diagnostic!.AnyFailed);
        Assert.All(diagnostic.Cells, c => Assert.Equal(DiagnosticStatus.Ok, c.Status));
    }

    [Fact]
    public async Task RunDraftPreflightAsync_failed_message_routes_to_correct_step_and_records_failed_step()
    {
        var stub = new StubDataConnectionClient
        {
            FailureMessageForHost = host => host == "broken.example.com"
                ? "SSL handshake failed"
                : null
        };
        var (state, telemetry) = BuildState(stub);

        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "draft";
        draft.Host = "broken.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        draft.Password = "secret";

        var diagnostic = await state.RunDraftPreflightAsync();

        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticStatus.Failed, diagnostic!.GetCell(DiagnosticStep.Ssl).Status);

        var completed = telemetry.Events.First(e => e.Name == "data_connections.test_completed");
        Assert.Equal("failed", completed.Properties!["result_kind"]);
        Assert.Equal("Ssl", completed.Properties!["failed_step"]);
    }

    [Fact]
    public void Provider_registry_resolves_postgres_concrete_and_sqlserver_stub()
    {
        var registry = BuildRegistry();

        var postgres = registry.GetById("postgres");
        var sqlServer = registry.GetById("sqlserver");

        Assert.False(postgres.IsStub);
        Assert.True(sqlServer.IsStub);
        Assert.Empty(sqlServer.ManagedHostingChecks);
        Assert.NotEmpty(postgres.ManagedHostingChecks);
        Assert.Equal(2, registry.All.Count);
    }

    [Fact]
    public void BeginCreateDraft_for_stub_provider_emits_provider_stub_viewed_event()
    {
        var stub = new StubDataConnectionClient();
        var (state, telemetry) = BuildState(stub);

        state.BeginCreateDraft("sqlserver");

        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.provider_stub_viewed");
    }

    [Fact]
    public async Task SubmitEditAsync_updates_existing_connection_and_records_event()
    {
        var seed = Sample("primary", isActive: true);
        var stub = new StubDataConnectionClient(new[] { seed });
        var (state, telemetry) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(seed.ConnectionId);

        var draft = state.BeginEditDraft(state.SelectedDetail!);
        draft.Description = "primary OLAP cluster";
        draft.Port = 6432;

        var result = await state.SubmitEditAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(6432, result.Value!.Port);
        Assert.Equal("primary OLAP cluster", result.Value!.Description);
        Assert.Null(state.Draft);
        Assert.Contains(telemetry.Events, e => e.Name == "data_connections.update_succeeded");
    }

    [Fact]
    public void GetCapabilityMatrix_marks_every_check_not_assessed_until_server_endpoint_lands()
    {
        var stub = new StubDataConnectionClient();
        var (state, _) = BuildState(stub);

        var matrix = state.GetCapabilityMatrix("postgres");

        Assert.False(matrix.IsServerSourced);
        Assert.NotEmpty(matrix.Checks);
        Assert.All(matrix.Checks, c => Assert.Equal(DiagnosticStatus.NotAssessed, c.Status));
    }

    private static (DataConnectionsState State, RecordingTelemetry Telemetry) BuildState(IDataConnectionClient client)
    {
        var registry = BuildRegistry();
        var telemetry = new RecordingTelemetry();
        var state = new DataConnectionsState(client, telemetry, registry);
        return (state, telemetry);
    }

    private static IProviderRegistry BuildRegistry()
    {
        return new ProviderRegistry(new IProviderRegistration[]
        {
            new PostgresProviderRegistration(),
            new SqlServerStubProviderRegistration()
        });
    }

    private static DataConnectionDetail Sample(string name, bool isActive) => new()
    {
        ConnectionId = Guid.NewGuid(),
        Name = name,
        Host = "db.example.com",
        Port = 5432,
        DatabaseName = "honua",
        Username = "honua",
        SslMode = "Require",
        StorageType = "managed",
        IsActive = isActive,
        HealthStatus = "unknown",
        CreatedBy = "operator",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };
}
