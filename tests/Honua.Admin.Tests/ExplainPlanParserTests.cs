using System.Text.Json;
using Honua.Admin.Models.SpatialSql;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class ExplainPlanParserTests
{
    [Fact]
    public void Parse_projects_postgres_explain_envelope_into_node_tree()
    {
        var json = """
            [
              {
                "Plan": {
                  "Node Type": "Aggregate",
                  "Actual Rows": 1,
                  "Plan Rows": 1,
                  "Actual Total Time": 14.2,
                  "Total Cost": 38.7,
                  "Plans": [
                    {
                      "Node Type": "Seq Scan",
                      "Relation Name": "parcels",
                      "Actual Rows": 12000,
                      "Plan Rows": 100,
                      "Actual Total Time": 12.4,
                      "Total Cost": 32.5
                    }
                  ]
                },
                "Planning Time": 0.4,
                "Execution Time": 16.1
              }
            ]
            """;

        var plan = ExplainPlanParser.Parse(json);

        Assert.False(plan.IsError);
        Assert.Equal("Aggregate", plan.Root.NodeType);
        Assert.Equal(16.1, plan.TotalElapsedMs, 1);
        Assert.Equal(0.4, plan.PlanningMs, 1);

        var child = Assert.Single(plan.Root.Children);
        Assert.Equal("Seq Scan", child.NodeType);
        Assert.Equal("parcels", child.Relation);
        Assert.Equal(12000, child.ActualRows);
        Assert.Equal(100, child.PlanRows);
        Assert.True(child.RowEstimateOff);
    }

    [Fact]
    public void Parse_handles_object_envelope_without_outer_array()
    {
        var json = """
            {
              "Plan": { "Node Type": "Result", "Actual Rows": 1, "Plan Rows": 1 },
              "Execution Time": 0.3
            }
            """;

        var plan = ExplainPlanParser.Parse(json);

        Assert.Equal("Result", plan.Root.NodeType);
        Assert.Empty(plan.Root.Children);
    }

    [Fact]
    public void Parse_throws_when_envelope_is_missing_plan()
    {
        var json = """[ { "Planning Time": 1.0 } ]""";
        Assert.Throws<System.InvalidOperationException>(() => ExplainPlanParser.Parse(json));
    }

    [Fact]
    public void Row_estimate_off_marks_dramatic_misestimates_in_either_direction()
    {
        var underEstimate = new ExplainNode { NodeType = "Seq Scan", PlanRows = 10, ActualRows = 200 };
        var overEstimate = new ExplainNode { NodeType = "Seq Scan", PlanRows = 200, ActualRows = 10 };
        var close = new ExplainNode { NodeType = "Seq Scan", PlanRows = 100, ActualRows = 130 };

        Assert.True(underEstimate.RowEstimateOff);
        Assert.True(overEstimate.RowEstimateOff);
        Assert.False(close.RowEstimateOff);
    }

    [Fact]
    public void Parse_accepts_jsonelement_overload()
    {
        var json = """[ { "Plan": { "Node Type": "X", "Actual Rows": 0, "Plan Rows": 0 } } ]""";
        using var doc = JsonDocument.Parse(json);
        var plan = ExplainPlanParser.Parse(doc.RootElement);
        Assert.Equal("X", plan.Root.NodeType);
    }
}
