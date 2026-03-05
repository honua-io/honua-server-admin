using Honua.Admin.Models;
using Honua.Admin.Workflow;

namespace Honua.Admin.Tests;

public sealed class CreateFormWorkflowTests
{
    [Fact]
    public void HasSelectedLayer_AcceptsZeroIdentifier()
    {
        var layers = new[]
        {
            new LayerDefinition { Id = 0, Name = "Zero Id Layer" }
        };

        var result = CreateFormWorkflow.HasSelectedLayer(0, layers);

        Assert.True(result);
    }

    [Fact]
    public void CanCreateFromLayer_RequiresMatchingLayerInCollection()
    {
        var layers = new[]
        {
            new LayerDefinition { Id = 0, Name = "Layer A" }
        };

        var result = CreateFormWorkflow.CanCreateFromLayer(
            selectedServiceId: "svc",
            selectedLayerId: 1,
            formName: "Inspection Form",
            layers: layers);

        Assert.False(result);
    }
}
