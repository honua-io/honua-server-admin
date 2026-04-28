// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;

namespace Honua.Admin.Models.PrintService;

public static class PrintOutputFormats
{
    public const string Pdf = "PDF";
    public const string Png = "PNG";

    public static IReadOnlyList<string> Options { get; } = [Pdf, Png];
}

public static class PrintJobStatuses
{
    public const string Queued = "Queued";
    public const string Rendering = "Rendering";
    public const string Complete = "Complete";
    public const string Failed = "Failed";
}

public sealed record PrintServiceSnapshot
{
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<PrintLayoutTemplate> Templates { get; init; } = Array.Empty<PrintLayoutTemplate>();
    public IReadOnlyList<PrintMapLayer> Layers { get; init; } = Array.Empty<PrintMapLayer>();
    public IReadOnlyList<PrintJobSummary> Jobs { get; init; } = Array.Empty<PrintJobSummary>();
}

public sealed record PrintLayoutTemplate
{
    public string TemplateId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PageSize { get; init; } = string.Empty;
    public string Orientation { get; init; } = string.Empty;
    public double WidthInches { get; init; }
    public double HeightInches { get; init; }
    public string RequiredEdition { get; init; } = "Community";
    public string DefaultFormat { get; init; } = PrintOutputFormats.Pdf;
    public bool SupportsAtlas { get; init; }
    public bool IncludeLegendByDefault { get; init; } = true;
    public bool IncludeScaleBarByDefault { get; init; } = true;
    public bool IncludeNorthArrowByDefault { get; init; } = true;
    public bool IncludeOverviewMapByDefault { get; init; }
}

public sealed record PrintMapLayer
{
    public string LayerId { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string LayerName { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public string GeometryType { get; init; } = string.Empty;
    public string SymbolColor { get; init; } = "#2563eb";
    public string Attribution { get; init; } = string.Empty;
    public long FeatureCount { get; init; }
    public bool VisibleByDefault { get; init; } = true;
}

public sealed record PrintJobRequest
{
    public string TemplateId { get; init; } = string.Empty;
    public string Format { get; init; } = PrintOutputFormats.Pdf;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public int ScaleDenominator { get; init; }
    public bool IncludeLegend { get; init; }
    public bool IncludeScaleBar { get; init; }
    public bool IncludeNorthArrow { get; init; }
    public bool IncludeOverviewMap { get; init; }
    public bool BatchAtlas { get; init; }
    public IReadOnlyList<string> VisibleLayerIds { get; init; } = Array.Empty<string>();
}

public sealed record PrintPreviewDocument
{
    public DateTimeOffset GeneratedAt { get; init; }
    public string TemplateId { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string Format { get; init; } = PrintOutputFormats.Pdf;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public string ExportUrl { get; init; } = string.Empty;
    public double WidthInches { get; init; }
    public double HeightInches { get; init; }
    public int ScaleDenominator { get; init; }
    public int PageCount { get; init; }
    public long EstimatedFileSizeBytes { get; init; }
    public IReadOnlyList<PrintPreviewLayer> Layers { get; init; } = Array.Empty<PrintPreviewLayer>();
    public IReadOnlyList<PrintLayoutElement> Elements { get; init; } = Array.Empty<PrintLayoutElement>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed record PrintPreviewLayer
{
    public string LayerId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Protocol { get; init; } = string.Empty;
    public string SymbolColor { get; init; } = "#2563eb";
    public long FeatureCount { get; init; }
}

public sealed record PrintLayoutElement
{
    public string Name { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
}

public sealed record PrintJobSummary
{
    public string JobId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string TemplateName { get; init; } = string.Empty;
    public string Format { get; init; } = PrintOutputFormats.Pdf;
    public string Status { get; init; } = PrintJobStatuses.Queued;
    public int PageCount { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string FileName { get; init; } = string.Empty;
}

public sealed record PrintExportPayload(
    string FileName,
    string MimeType,
    string Content);
