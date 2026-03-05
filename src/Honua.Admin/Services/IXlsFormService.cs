using Honua.Admin.Models;

namespace Honua.Admin.Services;

/// <summary>
/// Service for building XLSForms from layer metadata.
/// </summary>
public interface IXlsFormService
{
    /// <summary>
    /// Suggests the form field type for a catalog field.
    /// </summary>
    FormFieldSuggestion SuggestFormField(FieldDefinition field);

    /// <summary>
    /// Builds an xlsform template from a selected layer.
    /// </summary>
    Task<XlsForm> CreateFormFromLayerAsync(
        string serviceId,
        LayerDefinition layer,
        string formName,
        CancellationToken cancellationToken = default);
}
