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

public sealed record AppBuilderSnapshot
{
    public IReadOnlyList<AppTemplate> Templates { get; init; } = Array.Empty<AppTemplate>();
    public IReadOnlyList<AppWidgetDefinition> WidgetLibrary { get; init; } = Array.Empty<AppWidgetDefinition>();
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

public sealed record AppDraft
{
    public string DraftId { get; init; } = Guid.NewGuid().ToString("n");
    public string Name { get; init; } = "Operations dashboard";
    public string TemplateId { get; init; } = "operations-dashboard";
    public string ThemeName { get; init; } = "Civic light";
    public int AutoRefreshSeconds { get; init; } = 60;
    public IReadOnlyList<AppWidgetInstance> Widgets { get; init; } = Array.Empty<AppWidgetInstance>();
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
}
