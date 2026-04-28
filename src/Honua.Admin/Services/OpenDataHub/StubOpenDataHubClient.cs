using System.Text;
using Honua.Admin.Models.OpenDataHub;

namespace Honua.Admin.Services.OpenDataHub;

public sealed class StubOpenDataHubClient : IOpenDataHubClient
{
    public Task<OpenDataHubSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        => Task.FromResult(new OpenDataHubSnapshot
        {
            Metrics =
            [
                new OpenDataHubMetric
                {
                    Label = "Published datasets",
                    Value = "18",
                    Detail = "12 refreshed this week"
                },
                new OpenDataHubMetric
                {
                    Label = "Downloads",
                    Value = "24.8k",
                    Detail = "GeoJSON and CSV lead demand"
                },
                new OpenDataHubMetric
                {
                    Label = "API calls",
                    Value = "1.3M",
                    Detail = "Rate limits active on all keys"
                },
                new OpenDataHubMetric
                {
                    Label = "Feedback queue",
                    Value = "7",
                    Detail = "3 quality reports need triage"
                }
            ],
            Datasets =
            [
                Dataset(
                    "harbor-assets",
                    "Harbor assets",
                    "Authoritative public inventory of docks, terminals, bollards, lights, and safety equipment.",
                    "Infrastructure",
                    "Honolulu Harbor",
                    OpenDataDatasetStatus.Published,
                    true,
                    true,
                    true,
                    ["harbor", "assets", "operations"],
                    "CC BY 4.0",
                    "gis@honua.local",
                    "Daily",
                    "collections/harbor-assets",
                    "[-157.91,21.28,-157.84,21.34]",
                    "Light civic",
                    "{ \"type\": \"FeatureCollection\", \"features\": [{ \"id\": \"dock-14\", \"properties\": { \"asset_type\": \"Dock\" } }] }"),
                Dataset(
                    "coastal-resilience-zones",
                    "Coastal resilience zones",
                    "Planning overlays for inundation exposure, resilience projects, and community adaptation areas.",
                    "Planning",
                    "Oahu",
                    OpenDataDatasetStatus.InReview,
                    true,
                    true,
                    false,
                    ["resilience", "planning", "coast"],
                    "ODC-BY",
                    "planning@honua.local",
                    "Monthly",
                    "collections/coastal-resilience-zones",
                    "[-158.28,21.25,-157.64,21.72]",
                    "Topo",
                    "{ \"type\": \"FeatureCollection\", \"features\": [{ \"id\": \"zone-7\", \"properties\": { \"priority\": \"High\" } }] }"),
                Dataset(
                    "permit-activity",
                    "Permit activity",
                    "Daily building permit feed with status, valuation band, neighborhood, and inspection counts.",
                    "Permits",
                    "Citywide",
                    OpenDataDatasetStatus.Scheduled,
                    true,
                    false,
                    true,
                    ["permits", "inspections", "buildings"],
                    "Public domain",
                    "permits@honua.local",
                    "Daily",
                    "collections/permit-activity",
                    "[-158.12,21.26,-157.64,21.55]",
                    "Streets",
                    "{ \"records\": [{ \"permit_id\": \"B2026-1042\", \"status\": \"Issued\" }] }",
                    DateTimeOffset.Parse("2026-04-29T18:00:00Z")),
                Dataset(
                    "reef-monitoring",
                    "Reef monitoring observations",
                    "Research observations with survey locations, temperature, turbidity, and species presence.",
                    "Environment",
                    "South Shore",
                    OpenDataDatasetStatus.Blocked,
                    false,
                    true,
                    false,
                    ["reef", "water quality", "biology"],
                    "CC BY-NC 4.0",
                    "environment@honua.local",
                    "Weekly",
                    "collections/reef-monitoring",
                    "[-158.05,21.24,-157.72,21.31]",
                    "Satellite",
                    "{ \"observations\": [{ \"station\": \"SS-04\", \"turbidity\": 2.4 }] }")
            ]
        });

    public Task<OpenDataPublishResult> PublishAsync(string datasetId, CancellationToken cancellationToken)
        => Task.FromResult(new OpenDataPublishResult
        {
            DatasetId = datasetId,
            CatalogUrl = $"https://data.honua.local/datasets/{Slug(datasetId)}",
            ApiDocsUrl = $"https://data.honua.local/api/docs/{Slug(datasetId)}",
            PublishedAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
            Message = $"{datasetId} is published to the open data catalog."
        });

