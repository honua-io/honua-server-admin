using Honua.Admin.Models;

namespace Honua.Admin.Workflow;

/// <summary>
/// Helper methods for null-safe preview rendering.
/// </summary>
public static class FormPreviewWorkflow
{
    /// <summary>
    /// Returns survey questions that should be rendered in preview.
    /// </summary>
    public static IReadOnlyList<XlsFormSurveyRow> GetRenderableQuestions(XlsForm? form)
    {
        if (form?.Survey is null || form.Survey.Count == 0)
        {
            return Array.Empty<XlsFormSurveyRow>();
        }

        return form.Survey
            .Where(question => !IsGroupBoundary(question?.Type))
            .OfType<XlsFormSurveyRow>()
            .ToArray();
    }

    /// <summary>
    /// Counts required questions.
    /// </summary>
    public static int GetRequiredQuestionCount(XlsForm? form)
    {
        if (form?.Survey is null || form.Survey.Count == 0)
        {
            return 0;
        }

        return form.Survey.Count(question =>
            string.Equals(question.Required, "yes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGroupBoundary(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return false;
        }

        return type.StartsWith("begin_", StringComparison.OrdinalIgnoreCase)
               || type.StartsWith("end_", StringComparison.OrdinalIgnoreCase);
    }
}
