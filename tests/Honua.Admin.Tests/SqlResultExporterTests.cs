using System.Text.Json;
using Honua.Admin.Models.SpatialSql;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class SqlResultExporterTests
{
    [Fact]
    public void ToCsv_quotes_cells_with_commas_quotes_or_newlines()
    {
        var result = new SqlExecuteResult
        {
            Columns = new[]
            {
                new SqlColumn("id", "uuid"),
                new SqlColumn("county", "text"),
                new SqlColumn("notes", "text")
            },
            Rows = new[]
            {
                new SqlRow(new string?[] { "1", "Big Island", "no, comments" }),
                new SqlRow(new string?[] { "2", "Maui", "with \"quotes\"" }),
                new SqlRow(new string?[] { "3", "Oahu", "line1\nline2" })
            }
        };

        var csv = SqlResultExporter.ToCsv(result);

        Assert.Contains("id,county,notes", csv, StringComparison.Ordinal);
        Assert.Contains("\"no, comments\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"with \"\"quotes\"\"\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"line1\nline2\"", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void ToGeoJson_emits_feature_collection_with_properties_excluding_geometry()
    {
        var result = new SqlExecuteResult
        {
            Columns = new[]
            {
                new SqlColumn("id", "uuid"),
                new SqlColumn("county", "text"),
                new SqlColumn("geom", "geometry", IsGeometry: true)
            },
            Rows = new[]
            {
                new SqlRow(new string?[]
                {
                    "1",
                    "Big Island",
                    "{\"type\":\"Point\",\"coordinates\":[-155.5,19.6]}"
                })
            },
            GeometryColumnIndex = 2,
            GeometrySrid = 4326
        };

        var json = SqlResultExporter.ToGeoJson(result);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("FeatureCollection", root.GetProperty("type").GetString());
        var feature = root.GetProperty("features")[0];
        Assert.Equal("Point", feature.GetProperty("geometry").GetProperty("type").GetString());
        var properties = feature.GetProperty("properties");
        Assert.Equal("Big Island", properties.GetProperty("county").GetString());
        Assert.False(properties.TryGetProperty("geom", out _));
        Assert.Equal("EPSG:4326", root.GetProperty("crs").GetProperty("properties").GetProperty("name").GetString());
    }

    [Fact]
    public void ToGeoJson_throws_when_result_has_no_geometry_column()
    {
        var result = new SqlExecuteResult
        {
            Columns = new[] { new SqlColumn("id", "uuid") },
            Rows = new[] { new SqlRow(new string?[] { "1" }) }
        };

        Assert.Throws<System.InvalidOperationException>(() => SqlResultExporter.ToGeoJson(result));
    }
}
