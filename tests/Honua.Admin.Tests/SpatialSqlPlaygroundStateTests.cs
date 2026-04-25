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
        // LastSavedView is the surface the dialog uses to render the URL chips
        // and copy buttons; the dialog now stays open on success and reads from
        // this property rather than auto-closing and dropping the URLs.
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

        state.SetExportError("GeoJSON export requires WGS84 (SRID 4326); got SRID 3857.");

        Assert.NotNull(state.LastError);
        Assert.Contains("WGS84", state.LastError!, System.StringComparison.Ordinal);
        Assert.Contains(telemetry.Events, e => e.Event == "export_rejected");
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
