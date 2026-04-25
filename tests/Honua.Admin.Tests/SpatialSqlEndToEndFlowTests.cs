using Honua.Admin.Models.SpatialSql;
using Honua.Admin.Services.SpatialSql;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Honua.Admin.Tests;

/// <summary>
/// Stand-in for the bUnit-style end-to-end exercising the headline flow on the
/// playground page: load schema → run a geometry query → assert table + map are
/// renderable → save as named view → assert protocol URLs come back. Drives the
/// state store directly so the test stays AOT/trim-friendly without taking on a
/// bUnit dependency that admin does not yet ship.
/// </summary>
public sealed class SpatialSqlEndToEndFlowTests
{
    [Fact]
    public async Task Query_preview_save_view_round_trip_through_state_and_stub_client()
    {
        var state = new SpatialSqlPlaygroundState(new StubSpatialSqlClient(), new NullSpatialSqlTelemetry());

        await state.LoadSchemaAsync();
        Assert.NotNull(state.Schema);

        state.SetSql("SELECT id, county, geom FROM parcels");
        await state.RunQueryAsync();

        var result = state.LastResult;
        Assert.NotNull(result);
        Assert.False(result!.IsError);
        Assert.True(result.HasGeometry);
        Assert.Equal("map", state.ResultsTab);
        Assert.NotEmpty(state.BuildMapFeatures());

        await state.RunExplainAsync();
        Assert.NotNull(state.LastPlan);

        var registration = await state.SaveViewAsync("active_parcels_e2e", "demo flow", default);

        Assert.False(registration.IsError);
        Assert.False(string.IsNullOrEmpty(registration.FeatureServerUrl));
        Assert.False(string.IsNullOrEmpty(registration.OgcFeaturesUrl));
        Assert.False(string.IsNullOrEmpty(registration.ODataUrl));
    }

    [Fact]
    public void Page_is_routable_at_operator_sql()
    {
        var pageType = typeof(Honua.Admin.Pages.Operator.SpatialSqlPlayground);
        var routes = (RouteAttribute[])pageType.GetCustomAttributes(typeof(RouteAttribute), inherit: false);
        Assert.Contains(routes, r => r.Template == "/operator/sql");
    }

    private sealed class NullSpatialSqlTelemetry : ISpatialSqlTelemetry
    {
        public void Record(string eventName, System.Collections.Generic.IReadOnlyDictionary<string, object?>? properties = null) { }
        public void RecordLatency(string eventName, long elapsedMillis, System.Collections.Generic.IReadOnlyDictionary<string, object?>? properties = null) { }
    }
}
