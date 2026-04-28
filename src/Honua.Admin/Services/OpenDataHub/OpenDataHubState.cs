using Honua.Admin.Models.OpenDataHub;

namespace Honua.Admin.Services.OpenDataHub;

public sealed class OpenDataHubState
{
    private readonly IOpenDataHubClient _client;
    private int _loadRequestVersion;

    public OpenDataHubState(IOpenDataHubClient client)
    {
        _client = client;
    }

    public OpenDataHubStatus Status { get; private set; } = OpenDataHubStatus.Idle;

    public string? LastError { get; private set; }

    public IReadOnlyList<OpenDataDataset> Datasets { get; private set; } = Array.Empty<OpenDataDataset>();

    public IReadOnlyList<OpenDataHubMetric> Metrics { get; private set; } = Array.Empty<OpenDataHubMetric>();

    public string SearchText { get; private set; } = string.Empty;

    public string CategoryFilter { get; private set; } = "All";

    public string GeographyFilter { get; private set; } = "All";

    public string? SelectedDatasetId { get; private set; }

    public OpenDataPublishResult? LastPublish { get; private set; }

    public OpenDataDataset? SelectedDataset
    {
        get
        {
            var filtered = FilteredDatasets;
            return filtered.FirstOrDefault(dataset => string.Equals(dataset.DatasetId, SelectedDatasetId, StringComparison.OrdinalIgnoreCase)) ??
                filtered.FirstOrDefault();
        }
    }

