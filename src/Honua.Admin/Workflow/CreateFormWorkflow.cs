using Honua.Admin.Models;

namespace Honua.Admin.Workflow;

/// <summary>
/// Helper methods for create-form state checks.
/// </summary>
public static class CreateFormWorkflow
{
    /// <summary>
    /// Returns true when selected layer id exists in available layers.
    /// </summary>
    public static bool HasSelectedLayer(int? selectedLayerId, IReadOnlyCollection<LayerDefinition> layers)
    {
        if (!selectedLayerId.HasValue || layers.Count == 0)
        {
            return false;
        }

        var layerId = selectedLayerId.Value;
        return layers.Any(layer => layer.Id == layerId);
    }

    /// <summary>
    /// Returns true when all required inputs for create-from-layer are present.
    /// </summary>
    public static bool CanCreateFromLayer(
        string? selectedServiceId,
        int? selectedLayerId,
        string? formName,
        IReadOnlyCollection<LayerDefinition> layers)
    {
        return !string.IsNullOrWhiteSpace(selectedServiceId)
               && !string.IsNullOrWhiteSpace(formName)
               && HasSelectedLayer(selectedLayerId, layers);
    }

    /// <summary>
    /// Finds selected layer if available.
    /// </summary>
    public static LayerDefinition? TryGetSelectedLayer(int? selectedLayerId, IReadOnlyCollection<LayerDefinition> layers)
    {
        if (!selectedLayerId.HasValue || layers.Count == 0)
        {
            return null;
        }

        var layerId = selectedLayerId.Value;
        return layers.FirstOrDefault(layer => layer.Id == layerId);
    }

    /// <summary>
    /// Returns true when all required inputs for blank form creation are present.
    /// </summary>
    public static bool CanCreateBlankForm(string? formName)
    {
        return !string.IsNullOrWhiteSpace(formName);
    }

    /// <summary>
    /// Creates a minimal blank form model for manual design workflows.
    /// </summary>
    public static XlsForm CreateBlankForm(string formName, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formName);

        var normalizedName = formName.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        return new XlsForm
        {
            Name = normalizedName,
            Description = normalizedDescription,
            Version = "1.0.0",
            Settings = new XlsFormSettings
            {
                FormId = SanitizeFormId(normalizedName)
            },
            Survey = Array.Empty<XlsFormSurveyRow>(),
            Choices = Array.Empty<XlsFormChoiceRow>(),
            XFormsXml = null
        };
    }

    private static string SanitizeFormId(string formName)
    {
        var chars = formName
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var compacted = new string(chars);
        while (compacted.Contains("--", StringComparison.Ordinal))
        {
            compacted = compacted.Replace("--", "-", StringComparison.Ordinal);
        }

        compacted = compacted.Trim('-');
        return string.IsNullOrWhiteSpace(compacted) ? "form" : compacted;
    }
}
