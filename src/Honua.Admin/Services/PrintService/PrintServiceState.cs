// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.PrintService;

namespace Honua.Admin.Services.PrintService;

public enum PrintServiceStatus
{
    Idle,
    Loading,
    Ready,
    Error
}

public sealed class PrintServiceState
{
    private readonly IPrintServiceClient _client;
    private readonly HashSet<string> _visibleLayerIds = new(StringComparer.OrdinalIgnoreCase);
    private int _loadVersion;
    private int _previewVersion;

    public PrintServiceState(IPrintServiceClient client)
    {
        _client = client;
    }

    public PrintServiceStatus Status { get; private set; } = PrintServiceStatus.Idle;

    public string? LastError { get; private set; }

    public PrintServiceSnapshot? Snapshot { get; private set; }

    public PrintPreviewDocument? Preview { get; private set; }

    public string TemplateId { get; private set; } = string.Empty;

    public string OutputFormat { get; private set; } = PrintOutputFormats.Pdf;

    public string Title { get; private set; } = "Planning commission map";

    public string Subtitle { get; private set; } = "Parcels, zoning, and imagery context";

    public string Author { get; private set; } = "Honua operator";

    public int ScaleDenominator { get; private set; } = 24000;

    public bool IncludeLegend { get; private set; } = true;

    public bool IncludeScaleBar { get; private set; } = true;

    public bool IncludeNorthArrow { get; private set; } = true;

    public bool IncludeOverviewMap { get; private set; }

    public bool BatchAtlas { get; private set; }

    public event Action? OnChanged;

    public bool IsLoading => Status == PrintServiceStatus.Loading;

    public IReadOnlyList<PrintLayoutTemplate> Templates => Snapshot?.Templates ?? Array.Empty<PrintLayoutTemplate>();

    public IReadOnlyList<PrintMapLayer> Layers => Snapshot?.Layers ?? Array.Empty<PrintMapLayer>();

    public IReadOnlyList<PrintMapLayer> VisibleLayers
        => Layers.Where(layer => _visibleLayerIds.Contains(layer.LayerId)).ToArray();

    public IReadOnlyList<PrintJobSummary> Jobs { get; private set; } = Array.Empty<PrintJobSummary>();

    public PrintLayoutTemplate? SelectedTemplate
        => Templates.FirstOrDefault(template => string.Equals(template.TemplateId, TemplateId, StringComparison.OrdinalIgnoreCase));

