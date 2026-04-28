// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.PrintService;

namespace Honua.Admin.Services.PrintService;

public sealed class StubPrintServiceClient : IPrintServiceClient
{
    private static readonly DateTimeOffset BaselineNow = DateTimeOffset.Parse("2026-04-25T12:00:00Z", CultureInfo.InvariantCulture);
    private int _jobSequence = 320;

    public Task<PrintServiceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new PrintServiceSnapshot
        {
            GeneratedAt = BaselineNow,
            Templates = Templates,
            Layers = Layers,
            Jobs =
            [
                new PrintJobSummary
                {
                    JobId = "print-319",
                    Title = "Planning commission packet",
                    TemplateName = "Tabloid landscape",
                    Format = PrintOutputFormats.Pdf,
                    Status = PrintJobStatuses.Complete,
                    PageCount = 12,
                    CreatedAt = BaselineNow.AddMinutes(-42),
                    CompletedAt = BaselineNow.AddMinutes(-39),
                    FileName = "planning-commission-packet.pdf",
                },
                new PrintJobSummary
                {
                    JobId = "print-318",
                    Title = "Field inspection map",
                    TemplateName = "Letter portrait",
                    Format = PrintOutputFormats.Png,
                    Status = PrintJobStatuses.Rendering,
                    PageCount = 1,
                    CreatedAt = BaselineNow.AddMinutes(-8),
                    FileName = "field-inspection-map.png",
                },
            ],
        });
    }

    public Task<PrintPreviewDocument> PreviewAsync(PrintJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(BuildPreview(request, BaselineNow));
    }

    public Task<PrintJobSummary> QueueExportAsync(PrintJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var preview = BuildPreview(request, BaselineNow);
        var jobId = Interlocked.Increment(ref _jobSequence);
        var slug = Slugify(string.IsNullOrWhiteSpace(request.Title) ? "honua-map-export" : request.Title);
        var extension = string.Equals(request.Format, PrintOutputFormats.Png, StringComparison.OrdinalIgnoreCase) ? "png" : "pdf";

        return Task.FromResult(new PrintJobSummary
        {
            JobId = string.Create(CultureInfo.InvariantCulture, $"print-{jobId}"),
            Title = preview.Title,
            TemplateName = preview.TemplateName,
            Format = preview.Format,
            Status = request.BatchAtlas ? PrintJobStatuses.Queued : PrintJobStatuses.Complete,
            PageCount = preview.PageCount,
            CreatedAt = BaselineNow.AddMinutes(jobId - 320),
            CompletedAt = request.BatchAtlas ? null : BaselineNow.AddMinutes(jobId - 320).AddSeconds(18),
            FileName = $"{slug}.{extension}",
        });
    }

    private static PrintPreviewDocument BuildPreview(PrintJobRequest request, DateTimeOffset now)
    {
        var template = Templates.FirstOrDefault(item => string.Equals(item.TemplateId, request.TemplateId, StringComparison.OrdinalIgnoreCase))
            ?? Templates[0];
        var selectedIds = request.VisibleLayerIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedLayers = Layers
            .Where(layer => selectedIds.Contains(layer.LayerId))
            .Select(layer => new PrintPreviewLayer
            {
                LayerId = layer.LayerId,
                DisplayName = $"{layer.ServiceName} / {layer.LayerName}",
                Protocol = layer.Protocol,
                SymbolColor = layer.SymbolColor,
                FeatureCount = layer.FeatureCount,
            })
            .ToArray();
        var elements = BuildElements(request).ToArray();
        var warnings = BuildWarnings(request, selectedLayers, template).ToArray();
        var pageCount = request.BatchAtlas ? Math.Max(1, selectedLayers.Length * 3) : 1;

        return new PrintPreviewDocument
        {
            GeneratedAt = now,
            TemplateId = template.TemplateId,
            TemplateName = template.Name,
            Format = NormalizeFormat(request.Format),
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled map export" : request.Title.Trim(),
            Subtitle = request.Subtitle.Trim(),
            Author = string.IsNullOrWhiteSpace(request.Author) ? "Honua operator" : request.Author.Trim(),
            ExportUrl = BuildExportUrl(template, request),
            WidthInches = template.WidthInches,
            HeightInches = template.HeightInches,
            ScaleDenominator = request.ScaleDenominator,
            PageCount = pageCount,
            EstimatedFileSizeBytes = EstimateBytes(template, selectedLayers.Length, elements.Length, pageCount, request.Format),
            Layers = selectedLayers,
            Elements = elements,
            Warnings = warnings,
        };
    }

    private static IEnumerable<PrintLayoutElement> BuildElements(PrintJobRequest request)
    {
        yield return new PrintLayoutElement { Name = "Map frame", Position = "center" };
        yield return new PrintLayoutElement { Name = "Title block", Position = "top" };
        if (request.IncludeLegend)
        {
            yield return new PrintLayoutElement { Name = "Legend", Position = "right" };
        }

        if (request.IncludeScaleBar)
        {
            yield return new PrintLayoutElement { Name = "Scale bar", Position = "bottom-left" };
        }

        if (request.IncludeNorthArrow)
        {
            yield return new PrintLayoutElement { Name = "North arrow", Position = "top-right" };
        }

        if (request.IncludeOverviewMap)
        {
            yield return new PrintLayoutElement { Name = "Overview map", Position = "bottom-right" };
        }
    }

    private static IEnumerable<string> BuildWarnings(PrintJobRequest request, IReadOnlyCollection<PrintPreviewLayer> selectedLayers, PrintLayoutTemplate template)
    {
        if (selectedLayers.Count == 0)
        {
            yield return "No visible layers selected.";
        }

        if (request.BatchAtlas && !template.SupportsAtlas)
        {
            yield return "Selected template does not support atlas output.";
        }

        if (request.ScaleDenominator < 500)
        {
            yield return "Scale denominator is unusually detailed for server-side rendering.";
        }
    }

    private static string BuildExportUrl(PrintLayoutTemplate template, PrintJobRequest request)
    {
        var format = string.Equals(request.Format, PrintOutputFormats.Png, StringComparison.OrdinalIgnoreCase) ? "png" : "pdf";
        var layout = Uri.EscapeDataString(template.TemplateId);
        return $"/rest/services/default/MapServer/export?format={format}&layout={layout}";
    }

    private static long EstimateBytes(PrintLayoutTemplate template, int layerCount, int elementCount, int pageCount, string format)
    {
        var formatMultiplier = string.Equals(format, PrintOutputFormats.Png, StringComparison.OrdinalIgnoreCase) ? 1.4 : 1d;
        var pageArea = template.WidthInches * template.HeightInches;
        return (long)Math.Round((96_000 + pageArea * 1_800 + layerCount * 44_000 + elementCount * 8_500) * pageCount * formatMultiplier);
    }

    private static string NormalizeFormat(string format)
        => string.Equals(format, PrintOutputFormats.Png, StringComparison.OrdinalIgnoreCase)
            ? PrintOutputFormats.Png
            : PrintOutputFormats.Pdf;

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var compact = string.Join("-", new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(compact) ? "honua-map-export" : compact;
    }

    private static IReadOnlyList<PrintLayoutTemplate> Templates { get; } =
    [
        new PrintLayoutTemplate
        {
            TemplateId = "letter-portrait",
            Name = "Letter portrait",
            PageSize = "Letter",
            Orientation = "Portrait",
            WidthInches = 8.5,
            HeightInches = 11,
            RequiredEdition = "Community",
            DefaultFormat = PrintOutputFormats.Pdf,
            SupportsAtlas = false,
            IncludeOverviewMapByDefault = false,
        },
        new PrintLayoutTemplate
        {
            TemplateId = "tabloid-landscape",
            Name = "Tabloid landscape",
            PageSize = "Tabloid",
            Orientation = "Landscape",
            WidthInches = 17,
            HeightInches = 11,
            RequiredEdition = "Community",
            DefaultFormat = PrintOutputFormats.Pdf,
            SupportsAtlas = true,
            IncludeOverviewMapByDefault = true,
        },
        new PrintLayoutTemplate
        {
            TemplateId = "a3-atlas",
            Name = "A3 atlas",
            PageSize = "A3",
            Orientation = "Landscape",
            WidthInches = 16.5,
            HeightInches = 11.7,
            RequiredEdition = "Pro",
            DefaultFormat = PrintOutputFormats.Pdf,
            SupportsAtlas = true,
            IncludeOverviewMapByDefault = true,
        },
    ];

    private static IReadOnlyList<PrintMapLayer> Layers { get; } =
    [
        new PrintMapLayer
        {
            LayerId = "default:parcels",
            ServiceName = "default",
            LayerName = "Parcels",
            Protocol = "FeatureServer",
            GeometryType = "Polygon",
            SymbolColor = "#2563eb",
            Attribution = "County cadastral source",
            FeatureCount = 184_220,
        },
        new PrintMapLayer
        {
            LayerId = "planning:zoning",
            ServiceName = "planning",
            LayerName = "Zoning districts",
            Protocol = "MapServer",
            GeometryType = "Polygon",
            SymbolColor = "#16a34a",
            Attribution = "Planning department",
            FeatureCount = 2_918,
        },
        new PrintMapLayer
        {
            LayerId = "imagery:orthophoto",
            ServiceName = "imagery",
            LayerName = "Orthophoto mosaic",
            Protocol = "ImageServer",
            GeometryType = "Raster",
            SymbolColor = "#64748b",
            Attribution = "State imagery program",
            FeatureCount = 1,
        },
        new PrintMapLayer
        {
            LayerId = "basemap:addresses",
            ServiceName = "basemap",
            LayerName = "Address points",
            Protocol = "OData",
            GeometryType = "Point",
            SymbolColor = "#dc2626",
            Attribution = "Address authority",
            FeatureCount = 72_104,
            VisibleByDefault = false,
        },
    ];
}
