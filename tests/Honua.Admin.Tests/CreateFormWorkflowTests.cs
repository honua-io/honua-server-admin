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

    [Fact]
    public void CanCreateBlankForm_ReturnsTrue_WhenNameProvided()
    {
        var result = CreateFormWorkflow.CanCreateBlankForm("  Blank Form  ");

        Assert.True(result);
    }

    [Fact]
    public void CanCreateBlankForm_ReturnsFalse_WhenNameMissing()
    {
        var result = CreateFormWorkflow.CanCreateBlankForm("   ");

        Assert.False(result);
    }

    [Fact]
    public void CreateBlankForm_CreatesTemplateWithSanitizedFormId()
    {
        var form = CreateFormWorkflow.CreateBlankForm("  Damage Assessment 2026  ", "  mobile ops ");

        Assert.Equal("Damage Assessment 2026", form.Name);
        Assert.Equal("mobile ops", form.Description);
        Assert.Equal("damage-assessment-2026", form.Settings.FormId);
        Assert.Empty(form.Survey);
        Assert.Empty(form.Choices);
    }
}
