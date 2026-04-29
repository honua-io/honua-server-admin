using Honua.Admin.Models.SpecWorkspace;
using Honua.Admin.Services.SpecWorkspace;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class SpecCompletionQueryBuilderTests
{
    [Fact]
    public void Build_returns_dataset_completion_for_at_mentions()
    {
        const string text = "source=@par";

        var completion = SpecCompletionQueryBuilder.Build(text, text.Length, SpecSectionId.Sources, "operator");

        Assert.NotNull(completion);
        Assert.Equal(CatalogTrigger.AtMention, completion.Query.Trigger);
        Assert.Equal("par", completion.Query.Prefix);
        Assert.Equal(7, completion.ReplaceStart);
        Assert.Equal(11, completion.ReplaceEnd);
    }

    [Fact]
    public void Build_returns_column_completion_for_dot_members()
    {
        const string text = "aggregate inputs=@parcels.co";

        var completion = SpecCompletionQueryBuilder.Build(text, text.Length, SpecSectionId.Compute, "operator");

        Assert.NotNull(completion);
        Assert.Equal(CatalogTrigger.DotMember, completion.Query.Trigger);
        Assert.Equal("parcels", completion.Query.Parent);
        Assert.Equal("co", completion.Query.Prefix);
    }

    [Fact]
    public void Build_returns_param_list_completion_for_compute_argument_prefix()
    {
        const string text = "aggregate inputs=@parcels me";

        var completion = SpecCompletionQueryBuilder.Build(text, text.Length, SpecSectionId.Compute, "operator");

        Assert.NotNull(completion);
        Assert.Equal(CatalogTrigger.ParamList, completion.Query.Trigger);
        Assert.Equal("me", completion.Query.Prefix);
        Assert.Equal(text.LastIndexOf("me", StringComparison.Ordinal), completion.ReplaceStart);
        Assert.Equal(text.Length, completion.ReplaceEnd);
    }

    [Fact]
    public void Build_returns_symbology_completion_for_map_section_assignment()
    {
        const string text = "layer source=@parcels symbology=vi";

        var completion = SpecCompletionQueryBuilder.Build(text, text.Length, SpecSectionId.Map, "operator");

        Assert.NotNull(completion);
        Assert.Equal(CatalogTrigger.SymbologyRamp, completion.Query.Trigger);
        Assert.Equal("vi", completion.Query.Prefix);
        Assert.Equal(text.LastIndexOf("vi", StringComparison.Ordinal), completion.ReplaceStart);
    }

    [Fact]
    public void Build_returns_symbology_completion_for_map_symbology_call()
    {
        const string text = "map.symbology(pl";

        var completion = SpecCompletionQueryBuilder.Build(text, text.Length, SpecSectionId.Map, "operator");

        Assert.NotNull(completion);
        Assert.Equal(CatalogTrigger.SymbologyRamp, completion.Query.Trigger);
        Assert.Equal("pl", completion.Query.Prefix);
    }

    [Fact]
    public void GetInsertionText_adds_equals_for_param_list_candidates()
    {
        var completion = new SpecCompletionRequest(
            new ResolveQuery
            {
                Trigger = CatalogTrigger.ParamList,
                Prefix = "me"
            },
            0,
            2);
        var candidate = new CatalogCandidate
        {
            Id = "metric",
            Kind = CatalogCandidateKind.Operator,
            Label = "metric"
        };

        var text = SpecCompletionQueryBuilder.GetInsertionText(candidate, completion);

        Assert.Equal("metric=", text);
    }
}
