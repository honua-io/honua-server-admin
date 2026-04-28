using System;
using System.Collections.Generic;

namespace Honua.Admin.Models.OpenDataHub;

public enum OpenDataHubStatus
{
    Idle,
    Loading,
    Publishing,
    Published,
    Error
}

public enum OpenDataDatasetStatus
{
    Draft,
    InReview,
    Scheduled,
    Published,
    Blocked
}

public enum OpenDataDownloadFormat
{
    GeoJson,
    GeoParquet,
    Shapefile,
    Csv,
    Kml
}

public enum OpenDataCodeLanguage
{
    Curl,
    JavaScript,
    Python
}

public sealed record OpenDataHubSnapshot
{
    public IReadOnlyList<OpenDataDataset> Datasets { get; init; } = Array.Empty<OpenDataDataset>();
    public IReadOnlyList<OpenDataHubMetric> Metrics { get; init; } = Array.Empty<OpenDataHubMetric>();
}

public sealed record OpenDataDataset
{
    public string DatasetId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Geography { get; init; } = string.Empty;
    public string License { get; init; } = string.Empty;
    public string Contact { get; init; } = string.Empty;
    public string UpdateFrequency { get; init; } = string.Empty;
    public OpenDataDatasetStatus Status { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
    public DateTimeOffset? ScheduledPublishAt { get; init; }
    public bool PublicCatalogEnabled { get; init; }
    public bool ApiEnabled { get; init; }
    public bool EmbedEnabled { get; init; }
    public string StacCollectionId { get; init; } = string.Empty;
    public string SampleResponse { get; init; } = string.Empty;
    public OpenDataApiAccess ApiAccess { get; init; } = new();
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<OpenDataDownloadOption> Downloads { get; init; } = Array.Empty<OpenDataDownloadOption>();
    public IReadOnlyList<OpenDataApiEndpoint> ApiEndpoints { get; init; } = Array.Empty<OpenDataApiEndpoint>();
    public OpenDataEmbedConfig EmbedConfig { get; init; } = new();
}

public sealed record OpenDataApiAccess
{
    public bool PublicKeyEnabled { get; init; }
    public string PublicKeyLabel { get; init; } = string.Empty;
    public DateTimeOffset? LastRotated { get; init; }
    public int AnonymousRateLimitPerMinute { get; init; }
    public int RegisteredRateLimitPerMinute { get; init; }
    public bool BulkDownloadEnabled { get; init; }
    public IReadOnlyList<OpenDataCodeExample> CodeExamples { get; init; } = Array.Empty<OpenDataCodeExample>();
}

public sealed record OpenDataCodeExample
{
    public OpenDataCodeLanguage Language { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
}

public sealed record OpenDataDownloadOption
{
    public OpenDataDownloadFormat Format { get; init; }
    public string Url { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}

public sealed record OpenDataApiEndpoint
{
    public string Name { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public string Path { get; init; } = string.Empty;
    public string Example { get; init; } = string.Empty;
    public bool RequiresApiKey { get; init; }
    public int RateLimitPerMinute { get; init; }
}

public sealed record OpenDataEmbedConfig
{
    public string EmbedUrl { get; init; } = string.Empty;
    public string Basemap { get; init; } = string.Empty;
    public string InitialExtent { get; init; } = string.Empty;
    public string BrandingMode { get; init; } = string.Empty;
    public bool Responsive { get; init; }
    public bool WcagReady { get; init; }
}

public sealed record OpenDataHubMetric
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
}

public sealed record OpenDataValidationCheck
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record OpenDataPublishResult
{
    public string DatasetId { get; init; } = string.Empty;
    public string CatalogUrl { get; init; } = string.Empty;
    public string ApiDocsUrl { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