    private static OpenDataDataset Dataset(
        string datasetId,
        string title,
        string description,
        string category,
        string geography,
        OpenDataDatasetStatus status,
        bool publicCatalogEnabled,
        bool apiEnabled,
        bool embedEnabled,
        IReadOnlyList<string> keywords,
        string license,
        string contact,
        string updateFrequency,
        string stacCollectionId,
        string extent,
        string basemap,
        string sampleResponse,
        DateTimeOffset? scheduledPublishAt = null)
    {
        var basePath = $"/open-data/{Slug(datasetId)}";
        var slug = Slug(datasetId);
        return new OpenDataDataset
        {
            DatasetId = datasetId,
            Title = title,
            Description = description,
            Category = category,
            Geography = geography,
            License = license,
            Contact = contact,
            UpdateFrequency = updateFrequency,
            Status = status,
            LastUpdated = DateTimeOffset.Parse("2026-04-27T18:30:00Z"),
            ScheduledPublishAt = scheduledPublishAt,
            PublicCatalogEnabled = publicCatalogEnabled,
            ApiEnabled = apiEnabled,
            EmbedEnabled = embedEnabled,
            StacCollectionId = stacCollectionId,
            SampleResponse = sampleResponse,
            ApiAccess = new OpenDataApiAccess
            {
                PublicKeyEnabled = apiEnabled,
                PublicKeyLabel = $"{slug}-public",
                LastRotated = DateTimeOffset.Parse("2026-04-21T12:00:00Z"),
                AnonymousRateLimitPerMinute = 120,
                RegisteredRateLimitPerMinute = 600,
                BulkDownloadEnabled = apiEnabled,
                CodeExamples = apiEnabled ? CodeExamples(slug, stacCollectionId) : []
            },
            Keywords = keywords,
            Downloads =
            [
                Download(OpenDataDownloadFormat.GeoJson, $"{basePath}.geojson", 8_120_000),
                Download(OpenDataDownloadFormat.GeoParquet, $"{basePath}.parquet", 3_840_000),
                Download(OpenDataDownloadFormat.Shapefile, $"{basePath}.zip", 11_240_000),
                Download(OpenDataDownloadFormat.Csv, $"{basePath}.csv", 2_220_000),
                Download(OpenDataDownloadFormat.Kml, $"{basePath}.kml", 4_160_000)
            ],
            ApiEndpoints =
            [
                new OpenDataApiEndpoint
                {
                    Name = "Features",
                    Path = $"/api/open-data/{slug}/features",
                    Example = $"curl https://data.honua.local/api/open-data/{slug}/features?limit=100",
                    RequiresApiKey = false,
                    RateLimitPerMinute = 120
                },
                new OpenDataApiEndpoint
                {
                    Name = "STAC collection",
                    Path = $"/api/stac/{stacCollectionId}",
                    Example = $"fetch('/api/stac/{stacCollectionId}')",
                    RequiresApiKey = false,
                    RateLimitPerMinute = 240
                },
                new OpenDataApiEndpoint
                {
                    Name = "Bulk export",
                    Path = $"/api/open-data/{slug}/exports/latest",
                    Example = $"python -m honua_download https://data.honua.local/api/open-data/{slug}/exports/latest",
                    RequiresApiKey = true,
                    RateLimitPerMinute = 30
                }
            ],
            EmbedConfig = new OpenDataEmbedConfig
            {
                EmbedUrl = $"https://data.honua.local/embed/{slug}",
                Basemap = basemap,
                InitialExtent = extent,
                BrandingMode = "White label",
                Responsive = true,
                WcagReady = embedEnabled
            }
        };
    }

    private static IReadOnlyList<OpenDataCodeExample> CodeExamples(string slug, string stacCollectionId) =>
    [
        new OpenDataCodeExample
        {
            Language = OpenDataCodeLanguage.Curl,
            Label = "cURL",
            Snippet = $"curl 'https://data.honua.local/api/open-data/{slug}/features?limit=100'"
        },
        new OpenDataCodeExample
        {
            Language = OpenDataCodeLanguage.JavaScript,
            Label = "JavaScript",
            Snippet = $"const response = await fetch('https://data.honua.local/api/stac/{stacCollectionId}');\nconst collection = await response.json();"
        },
        new OpenDataCodeExample
        {
            Language = OpenDataCodeLanguage.Python,
            Label = "Python",
            Snippet = $"import requests\nurl = 'https://data.honua.local/api/open-data/{slug}/exports/latest'\nfeatures = requests.get(url, timeout=30).json()"
        }
    ];

    private static OpenDataDownloadOption Download(OpenDataDownloadFormat format, string url, long sizeBytes)
        => new()
        {
            Format = format,
            Url = url,
            SizeBytes = sizeBytes,
            GeneratedAt = DateTimeOffset.Parse("2026-04-27T18:30:00Z")
        };

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length--;
        }

        return builder.Length == 0 ? "untitled-dataset" : builder.ToString();
    }
}
