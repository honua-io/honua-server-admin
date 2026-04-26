using Honua.Admin.Models.SpatialSql;
using Honua.Admin.Services.SpatialSql;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class StubSpatialSqlClientTests
{
    private readonly StubSpatialSqlClient _client = new();

    [Fact]
    public async Task GetSchemaAsync_exposes_seeded_tables_with_geometry_columns()
    {
        var snapshot = await _client.GetSchemaAsync(CancellationToken.None);

        Assert.Contains(snapshot.Tables, t => t.Name == "parcels" && t.GeometryColumn == "geom" && t.Srid == 4326);
        Assert.Contains(snapshot.Tables, t => t.Name == "wells" && t.GeometryColumn == "geom");
        Assert.NotEmpty(snapshot.Functions);
        Assert.Contains(snapshot.Functions, f => f.Name == "ST_Buffer");
        Assert.Contains(snapshot.Operators, o => o.Symbol == "&&");
    }

    [Fact]
    public async Task ExecuteAsync_returns_geometry_metadata_and_rows_for_parcels()
    {
        var result = await _client.ExecuteAsync(new SqlExecuteRequest
        {
            Sql = "SELECT id, county, acreage, geom FROM parcels"
        }, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.True(result.HasGeometry);
        Assert.Equal(4326, result.GeometrySrid);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("geom", result.Columns[result.GeometryColumnIndex!.Value].Name);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task ExecuteAsync_blocks_mutation_by_default_and_returns_an_error_payload()
    {
        var result = await _client.ExecuteAsync(new SqlExecuteRequest
        {
            Sql = "DELETE FROM parcels"
        }, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("mutation_blocked", result.Error!.Code);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task ExecuteAsync_runs_mutation_and_assigns_audit_id_when_override_set()
    {
        var result = await _client.ExecuteAsync(new SqlExecuteRequest
        {
            Sql = "UPDATE parcels SET county='X' WHERE id IS NULL",
            AllowMutation = true
        }, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.AuditEntryId);
        Assert.Equal("status", result.Columns[0].Name);
    }

    [Fact]
    public async Task ExecuteAsync_caps_rows_at_request_limit_and_marks_truncated()
    {
        var result = await _client.ExecuteAsync(new SqlExecuteRequest
        {
            Sql = "SELECT * FROM parcels",
            RowLimit = 1
        }, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Single(result.Rows);
        Assert.True(result.Truncated);
        Assert.Equal(1, result.RowLimit);
    }

    [Fact]
    public async Task ExplainAsync_builds_aggregate_over_table_when_table_resolves()
    {
        var plan = await _client.ExplainAsync(new SqlExplainRequest
        {
            Sql = "SELECT count(*) FROM wells"
        }, CancellationToken.None);

        Assert.False(plan.IsError);
        Assert.Equal("Aggregate", plan.Root.NodeType);
        var child = Assert.Single(plan.Root.Children);
        Assert.Equal("Seq Scan", child.NodeType);
        Assert.Equal("wells", child.Relation);
    }

    [Fact]
    public async Task SaveViewAsync_returns_protocol_urls_and_rejects_duplicates()
    {
        var first = await _client.SaveViewAsync(new SaveViewRequest
        {
            Name = "active_parcels",
            Sql = "SELECT * FROM parcels"
        }, CancellationToken.None);

        Assert.False(first.IsError);
        Assert.NotNull(first.FeatureServerUrl);
        Assert.NotNull(first.OgcFeaturesUrl);
        Assert.NotNull(first.ODataUrl);

        var dup = await _client.SaveViewAsync(new SaveViewRequest
        {
            Name = "active_parcels",
            Sql = "SELECT 1"
        }, CancellationToken.None);

        Assert.True(dup.IsError);
        Assert.Equal("duplicate_name", dup.Error!.Code);
    }

    [Fact]
    public async Task SaveViewAsync_rejects_mutating_sql()
    {
        var result = await _client.SaveViewAsync(new SaveViewRequest
        {
            Name = "bad",
            Sql = "DELETE FROM parcels"
        }, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("mutation_blocked", result.Error!.Code);
    }
}
