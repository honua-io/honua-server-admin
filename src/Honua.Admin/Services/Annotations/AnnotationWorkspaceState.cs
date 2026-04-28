using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using Honua.Admin.Models.Annotations;

namespace Honua.Admin.Services.Annotations;

public sealed class AnnotationWorkspaceState
{
    private static readonly AnnotationPoint MapCenter = new(-157.8583, 21.3069);
    private readonly List<AnnotationLayer> _layers = new();
    private readonly List<AnnotationShape> _shapes = new();
    private readonly List<AnnotationThread> _threads = new();
    private readonly List<AnnotationSetSnapshot> _savedSets = new();
    private int _draftOrdinal;

    public AnnotationWorkspaceState()
    {
        SeedWorkspace();
    }

    public IReadOnlyList<AnnotationLayer> Layers => _layers;

    public IReadOnlyList<AnnotationShape> Shapes => _shapes;

    public IReadOnlyList<AnnotationThread> Threads => _threads;

    public IReadOnlyList<AnnotationSetSnapshot> SavedSets => _savedSets;

    public AnnotationTool ActiveTool { get; private set; } = AnnotationTool.Select;

    public AnnotationEdition ActiveEdition { get; private set; } = AnnotationEdition.Enterprise;

    public AnnotationStyle Style { get; private set; } = new("#0f766e", "#99f6e4", 2, 0.24);

    public Guid ActiveLayerId { get; private set; }

    public Guid? SelectedShapeId { get; private set; }

    public Guid? SelectedThreadId { get; private set; }

    public string CurrentSetName { get; private set; } = "Waikiki field review";

    public AnnotationExportPayload? LastExport { get; private set; }

    public string? LastError { get; private set; }

    public event Action? OnChanged;

    public IEnumerable<AnnotationShape> VisibleShapes =>
        from shape in _shapes
        join layer in _layers on shape.LayerId equals layer.Id
        where layer.IsVisible
        orderby layer.SortOrder, shape.CreatedAt
        select shape;

    public int CommentCount => _threads.Sum(thread => thread.Comments.Count);

    public int CommentLimit => ActiveEdition == AnnotationEdition.Community ? 5 : int.MaxValue;

    public bool AllowsGuestComments => ActiveEdition is AnnotationEdition.Pro or AnnotationEdition.Enterprise;

    public bool AllowsThreadedComments => ActiveEdition is AnnotationEdition.Pro or AnnotationEdition.Enterprise;

    public bool AllowsModeration => ActiveEdition == AnnotationEdition.Enterprise;

    public bool AllowsPdfExport => ActiveEdition == AnnotationEdition.Enterprise;

    public AnnotationLayer? ActiveLayer => _layers.FirstOrDefault(layer => layer.Id == ActiveLayerId);

    public AnnotationShape? SelectedShape => SelectedShapeId is Guid id
        ? _shapes.FirstOrDefault(shape => shape.Id == id)
        : null;

    public void SelectTool(AnnotationTool tool)
    {
        ActiveTool = tool;
        LastError = null;
        Notify();
    }

    public void SetEdition(AnnotationEdition edition)
    {
        ActiveEdition = edition;
        LastError = null;
        Notify();
    }