    public IReadOnlyList<OpenDataDataset> FilteredDatasets =>
        Datasets
            .Where(MatchesSearch)
            .Where(MatchesCategory)
            .Where(MatchesGeography)
            .OrderByDescending(dataset => dataset.Status == OpenDataDatasetStatus.Published)
            .ThenBy(dataset => dataset.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<string> CategoryOptions =>
        ["All", .. Datasets.Select(dataset => dataset.Category).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    public IReadOnlyList<string> GeographyOptions =>
        ["All", .. Datasets.Select(dataset => dataset.Geography).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    public IReadOnlyList<OpenDataValidationCheck> ValidationChecks
    {
        get
        {
            var dataset = SelectedDataset;
            if (dataset is null)
            {
                return
                [
                    new OpenDataValidationCheck
                    {
                        Key = "dataset",
                        Label = "Dataset",
                        Passed = false,
                        Message = "Select a dataset before publishing."
                    }
                ];
            }

            var hasMetadata = HasText(dataset.Title) &&
                HasText(dataset.Description) &&
                HasText(dataset.Category) &&
                HasText(dataset.Geography) &&
                HasText(dataset.License) &&
                HasText(dataset.Contact) &&
                HasText(dataset.UpdateFrequency);
            var hasDownloads = RequiredDownloadFormats.All(format => dataset.Downloads.Any(download => download.Format == format));
            var hasApis = dataset.ApiEnabled &&
                dataset.ApiEndpoints.Any(endpoint => !endpoint.RequiresApiKey) &&
                HasText(dataset.StacCollectionId) &&
                HasText(dataset.SampleResponse);
            var hasEmbed = !dataset.EmbedEnabled ||
                dataset.EmbedConfig.Responsive &&
                dataset.EmbedConfig.WcagReady &&
                HasText(dataset.EmbedConfig.EmbedUrl) &&
                HasText(dataset.EmbedConfig.InitialExtent);
            var canPublish = dataset.Status != OpenDataDatasetStatus.Blocked && dataset.PublicCatalogEnabled;

            return
            [
                new OpenDataValidationCheck
                {
                    Key = "metadata",
                    Label = "Catalog metadata",
                    Passed = hasMetadata,
                    Message = hasMetadata ? $"{dataset.License} license, {dataset.UpdateFrequency.ToLowerInvariant()} updates." : "Title, description, category, geography, license, contact, and update frequency are required."
                },
                new OpenDataValidationCheck
                {
                    Key = "downloads",
                    Label = "Download formats",
                    Passed = hasDownloads,
                    Message = hasDownloads ? "GeoJSON, GeoParquet, Shapefile, CSV, and KML exports are available." : "All required download formats must be generated."
                },
                new OpenDataValidationCheck
                {
                    Key = "api",
                    Label = "Civic tech API",
                    Passed = hasApis,
                    Message = hasApis ? $"{dataset.ApiEndpoints.Count} endpoint(s), including no-auth public access." : "Enable public API endpoints, STAC metadata, and a sample response."
                },
                new OpenDataValidationCheck
                {
                    Key = "embed",
                    Label = "Embed readiness",
                    Passed = hasEmbed,
                    Message = hasEmbed ? "Responsive embed configuration is ready." : "Embed viewers need responsive and WCAG-ready configuration."
                },
                new OpenDataValidationCheck
                {
                    Key = "workflow",
                    Label = "Publishing workflow",
                    Passed = canPublish,
                    Message = canPublish ? $"{dataset.Status} is clear for catalog publishing." : "Dataset must be catalog-enabled and unblocked."
                }
            ];
        }
    }

    public bool HasBlockingValidation => ValidationChecks.Any(check => !check.Passed);

    public event Action? OnChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var loadVersion = Interlocked.Increment(ref _loadRequestVersion);
        Status = OpenDataHubStatus.Loading;
        LastError = null;
        LastPublish = null;
        Notify();

        try
        {
            var snapshot = await _client.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (!IsCurrentLoad(loadVersion))
            {
                return;
            }

            Datasets = snapshot.Datasets;
            Metrics = snapshot.Metrics;
            SelectedDatasetId = Datasets.Any(dataset => string.Equals(dataset.DatasetId, SelectedDatasetId, StringComparison.OrdinalIgnoreCase))
                ? SelectedDatasetId
                : Datasets.FirstOrDefault()?.DatasetId;
            LastPublish = null;
            Status = OpenDataHubStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentLoad(loadVersion))
            {
                Status = OpenDataHubStatus.Idle;
                Notify();
            }

            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (!IsCurrentLoad(loadVersion))
            {
                return;
            }

            Status = OpenDataHubStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public void SelectDataset(string datasetId)
    {
        if (IsLocked || !Datasets.Any(dataset => string.Equals(dataset.DatasetId, datasetId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        SelectedDatasetId = datasetId;
        ResetTransientPublishState();
        Notify();
    }

    public void SetSearchText(string? searchText)
    {
        if (IsLocked)
        {
            return;
        }

        SearchText = searchText?.Trim() ?? string.Empty;
        AlignSelectionWithFilters();
        Notify();
    }

    public void SetCategoryFilter(string? category)
    {
        if (IsLocked)
        {
            return;
        }

        CategoryFilter = NormalizeOption(category, CategoryOptions);
        AlignSelectionWithFilters();
        Notify();
    }

    public void SetGeographyFilter(string? geography)
    {
        if (IsLocked)
        {
            return;
        }

        GeographyFilter = NormalizeOption(geography, GeographyOptions);
        AlignSelectionWithFilters();
        Notify();
    }

    public void ClearFilters()
    {
        if (IsLocked)
        {
            return;
        }

        SearchText = string.Empty;
        CategoryFilter = "All";
        GeographyFilter = "All";
        AlignSelectionWithFilters();
        Notify();
    }

    public async Task PublishSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (Status is OpenDataHubStatus.Loading or OpenDataHubStatus.Publishing)
        {
            return;
        }

        var dataset = SelectedDataset;
        if (dataset is null || HasBlockingValidation)
        {
            Status = OpenDataHubStatus.Error;
            LastError = "Resolve validation checks before publishing.";
            LastPublish = null;
            Notify();
            return;
        }

        Status = OpenDataHubStatus.Publishing;
        LastError = null;
        LastPublish = null;
        Notify();

        try
        {
            LastPublish = await _client.PublishAsync(dataset.DatasetId, cancellationToken).ConfigureAwait(false);
            Datasets = Datasets
                .Select(item => string.Equals(item.DatasetId, dataset.DatasetId, StringComparison.OrdinalIgnoreCase)
                    ? item with { Status = OpenDataDatasetStatus.Published, PublicCatalogEnabled = true }
                    : item)
                .ToArray();
            SelectedDatasetId = dataset.DatasetId;
            Status = OpenDataHubStatus.Published;
        }
        catch (OperationCanceledException)
        {
            Status = OpenDataHubStatus.Idle;
            Notify();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = OpenDataHubStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    private static readonly OpenDataDownloadFormat[] RequiredDownloadFormats =
    [
        OpenDataDownloadFormat.GeoJson,
        OpenDataDownloadFormat.GeoParquet,
        OpenDataDownloadFormat.Shapefile,
        OpenDataDownloadFormat.Csv,
        OpenDataDownloadFormat.Kml
    ];

    private bool MatchesSearch(OpenDataDataset dataset)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return Contains(dataset.Title, SearchText) ||
            Contains(dataset.Description, SearchText) ||
            Contains(dataset.Category, SearchText) ||
            Contains(dataset.Geography, SearchText) ||
            dataset.Keywords.Any(keyword => Contains(keyword, SearchText));
    }

    private bool MatchesCategory(OpenDataDataset dataset)
        => IsAll(CategoryFilter) || string.Equals(dataset.Category, CategoryFilter, StringComparison.OrdinalIgnoreCase);

    private bool MatchesGeography(OpenDataDataset dataset)
        => IsAll(GeographyFilter) || string.Equals(dataset.Geography, GeographyFilter, StringComparison.OrdinalIgnoreCase);

    private void AlignSelectionWithFilters()
    {
        var filtered = FilteredDatasets;
        if (filtered.Any(dataset => string.Equals(dataset.DatasetId, SelectedDatasetId, StringComparison.OrdinalIgnoreCase)))
        {
            ResetTransientPublishState();
            return;
        }

        SelectedDatasetId = filtered.FirstOrDefault()?.DatasetId;
        ResetTransientPublishState();
    }

    private void ResetTransientPublishState()
    {
        LastPublish = null;
        LastError = null;
        if (Status is OpenDataHubStatus.Error or OpenDataHubStatus.Published)
        {
            Status = OpenDataHubStatus.Idle;
        }
    }

    private static string NormalizeOption(string? option, IReadOnlyList<string> allowedOptions)
    {
        if (string.IsNullOrWhiteSpace(option))
        {
            return "All";
        }

        return allowedOptions.FirstOrDefault(value => string.Equals(value, option.Trim(), StringComparison.OrdinalIgnoreCase)) ?? "All";
    }

    private static bool Contains(string value, string searchText)
        => value.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private static bool HasText(string value)
        => !string.IsNullOrWhiteSpace(value);

    private static bool IsAll(string value)
        => string.Equals(value, "All", StringComparison.OrdinalIgnoreCase);

    private bool IsCurrentLoad(int loadVersion) => loadVersion == Volatile.Read(ref _loadRequestVersion);

    private bool IsLocked => Status is OpenDataHubStatus.Loading or OpenDataHubStatus.Publishing;

    private void Notify() => OnChanged?.Invoke();
}