    public int VisibleLayerCount => _visibleLayerIds.Count;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _loadVersion);
        Status = PrintServiceStatus.Loading;
        LastError = null;
        Notify();

        try
        {
            var snapshot = await _client.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (version != Volatile.Read(ref _loadVersion))
            {
                return;
            }

            Snapshot = snapshot;
            Jobs = snapshot.Jobs.OrderByDescending(job => job.CreatedAt).ToArray();
            ApplyInitialSelection();
            var previewVersion = Interlocked.Increment(ref _previewVersion);
            var preview = await _client.PreviewAsync(BuildRequest(), cancellationToken).ConfigureAwait(false);
            if (version != Volatile.Read(ref _loadVersion) ||
                previewVersion != Volatile.Read(ref _previewVersion))
            {
                return;
            }

            Preview = preview;
            Status = PrintServiceStatus.Ready;
        }
        catch (OperationCanceledException)
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                Status = PrintServiceStatus.Idle;
            }

            throw;
        }
        catch (Exception ex)
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                Status = PrintServiceStatus.Error;
                LastError = ex.Message;
            }
        }
        finally
        {
            if (version == Volatile.Read(ref _loadVersion))
            {
                Notify();
            }
        }
    }

    public async Task RefreshPreviewAsync(CancellationToken cancellationToken = default)
    {
        if (Snapshot is null)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var version = Interlocked.Increment(ref _previewVersion);
        Status = PrintServiceStatus.Loading;
        LastError = null;
        Notify();

        try
        {
            var preview = await _client.PreviewAsync(BuildRequest(), cancellationToken).ConfigureAwait(false);
            if (version != Volatile.Read(ref _previewVersion))
            {
                return;
            }

            Preview = preview;
            Status = PrintServiceStatus.Ready;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (version == Volatile.Read(ref _previewVersion))
            {
                Status = PrintServiceStatus.Error;
                LastError = ex.Message;
            }
        }
        finally
        {
            if (version == Volatile.Read(ref _previewVersion))
            {
                Notify();
            }
        }
    }

    public async Task QueueExportAsync(CancellationToken cancellationToken = default)
    {
        if (Snapshot is null)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (Snapshot is null)
            {
                return;
            }
        }

        var version = Interlocked.Increment(ref _previewVersion);
        Status = PrintServiceStatus.Loading;
        LastError = null;
        Notify();

        try
        {
            var request = BuildRequest();
            var job = await _client.QueueExportAsync(request, cancellationToken).ConfigureAwait(false);
            Jobs = Jobs.Prepend(job).ToArray();
            var preview = await _client.PreviewAsync(request, cancellationToken).ConfigureAwait(false);
            if (version != Volatile.Read(ref _previewVersion))
            {
                return;
            }

            Preview = preview;
            Status = PrintServiceStatus.Ready;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (version == Volatile.Read(ref _previewVersion))
            {
                Status = PrintServiceStatus.Error;
                LastError = ex.Message;
            }
        }
        finally
        {
            if (version == Volatile.Read(ref _previewVersion))
            {
                Notify();
            }
        }
    }

    public void SetTemplate(string? templateId)
    {
        var template = Templates.FirstOrDefault(item => string.Equals(item.TemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            ?? Templates.FirstOrDefault();
        if (template is null)
        {
            return;
        }

        TemplateId = template.TemplateId;
        OutputFormat = template.DefaultFormat;
        IncludeLegend = template.IncludeLegendByDefault;
        IncludeScaleBar = template.IncludeScaleBarByDefault;
        IncludeNorthArrow = template.IncludeNorthArrowByDefault;
        IncludeOverviewMap = template.IncludeOverviewMapByDefault;
        if (!template.SupportsAtlas)
        {
            BatchAtlas = false;
        }

        Notify();
    }

    public void SetOutputFormat(string? format)
    {
        OutputFormat = PrintOutputFormats.Options.FirstOrDefault(option => string.Equals(option, format, StringComparison.OrdinalIgnoreCase))
            ?? PrintOutputFormats.Pdf;
        Notify();
    }

    public void SetTitle(string? title)
    {
        Title = string.IsNullOrWhiteSpace(title) ? "Untitled map export" : title;
        Notify();
    }

    public void SetSubtitle(string? subtitle)
    {
        Subtitle = subtitle ?? string.Empty;
        Notify();
    }

    public void SetAuthor(string? author)
    {
        Author = string.IsNullOrWhiteSpace(author) ? "Honua operator" : author;
        Notify();
    }

    public void SetScaleDenominator(int value)
    {
        ScaleDenominator = Math.Clamp(value, 100, 10_000_000);
        Notify();
    }

    public void SetIncludeLegend(bool value)
    {
        IncludeLegend = value;
        Notify();
    }

    public void SetIncludeScaleBar(bool value)
    {
        IncludeScaleBar = value;
        Notify();
    }

    public void SetIncludeNorthArrow(bool value)
    {
        IncludeNorthArrow = value;
        Notify();
    }

    public void SetIncludeOverviewMap(bool value)
    {
        IncludeOverviewMap = value;
        Notify();
    }

    public void SetBatchAtlas(bool value)
    {
        BatchAtlas = value && SelectedTemplate?.SupportsAtlas == true;
        Notify();
    }

    public void SetLayerVisibility(string layerId, bool visible)
    {
        if (visible)
        {
            _visibleLayerIds.Add(layerId);
        }
        else
        {
            _visibleLayerIds.Remove(layerId);
        }

        Notify();
    }

    public bool IsLayerVisible(string layerId) => _visibleLayerIds.Contains(layerId);

    public PrintExportPayload ExportPreviewManifest()
    {
        if (Preview is null)
        {
            throw new InvalidOperationException("Print preview has not loaded.");
        }

        var builder = new StringBuilder();
        builder.AppendLine("Honua print preview");
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Generated: {Preview.GeneratedAt:u}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Title: {Preview.Title}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Template: {Preview.TemplateName}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Format: {Preview.Format}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Scale: 1:{Preview.ScaleDenominator:N0}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Pages: {Preview.PageCount:N0}"));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Export URL: {Preview.ExportUrl}"));
        builder.AppendLine("Layers:");
        foreach (var layer in Preview.Layers)
        {
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"- {layer.DisplayName} ({layer.Protocol})"));
        }

        var slug = Slugify(Preview.Title);
        return new PrintExportPayload($"{slug}-preview.txt", "text/plain", builder.ToString());
    }

    private void ApplyInitialSelection()
    {
        if (string.IsNullOrWhiteSpace(TemplateId) ||
            !Templates.Any(template => string.Equals(template.TemplateId, TemplateId, StringComparison.OrdinalIgnoreCase)))
        {
            SetTemplate(Templates.FirstOrDefault()?.TemplateId);
        }

        var validLayerIds = Layers.Select(layer => layer.LayerId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _visibleLayerIds.RemoveWhere(layerId => !validLayerIds.Contains(layerId));

        if (_visibleLayerIds.Count == 0)
        {
            foreach (var layer in Layers.Where(layer => layer.VisibleByDefault))
            {
                _visibleLayerIds.Add(layer.LayerId);
            }
        }
    }

    private PrintJobRequest BuildRequest()
        => new()
        {
            TemplateId = TemplateId,
            Format = OutputFormat,
            Title = Title,
            Subtitle = Subtitle,
            Author = Author,
            ScaleDenominator = ScaleDenominator,
            IncludeLegend = IncludeLegend,
            IncludeScaleBar = IncludeScaleBar,
            IncludeNorthArrow = IncludeNorthArrow,
            IncludeOverviewMap = IncludeOverviewMap,
            BatchAtlas = BatchAtlas,
            VisibleLayerIds = _visibleLayerIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
        };

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

    private void Notify() => OnChanged?.Invoke();
}