    public void SetStrokeColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return;
        }
        Style = Style with { StrokeColor = color };
        Notify();
    }

    public void SetFillColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return;
        }
        Style = Style with { FillColor = color };
        Notify();
    }

    public void SetStrokeWidth(double value)
    {
        Style = Style with { StrokeWidth = Math.Clamp(value, 1, 8) };
        Notify();
    }

    public void SetFillOpacity(double value)
    {
        Style = Style with { FillOpacity = Math.Clamp(value, 0, 1) };
        Notify();
    }

    public void SelectLayer(Guid layerId)
    {
        var layer = _layers.FirstOrDefault(candidate => candidate.Id == layerId);
        if (layer is null || layer.IsFeatureLayer)
        {
            return;
        }
        ActiveLayerId = layerId;
        LastError = null;
        Notify();
    }

    public void ToggleLayerVisibility(Guid layerId)
    {
        var index = _layers.FindIndex(layer => layer.Id == layerId);
        if (index < 0)
        {
            return;
        }
        var layer = _layers[index];
        _layers[index] = layer with { IsVisible = !layer.IsVisible };
        Notify();
    }

    public void ToggleLayerLock(Guid layerId)
    {
        var index = _layers.FindIndex(layer => layer.Id == layerId);
        if (index < 0 || _layers[index].IsFeatureLayer)
        {
            return;
        }
        var layer = _layers[index];
        _layers[index] = layer with { IsLocked = !layer.IsLocked };
        Notify();
    }

    public Guid AddLayer(string? name)
    {
        var normalized = string.IsNullOrWhiteSpace(name)
            ? $"Annotation layer {_layers.Count(layer => !layer.IsFeatureLayer) + 1}"
            : name.Trim();
        var layer = new AnnotationLayer(Guid.NewGuid(), normalized, false, true, false, _layers.Count);
        _layers.Add(layer);
        ActiveLayerId = layer.Id;
        LastError = null;
        Notify();
        return layer.Id;
    }

    public AnnotationShape? PlaceDraftAnnotation()
    {
        if (ActiveTool is AnnotationTool.Select or AnnotationTool.CommentPin or AnnotationTool.CommentArea)
        {
            LastError = "Choose a drawing tool before placing an annotation.";
            Notify();
            return null;
        }

        var layer = ActiveLayer;
        if (layer is null || layer.IsFeatureLayer || layer.IsLocked)
        {
            LastError = "Select an unlocked annotation layer before placing an annotation.";
            Notify();
            return null;
        }

        var shape = BuildDraftShape(layer.Id, ActiveTool, ++_draftOrdinal);
        _shapes.Add(shape);
        SelectedShapeId = shape.Id;
        LastError = null;
        Notify();
        return shape;
    }

    public void SelectShape(Guid shapeId)
    {
        if (_shapes.Any(shape => shape.Id == shapeId))
        {
            SelectedShapeId = shapeId;
            LastError = null;
            Notify();
        }
    }

    public AnnotationThread? AddComment(string? body, bool guest)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            LastError = "Enter a comment before posting.";
            Notify();
            return null;
        }

        if (guest && !AllowsGuestComments)
        {
            LastError = "Guest comments require Pro or Enterprise.";
            Notify();
            return null;
        }

        if (CommentCount >= CommentLimit)
        {
            LastError = "Community workspaces allow up to 5 comments per map.";
            Notify();
            return null;
        }

        var selectedShape = SelectedShape;
        var anchor = selectedShape?.Points.FirstOrDefault() ?? MapCenter;
        var comment = new AnnotationComment(
            Guid.NewGuid(),
            guest ? "Guest reviewer" : "Map owner",
            body.Trim(),
            guest,
            DateTimeOffset.UtcNow);
        var status = guest && AllowsModeration ? AnnotationCommentStatus.Pending : AnnotationCommentStatus.Open;
        var thread = new AnnotationThread(
            Guid.NewGuid(),
            selectedShape?.Id,
            ActiveTool == AnnotationTool.CommentArea ? AnnotationTool.CommentArea : AnnotationTool.CommentPin,
            anchor,
            status,
            new[] { comment });
        _threads.Add(thread);
        SelectedThreadId = thread.Id;
        LastError = null;
        Notify();
        return thread;
    }

    public void AddReply(Guid threadId, string? body)
    {
        if (!AllowsThreadedComments)
        {
            LastError = "Threaded replies require Pro or Enterprise.";
            Notify();
            return;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            LastError = "Enter a reply before posting.";
            Notify();
            return;
        }

        var index = _threads.FindIndex(thread => thread.Id == threadId);
        if (index < 0)
        {
            return;
        }

        var reply = new AnnotationComment(
            Guid.NewGuid(),
            "Map owner",
            body.Trim(),
            false,
            DateTimeOffset.UtcNow);
        var thread = _threads[index];
        _threads[index] = thread with { Comments = thread.Comments.Concat(new[] { reply }).ToArray() };
        SelectedThreadId = threadId;
        LastError = null;
        Notify();
    }

    public void ApproveThread(Guid threadId)
    {
        if (!AllowsModeration)
        {
            LastError = "Guest approval requires Enterprise.";
            Notify();
            return;
        }
        UpdateThreadStatus(threadId, AnnotationCommentStatus.Open);
    }

    public void ResolveThread(Guid threadId) => UpdateThreadStatus(threadId, AnnotationCommentStatus.Resolved);

    public AnnotationSetSnapshot SaveCurrentSet(string? name)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? CurrentSetName : name.Trim();
        CurrentSetName = normalized;
        var snapshot = new AnnotationSetSnapshot(
            Guid.NewGuid(),
            normalized,
            DateTimeOffset.UtcNow,
            _layers.Select(CloneLayer).ToArray(),
            _shapes.Select(CloneShape).ToArray(),
            _threads.Select(CloneThread).ToArray());

        _savedSets.RemoveAll(set => string.Equals(set.Name, normalized, StringComparison.OrdinalIgnoreCase));
        _savedSets.Insert(0, snapshot);
        LastError = null;
        Notify();
        return snapshot;
    }

    public void LoadSet(Guid setId)
    {
        var snapshot = _savedSets.FirstOrDefault(set => set.Id == setId);
        if (snapshot is null)
        {
            return;
        }

        CurrentSetName = snapshot.Name;
        _layers.Clear();
        _layers.AddRange(snapshot.Layers.Select(CloneLayer));
        _shapes.Clear();
        _shapes.AddRange(snapshot.Shapes.Select(CloneShape));
        _threads.Clear();
        _threads.AddRange(snapshot.Threads.Select(CloneThread));
        ActiveLayerId = _layers.FirstOrDefault(layer => !layer.IsFeatureLayer)?.Id ?? Guid.Empty;
        SelectedShapeId = _shapes.FirstOrDefault()?.Id;
        SelectedThreadId = _threads.FirstOrDefault()?.Id;
        LastError = null;
        Notify();
    }

    public AnnotationExportPayload ExportGeoJson()
    {
        var payload = new AnnotationExportPayload(
            SafeFileName(CurrentSetName, "geojson"),
            "application/geo+json",
            BuildGeoJson());
        LastExport = payload;
        LastError = null;
        Notify();
        return payload;
    }

    public AnnotationExportPayload ExportSvg()
    {
        var payload = new AnnotationExportPayload(
            SafeFileName(CurrentSetName, "svg"),
            "image/svg+xml",
            BuildSvg());
        LastExport = payload;
        LastError = null;
        Notify();
        return payload;
    }

    public AnnotationExportPayload? ExportPdf()
    {
        if (!AllowsPdfExport)
        {
            LastError = "PDF review packets require Enterprise.";
            Notify();
            return null;
        }

        var payload = new AnnotationExportPayload(
            SafeFileName(CurrentSetName, "pdf"),
            "application/pdf",
            BuildPdf());
        LastExport = payload;
        LastError = null;
        Notify();
        return payload;
    }

    private void SeedWorkspace()
    {
        var referenceLayer = new AnnotationLayer(Guid.NewGuid(), "Feature data", true, true, true, 0);
        var reviewLayer = new AnnotationLayer(Guid.NewGuid(), "Field review annotations", false, true, false, 1);
        var ownerLayer = new AnnotationLayer(Guid.NewGuid(), "Owner comments", false, true, false, 2);
        _layers.Add(referenceLayer);
        _layers.Add(reviewLayer);
        _layers.Add(ownerLayer);
        ActiveLayerId = reviewLayer.Id;

        _shapes.Add(new AnnotationShape(
            Guid.NewGuid(),
            reviewLayer.Id,
            AnnotationTool.Polygon,
            AnnotationGeometryKind.Polygon,
            "Permit review area",
            new[]
            {
                new AnnotationPoint(-157.873, 21.300),
                new AnnotationPoint(-157.858, 21.299),
                new AnnotationPoint(-157.854, 21.311),
                new AnnotationPoint(-157.869, 21.314),
                new AnnotationPoint(-157.873, 21.300)
            },
            new AnnotationStyle("#0f766e", "#99f6e4", 2, 0.25),
            null,
            DateTimeOffset.UtcNow.AddMinutes(-22)));
        _shapes.Add(new AnnotationShape(
            Guid.NewGuid(),
            ownerLayer.Id,
            AnnotationTool.Text,
            AnnotationGeometryKind.Point,
            "Owner note",
            new[] { new AnnotationPoint(-157.862, 21.306) },
            new AnnotationStyle("#7c3aed", "#ddd6fe", 2, 0.18),
            "Confirm access",
            DateTimeOffset.UtcNow.AddMinutes(-17)));

        SelectedShapeId = _shapes[0].Id;
        _threads.Add(new AnnotationThread(
            Guid.NewGuid(),
            _shapes[0].Id,
            AnnotationTool.CommentArea,
            _shapes[0].Points[0],
            AnnotationCommentStatus.Open,
            new[]
            {
                new AnnotationComment(
                    Guid.NewGuid(),
                    "Map owner",
                    "Check utility easement before publishing.",
                    false,
                    DateTimeOffset.UtcNow.AddMinutes(-15))
            }));
        SelectedThreadId = _threads[0].Id;
        SaveCurrentSet(CurrentSetName);
    }

    private AnnotationShape BuildDraftShape(Guid layerId, AnnotationTool tool, int ordinal)
    {
        var offset = ordinal * 0.004;
        var points = tool switch
        {
            AnnotationTool.Pen => new[]
            {
                new AnnotationPoint(-157.884 + offset, 21.305 + offset),
                new AnnotationPoint(-157.876 + offset, 21.310 + offset),
                new AnnotationPoint(-157.868 + offset, 21.307 + offset)
            },
            AnnotationTool.Rectangle => new[]
            {
                new AnnotationPoint(-157.878 + offset, 21.296 + offset),
                new AnnotationPoint(-157.864 + offset, 21.296 + offset),
                new AnnotationPoint(-157.864 + offset, 21.307 + offset),
                new AnnotationPoint(-157.878 + offset, 21.307 + offset),
                new AnnotationPoint(-157.878 + offset, 21.296 + offset)
            },
            AnnotationTool.Circle => BuildCircle(-157.864 + offset, 21.309 + offset, 0.006),
            AnnotationTool.Polygon => new[]
            {
                new AnnotationPoint(-157.888 + offset, 21.318 + offset),
                new AnnotationPoint(-157.872 + offset, 21.322 + offset),
                new AnnotationPoint(-157.863 + offset, 21.314 + offset),
                new AnnotationPoint(-157.879 + offset, 21.311 + offset),
                new AnnotationPoint(-157.888 + offset, 21.318 + offset)
            },
            AnnotationTool.Arrow => new[]
            {
                new AnnotationPoint(-157.886 + offset, 21.296 + offset),
                new AnnotationPoint(-157.868 + offset, 21.304 + offset)
            },
            AnnotationTool.Text => new[] { new AnnotationPoint(-157.856 + offset, 21.315 + offset) },
            _ => new[] { MapCenter }
        };

        var geometry = tool switch
        {
            AnnotationTool.Pen or AnnotationTool.Arrow => AnnotationGeometryKind.LineString,
            AnnotationTool.Text => AnnotationGeometryKind.Point,
            _ => AnnotationGeometryKind.Polygon
        };

        return new AnnotationShape(
            Guid.NewGuid(),
            layerId,
            tool,
            geometry,
            $"{ToolLabel(tool)} {ordinal}",
            points,
            Style,
            tool == AnnotationTool.Text ? "Label" : null,
            DateTimeOffset.UtcNow);
    }

    private static AnnotationPoint[] BuildCircle(double centerLongitude, double centerLatitude, double radius)
    {
        var points = new List<AnnotationPoint>();
        for (var i = 0; i <= 20; i++)
        {
            var radians = i * Math.Tau / 20;
            points.Add(new AnnotationPoint(
                centerLongitude + Math.Cos(radians) * radius,
                centerLatitude + Math.Sin(radians) * radius));
        }
        return points.ToArray();
    }

    private void UpdateThreadStatus(Guid threadId, AnnotationCommentStatus status)
    {
        var index = _threads.FindIndex(thread => thread.Id == threadId);
        if (index < 0)
        {
            return;
        }
        _threads[index] = _threads[index] with { Status = status };
        SelectedThreadId = threadId;
        LastError = null;
        Notify();
    }

    private string BuildGeoJson()
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "FeatureCollection");
            writer.WriteStartArray("features");
            foreach (var shape in VisibleShapes)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "Feature");
                writer.WritePropertyName("geometry");
                WriteGeometry(writer, shape);
                writer.WriteStartObject("properties");
                writer.WriteString("id", shape.Id);
                writer.WriteString("title", shape.Title);
                writer.WriteString("tool", shape.Tool.ToString());
                writer.WriteString("layer", _layers.First(layer => layer.Id == shape.LayerId).Name);
                writer.WriteString("stroke", shape.Style.StrokeColor);
                writer.WriteString("fill", shape.Style.FillColor);
                writer.WriteNumber("strokeWidth", shape.Style.StrokeWidth);
                writer.WriteNumber("fillOpacity", shape.Style.FillOpacity);
                writer.WriteNumber("comments", _threads.Count(thread => thread.ShapeId == shape.Id));
                if (!string.IsNullOrWhiteSpace(shape.Text))
                {
                    writer.WriteString("text", shape.Text);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteGeometry(Utf8JsonWriter writer, AnnotationShape shape)
    {
        writer.WriteStartObject();
        writer.WriteString("type", shape.GeometryKind.ToString());
        writer.WritePropertyName("coordinates");
        if (shape.GeometryKind == AnnotationGeometryKind.Point)
        {
            WritePoint(writer, shape.Points.FirstOrDefault());
        }
        else if (shape.GeometryKind == AnnotationGeometryKind.LineString)
        {
            writer.WriteStartArray();
            foreach (var point in shape.Points)
            {
                WritePoint(writer, point);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteStartArray();
            writer.WriteStartArray();
            foreach (var point in EnsureClosed(shape.Points))
            {
                WritePoint(writer, point);
            }
            writer.WriteEndArray();
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    private static void WritePoint(Utf8JsonWriter writer, AnnotationPoint point)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(point.Longitude);
        writer.WriteNumberValue(point.Latitude);
        writer.WriteEndArray();
    }

    private string BuildSvg()
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 800 500" role="img" aria-label="Annotation export">""");
        sb.AppendLine("""  <rect width="800" height="500" fill="#eef4f1" />""");
        sb.AppendLine("""  <path d="M80 360 C210 260 320 310 440 220 S650 180 720 110" fill="none" stroke="#7c8f88" stroke-width="10" stroke-opacity="0.35" />""");
        foreach (var shape in VisibleShapes)
        {
            var stroke = EscapeXml(shape.Style.StrokeColor);
            var fill = EscapeXml(shape.Style.FillColor);
            var strokeWidth = shape.Style.StrokeWidth.ToString("0.##", CultureInfo.InvariantCulture);
            var opacity = shape.Style.FillOpacity.ToString("0.##", CultureInfo.InvariantCulture);
            if (shape.GeometryKind == AnnotationGeometryKind.Point)
            {
                var point = shape.Points.FirstOrDefault();
                var (x, y) = Project(point);
                sb.Append(CultureInfo.InvariantCulture, $"  <circle cx=\"{x:0.##}\" cy=\"{y:0.##}\" r=\"8\" fill=\"{fill}\" fill-opacity=\"{opacity}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />\n");
                if (!string.IsNullOrWhiteSpace(shape.Text))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"  <text x=\"{x + 12:0.##}\" y=\"{y + 4:0.##}\" font-family=\"Arial\" font-size=\"14\" fill=\"#1f2937\">{EscapeXml(shape.Text)}</text>\n");
                }
            }
            else
            {
                var points = string.Join(" ", shape.Points.Select(point =>
                {
                    var (x, y) = Project(point);
                    return string.Create(CultureInfo.InvariantCulture, $"{x:0.##},{y:0.##}");
                }));
                if (shape.GeometryKind == AnnotationGeometryKind.Polygon)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  <polygon points=\"{points}\" fill=\"{fill}\" fill-opacity=\"{opacity}\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" />");
                }
                else
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  <polyline points=\"{points}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
                }
            }
        }
        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private string BuildPdf()
    {
        var lines = new List<string>
        {
            $"Annotation set: {CurrentSetName}",
            $"Shapes: {VisibleShapes.Count()}",
            $"Threads: {_threads.Count}",
            $"Saved: {DateTimeOffset.UtcNow:u}"
        };
        lines.AddRange(VisibleShapes.Take(12).Select(shape => $"{shape.Title} - {ToolLabel(shape.Tool)}"));

        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 14 Tf");
        content.AppendLine("72 742 Td");
        foreach (var line in lines)
        {
            content.Append(CultureInfo.InvariantCulture, $"({EscapePdf(line)}) Tj\n");
            content.AppendLine("0 -20 Td");
        }
        content.AppendLine("ET");

        return BuildSimplePdf(content.ToString());
    }

    private static string BuildSimplePdf(string content)
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}endstream"
        };

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n");
        sb.AppendLine("0000000000 65535 f ");
        for (var i = 1; i < offsets.Count; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{offsets[i]:0000000000} 00000 n ");
        }
        sb.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");
        return sb.ToString();
    }

    private static IEnumerable<AnnotationPoint> EnsureClosed(IReadOnlyList<AnnotationPoint> points)
    {
        if (points.Count == 0)
        {
            yield break;
        }
        foreach (var point in points)
        {
            yield return point;
        }
        var first = points[0];
        var last = points[^1];
        if (!first.Equals(last))
        {
            yield return first;
        }
    }

    private static (double X, double Y) Project(AnnotationPoint point)
    {
        const double minLongitude = -157.91;
        const double maxLongitude = -157.83;
        const double minLatitude = 21.285;
        const double maxLatitude = 21.335;
        var x = (point.Longitude - minLongitude) / (maxLongitude - minLongitude) * 800;
        var y = (maxLatitude - point.Latitude) / (maxLatitude - minLatitude) * 500;
        return (Math.Clamp(x, 24, 776), Math.Clamp(y, 24, 476));
    }

    private static AnnotationLayer CloneLayer(AnnotationLayer layer) => layer with { };

    private static AnnotationShape CloneShape(AnnotationShape shape) =>
        shape with { Points = shape.Points.ToArray(), Style = shape.Style with { } };

    private static AnnotationThread CloneThread(AnnotationThread thread) =>
        thread with { Comments = thread.Comments.ToArray() };

    private static string ToolLabel(AnnotationTool tool) => tool switch
    {
        AnnotationTool.CommentPin => "Comment pin",
        AnnotationTool.CommentArea => "Comment area",
        _ => tool.ToString()
    };

    private static string SafeFileName(string value, string extension)
    {
        var name = new string(value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray())
            .Trim('-');
        return $"{(string.IsNullOrWhiteSpace(name) ? "annotations" : name)}.{extension}";
    }

    private static string EscapeXml(string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal);

    private static string EscapePdf(string? value)
        => (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);

    private void Notify() => OnChanged?.Invoke();
}
