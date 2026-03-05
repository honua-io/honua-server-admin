using Honua.Admin.Models;

namespace Honua.Admin.Services;

/// <summary>
/// Default XLSForm generation service.
/// </summary>
public sealed class XlsFormService : IXlsFormService
{
    /// <inheritdoc />
    public FormFieldSuggestion SuggestFormField(FieldDefinition field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var suggestedType = field.Type switch
        {
            FieldType.Integer => "integer",
            FieldType.Decimal => "decimal",
            FieldType.Double => "decimal",
            FieldType.Date => "date",
            FieldType.DateTime => "date",
            FieldType.Boolean => "select_one",
            _ => "text"
        };

        return new FormFieldSuggestion { SuggestedType = suggestedType };
    }

    /// <inheritdoc />
    public Task<XlsForm> CreateFormFromLayerAsync(
        string serviceId,
        LayerDefinition layer,
        string formName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentNullException.ThrowIfNull(layer);
        ArgumentException.ThrowIfNullOrWhiteSpace(formName);

        var survey = layer.AttributeFields.Select(field =>
        {
            var suggestion = SuggestFormField(field);
            return new XlsFormSurveyRow
            {
                Type = suggestion.SuggestedType,
                Name = field.Name.ToLowerInvariant(),
                Label = string.IsNullOrWhiteSpace(field.Alias) ? field.Name : field.Alias,
                Required = field.IsNullable ? "no" : "yes",
                Hint = field.IsNullable ? "Optional field" : "Required field"
            };
        }).ToArray();

        var form = new XlsForm
        {
            Name = formName,
            Description = layer.Description,
            Version = "1.0.0",
            Settings = new XlsFormSettings
            {
                FormId = $"{serviceId}-{layer.Id}-{SanitizeFormId(formName)}"
            },
            Survey = survey,
            XFormsXml = null
        };

        return Task.FromResult(form);
    }

    private static string SanitizeFormId(string formName)
    {
        var chars = formName
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var compacted = new string(chars).Replace("--", "-", StringComparison.Ordinal);
        return compacted.Trim('-');
    }
}
