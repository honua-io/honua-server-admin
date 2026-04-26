using System;
using System.Collections.Generic;
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
    public void BeginCreateDraft_throws_for_unknown_provider_so_callers_must_TryGet_first()
    {
        // Pin the contract Create.razor relies on: BeginCreateDraft is the
        // strict path and throws for unknown ids. The page must consult the
        // registry's TryGet first or it will crash before the
        // missing-provider alert can render.
        var stub = new StubDataConnectionClient();
        var (state, _) = BuildState(stub);

        var registry = state.ProviderRegistry;
        Assert.False(registry.TryGet("bogus", out _));

        Assert.Throws<InvalidOperationException>(() => state.BeginCreateDraft("bogus"));
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
    public async Task LoadDetailAsync_failure_for_new_id_clears_prior_selection()
    {
        // Bug-fix invariant: when the route id changes, a failed Detail load
        // must not leave SelectedDetail pointing at the previous connection.
        var first = Sample("primary", isActive: true);
        var stub = new ThrowOnGetStubClient(seed: first);
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(first.ConnectionId);
        Assert.NotNull(state.SelectedDetail);
        Assert.Equal(first.ConnectionId, state.SelectedDetail!.ConnectionId);

        var unknownId = Guid.NewGuid();
        stub.FailNextGet = true;
        await state.LoadDetailAsync(unknownId);

        Assert.Null(state.SelectedDetail);
        Assert.Equal(WorkspaceListStatus.Error, state.Status);
        Assert.NotNull(state.LastError);
    }

    [Fact]
    public async Task LoadDetailAsync_failure_for_same_id_preserves_selection_for_retry()
    {
        // Same-id reloads (a transient blip while staying on the same Detail
        // page) should keep the previously-loaded value so the page does not
        // blank between retries.
        var seed = Sample("primary", isActive: true);
        var stub = new ThrowOnGetStubClient(seed: seed);
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(seed.ConnectionId);
        Assert.NotNull(state.SelectedDetail);

        stub.FailNextGet = true;
        await state.LoadDetailAsync(seed.ConnectionId);

        Assert.NotNull(state.SelectedDetail);
        Assert.Equal(seed.ConnectionId, state.SelectedDetail!.ConnectionId);
        Assert.Equal(WorkspaceListStatus.Error, state.Status);
    }

    [Fact]
    public async Task RunExistingPreflightAsync_success_reconciles_health_in_selected_detail_and_list()
    {
        // The server's test endpoint does not persist HealthStatus to the
        // row, so the state store must derive the refreshed health locally
        // from the test outcome — otherwise the Detail page and list view
        // keep showing stale "unknown" until the operator re-creates the
        // connection.
        var seed = Sample("primary", isActive: true);
        var stub = new StubDataConnectionClient(new[] { seed });
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(seed.ConnectionId);
        Assert.Equal("unknown", state.SelectedDetail!.HealthStatus);

        var diagnostic = await state.RunExistingPreflightAsync(seed.ConnectionId);

        Assert.NotNull(diagnostic);
        Assert.False(diagnostic!.AnyFailed);
        Assert.Equal("Healthy", state.SelectedDetail!.HealthStatus);
        Assert.NotNull(state.SelectedDetail.LastHealthCheck);

        var listEntry = state.Connections.Single(c => c.ConnectionId == seed.ConnectionId);
        Assert.Equal("Healthy", listEntry.HealthStatus);
        Assert.NotNull(listEntry.LastHealthCheck);
    }

    [Fact]
    public async Task RunExistingPreflightAsync_failed_outcome_marks_unhealthy_locally()
    {
        var seed = Sample("primary", isActive: true);
        var stub = new StubDataConnectionClient(new[] { seed })
        {
            FailureMessageForHost = host => host == seed.Host ? "DNS lookup failed" : null
        };
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(seed.ConnectionId);

        var diagnostic = await state.RunExistingPreflightAsync(seed.ConnectionId);

        Assert.NotNull(diagnostic);
        Assert.True(diagnostic!.AnyFailed);
        Assert.Equal("Unhealthy", state.SelectedDetail!.HealthStatus);
        Assert.NotNull(state.SelectedDetail.LastHealthCheck);
    }

    [Fact]
    public async Task ListAsync_filters_disabled_connections_to_match_server_contract()
    {
        // PostgresSecureConnectionRegistry.GetActiveConnectionsAsync's SQL is
        // `WHERE is_active = true`, so disabled rows never appear in the
        // workspace list. Stub must mirror that for parity.
        var active = Sample("primary", isActive: true);
        var disabled = Sample("legacy", isActive: false);
        var stub = new StubDataConnectionClient(new[] { active, disabled });
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();

        Assert.Single(state.Connections);
        Assert.Equal(active.ConnectionId, state.Connections[0].ConnectionId);
    }

    [Fact]
    public async Task ClearDraft_when_navigating_to_another_connection_drops_stale_edit_buffer()
    {
        // Sibling invariant for Detail.razor's route-change reset: editing
        // connection A and then navigating to connection B must clear the
        // draft so SubmitEdit cannot fire B's id with A's wire body. The
        // page-level reset (ResetEditState) calls ClearDraft only when
        // Draft.ConnectionId differs from the new route id; pin the
        // state-store contract that supports it.
        var first = Sample("first", isActive: true);
        var second = Sample("second", isActive: true);
        var stub = new StubDataConnectionClient(new[] { first, second });
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(first.ConnectionId);

        var draft = state.BeginEditDraft(state.SelectedDetail!);
        Assert.Equal(first.ConnectionId, draft.ConnectionId);

        // Simulate the page-level route-change reset condition.
        if (state.Draft is { } current && current.ConnectionId != second.ConnectionId)
        {
            state.ClearDraft();
        }

        Assert.Null(state.Draft);
    }

    [Fact]
    public async Task SubmitEditAsync_does_not_send_Name_or_SecretReference_to_match_server_contract()
    {
        // honua-server's UpdateSecureConnectionRequest has no Name,
        // SecretReference, or SecretType fields. The edit form is gated on
        // IsEdit so the operator never sees those fields, but pin the
        // SubmitEditAsync wire shape too so a future form change cannot
        // silently start sending values the server will drop.
        var seed = Sample("primary", isActive: true);
        var stub = new RecordingUpdateStubClient(seed);
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(seed.ConnectionId);

        var draft = state.BeginEditDraft(state.SelectedDetail!);
        // Operator-edited values that the form locks down — even if a future
        // form regression lets them through, SubmitEditAsync must not forward
        // them to the server.
        draft.Name = "renamed-but-ignored";
        draft.SecretReference = "vault:bogus";
        draft.SecretType = "vault";
        draft.Description = "primary OLAP";

        await state.SubmitEditAsync();

        Assert.NotNull(stub.LastUpdateRequest);
        Assert.Equal("primary OLAP", stub.LastUpdateRequest!.Description);
        // The wire shape has no Name / SecretReference / SecretType slots,
        // so the values cannot be forwarded — assert the request type itself
        // does not expose them.
        Assert.DoesNotContain(typeof(UpdateConnectionRequest).GetProperties(), p => p.Name == "Name");
        Assert.DoesNotContain(typeof(UpdateConnectionRequest).GetProperties(), p => p.Name == "SecretReference");
        Assert.DoesNotContain(typeof(UpdateConnectionRequest).GetProperties(), p => p.Name == "SecretType");
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

    private sealed class RecordingUpdateStubClient : IDataConnectionClient
    {
        private readonly StubDataConnectionClient _inner;

        public RecordingUpdateStubClient(DataConnectionDetail seed)
        {
            _inner = new StubDataConnectionClient(new[] { seed });
        }

        public UpdateConnectionRequest? LastUpdateRequest { get; private set; }

        public Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(System.Threading.CancellationToken cancellationToken = default) =>
            _inner.ListAsync(cancellationToken);

        public Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.GetAsync(id, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> CreateAsync(CreateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.CreateAsync(request, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> UpdateAsync(Guid id, UpdateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;
            return _inner.UpdateAsync(id, request, cancellationToken);
        }

        public Task<ConnectionResult<DataConnectionSummary>> DisableAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.DisableAsync(id, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> EnableAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.EnableAsync(id, cancellationToken);

        public Task<ConnectionResult<bool>> DeleteAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(id, cancellationToken);

        public Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.TestDraftAsync(request, cancellationToken);

        public Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.TestExistingAsync(id, cancellationToken);
    }

    private sealed class ThrowOnGetStubClient : IDataConnectionClient
    {
        private readonly StubDataConnectionClient _inner;

        public ThrowOnGetStubClient(DataConnectionDetail seed)
        {
            _inner = new StubDataConnectionClient(new[] { seed });
        }

        public bool FailNextGet { get; set; }

        public Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(System.Threading.CancellationToken cancellationToken = default) =>
            _inner.ListAsync(cancellationToken);

        public Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, System.Threading.CancellationToken cancellationToken = default)
        {
            if (FailNextGet)
            {
                FailNextGet = false;
                return Task.FromResult(ConnectionResult<DataConnectionDetail>.Fail(
                    new ConnectionOperationError(ConnectionErrorKind.Network, "error.network")));
            }
            return _inner.GetAsync(id, cancellationToken);
        }

        public Task<ConnectionResult<DataConnectionSummary>> CreateAsync(CreateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.CreateAsync(request, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> UpdateAsync(Guid id, UpdateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.UpdateAsync(id, request, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> DisableAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.DisableAsync(id, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> EnableAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.EnableAsync(id, cancellationToken);

        public Task<ConnectionResult<bool>> DeleteAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(id, cancellationToken);

        public Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.TestDraftAsync(request, cancellationToken);

        public Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.TestExistingAsync(id, cancellationToken);
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

    [Fact]
    public async Task Stale_LoadDetail_response_does_not_overwrite_newer_LoadDetail()
    {
        // Codex finding: LoadDetailAsync awaits GetAsync(id) and unconditionally
        // assigns SelectedDetail = result.Value. If connection A's slow GET
        // completes after connection B's fast GET, SelectedDetail rewinds to
        // A while the route is B. Pin the staleness guard.
        var first = Sample("first", isActive: true);
        var second = Sample("second", isActive: true);
        var stub = new GatedDataConnectionClient(new[] { first, second });
        var (state, _) = BuildState(stub);

        // Hold the slow load for `first` so the second's faster load can
        // complete first.
        stub.HoldGet(first.ConnectionId);
        var slow = state.LoadDetailAsync(first.ConnectionId);

        var fast = state.LoadDetailAsync(second.ConnectionId);
        await fast;
        Assert.Equal(second.ConnectionId, state.SelectedDetail!.ConnectionId);

        // Releasing the slow GET must NOT rewind SelectedDetail to first.
        stub.ReleaseGet(first.ConnectionId);
        await slow;

        Assert.Equal(second.ConnectionId, state.SelectedDetail!.ConnectionId);
    }

    [Fact]
    public async Task Stale_RunExistingPreflight_response_does_not_overwrite_newer_preflight()
    {
        // Sibling invariant for RunExistingPreflightAsync: a late TestExisting
        // for a prior connection cannot replace the current diagnostics grid.
        var first = Sample("first", isActive: true) with { Host = "broken.example.com" };
        var second = Sample("second", isActive: true) with { Host = "ok.example.com" };
        var stub = new GatedDataConnectionClient(new[] { first, second })
        {
            FailureMessageForHost = host => host == first.Host ? "DNS lookup failed" : null
        };
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(first.ConnectionId);

        // Slow preflight for first (would be Unhealthy if it landed).
        stub.HoldTestExisting(first.ConnectionId);
        var slow = state.RunExistingPreflightAsync(first.ConnectionId);

        // The user navigates to second and triggers a fresh preflight.
        await state.LoadDetailAsync(second.ConnectionId);
        var fastDiagnostic = await state.RunExistingPreflightAsync(second.ConnectionId);

        Assert.NotNull(fastDiagnostic);
        Assert.False(fastDiagnostic!.AnyFailed);
        Assert.Same(fastDiagnostic, state.LatestDiagnostic);

        // Releasing the slow preflight must drop its result silently.
        stub.ReleaseTestExisting(first.ConnectionId);
        var slowDiagnostic = await slow;

        Assert.Null(slowDiagnostic);
        Assert.Same(fastDiagnostic, state.LatestDiagnostic);
        // The list reconciliation for `first` must not have stomped second's
        // health either — second's row stays Healthy.
        var secondRow = state.Connections.Single(c => c.ConnectionId == second.ConnectionId);
        Assert.Equal("Healthy", secondRow.HealthStatus);
    }

    [Fact]
    public async Task Stale_LoadDetail_after_BeginCreateDraft_does_not_resurrect_prior_selection()
    {
        // BeginCreateDraft is a user-driven supersede: a slow LoadDetail
        // already in flight must drop its post-await write rather than
        // restore SelectedDetail over the new "create" context.
        var seed = Sample("first", isActive: true);
        var stub = new GatedDataConnectionClient(new[] { seed });
        var (state, _) = BuildState(stub);

        stub.HoldGet(seed.ConnectionId);
        var slow = state.LoadDetailAsync(seed.ConnectionId);

        // User pivots to "new connection" before the load lands.
        state.BeginCreateDraft("postgres");
        Assert.Null(state.SelectedDetail);

        stub.ReleaseGet(seed.ConnectionId);
        await slow;

        Assert.Null(state.SelectedDetail);
        Assert.NotNull(state.Draft);
    }

    [Fact]
    public async Task Stale_TryRefresh_after_route_change_does_not_overwrite_new_LoadDetail()
    {
        // SubmitEditAsync's internal TryRefreshSelectedDetailAsync (a follower
        // of the user-intended owner id) must drop if the user has navigated
        // away mid-flight. Without the id-snapshot guard, the late refresh
        // for connection A would clobber a successful LoadDetail for B.
        var first = Sample("first", isActive: true);
        var second = Sample("second", isActive: true);
        var stub = new GatedDataConnectionClient(new[] { first, second });
        var (state, _) = BuildState(stub);

        await state.RefreshListAsync();
        await state.LoadDetailAsync(first.ConnectionId);

        var draft = state.BeginEditDraft(state.SelectedDetail!);
        draft.Description = "updated";

        // SubmitEditAsync internally awaits UpdateAsync, then awaits
        // TryRefreshSelectedDetailAsync. We hold the TryRefresh's GetAsync so
        // we can interleave a route change before it lands.
        stub.HoldGet(first.ConnectionId);
        var submit = state.SubmitEditAsync();

        // Wait for the UpdateAsync to land (synchronous in the gated stub —
        // only Get/TestExisting are gated). The TryRefresh is now waiting on
        // the held GetAsync(first).
        // Now navigate to the second connection — LoadDetail bumps the
        // detail generation and updates the owner id.
        await state.LoadDetailAsync(second.ConnectionId);
        Assert.Equal(second.ConnectionId, state.SelectedDetail!.ConnectionId);

        // Releasing the slow TryRefresh must NOT overwrite SelectedDetail
        // back to first — its captured generation is stale AND its target
        // id no longer matches the user-intended owner.
        stub.ReleaseGet(first.ConnectionId);
        await submit;

        Assert.Equal(second.ConnectionId, state.SelectedDetail!.ConnectionId);
    }

    [Fact]
    public async Task Stale_RunDraftPreflight_after_ClearDraft_does_not_repaint_grid()
    {
        // Sibling invariant for RunDraftPreflightAsync: cancelling the draft
        // (ClearDraft) mid-flight must drop the late preflight result so it
        // cannot repaint the diagnostic grid after the user has abandoned
        // the create flow.
        var stub = new GatedDataConnectionClient(Array.Empty<DataConnectionDetail>());
        var (state, _) = BuildState(stub);

        var draft = state.BeginCreateDraft("postgres");
        draft.Name = "demo";
        draft.Host = "db.example.com";
        draft.DatabaseName = "honua";
        draft.Username = "honua";
        draft.Password = "secret";

        stub.HoldTestDraft();
        var slow = state.RunDraftPreflightAsync();

        state.ClearDraft();
        Assert.Null(state.LatestDiagnostic);

        stub.ReleaseTestDraft();
        var diagnostic = await slow;

        Assert.Null(diagnostic);
        Assert.Null(state.LatestDiagnostic);
    }

    /// <summary>
    /// Per-method gates that hold a single in-flight call until released, so
    /// tests can pin the stale-write invariants in <c>DataConnectionsState</c>
    /// without depending on real timing. All other operations pass through to
    /// <see cref="StubDataConnectionClient"/>.
    /// </summary>
    private sealed class GatedDataConnectionClient : IDataConnectionClient
    {
        private readonly StubDataConnectionClient _inner;
        private readonly Dictionary<Guid, TaskCompletionSource> _getGates = new();
        private readonly Dictionary<Guid, TaskCompletionSource> _testExistingGates = new();
        private TaskCompletionSource? _testDraftGate;

        public GatedDataConnectionClient(IEnumerable<DataConnectionDetail> seed)
        {
            _inner = new StubDataConnectionClient(seed);
        }

        public Func<string, string?>? FailureMessageForHost
        {
            get => _inner.FailureMessageForHost;
            set => _inner.FailureMessageForHost = value;
        }

        public void HoldGet(Guid id) => _getGates[id] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseGet(Guid id) => _getGates[id].TrySetResult();

        public void HoldTestExisting(Guid id) => _testExistingGates[id] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseTestExisting(Guid id) => _testExistingGates[id].TrySetResult();

        public void HoldTestDraft() => _testDraftGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseTestDraft() => _testDraftGate!.TrySetResult();

        public Task<ConnectionResult<IReadOnlyList<DataConnectionSummary>>> ListAsync(System.Threading.CancellationToken cancellationToken = default) =>
            _inner.ListAsync(cancellationToken);

        public async Task<ConnectionResult<DataConnectionDetail>> GetAsync(Guid id, System.Threading.CancellationToken cancellationToken = default)
        {
            if (_getGates.TryGetValue(id, out var gate))
            {
                await gate.Task.ConfigureAwait(false);
            }
            return await _inner.GetAsync(id, cancellationToken).ConfigureAwait(false);
        }

        public Task<ConnectionResult<DataConnectionSummary>> CreateAsync(CreateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.CreateAsync(request, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> UpdateAsync(Guid id, UpdateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.UpdateAsync(id, request, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> DisableAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.DisableAsync(id, cancellationToken);

        public Task<ConnectionResult<DataConnectionSummary>> EnableAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.EnableAsync(id, cancellationToken);

        public Task<ConnectionResult<bool>> DeleteAsync(Guid id, System.Threading.CancellationToken cancellationToken = default) =>
            _inner.DeleteAsync(id, cancellationToken);

        public async Task<ConnectionResult<ConnectionTestOutcome>> TestDraftAsync(CreateConnectionRequest request, System.Threading.CancellationToken cancellationToken = default)
        {
            if (_testDraftGate is { } gate)
            {
                await gate.Task.ConfigureAwait(false);
            }
            return await _inner.TestDraftAsync(request, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ConnectionResult<ConnectionTestOutcome>> TestExistingAsync(Guid id, System.Threading.CancellationToken cancellationToken = default)
        {
            if (_testExistingGates.TryGetValue(id, out var gate))
            {
                await gate.Task.ConfigureAwait(false);
            }
            return await _inner.TestExistingAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }
}
