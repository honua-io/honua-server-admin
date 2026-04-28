using System;
using System.Collections.Generic;

namespace Honua.Admin.Models.AppBuilder;

public enum AppBuilderStatus
{
    Idle,
    Loading,
    Publishing,
    Published,
    Error
}

public enum AppPublishChannelKind
{
    StandaloneUrl,
    IframeEmbed,
    CustomDomain
}

public enum AppWidgetKind
{
    Map,
    Chart,
    Indicator,
    List,
    Filter,
    Legend,
    Search,
    Gauge,
    RichText
}

public enum AppInteractionEventKind
{
    MapFeatureClick,
    FilterChanged,
    ListRowSelect
}

public enum AppInteractionActionKind
{
    FilterWidget,
    HighlightFeature,
    ZoomToFeature
}

public sealed record AppBuilderSnapshot
{
    public IReadOnlyList<AppTemplate> Templates { get; init; } = Array.Empty<AppTemplate>();
    public IReadOnlyList<AppWidgetDefinition> WidgetLibrary { get; init; } = Array.Empty<AppWidgetDefinition>();
    public IReadOnlyList<AppPublishChannel> PublishChannels { get; init; } = Array.Empty<AppPublishChannel>();
    public AppQuotaState Quota { get; init; } = new();
    public AppDraft Draft { get; init; } = new();
}

public sealed record AppTemplate
{
    public string TemplateId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string PublishMode { get; init; } = string.Empty;
    public IReadOnlyList<AppWidgetKind> StarterWidgets { get; init; } = Array.Empty<AppWidgetKind>();
    public IReadOnlyList<string> Breakpoints { get; init; } = Array.Empty<string>();
}

public sealed record AppWidgetDefinition
{
    public AppWidgetKind Kind { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool SupportsDataBinding { get; init; }
    public string DefaultBinding { get; init; } = string.Empty;
}

public sealed record AppPublishChannel
{
    public string ChannelId { get; init; } = string.Empty;
    public AppPublishChannelKind Kind { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public bool Enabled { get; init; }
    public string RequiredEdition { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed record AppQuotaState
{
    public string Edition { get; init; } = "Pro";
    public int PublishedApps { get; init; }
    public int? AppLimit { get; init; } = 5;
    public bool CanPublishMore => AppLimit is null || PublishedApps < AppLimit.Value;
}

public sealed record AppDraft
{
    public string DraftId { get; init; } = Guid.NewGuid().ToString("n");
    public string Name { get; init; } = "Operations dashboard";
    public string TemplateId { get; init; } = "operations-dashboard";
    public string ThemeName { get; init; } = "Civic light";
    public int AutoRefreshSeconds { get; init; } = 60;
    public bool IsPublished { get; init; }
    public IReadOnlyList<AppWidgetInstance> Widgets { get; init; } = Array.Empty<AppWidgetInstance>();
    public IReadOnlyList<AppWidgetInteraction> Interactions { get; init; } = Array.Empty<AppWidgetInteraction>();
}

public sealed record AppWidgetInstance
{
    public string WidgetId { get; init; } = Guid.NewGuid().ToString("n");
    public AppWidgetKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string DataBinding { get; init; } = string.Empty;
    public int Column { get; init; }
    public int Row { get; init; }
    public int Width { get; init; } = 4;
    public int Height { get; init; } = 2;
}

public sealed record AppWidgetInteraction
{
    public string InteractionId { get; init; } = Guid.NewGuid().ToString("n");
    public string SourceWidgetId { get; init; } = string.Empty;
    public string SourceTitle { get; init; } = string.Empty;
    public AppInteractionEventKind Event { get; init; }
    public string TargetWidgetId { get; init; } = string.Empty;
    public string TargetTitle { get; init; } = string.Empty;
    public AppInteractionActionKind Action { get; init; }
    public string Binding { get; init; } = string.Empty;
    public bool Enabled { get; init; } = true;
}

public sealed record AppValidationCheck
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record AppPublishResult
{
    public string PublishedUrl { get; init; } = string.Empty;
    public string EmbedUrl { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool ConsumedQuotaSlot { get; init; }
}
