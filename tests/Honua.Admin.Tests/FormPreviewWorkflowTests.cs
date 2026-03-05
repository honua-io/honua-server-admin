using Honua.Admin.Models;
using Honua.Admin.Workflow;

namespace Honua.Admin.Tests;

public sealed class FormPreviewWorkflowTests
{
    [Fact]
    public void GetRenderableQuestions_ReturnsEmptyList_WhenFormIsNull()
    {
        var questions = FormPreviewWorkflow.GetRenderableQuestions(null);

        Assert.Empty(questions);
    }

    [Fact]
    public void GetRenderableQuestions_FiltersGroupBoundaryRows()
    {
        var form = new XlsForm
        {
            Survey =
            [
                new XlsFormSurveyRow { Type = "begin_group", Name = "grp_start" },
                new XlsFormSurveyRow { Type = "text", Name = "field_1" },
                new XlsFormSurveyRow { Type = "END_REPEAT", Name = "grp_end" },
                new XlsFormSurveyRow { Type = "integer", Name = "field_2" }
            ]
        };

        var questions = FormPreviewWorkflow.GetRenderableQuestions(form);

        Assert.Collection(
            questions,
            question => Assert.Equal("field_1", question.Name),
            question => Assert.Equal("field_2", question.Name));
    }

    [Fact]
    public void GetRequiredQuestionCount_IsCaseInsensitive()
    {
        var form = new XlsForm
        {
            Survey =
            [
                new XlsFormSurveyRow { Name = "field_1", Required = "YES" },
                new XlsFormSurveyRow { Name = "field_2", Required = "no" }
            ]
        };

        var requiredCount = FormPreviewWorkflow.GetRequiredQuestionCount(form);

        Assert.Equal(1, requiredCount);
    }
}
