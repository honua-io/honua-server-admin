// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.PrintService;
using Honua.Admin.Services.PrintService;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class PrintServiceStateTests
{
    [Fact]
    public async Task LoadAsync_populates_templates_preview_layers_and_queue()
    {
        var state = new PrintServiceState(new StubPrintServiceClient());

        await state.LoadAsync();

        Assert.Equal(PrintServiceStatus.Ready, state.Status);
        Assert.NotEmpty(state.Templates);
        Assert.NotEmpty(state.Layers);
        Assert.NotEmpty(state.VisibleLayers);
        Assert.NotEmpty(state.Jobs);
        Assert.NotNull(state.Preview);
        Assert.Contains("/rest/services/default/MapServer/export", state.Preview.ExportUrl, StringComparison.Ordinal);
        Assert.Contains("layout=letter-portrait", state.Preview.ExportUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preview_reflects_template_layer_and_atlas_options()
    {
        var state = new PrintServiceState(new StubPrintServiceClient());
        await state.LoadAsync();

        state.SetTemplate("a3-atlas");
        state.SetBatchAtlas(true);
        foreach (var layer in state.Layers)
        {
            state.SetLayerVisibility(layer.LayerId, visible: false);
        }

        await state.RefreshPreviewAsync();

        Assert.Equal("A3 atlas", state.Preview?.TemplateName);
        Assert.Contains("No visible layers selected.", state.Preview?.Warnings ?? Array.Empty<string>());
        Assert.Empty(state.Preview?.Layers ?? Array.Empty<PrintPreviewLayer>());

        state.SetLayerVisibility("default:parcels", visible: true);
        await state.RefreshPreviewAsync();

        Assert.True(state.Preview?.PageCount > 1);
        var previewLayer = Assert.Single(state.Preview?.Layers ?? Array.Empty<PrintPreviewLayer>());
        Assert.Equal("default / Parcels", previewLayer.DisplayName);
    }

    [Fact]
    public async Task QueueExportAsync_adds_a_job_using_current_format_and_template()
    {
        var state = new PrintServiceState(new StubPrintServiceClient());
        await state.LoadAsync();

        state.SetTemplate("tabloid-landscape");
        state.SetOutputFormat("png");
        state.SetTitle("Field inspection export");
        await state.QueueExportAsync();

        var job = state.Jobs[0];
        Assert.Equal("Field inspection export", job.Title);
        Assert.Equal("Tabloid landscape", job.TemplateName);
        Assert.Equal(PrintOutputFormats.Png, job.Format);
        Assert.EndsWith(".png", job.FileName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueExportAsync_stops_when_snapshot_bootstrap_fails()
    {
        var client = new FailingSnapshotClient();
        var state = new PrintServiceState(client);

        await state.QueueExportAsync();

        Assert.Equal(PrintServiceStatus.Error, state.Status);
        Assert.Equal("snapshot failed", state.LastError);
        Assert.False(client.QueueExportCalled);
    }

    [Fact]
    public async Task ExportPreviewManifest_includes_print_request_context()
    {
        var state = new PrintServiceState(new StubPrintServiceClient());
        await state.LoadAsync();

        var payload = state.ExportPreviewManifest();

        Assert.Equal("text/plain", payload.MimeType);
        Assert.EndsWith("-preview.txt", payload.FileName, StringComparison.Ordinal);
        Assert.Contains("Honua print preview", payload.Content, StringComparison.Ordinal);
        Assert.Contains("Scale: 1:24,000", payload.Content, StringComparison.Ordinal);
        Assert.Contains("default / Parcels", payload.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshPreviewAsync_ignores_stale_preview_responses()
    {
        var stalePreview = new TaskCompletionSource<PrintPreviewDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new SequencedPrintServiceClient(
            Snapshot(DefaultLayers()),
            PreviewStep(request => Preview(request)),
            PreviewStep(_ => stalePreview.Task),
            PreviewStep(request => Preview(request)));
        var state = new PrintServiceState(client);

        await state.LoadAsync();
        state.SetTitle("Old title");
        var staleRefresh = state.RefreshPreviewAsync();
        state.SetTitle("New title");
        await state.RefreshPreviewAsync();

        stalePreview.SetResult(Preview(new PrintJobRequest { Title = "Old title", TemplateId = "letter-portrait" }));
        await staleRefresh;

        Assert.Equal("New title", state.Preview?.Title);
    }

    [Fact]
    public async Task RefreshPreviewAsync_ignores_stale_preview_failures()
    {
        var stalePreview = new TaskCompletionSource<PrintPreviewDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new SequencedPrintServiceClient(
            Snapshot(DefaultLayers()),
            PreviewStep(request => Preview(request)),
            PreviewStep(_ => stalePreview.Task),
            PreviewStep(request => Preview(request)));
        var state = new PrintServiceState(client);

        await state.LoadAsync();
        var staleRefresh = state.RefreshPreviewAsync();
        state.SetTitle("Recovered title");
        await state.RefreshPreviewAsync();

        stalePreview.SetException(new InvalidOperationException("stale preview failed"));
        await staleRefresh;

        Assert.Equal(PrintServiceStatus.Ready, state.Status);
        Assert.Null(state.LastError);
        Assert.Equal("Recovered title", state.Preview?.Title);
    }

    [Fact]
    public async Task LoadAsync_reconciles_visible_layers_when_snapshot_catalog_changes()
    {
        var client = new SequencedPrintServiceClient(
            Snapshot(DefaultLayers()),
            PreviewStep(request => Preview(request)),
            Snapshot(DefaultLayers().Skip(1).ToArray()),
            PreviewStep(request => Preview(request)));
        var state = new PrintServiceState(client);

        await state.LoadAsync();
        Assert.True(state.IsLayerVisible("default:parcels"));

        await state.LoadAsync();

        Assert.False(state.IsLayerVisible("default:parcels"));
        Assert.DoesNotContain("default:parcels", client.LastPreviewRequest?.VisibleLayerIds ?? Array.Empty<string>());
    }

    [Fact]
    public async Task LoadAsync_reselects_template_when_snapshot_catalog_changes()
    {
        var client = new SequencedPrintServiceClient(
            Snapshot(DefaultLayers(), [Template("letter-portrait", "Letter portrait")]),
            PreviewStep(request => Preview(request)),
            Snapshot(DefaultLayers(), [Template("tabloid-landscape", "Tabloid landscape")]),
            PreviewStep(request => Preview(request)));
        var state = new PrintServiceState(client);

        await state.LoadAsync();
        Assert.Equal("letter-portrait", state.TemplateId);

        await state.LoadAsync();

        Assert.Equal("tabloid-landscape", state.TemplateId);
        Assert.Equal("tabloid-landscape", client.LastPreviewRequest?.TemplateId);
    }

    private static IReadOnlyList<PrintMapLayer> DefaultLayers()
        =>
        [
            new PrintMapLayer
            {
                LayerId = "default:parcels",
                ServiceName = "default",
                LayerName = "Parcels",
                Protocol = "FeatureServer",
                VisibleByDefault = true,
            },
            new PrintMapLayer
            {
                LayerId = "planning:zoning",
                ServiceName = "planning",
                LayerName = "Zoning districts",
                Protocol = "MapServer",
                VisibleByDefault = true,
            },
        ];

    private static PrintServiceSnapshot Snapshot(
        IReadOnlyList<PrintMapLayer> layers,
        IReadOnlyList<PrintLayoutTemplate>? templates = null)
        => new()
        {
            GeneratedAt = DateTimeOffset.Parse("2026-04-25T12:00:00Z"),
            Templates = templates ?? [Template("letter-portrait", "Letter portrait")],
            Layers = layers,
        };

    private static PrintLayoutTemplate Template(string templateId, string name)
        => new()
        {
            TemplateId = templateId,
            Name = name,
            PageSize = "Letter",
            Orientation = "Portrait",
            WidthInches = 8.5,
            HeightInches = 11,
            IncludeLegendByDefault = true,
            IncludeScaleBarByDefault = true,
            IncludeNorthArrowByDefault = true,
        };

    private static PrintPreviewDocument Preview(PrintJobRequest request)
        => new()
        {
            GeneratedAt = DateTimeOffset.Parse("2026-04-25T12:00:00Z"),
            TemplateId = request.TemplateId,
            TemplateName = "Letter portrait",
            Format = request.Format,
            Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled map export" : request.Title,
            ScaleDenominator = request.ScaleDenominator,
            ExportUrl = "/rest/services/default/MapServer/export?format=pdf&layout=letter-portrait",
        };

    private static Func<PrintJobRequest, Task<PrintPreviewDocument>> PreviewStep(Func<PrintJobRequest, PrintPreviewDocument> build)
        => request => Task.FromResult(build(request));

    private static Func<PrintJobRequest, Task<PrintPreviewDocument>> PreviewStep(Func<PrintJobRequest, Task<PrintPreviewDocument>> build)
        => build;

    private sealed class SequencedPrintServiceClient : IPrintServiceClient
    {
        private readonly Queue<PrintServiceSnapshot> _snapshots = new();
        private readonly Queue<Func<PrintJobRequest, Task<PrintPreviewDocument>>> _previews = new();

        public SequencedPrintServiceClient(params object[] steps)
        {
            foreach (var step in steps)
            {
                switch (step)
                {
                    case PrintServiceSnapshot snapshot:
                        _snapshots.Enqueue(snapshot);
                        break;
                    case Func<PrintJobRequest, Task<PrintPreviewDocument>> preview:
                        _previews.Enqueue(preview);
                        break;
                }
            }
        }

        public PrintJobRequest? LastPreviewRequest { get; private set; }

        public Task<PrintServiceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult(_snapshots.Dequeue());

        public Task<PrintPreviewDocument> PreviewAsync(PrintJobRequest request, CancellationToken cancellationToken)
        {
            LastPreviewRequest = request;
            return _previews.Dequeue()(request);
        }

        public Task<PrintJobSummary> QueueExportAsync(PrintJobRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new PrintJobSummary
            {
                JobId = "print-test",
                Title = request.Title,
                TemplateName = "Letter portrait",
                Format = request.Format,
                CreatedAt = DateTimeOffset.Parse("2026-04-25T12:00:00Z"),
            });
    }

    private sealed class FailingSnapshotClient : IPrintServiceClient
    {
        public bool QueueExportCalled { get; private set; }

        public Task<PrintServiceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("snapshot failed");

        public Task<PrintPreviewDocument> PreviewAsync(PrintJobRequest request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("preview should not run");

        public Task<PrintJobSummary> QueueExportAsync(PrintJobRequest request, CancellationToken cancellationToken)
        {
            QueueExportCalled = true;
            return Task.FromResult(new PrintJobSummary());
        }
    }
}
