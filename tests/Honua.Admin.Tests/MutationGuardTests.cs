using Honua.Admin.Models.SpatialSql;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class MutationGuardTests
{
    [Theory]
    [InlineData("SELECT * FROM parcels")]
    [InlineData("WITH t AS (SELECT 1) SELECT * FROM t")]
    [InlineData("EXPLAIN ANALYZE SELECT * FROM wells")]
    [InlineData("SELECT ST_Area(geom) FROM parcels WHERE county = 'Big Island'")]
    [InlineData("SELECT 'INSERT INTO not really' AS note FROM parcels")]
    [InlineData("-- DELETE FROM parcels\nSELECT 1")]
    [InlineData("/* DROP TABLE parcels */ SELECT 1")]
    [InlineData("")]
    [InlineData(null)]
    public void Read_only_or_empty_sql_is_not_flagged(string? sql)
    {
        Assert.False(MutationGuard.IsMutating(sql));
    }

    [Theory]
    [InlineData("INSERT INTO parcels(id) VALUES('x')")]
    [InlineData("UPDATE parcels SET county='X'")]
    [InlineData("DELETE FROM parcels")]
    [InlineData("DROP TABLE parcels")]
    [InlineData("CREATE INDEX ON parcels(geom)")]
    [InlineData("ALTER TABLE parcels ADD COLUMN foo text")]
    [InlineData("TRUNCATE parcels")]
    [InlineData("MERGE INTO parcels USING staging ON true")]
    [InlineData("  insert into parcels values(1)  ")]
    public void Mutating_sql_is_flagged_independent_of_case(string sql)
    {
        Assert.True(MutationGuard.IsMutating(sql));
    }

    [Fact]
    public void Mutating_keyword_inside_an_identifier_is_not_flagged()
    {
        Assert.False(MutationGuard.IsMutating("SELECT updates_count FROM stats"));
    }
}
