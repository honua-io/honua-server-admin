using System.Collections.Generic;
using Honua.Admin.Models.SpatialSql;
using Honua.Admin.Services.SpatialSql;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class SpatialSqlPlaygroundStateTests
{
    [Fact]
    public async Task LoadSchemaAsync_populates_schema_and_records_telemetry_event()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), telemetry);

        await state.LoadSchemaAsync();

        Assert.NotNull(state.Schema);
        Assert.Equal(SpatialSqlPaneStatus.Idle, state.Status);
        Assert.Contains(telemetry.Events, e => e.Event == "schema_loaded");
    }

    [Fact]
    public async Task RunQueryAsync_blocks_mutation_until_override_is_armed()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), telemetry);
        await state.LoadSchemaAsync();

        state.SetSql("DELETE FROM parcels");
        await state.RunQueryAsync();

        Assert.Equal(SpatialSqlPaneStatus.Error, state.Status);
        Assert.NotNull(state.LastResult?.Error);
        Assert.Equal("mutation_blocked", state.LastResult!.Error!.Code);
        Assert.Contains(telemetry.Events, e => e.Event == "query_rejected");

        state.ArmMutationOverride();
        await state.RunQueryAsync();

        Assert.Equal(SpatialSqlPaneStatus.Idle, state.Status);
        Assert.NotNull(state.LastResult?.AuditEntryId);
        Assert.Contains(telemetry.Events, e => e.Event == "mutation_override_accepted");

        // Override is single-shot — sending the same SQL again must require a fresh confirmation.
        await state.RunQueryAsync();
        Assert.Equal(SpatialSqlPaneStatus.Error, state.Status);
    }

    [Fact]
    public async Task RunQueryAsync_with_geometry_result_switches_to_map_tab_and_builds_features()
    {
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), new RecordingTelemetry());
        await state.LoadSchemaAsync();

        state.SetSql("SELECT id, county, geom FROM parcels");
        await state.RunQueryAsync();

        Assert.False(state.LastResult?.IsError);
        Assert.True(state.LastResult!.HasGeometry);
        Assert.Equal("map", state.ResultsTab);
        var features = state.BuildMapFeatures();
        Assert.Equal(2, features.Count);
        Assert.All(features, f => Assert.Contains("\"type\"", f.GeoJson, System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunQueryAsync_truncated_result_blocks_export_until_confirmed()
    {
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), new RecordingTelemetry());
        await state.LoadSchemaAsync();
        state.SetSql("SELECT * FROM parcels");
        // Force a truncated result by running with row limit 1 via direct request.
        var truncated = await new StubSpatialSqlClient().ExecuteAsync(new SqlExecuteRequest
        {
            Sql = "SELECT * FROM parcels",
            RowLimit = 1
        }, default);
        Assert.True(truncated.Truncated);
        // Replay the truncated result through the state via the standard path: re-run the query
        // and override row limit through a fresh client wired into state.
        var capStub = new CappingClient(rowLimit: 1);
        var cappedState = new SpatialSqlPlaygroundState(capStub, new RecordingTelemetry());
        await cappedState.LoadSchemaAsync();
        cappedState.SetSql("SELECT * FROM parcels");
        await cappedState.RunQueryAsync();

        Assert.True(cappedState.LastResult!.Truncated);
        Assert.False(cappedState.CanExportRows());

        cappedState.ConfirmTruncatedExport();
        Assert.True(cappedState.CanExportRows());
    }

    [Fact]
    public async Task RunExplainAsync_populates_LastPlan_and_records_completion_telemetry()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), telemetry);

        state.SetSql("SELECT count(*) FROM wells");
        await state.RunExplainAsync();

        Assert.NotNull(state.LastPlan);
        Assert.Equal("Aggregate", state.LastPlan!.Root.NodeType);
        Assert.Contains(telemetry.Events, e => e.Event == "explain_completed");
    }

    [Fact]
    public async Task SaveViewAsync_records_view_saved_and_returns_protocol_urls()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), telemetry);
        await state.LoadSchemaAsync();
        state.SetSql("SELECT id, county, geom FROM parcels");
        await state.RunQueryAsync();

        var registration = await state.SaveViewAsync("active_parcels", "polygons in active counties", default);

        Assert.False(registration.IsError);
        Assert.NotNull(registration.FeatureServerUrl);
        Assert.NotNull(registration.OgcFeaturesUrl);
        Assert.NotNull(registration.ODataUrl);
        Assert.Contains(telemetry.Events, e => e.Event == "view_saved");
        // LastSavedView mirrors the registration on the state store so external
        // observers (tests, future surfaces such as a saved-views list) can recover
        // the URLs after the dialog closes. The dialog itself renders from its own
        // local `Registration` field captured from SaveViewAsync's return value.
        Assert.Same(registration, state.LastSavedView);
    }

    [Fact]
    public async Task ExportCsv_records_export_triggered_event()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), telemetry);
        await state.LoadSchemaAsync();
        state.SetSql("SELECT id, county FROM parcels");
        await state.RunQueryAsync();

        var csv = state.ExportCsv();
        Assert.Contains("id,county", csv, System.StringComparison.Ordinal);
        Assert.Contains(telemetry.Events, e => e.Event == "export_triggered");
    }

    [Fact]
    public void SetExportError_surfaces_message_and_emits_export_rejected_event()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), telemetry);
        // Status is intentionally preserved: the underlying query result is still
        // valid — only the export action failed — so the chip stays at its prior
        // value while the banner communicates the rejection.
        var preStatus = state.Status;

        state.SetExportError("GeoJSON export requires WGS84 (SRID 4326); got SRID 3857.");

        Assert.NotNull(state.LastError);
        Assert.Contains("WGS84", state.LastError!, System.StringComparison.Ordinal);
        Assert.Contains(telemetry.Events, e => e.Event == "export_rejected");
        Assert.Equal(preStatus, state.Status);
    }

    [Fact]
    public async Task RunQueryAsync_with_non_wgs84_geometry_blocks_map_preview_and_keeps_table_tab()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new NonWgs84Client(), telemetry);
        await state.LoadSchemaAsync();

        state.SetSql("SELECT id, geom FROM mercator");
        await state.RunQueryAsync();

        Assert.True(state.LastResult!.HasGeometry);
        Assert.Equal(3857, state.LastResult.GeometrySrid);
        // Auto-tab-switch must NOT promote the operator into a map view that would
        // mis-render — keep them on the table with the blocked-reason banner.
        Assert.Equal("table", state.ResultsTab);
        Assert.NotNull(state.MapPreviewBlockedReason);
        Assert.Contains("WGS84", state.MapPreviewBlockedReason!, System.StringComparison.Ordinal);
        Assert.Contains("3857", state.MapPreviewBlockedReason!, System.StringComparison.Ordinal);
        // BuildMapFeatures mirrors the SqlResultExporter guard: refuse to hand
        // non-WGS84 coordinates to MapLibre even if the caller forces the tab.
        Assert.Empty(state.BuildMapFeatures());
    }

    [Fact]
    public async Task RunQueryAsync_with_wgs84_geometry_leaves_blocked_reason_null()
    {
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), new RecordingTelemetry());
        await state.LoadSchemaAsync();
        state.SetSql("SELECT id, county, geom FROM parcels");
        await state.RunQueryAsync();

        Assert.True(state.LastResult!.HasGeometry);
        Assert.Null(state.MapPreviewBlockedReason);
        Assert.Equal("map", state.ResultsTab);
    }

    [Fact]
    public async Task SaveViewAsync_routes_OperationCanceledException_through_idle_terminal_state()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new CancellingSaveClient(), telemetry);
        var observed = new List<SpatialSqlPaneStatus>();
        state.OnChanged += () => observed.Add(state.Status);

        await Assert.ThrowsAsync<OperationCanceledException>(() => state.SaveViewAsync("v", "d", default));

        // Cancellation lands in Idle (matches LoadSchema/RunQuery/RunExplain), and
        // is NOT misclassified as a transport_error in telemetry.
        Assert.Equal(SpatialSqlPaneStatus.Idle, state.Status);
        Assert.Null(state.LastError);
        Assert.DoesNotContain(telemetry.Events, e => e.Event == "view_save_rejected");
        // Subscribers must observe both the Saving and Idle transitions so any
        // 'Saving…' spinner can clear.
        Assert.Contains(SpatialSqlPaneStatus.Saving, observed);
        Assert.Equal(SpatialSqlPaneStatus.Idle, observed[^1]);
    }

    [Fact]
    public async Task LoadSchemaAsync_notifies_subscribers_before_re_throwing_cancellation()
    {
        var state = new SpatialSqlPlaygroundState(new CancellingSchemaClient(), new RecordingTelemetry());
        var observed = new List<SpatialSqlPaneStatus>();
        state.OnChanged += () => observed.Add(state.Status);

        await Assert.ThrowsAsync<OperationCanceledException>(() => state.LoadSchemaAsync());

        // The trailing Notify() is bypassed by the re-throw, so the OCE catch must
        // notify before propagating — otherwise subscribers get stuck on Loading.
        Assert.Equal(SpatialSqlPaneStatus.Idle, state.Status);
        Assert.Contains(SpatialSqlPaneStatus.Loading, observed);
        Assert.Equal(SpatialSqlPaneStatus.Idle, observed[^1]);
    }

    [Fact]
    public async Task RunQueryAsync_superseded_completion_does_not_overwrite_active_query_state()
    {
        // A slow query starts, a fast query supersedes it. When the slow query's
        // await finally resolves it must NOT write LastResult/Status/telemetry —
        // otherwise it races with the active query's terminal state and undoes it.
        var slowGate = new TaskCompletionSource<SqlExecuteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastResult = new SqlExecuteResult
        {
            Columns = new[] { new SqlColumn("payload", "text") },
            Rows = new[] { new SqlRow(new string?[] { "fast-result" }) }
        };
        var client = new GatedExecuteClient(firstAttempt: () => slowGate.Task, fallback: fastResult);
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(client, telemetry);

        state.SetSql("SELECT * FROM slow");
        var slowTask = state.RunQueryAsync();

        // Now run a second query; it cancels the slow one's cts and, since the
        // gated client's first attempt is already pending, this call hits the
        // fallback path and completes synchronously.
        state.SetSql("SELECT * FROM fast");
        await state.RunQueryAsync();

        // Active state must reflect the fast query.
        Assert.Equal("fast-result", state.LastResult!.Rows[0].Cells[0]);
        Assert.Equal(SpatialSqlPaneStatus.Idle, state.Status);

        // Now release the superseded slow query — its post-await writes must be
        // discarded by the IsActiveExecution gate.
        slowGate.SetResult(new SqlExecuteResult
        {
            Columns = new[] { new SqlColumn("payload", "text") },
            Rows = new[] { new SqlRow(new string?[] { "slow-result" }) }
        });
        await slowTask;

        Assert.Equal("fast-result", state.LastResult!.Rows[0].Cells[0]);
        Assert.Equal(SpatialSqlPaneStatus.Idle, state.Status);
        // Only one query_completed should have fired — the superseded one is silent.
        Assert.Single(telemetry.Events, e => e.Event == "query_completed");
    }

    [Fact]
    public async Task RunQueryAsync_pairs_LastResult_with_the_SQL_that_produced_it()
    {
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), new RecordingTelemetry());
        await state.LoadSchemaAsync();

        const string executedSql = "SELECT id, county, geom FROM parcels";
        state.SetSql(executedSql);
        await state.RunQueryAsync();

        Assert.Equal(executedSql, state.LastResultSql);
        Assert.True(state.CanSaveCurrentResult());

        // Edit the editor buffer; CanSaveCurrentResult must drop until the operator
        // re-runs the query. Otherwise Save would register the edited SQL with the
        // executed result's geometry metadata.
        state.SetSql(executedSql + "  -- adding a comment");
        Assert.False(state.CanSaveCurrentResult());
        Assert.Equal(executedSql, state.LastResultSql);
    }

    [Fact]
    public async Task RunQueryAsync_clears_mutation_override_when_execute_throws()
    {
        // Without consuming the override at snapshot, a transport failure leaves
        // MutationOverrideArmed=true; the next click of Run silently resubmits
        // with allowMutation=true. Pin the consumption-on-failure contract.
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new ThrowingExecuteClient(), telemetry);

        state.SetSql("DELETE FROM parcels");
        state.ArmMutationOverride();
        Assert.True(state.MutationOverrideArmed);

        await state.RunQueryAsync();

        Assert.Equal(SpatialSqlPaneStatus.Error, state.Status);
        Assert.False(state.MutationOverrideArmed);
        Assert.Contains(telemetry.Events, e => e.Event == "query_rejected" &&
            e.Properties is not null &&
            e.Properties.TryGetValue("reason", out var r) &&
            string.Equals(r as string, "transport_error", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunQueryAsync_clears_mutation_override_when_execute_is_cancelled()
    {
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new CancellingExecuteClient(), telemetry);

        state.SetSql("DELETE FROM parcels");
        state.ArmMutationOverride();

        await state.RunQueryAsync();

        // OCE path lands in Idle (not Error), but the override must still be
        // consumed — otherwise a cancelled mutation re-arms invisibly.
        Assert.Equal(SpatialSqlPaneStatus.Idle, state.Status);
        Assert.False(state.MutationOverrideArmed);
    }

    [Fact]
    public async Task RunExplainAsync_rejects_mutating_sql_with_mutation_blocked_reason()
    {
        // EXPLAIN ANALYZE actually runs the statement; the server's EXPLAIN
        // endpoint has no AllowMutation contract or audit hook, so mutating SQL
        // must be rejected outright on the EXPLAIN path even when the operator
        // armed the per-query Run override.
        var telemetry = new RecordingTelemetry();
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), telemetry);

        state.SetSql("DELETE FROM parcels");
        state.ArmMutationOverride();
        await state.RunExplainAsync();

        Assert.Equal(SpatialSqlPaneStatus.Error, state.Status);
        Assert.NotNull(state.LastError);
        Assert.Null(state.LastPlan);
        Assert.Contains(telemetry.Events, e => e.Event == "explain_rejected" &&
            e.Properties is not null &&
            e.Properties.TryGetValue("reason", out var r) &&
            string.Equals(r as string, "mutation_blocked", System.StringComparison.Ordinal));
        Assert.DoesNotContain(telemetry.Events, e => e.Event == "explain_completed");
    }

    [Fact]
    public async Task SaveViewAsync_uses_LastResultSql_not_the_live_editor_buffer()
    {
        var capture = new RequestCapturingClient();
        var state = new SpatialSqlPlaygroundState(capture, new RecordingTelemetry());
        await state.LoadSchemaAsync();

        const string executedSql = "SELECT id, county, geom FROM parcels";
        state.SetSql(executedSql);
        await state.RunQueryAsync();

        // Operator drifts the buffer before opening the dialog.
        state.SetSql("SELECT id FROM parcels WHERE county='OOPS'");

        await state.SaveViewAsync("active_parcels_drifted", "still ok", default);

        Assert.NotNull(capture.LastSaveRequest);
        Assert.Equal(executedSql, capture.LastSaveRequest!.Sql);
        Assert.Equal("geom", capture.LastSaveRequest.GeometryColumn);
    }

    private sealed class NonWgs84Client : ISpatialSqlClient
    {
        private readonly StubSpatialSqlClient _inner = new();

        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            _inner.GetSchemaAsync(cancellationToken);

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new SqlExecuteResult
            {
                Columns = new[]
                {
                    new SqlColumn("id", "uuid"),
                    new SqlColumn("geom", "geometry", IsGeometry: true)
                },
                Rows = new[]
                {
                    new SqlRow(new string?[]
                    {
                        "1",
                        "{\"type\":\"Point\",\"coordinates\":[1000000,2000000]}"
                    })
                },
                GeometryColumnIndex = 1,
                GeometrySrid = 3857
            });

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            _inner.ExplainAsync(request, cancellationToken);

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken) =>
            _inner.SaveViewAsync(request, cancellationToken);
    }

    private sealed class CancellingSaveClient : ISpatialSqlClient
    {
        private readonly StubSpatialSqlClient _inner = new();

        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            _inner.GetSchemaAsync(cancellationToken);

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken) =>
            _inner.ExecuteAsync(request, cancellationToken);

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            _inner.ExplainAsync(request, cancellationToken);

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken) =>
            Task.FromException<NamedViewRegistration>(new OperationCanceledException());
    }

    private sealed class CancellingSchemaClient : ISpatialSqlClient
    {
        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            Task.FromException<SchemaSnapshot>(new OperationCanceledException());

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken) =>
            throw new System.NotSupportedException();

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            throw new System.NotSupportedException();

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken) =>
            throw new System.NotSupportedException();
    }

    private sealed class ThrowingExecuteClient : ISpatialSqlClient
    {
        private readonly StubSpatialSqlClient _inner = new();

        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            _inner.GetSchemaAsync(cancellationToken);

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken) =>
            Task.FromException<SqlExecuteResult>(new System.InvalidOperationException("simulated transport failure"));

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            _inner.ExplainAsync(request, cancellationToken);

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken) =>
            _inner.SaveViewAsync(request, cancellationToken);
    }

    private sealed class CancellingExecuteClient : ISpatialSqlClient
    {
        private readonly StubSpatialSqlClient _inner = new();

        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            _inner.GetSchemaAsync(cancellationToken);

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken) =>
            Task.FromException<SqlExecuteResult>(new OperationCanceledException());

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            _inner.ExplainAsync(request, cancellationToken);

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken) =>
            _inner.SaveViewAsync(request, cancellationToken);
    }

    private sealed class GatedExecuteClient : ISpatialSqlClient
    {
        private readonly StubSpatialSqlClient _inner = new();
        private readonly System.Func<Task<SqlExecuteResult>> _firstAttempt;
        private readonly SqlExecuteResult _fallback;
        private int _calls;

        public GatedExecuteClient(System.Func<Task<SqlExecuteResult>> firstAttempt, SqlExecuteResult fallback)
        {
            _firstAttempt = firstAttempt;
            _fallback = fallback;
        }

        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            _inner.GetSchemaAsync(cancellationToken);

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken)
        {
            var index = System.Threading.Interlocked.Increment(ref _calls);
            return index == 1 ? _firstAttempt() : Task.FromResult(_fallback);
        }

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            _inner.ExplainAsync(request, cancellationToken);

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken) =>
            _inner.SaveViewAsync(request, cancellationToken);
    }

    private sealed class RequestCapturingClient : ISpatialSqlClient
    {
        private readonly StubSpatialSqlClient _inner = new();
        public SaveViewRequest? LastSaveRequest { get; private set; }

        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            _inner.GetSchemaAsync(cancellationToken);

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken) =>
            _inner.ExecuteAsync(request, cancellationToken);

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            _inner.ExplainAsync(request, cancellationToken);

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken)
        {
            LastSaveRequest = request;
            return _inner.SaveViewAsync(request, cancellationToken);
        }
    }

    private sealed class CappingClient : ISpatialSqlClient
    {
        private readonly StubSpatialSqlClient _inner = new();
        private readonly int _rowLimit;

        public CappingClient(int rowLimit) => _rowLimit = rowLimit;

        public Task<SchemaSnapshot> GetSchemaAsync(CancellationToken cancellationToken) =>
            _inner.GetSchemaAsync(cancellationToken);

        public Task<SqlExecuteResult> ExecuteAsync(SqlExecuteRequest request, CancellationToken cancellationToken) =>
            _inner.ExecuteAsync(request with { RowLimit = _rowLimit }, cancellationToken);

        public Task<ExplainPlan> ExplainAsync(SqlExplainRequest request, CancellationToken cancellationToken) =>
            _inner.ExplainAsync(request, cancellationToken);

        public Task<NamedViewRegistration> SaveViewAsync(SaveViewRequest request, CancellationToken cancellationToken) =>
            _inner.SaveViewAsync(request, cancellationToken);
    }

    private sealed class RecordingTelemetry : ISpatialSqlTelemetry
    {
        public List<(string Event, IReadOnlyDictionary<string, object?>? Properties, long? ElapsedMs)> Events { get; } = new();

        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null) =>
            Events.Add((eventName, properties, null));

        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null) =>
            Events.Add((eventName, properties, elapsedMillis));
    }
}
