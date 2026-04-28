using System;
using System.Collections.Generic;

namespace Honua.Admin.Models.Annotations;

public enum AnnotationTool
{
    Select,
    Pen,
    Rectangle,
    Circle,
    Polygon,
    Arrow,
    Text,
    CommentPin,
    CommentArea
}

public enum AnnotationGeometryKind
{
    Point,
    LineString,
    Polygon
}

public enum AnnotationEdition
{
    Community,
    Pro,
    Enterprise
}

public enum AnnotationCommentStatus
{
    Open,
    Pending,
    Resolved
}

public readonly record struct AnnotationPoint(double Longitude, double Latitude);

public sealed record AnnotationStyle(
    string StrokeColor,
    string FillColor,
    double StrokeWidth,
    double FillOpacity);

public sealed record AnnotationLayer(
    Guid Id,
    string Name,
    bool IsFeatureLayer,
    bool IsVisible,
    bool IsLocked,
    int SortOrder);

public sealed record AnnotationShape(
    Guid Id,
    Guid LayerId,
    AnnotationTool Tool,
    AnnotationGeometryKind GeometryKind,
    string Title,
    IReadOnlyList<AnnotationPoint> Points,
    AnnotationStyle Style,
    string? Text,
    DateTimeOffset CreatedAt);

public sealed record AnnotationComment(
    Guid Id,
    string Author,
    string Body,
    bool IsGuest,
    DateTimeOffset CreatedAt);

public sealed record AnnotationThread(
    Guid Id,
    Guid? ShapeId,
    AnnotationTool AnchorTool,
    AnnotationPoint Anchor,
    AnnotationCommentStatus Status,
    IReadOnlyList<AnnotationComment> Comments);

public sealed record AnnotationSetSnapshot(
    Guid Id,
    string Name,
    DateTimeOffset SavedAt,
    IReadOnlyList<AnnotationLayer> Layers,
    IReadOnlyList<AnnotationShape> Shapes,
    IReadOnlyList<AnnotationThread> Threads);

public sealed record AnnotationExportPayload(
    string FileName,
    string MimeType,
    string Content);
