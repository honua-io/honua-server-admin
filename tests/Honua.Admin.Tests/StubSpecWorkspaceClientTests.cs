using Honua.Admin.Models.SpecWorkspace;
using Honua.Admin.Services.SpecWorkspace;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class StubSpecWorkspaceClientTests
{
    private readonly StubSpecWorkspaceClient _client = new();

    [Theory]
    [InlineData("dataset", ClarificationKind.PickDataset)]
    [InlineData("aggregate count of @parcels", ClarificationKind.PickColumn)]
    [InlineData("filter @parcels where @parcels.county", ClarificationKind.PickValue)]
    [InlineData("buffer @parcels by 100", ClarificationKind.SpecifyUnit)]
    [InlineData("set crs for @parcels", ClarificationKind.SpecifyCrs)]
    [InlineData("analyze @parcels", ClarificationKind.ChooseOp)]
    public async Task SubmitIntentAsync_returns_expected_structured_clarifications(string prompt, ClarificationKind expectedKind)
    {
        var outcome = await _client.SubmitIntentAsync(new IntentRequest
        {
            Prompt = prompt,
            CurrentSpec = SpecDocument.Empty
        }, CancellationToken.None);

        Assert.Equal(IntentResponseKind.Clarification, outcome.Kind);
        Assert.NotNull(outcome.Clarification);
        Assert.Equal(expectedKind, outcome.Clarification!.Kind);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Clarification.Id));
    }

    [Fact]
    public async Task Clarification_answers_apply_follow_up_mutations()
    {
        var outcome = await _client.SubmitIntentAsync(new IntentRequest
        {
            Prompt = "clarification:pick-column:aggregate:parcels=county",
            CurrentSpec = SpecDocument.Empty,
            ClarificationId = "pick-column:aggregate:parcels",
            ClarificationValue = "county"
        }, CancellationToken.None);

        Assert.Equal(IntentResponseKind.Mutation, outcome.Kind);
        Assert.NotNull(outcome.Mutation?.NextDocument);
        Assert.Single(outcome.Mutation!.NextDocument!.Compute);
        Assert.Equal("@parcels.county", outcome.Mutation.NextDocument.Compute[0].Args["by"]);
    }
}
