namespace Honua.Admin.Models;

/// <summary>
/// Root XLSForm model used by preview/deploy workflows.
/// </summary>
public sealed class XlsForm
{
    /// <summary>
    /// Form name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional form description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Form version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Settings block.
    /// </summary>
    public XlsFormSettings Settings { get; set; } = new();

    /// <summary>
    /// Survey rows.
    /// </summary>
    public IReadOnlyList<XlsFormSurveyRow> Survey { get; set; } = Array.Empty<XlsFormSurveyRow>();

    /// <summary>
    /// Choice rows used by select questions.
    /// </summary>
    public IReadOnlyList<XlsFormChoiceRow> Choices { get; set; } = Array.Empty<XlsFormChoiceRow>();

    /// <summary>
    /// Generated xforms XML.
    /// </summary>
    public string? XFormsXml { get; set; }
}

/// <summary>
/// XLSForm settings.
/// </summary>
public sealed class XlsFormSettings
{
    /// <summary>
    /// OpenRosa form identifier.
    /// </summary>
    public string FormId { get; set; } = string.Empty;
}

/// <summary>
/// Survey row model.
/// </summary>
public sealed class XlsFormSurveyRow
{
    /// <summary>
    /// Question type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Question name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Question label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Required flag ("yes"/"no").
    /// </summary>
    public string? Required { get; set; }

    /// <summary>
    /// Optional helper text.
    /// </summary>
    public string? Hint { get; set; }

    /// <summary>
    /// Choice list name for select types.
    /// </summary>
    public string? Choice { get; set; }
}

/// <summary>
/// Choice row model.
/// </summary>
public sealed class XlsFormChoiceRow
{
    /// <summary>
    /// Choice list group name.
    /// </summary>
    public string ListName { get; set; } = string.Empty;

    /// <summary>
    /// Choice value.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Choice display label.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Preview metadata.
/// </summary>
public sealed class FormPreview
{
    /// <summary>
    /// Preview URL for mobile access.
    /// </summary>
    public string? PreviewUrl { get; set; }

    /// <summary>
    /// Validation results.
    /// </summary>
    public IReadOnlyList<FormValidationResult> ValidationResults { get; set; } = Array.Empty<FormValidationResult>();
}

/// <summary>
/// Validation severity levels.
/// </summary>
public enum FormValidationSeverity
{
    Info = 0,
    Warning,
    Error
}

/// <summary>
/// Validation result.
/// </summary>
public sealed class FormValidationResult
{
    /// <summary>
    /// Severity.
    /// </summary>
    public FormValidationSeverity Severity { get; set; }

    /// <summary>
    /// Message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional field name.
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// Optional remediation suggestion.
    /// </summary>
    public string? Suggestion { get; set; }
}

/// <summary>
/// Analytics payload.
/// </summary>
public sealed class FormAnalytics
{
    /// <summary>
    /// Total submission count.
    /// </summary>
    public int TotalSubmissions { get; set; }

    /// <summary>
    /// Submission count for current day.
    /// </summary>
    public int SubmissionsToday { get; set; }

    /// <summary>
    /// Submission count for current week.
    /// </summary>
    public int SubmissionsThisWeek { get; set; }

    /// <summary>
    /// Average form completion time in seconds.
    /// </summary>
    public double AverageCompletionTime { get; set; }

    /// <summary>
    /// Most active devices.
    /// </summary>
    public IReadOnlyList<string> MostActiveDevices { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Latest submission timestamp.
    /// </summary>
    public DateTime? LastSubmission { get; set; }

    /// <summary>
    /// Completion percentages by field.
    /// </summary>
    public IReadOnlyDictionary<string, int> FieldCompletionRates { get; set; } = new Dictionary<string, int>();
}

/// <summary>
/// Suggested XLSForm question shape for a catalog field.
/// </summary>
public sealed class FormFieldSuggestion
{
    /// <summary>
    /// Suggested XLSForm question type.
    /// </summary>
    public string SuggestedType { get; init; } = "text";
}

/// <summary>
/// Result of deployment operation.
/// </summary>
public sealed class FormDeploymentResult
{
    /// <summary>
    /// True when deployment was accepted by backend.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// User-facing status message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional deployment identifier.
    /// </summary>
    public string? DeploymentId { get; init; }

    /// <summary>
    /// Constructs a successful result.
    /// </summary>
    public static FormDeploymentResult Success(string message, string? deploymentId = null)
    {
        return new FormDeploymentResult
        {
            Succeeded = true,
            Message = message,
            DeploymentId = deploymentId
        };
    }

    /// <summary>
    /// Constructs a failed result.
    /// </summary>
    public static FormDeploymentResult Failure(string message)
    {
        return new FormDeploymentResult
        {
            Succeeded = false,
            Message = message
        };
    }
}
