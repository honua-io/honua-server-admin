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

    [Fact]
    public async Task PlanAsync_marks_compute_cache_keys_and_durable_app_outputs()
    {
        var document = BuildAppScaffoldDocument();

        var plan = await _client.PlanAsync(document, CancellationToken.None);

        var aggregate = Assert.Single(plan.Nodes, node => node.Op == "aggregate");
        Assert.Equal(PlanCachePolicy.ContentHash, aggregate.CachePolicy);
        Assert.StartsWith("sha256:", aggregate.ContentHash, StringComparison.Ordinal);
        Assert.Equal(PlanMaterializationKind.Ephemeral, aggregate.Materialization);

        var output = Assert.Single(plan.Nodes, node => node.Id == "output-appscaffold");
        Assert.Equal(PlanCachePolicy.None, output.CachePolicy);
        Assert.Equal(PlanMaterializationKind.DurableApp, output.Materialization);
        Assert.StartsWith("sha256:", output.ContentHash, StringComparison.Ordinal);
        Assert.Equal(new[] { "aggregate-1" }, output.Inputs);
    }

    [Fact]
    public async Task PlanAsync_normalizes_source_dataset_casing_for_cache_hashes()
    {
        var lower = await _client.PlanAsync(new SpecDocument
        {
            Sources = new[] { new SpecSourceEntry("study", "parcels", "v1") },
        }, CancellationToken.None);
        var mixed = await _client.PlanAsync(new SpecDocument
        {
            Sources = new[] { new SpecSourceEntry("study", "Parcels", "v1") },
        }, CancellationToken.None);

        Assert.Equal(lower.Nodes[0].ContentHash, mixed.Nodes[0].ContentHash);
    }

    [Fact]
    public async Task PlanAsync_includes_source_revision_in_compute_cache_hash()
    {
        var first = await _client.PlanAsync(BuildAppScaffoldDocument("v1"), CancellationToken.None);
        var second = await _client.PlanAsync(BuildAppScaffoldDocument("v2"), CancellationToken.None);

        var firstAggregate = Assert.Single(first.Nodes, node => node.Op == "aggregate");
        var secondAggregate = Assert.Single(second.Nodes, node => node.Op == "aggregate");

        Assert.NotEqual(firstAggregate.ContentHash, secondAggregate.ContentHash);
    }

    [Fact]
    public async Task PlanAsync_includes_input_hashes_in_output_content_hash()
    {
        var first = await _client.PlanAsync(BuildAppScaffoldDocument("v1"), CancellationToken.None);
        var second = await _client.PlanAsync(BuildAppScaffoldDocument("v2"), CancellationToken.None);

        var firstOutput = Assert.Single(first.Nodes, node => node.Id == "output-appscaffold");
        var secondOutput = Assert.Single(second.Nodes, node => node.Id == "output-appscaffold");

        Assert.NotEqual(firstOutput.ContentHash, secondOutput.ContentHash);
    }

    [Fact]
    public async Task PlanAsync_includes_parameter_defaults_in_compute_cache_hash()
    {
        var first = BuildFilterDocument("Alpha");
        var second = BuildFilterDocument("Beta");

        var firstPlan = await _client.PlanAsync(first, CancellationToken.None);
        var secondPlan = await _client.PlanAsync(second, CancellationToken.None);

        var firstFilter = Assert.Single(firstPlan.Nodes, node => node.Op == "filter");
        var secondFilter = Assert.Single(secondPlan.Nodes, node => node.Op == "filter");

        Assert.NotEqual(firstFilter.ContentHash, secondFilter.ContentHash);
    }

    [Fact]
    public async Task PlanAsync_treats_subtraction_after_parameter_reference_as_expression_text()
    {
        var first = BuildFilterDocument("10", "@parcels.acreage>$limit-5", "limit");
        var second = BuildFilterDocument("20", "@parcels.acreage>$limit-5", "limit");

        var diagnostics = _client.Validate(first);
        var firstPlan = await _client.PlanAsync(first, CancellationToken.None);
        var secondPlan = await _client.PlanAsync(second, CancellationToken.None);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Code == "unknown-parameter");
        Assert.NotEqual(
            Assert.Single(firstPlan.Nodes, node => node.Op == "filter").ContentHash,
            Assert.Single(secondPlan.Nodes, node => node.Op == "filter").ContentHash);
    }

    [Fact]
    public async Task PlanAndApply_expose_parameter_bindings_for_app_scaffolds()
    {
        var document = BuildAppScaffoldDocument() with
        {
            Parameters = new[] { new SpecParameterEntry("county", "string", "Honolulu", Required: true) }
        };

        var plan = await _client.PlanAsync(document, CancellationToken.None);
        var binding = Assert.Single(plan.Parameters);

        Assert.Equal("county", binding.Name);
        Assert.Equal("string", binding.Type);
        Assert.Equal("Honolulu", binding.Default);
        Assert.True(binding.Required);
        Assert.StartsWith("sha256:", binding.ContentHash, StringComparison.Ordinal);

        var payload = await CollectCompletedPayloadAsync(document);
        var scaffoldParameter = Assert.Single(payload!.AppScaffold!.Parameters);

        Assert.Equal("county", scaffoldParameter.Name);
        Assert.Equal("Honolulu", scaffoldParameter.Default);
        Assert.Single(payload.ParameterBindings);
    }

    [Fact]
    public async Task PlanAsync_uses_output_kind_for_app_scaffold_inputs_when_map_layers_exist()
    {
        var document = BuildAppScaffoldDocument() with
        {
            Map = new SpecMap
            {
                Layers = new[] { new SpecMapLayer("parcels", "viridis") }
            }
        };

        var plan = await _client.PlanAsync(document, CancellationToken.None);

        var output = Assert.Single(plan.Nodes, node => node.Id == "output-appscaffold");
        Assert.Equal(PlanMaterializationKind.DurableApp, output.Materialization);
        Assert.Contains("aggregate-1", output.Inputs);
        Assert.Contains("map-parcels", output.Inputs);
    }

    [Fact]
    public async Task PlanAsync_uses_effective_map_output_when_analysis_output_has_map_layers()
    {
        var document = new SpecDocument
        {
            Sources = new[] { new SpecSourceEntry("parcels", "parcels", "v1") },
            Map = new SpecMap
            {
                Layers = new[] { new SpecMapLayer("parcels", "viridis") }
            },
            Output = new SpecOutput { Kind = SpecOutputKind.Analysis, Target = "preview" }
        };

        var plan = await _client.PlanAsync(document, CancellationToken.None);
        var payload = await CollectCompletedPayloadAsync(document);

        var output = Assert.Single(plan.Nodes, node => node.Id == "output-map");
        Assert.Equal(PlanMaterializationKind.PreviewOnly, output.Materialization);
        Assert.Equal(SpecOutputKind.Map, payload!.Kind);
    }

    [Fact]
    public async Task PlanAsync_skips_output_node_when_requested_map_has_no_effective_payload()
    {
        var document = new SpecDocument
        {
            Sources = new[] { new SpecSourceEntry("parcels", "parcels", "v1") },
            Output = new SpecOutput { Kind = SpecOutputKind.Map, Target = "preview" }
        };

        var plan = await _client.PlanAsync(document, CancellationToken.None);
        var payload = await CollectCompletedPayloadAsync(document);

        Assert.DoesNotContain(plan.Nodes, node => node.Op == "output");
        Assert.Equal(SpecOutputKind.None, payload!.Kind);
    }

    [Fact]
    public async Task ApplyAsync_emits_cache_keys_and_materialized_durable_outputs()
    {
        var document = BuildAppScaffoldDocument();
        var events = new List<ApplyEvent>();

        await foreach (var evt in _client.ApplyAsync(document, Guid.NewGuid().ToString("n"), CancellationToken.None))
        {
            events.Add(evt);
        }

        Assert.Contains(events, evt =>
            evt.NodeOp == "aggregate"
            && evt.Status is ApplyNodeStatus.Completed or ApplyNodeStatus.CacheHit
            && evt.CacheKey?.StartsWith("sha256:", StringComparison.Ordinal) == true);

        var output = Assert.Single(events, evt => evt.NodeId == "output-appscaffold" && evt.MaterializedResource is not null);
        Assert.Equal(PlanMaterializationKind.DurableApp, output.MaterializedResource!.Kind);
        Assert.StartsWith("sha256:", output.MaterializedResource.Version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_returns_app_scaffold_payload_when_app_output_has_map_layers()
    {
        var document = BuildAppScaffoldDocument() with
        {
            Map = new SpecMap
            {
                Layers = new[] { new SpecMapLayer("parcels", "viridis") }
            }
        };

        var payload = await CollectCompletedPayloadAsync(document);

        Assert.NotNull(payload);
        Assert.Equal(SpecOutputKind.AppScaffold, payload!.Kind);
        Assert.NotNull(payload.AppScaffold);
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

    private static SpecDocument BuildAppScaffoldDocument(string sourcePin = "v1") => new()
    {
        Sources = new[] { new SpecSourceEntry("parcels", "parcels", sourcePin) },
        Compute = new[]
        {
            new SpecComputeStep(
                "aggregate",
                new[] { "parcels" },
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["by"] = "@parcels.county",
                    ["metric"] = "count"
                })
        },
        Output = new SpecOutput { Kind = SpecOutputKind.AppScaffold, Target = "preview" }
    };

    private static SpecDocument BuildFilterDocument(
        string defaultValue,
        string where = "@parcels.county=$county",
        string parameterName = "county")
        => new()
        {
            Sources = new[] { new SpecSourceEntry("parcels", "parcels", "v1") },
            Parameters = new[] { new SpecParameterEntry(parameterName, "string", defaultValue) },
            Compute = new[]
            {
                new SpecComputeStep(
                    "filter",
                    new[] { "parcels" },
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["where"] = where
                    })
            },
            Output = new SpecOutput { Kind = SpecOutputKind.Analysis, Target = "preview" }
        };
}
