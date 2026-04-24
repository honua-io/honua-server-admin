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
    public async Task ApplyAsync_resolves_map_layer_features_through_aliased_sources()
    {
        var document = new SpecDocument
        {
            Sources = new[] { new SpecSourceEntry("study", "parcels") },
            Map = new SpecMap
            {
                Layers = new[] { new SpecMapLayer("study", "viridis") }
            },
            Output = new SpecOutput { Kind = SpecOutputKind.Map, Target = "preview" }
        };

        var payload = await CollectCompletedPayloadAsync(document);

        Assert.NotNull(payload);
        Assert.Equal(SpecOutputKind.Map, payload!.Kind);
        Assert.NotEmpty(payload.MapFeatures);
        Assert.All(payload.MapFeatures, feature => Assert.Equal("study", feature.Source));
    }

    [Theory]
    [InlineData("buffer @parcels by 100mi", "100mi")]
    [InlineData("buffer @parcels by 100km", "100km")]
    [InlineData("buffer @parcels by 100m", "100m")]
    [InlineData("buffer @parcels by 2.5mi", "2.5mi")]
    public async Task SubmitIntentAsync_parses_buffer_unit_longest_first(string prompt, string expectedDistance)
    {
        var outcome = await _client.SubmitIntentAsync(new IntentRequest
        {
            Prompt = prompt,
            CurrentSpec = SpecDocument.Empty
        }, CancellationToken.None);

        Assert.Equal(IntentResponseKind.Mutation, outcome.Kind);
        var buffer = Assert.Single(outcome.Mutation!.NextDocument!.Compute);
        Assert.Equal("buffer", buffer.Op);
        Assert.Equal(expectedDistance, buffer.Args["distance"]);
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

    private async Task<ApplyPayload?> CollectCompletedPayloadAsync(SpecDocument document)
    {
        var jobId = Guid.NewGuid().ToString("n");
        await foreach (var evt in _client.ApplyAsync(document, jobId, CancellationToken.None))
        {
            if (evt.Kind == ApplyEventKind.Completed)
            {
                return evt.Payload;
            }
        }

        return null;
    }
}
