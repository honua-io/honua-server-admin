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

    [Theory]
    [InlineData("SELECT $$DELETE FROM x$$ AS payload FROM parcels")]
    [InlineData("SELECT $tag$DROP TABLE wells$tag$ AS payload")]
    [InlineData("SELECT $body$ INSERT INTO logs VALUES (1) $body$ FROM parcels")]
    [InlineData("SELECT $$ /* DELETE */ -- DROP\n DROP $$ FROM parcels")]
    public void Mutation_keyword_inside_dollar_quoted_literal_is_not_flagged(string sql)
    {
        // PostgreSQL dollar-quoted strings: $$...$$ and $tag$...$tag$. Their bodies
        // are arbitrary text and must be skipped before keyword scanning, otherwise
        // a benign read-only SELECT carrying mutation keywords inside a literal
        // would trigger the override dialog.
        Assert.False(MutationGuard.IsMutating(sql));
    }

    [Theory]
    [InlineData("DELETE FROM parcels WHERE name = $$bob$$")]
    [InlineData("UPDATE parcels SET note = $note$some text$note$ WHERE id = 1")]
    public void Mutating_sql_with_dollar_quoted_arguments_is_still_flagged(string sql)
    {
        // The dollar-quoted body is stripped, but the surrounding mutation verb
        // remains and must still trip the guard.
        Assert.True(MutationGuard.IsMutating(sql));
    }

    [Fact]
    public void Positional_parameter_dollar_n_is_not_treated_as_dollar_quote_opener()
    {
        // $1, $2, … are PostgreSQL positional parameters, not dollar-quote openers
        // (the tag rule forbids a leading digit). The guard must not over-consume
        // and accidentally swallow a real DELETE/INSERT that follows.
        Assert.True(MutationGuard.IsMutating("DELETE FROM parcels WHERE id = $1"));
        Assert.False(MutationGuard.IsMutating("SELECT $1, $2 FROM parcels"));
    }

    [Fact]
    public void Unterminated_dollar_quote_consumes_to_end_so_trailing_keywords_are_not_flagged()
    {
        // A buffer mid-edit may have an opener with no closer. Treating the
        // unterminated body as quoted matches PostgreSQL's lex behavior and
        // prevents a false positive while the operator is still typing.
        Assert.False(MutationGuard.IsMutating("SELECT $$incomplete body DELETE FROM parcels"));
    }

    [Theory]
    [InlineData("SELECT * INTO new_table FROM parcels")]
    [InlineData("SELECT id, geom INTO archived FROM parcels WHERE retired_at IS NOT NULL")]
    [InlineData("SELECT * INTO TEMP scratch FROM parcels")]
    [InlineData("SELECT * INTO TEMPORARY scratch FROM parcels")]
    [InlineData("SELECT * INTO UNLOGGED bulk FROM parcels")]
    [InlineData("SELECT * INTO TABLE archived FROM parcels")]
    [InlineData("select * into new_table from parcels")]
    public void Select_into_creates_a_table_and_must_be_flagged(string sql)
    {
        // EXPLAIN ANALYZE on SELECT ... INTO actually creates and fills the
        // target table — it is a write under the EXPLAIN endpoint just like
        // INSERT/UPDATE. The bare INTO keyword is sufficient because INSERT
        // INTO and MERGE INTO are already flagged by their primary verbs.
        // https://www.postgresql.org/docs/current/sql-selectinto.html
        Assert.True(MutationGuard.IsMutating(sql));
    }

    [Theory]
    [InlineData("EXECUTE my_prepared_statement")]
    [InlineData("EXECUTE my_prepared_statement(1, 'foo')")]
    [InlineData("execute my_prepared_statement")]
    public void Execute_runs_an_opaque_prepared_statement_and_must_be_flagged(string sql)
    {
        // EXECUTE invokes a previously prepared statement whose body the
        // client cannot inspect; under EXPLAIN ANALYZE it actually runs
        // the prepared statement, so the conservative guard rejects all
        // EXECUTE forms. https://www.postgresql.org/docs/current/sql-execute.html
        Assert.True(MutationGuard.IsMutating(sql));
    }

    [Theory]
    [InlineData("SELECT 'INTO' AS note FROM parcels")]
    [InlineData("SELECT 'going INTO production' AS note FROM logs")]
    [InlineData("SELECT into_col FROM stats")]
    [InlineData("SELECT executions FROM stats")]
    [InlineData("SELECT col_into_x FROM stats")]
    public void Into_or_execute_inside_literal_or_identifier_is_not_flagged(string sql)
    {
        // String literals are stripped before keyword scanning; identifier
        // word-boundary rules prevent `into_col`, `col_into_x`, and
        // `executions` from matching the new INTO/EXECUTE keywords.
        Assert.False(MutationGuard.IsMutating(sql));
    }
}
