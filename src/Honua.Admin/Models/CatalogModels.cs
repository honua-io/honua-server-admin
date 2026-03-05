namespace Honua.Admin.Models;

/// <summary>
/// Available geometry types for a layer.
/// </summary>
public enum GeometryType
{
    Unknown = 0,
    Point,
    MultiPoint,
    LineString,
    MultiLineString,
    Polygon,
    MultiPolygon
}

/// <summary>
/// Supported attribute field types.
/// </summary>
public enum FieldType
{
    String = 0,
    Integer,
    Decimal,
    Double,
    Date,
    DateTime,
    Boolean
}

/// <summary>
/// Catalog field metadata used by form generation.
/// </summary>
public sealed class FieldDefinition
{
    /// <summary>
    /// Field name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Field type.
    /// </summary>
    public FieldType Type { get; set; }

    /// <summary>
    /// Optional display alias.
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// Optional maximum length for text.
    /// </summary>
    public int? Length { get; set; }

    /// <summary>
    /// Whether the field allows null values.
    /// </summary>
    public bool IsNullable { get; set; }
}

/// <summary>
/// Layer metadata used by the form wizard.
/// </summary>
public sealed class LayerDefinition
{
    /// <summary>
    /// Layer identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Layer display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Geometry type.
    /// </summary>
    public GeometryType GeometryType { get; set; }

    /// <summary>
    /// Whether users can edit features in this layer.
    /// </summary>
    public bool IsEditable { get; set; }

    /// <summary>
    /// Attribute fields.
    /// </summary>
    public IReadOnlyList<FieldDefinition> AttributeFields { get; set; } = Array.Empty<FieldDefinition>();
}

/// <summary>
/// Service metadata used by the form wizard.
/// </summary>
public sealed class ServiceDefinition
{
    /// <summary>
    /// Service identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Service display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional service description.
    /// </summary>
    public string? Description { get; set; }
}
