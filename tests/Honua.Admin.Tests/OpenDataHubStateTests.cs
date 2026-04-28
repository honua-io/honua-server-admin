// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.OpenDataHub;
using Honua.Admin.Services.OpenDataHub;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class OpenDataHubStateTests
{
    [Fact]
    public async Task LoadAsync_populates_metrics_datasets_and_validation()
    {
        var state = new OpenDataHubState(new StubOpenDataHubClient());

        await state.LoadAsync();

        Assert.Equal(OpenDataHubStatus.Idle, state.Status);
        Assert.Contains(state.Metrics, metric => metric.Label == "Published datasets");
        Assert.Contains(state.Datasets, dataset => dataset.DatasetId == "harbor-assets");
        Assert.Equal("harbor-assets", state.SelectedDataset?.DatasetId);
        Assert.Equal("harbor-assets-public", state.SelectedDataset?.ApiAccess.PublicKeyLabel);
        Assert.Contains(state.SelectedDataset!.ApiAccess.CodeExamples, example => example.Language == OpenDataCodeLanguage.JavaScript);
        Assert.False(state.HasBlockingValidation);
    }

    [Fact]
    public async Task Filters_update_catalog_results_and_selection()
    {
        var state = new OpenDataHubState(new StubOpenDataHubClient());
        await state.LoadAsync();

        state.SetSearchText("permit");
        state.SetCategoryFilter("Permits");
        state.SetGeographyFilter("Citywide");

        var dataset = Assert.Single(state.FilteredDatasets);
        Assert.Equal("permit-activity", dataset.DatasetId);
        Assert.Equal("permit-activity", state.SelectedDataset?.DatasetId);

        state.ClearFilters();

        Assert.Equal(4, state.FilteredDatasets.Count);
        Assert.Equal("harbor-assets", state.FilteredDatasets[0].DatasetId);
    }

    [Fact]
    public async Task LoadAsync_ignores_stale_snapshot_responses()
    {
        var client = new SequencedLoadOpenDataHubClient();
        var state = new OpenDataHubState(client);

        var staleLoad = state.LoadAsync();
        var freshLoad = state.LoadAsync();
        client.CompleteLoad(1, Snapshot("fresh-dataset", "Fresh dataset"));
        await freshLoad;

        Assert.Equal("fresh-dataset", state.SelectedDataset?.DatasetId);

        client.CompleteLoad(0, Snapshot("stale-dataset", "Stale dataset"));
        await staleLoad;

        Assert.Equal("fresh-dataset", state.SelectedDataset?.DatasetId);
        Assert.Equal(OpenDataHubStatus.Idle, state.Status);
    }

    [Fact]
    public async Task PublishSelectedAsync_returns_catalog_and_docs_urls_when_valid()
    {
        var state = new OpenDataHubState(new StubOpenDataHubClient());
        await state.LoadAsync();

        await state.PublishSelectedAsync();

        Assert.Equal(OpenDataHubStatus.Published, state.Status);
        Assert.NotNull(state.LastPublish);
        Assert.Equal("https://data.honua.local/datasets/harbor-assets", state.LastPublish.CatalogUrl);
        Assert.Equal("https://data.honua.local/api/docs/harbor-assets", state.LastPublish.ApiDocsUrl);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task PublishSelectedAsync_blocks_when_dataset_fails_readiness()
    {
        var state = new OpenDataHubState(new StubOpenDataHubClient());
        await state.LoadAsync();
        state.SelectDataset("reef-monitoring");

        await state.PublishSelectedAsync();

        Assert.Equal(OpenDataHubStatus.Error, state.Status);
        Assert.Equal("Resolve validation checks before publishing.", state.LastError);
        Assert.Null(state.LastPublish);
        Assert.True(state.HasBlockingValidation);
    }

    [Fact]
    public async Task ValidationChecks_require_public_api_access_and_code_examples()
    {
        var snapshot = Snapshot("api-dataset", "API dataset");
        var dataset = snapshot.Datasets[0] with
        {
            ApiAccess = snapshot.Datasets[0].ApiAccess with
            {
                PublicKeyEnabled = false,
                CodeExamples = [],
            },
        };
        var state = new OpenDataHubState(new FixedOpenDataHubClient(snapshot with { Datasets = [dataset] }));

        await state.LoadAsync();

        var api = Assert.Single(state.ValidationChecks, check => check.Key == "api");
        Assert.False(api.Passed);
        Assert.Contains("public key access", api.Message);
        Assert.True(state.HasBlockingValidation);
    }

    [Fact]
    public async Task Mutators_ignore_changes_while_publish_is_in_flight()
    {
        var client = new BlockingPublishOpenDataHubClient();
        var state = new OpenDataHubState(client);
        await state.LoadAsync();
        var originalSelection = state.SelectedDatasetId;

        var publishTask = state.PublishSelectedAsync();
        await client.PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        state.SetSearchText("permit");
        state.SetCategoryFilter("Permits");
        state.SetGeographyFilter("Citywide");
        state.SelectDataset("permit-activity");
        state.ClearFilters();

        Assert.Equal(OpenDataHubStatus.Publishing, state.Status);
        Assert.Equal(originalSelection, state.SelectedDatasetId);
        Assert.Equal(string.Empty, state.SearchText);
        Assert.Equal("All", state.CategoryFilter);
        Assert.Equal("All", state.GeographyFilter);

        client.Complete();
        await publishTask;

        Assert.Equal(OpenDataHubStatus.Published, state.Status);
        Assert.Equal(originalSelection, client.PublishedDatasetId);
    }

    [Fact]
    public async Task PublishSelectedAsync_ignores_reentrant_publish_calls()
    {
        var client = new BlockingPublishOpenDataHubClient();
        var state = new OpenDataHubState(client);
        await state.LoadAsync();

        var firstPublish = state.PublishSelectedAsync();
        await client.PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await state.PublishSelectedAsync();

        Assert.Equal(1, client.PublishCalls);

        client.Complete();
        await firstPublish;

        Assert.Equal(OpenDataHubStatus.Published, state.Status);
    }

    [Fact]
    public async Task PublishSelectedAsync_clears_stale_publish_result_when_retry_fails()
    {
        var state = new OpenDataHubState(new FailingRetryOpenDataHubClient());
        await state.LoadAsync();
        await state.PublishSelectedAsync();
        Assert.NotNull(state.LastPublish);

        await state.PublishSelectedAsync();

        Assert.Equal(OpenDataHubStatus.Error, state.Status);
        Assert.Equal("publish failed", state.LastError);
        Assert.Null(state.LastPublish);
    }

    [Fact]
    public async Task LoadAsync_clears_stale_publish_result_when_refresh_fails()
    {
        var state = new OpenDataHubState(new FailingRefreshAfterPublishOpenDataHubClient());
        await state.LoadAsync();
        await state.PublishSelectedAsync();
        Assert.NotNull(state.LastPublish);

        await state.LoadAsync();

        Assert.Equal(OpenDataHubStatus.Error, state.Status);
        Assert.Equal("refresh failed", state.LastError);
        Assert.Null(state.LastPublish);
    }

    [Fact]
    public async Task PublishSelectedAsync_notifies_when_publish_is_canceled()
    {
        var state = new OpenDataHubState(new CancelingPublishOpenDataHubClient());
        await state.LoadAsync();
        var observedStatuses = new List<OpenDataHubStatus>();
        state.OnChanged += () => observedStatuses.Add(state.Status);

        await Assert.ThrowsAsync<OperationCanceledException>(() => state.PublishSelectedAsync());

        Assert.Equal(OpenDataHubStatus.Idle, state.Status);
        Assert.Contains(OpenDataHubStatus.Publishing, observedStatuses);
        Assert.Equal(OpenDataHubStatus.Idle, observedStatuses[^1]);
    }

    [Fact]
    public async Task StubPublishAsync_sanitizes_catalog_url_slug()
    {
        var client = new StubOpenDataHubClient();

        var result = await client.PublishAsync("Ops/2026?Launch #1", CancellationToken.None);

        Assert.Equal("https://data.honua.local/datasets/ops-2026-launch-1", result.CatalogUrl);
        Assert.Equal("https://data.honua.local/api/docs/ops-2026-launch-1", result.ApiDocsUrl);
    }

    private sealed class SequencedLoadOpenDataHubClient : IOpenDataHubClient
    {
        private readonly List<TaskCompletionSource<OpenDataHubSnapshot>> _loads = [];

        public Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            var load = new TaskCompletionSource<OpenDataHubSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            _loads.Add(load);
            return load.Task;
        }

        public Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void CompleteLoad(int index, OpenDataHubSnapshot snapshot)
            => _loads[index].SetResult(snapshot);
    }

    private sealed class FixedOpenDataHubClient : IOpenDataHubClient
    {
        private readonly OpenDataHubSnapshot _snapshot;

        public FixedOpenDataHubClient(OpenDataHubSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult(_snapshot);

        public Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken)
            => Task.FromResult(new OpenDataPublishResult
            {
                DatasetId = datasetId,
                CatalogUrl = "https://data.honua.local/datasets/test",
                ApiDocsUrl = "https://data.honua.local/api/docs/test",
                PublishedAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
                Message = "Published",
            });
    }

    private sealed class FailingRetryOpenDataHubClient : IOpenDataHubClient
    {
        private readonly StubOpenDataHubClient _inner = new();
        private int _publishCount;

        public Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => _inner.GetSnapshotAsync(cancellationToken);

        public Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken)
        {
            _publishCount++;
            if (_publishCount == 1)
            {
                return _inner.PublishAsync(datasetId, cancellationToken);
            }

            throw new InvalidOperationException("publish failed");
        }
    }

    private sealed class CancelingPublishOpenDataHubClient : IOpenDataHubClient
    {
        private readonly StubOpenDataHubClient _inner = new();

        public Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => _inner.GetSnapshotAsync(cancellationToken);

        public Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken)
            => throw new OperationCanceledException(cancellationToken);
    }

    private sealed class FailingRefreshAfterPublishOpenDataHubClient : IOpenDataHubClient
    {
        private readonly StubOpenDataHubClient _inner = new();
        private int _loadCount;

        public Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            _loadCount++;
            if (_loadCount == 1)
            {
                return _inner.GetSnapshotAsync(cancellationToken);
            }

            throw new InvalidOperationException("refresh failed");
        }

        public Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken)
            => _inner.PublishAsync(datasetId, cancellationToken);
    }

    private sealed class BlockingPublishOpenDataHubClient : IOpenDataHubClient
    {
        private readonly StubOpenDataHubClient _inner = new();
        private readonly TaskCompletionSource<OpenDataPublishResult> _publishResult =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> PublishStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? PublishedDatasetId { get; private set; }

        public int PublishCalls { get; private set; }

        public Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => _inner.GetSnapshotAsync(cancellationToken);

        public Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken)
        {
            PublishCalls++;
            PublishedDatasetId = datasetId;
            PublishStarted.TrySetResult(true);
            return _publishResult.Task;
        }

        public void Complete()
            => _publishResult.SetResult(new OpenDataPublishResult
            {
                DatasetId = PublishedDatasetId ?? "unknown",
                CatalogUrl = "https://data.honua.local/datasets/published",
                ApiDocsUrl = "https://data.honua.local/api/docs/published",
                PublishedAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
                Message = "Published",
            });
    }

    private static OpenDataHubSnapshot Snapshot(string datasetId, string title)
        => new()
        {
            Datasets =
            [
                new OpenDataDataset
                {
                    DatasetId = datasetId,
                    Title = title,
                    Description = "Public dataset",
                    Category = "Infrastructure",
                    Geography = "Harbor",
                    License = "CC BY 4.0",
                    Contact = "gis@honua.local",
                    UpdateFrequency = "Daily",
                    Status = OpenDataDatasetStatus.Published,
                    LastUpdated = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
                    PublicCatalogEnabled = true,
                    ApiEnabled = true,
                    EmbedEnabled = true,
                    StacCollectionId = $"collections/{datasetId}",
                    SampleResponse = "{}",
                    ApiAccess = new OpenDataApiAccess
                    {
                        PublicKeyEnabled = true,
                        PublicKeyLabel = $"{datasetId}-public",
                        LastRotated = DateTimeOffset.Parse("2026-04-21T12:00:00Z"),
                        AnonymousRateLimitPerMinute = 120,
                        RegisteredRateLimitPerMinute = 600,
                        BulkDownloadEnabled = true,
                        CodeExamples =
                        [
                            CodeExample(OpenDataCodeLanguage.Curl),
                            CodeExample(OpenDataCodeLanguage.JavaScript),
                            CodeExample(OpenDataCodeLanguage.Python),
                        ],
                    },
                    Keywords = ["public"],
                    Downloads =
                    [
                        Download(OpenDataDownloadFormat.GeoJson),
                        Download(OpenDataDownloadFormat.GeoParquet),
                        Download(OpenDataDownloadFormat.Shapefile),
                        Download(OpenDataDownloadFormat.Csv),
                        Download(OpenDataDownloadFormat.Kml),
                    ],
                    ApiEndpoints =
                    [
                        new OpenDataApiEndpoint
                        {
                            Name = "Features",
                            Path = "/api/open-data/features",
                            RequiresApiKey = false,
                            RateLimitPerMinute = 120,
                        },
                    ],
                    EmbedConfig = new OpenDataEmbedConfig
                    {
                        EmbedUrl = "https://data.honua.local/embed/dataset",
                        Basemap = "Light",
                        InitialExtent = "[-158,21,-157,22]",
                        BrandingMode = "White label",
                        Responsive = true,
                        WcagReady = true,
                    },
                },
            ],
        };

    private static OpenDataDownloadOption Download(OpenDataDownloadFormat format)
        => new()
        {
            Format = format,
            Url = $"/downloads/{format}",
            SizeBytes = 100,
            GeneratedAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
        };

    private static OpenDataCodeExample CodeExample(OpenDataCodeLanguage language)
        => new()
        {
            Language = language,
            Label = language.ToString(),
            Snippet = $"{language} example",
        };
}
