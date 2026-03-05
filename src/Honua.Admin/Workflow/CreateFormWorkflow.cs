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
}
