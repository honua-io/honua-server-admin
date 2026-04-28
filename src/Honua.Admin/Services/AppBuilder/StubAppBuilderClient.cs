using System.Text;
using Honua.Admin.Models.AppBuilder;

namespace Honua.Admin.Services.AppBuilder;

public sealed class StubAppBuilderClient : IAppBuilderClient
{
    private readonly HashSet<string> _publishedDraftIds = new(StringComparer.Ordinal);

    public Task<AppBuilderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = new AppBuilderSnapshot
        {
            Templates =
            [
                new AppTemplate
                {
                    TemplateId = "operations-dashboard",
                    Name = "Operations dashboard",
                    Description = "Multi-widget monitoring surface with map, indicators, chart, and list.",
                    PublishMode = "Standalone URL and iframe",
                    StarterWidgets = [AppWidgetKind.Map, AppWidgetKind.Indicator, AppWidgetKind.Chart, AppWidgetKind.List],
                    Breakpoints = ["Desktop", "Tablet", "Mobile"]
                },
                new AppTemplate
                {
                    TemplateId = "public-viewer",
                    Name = "Public viewer",
                    Description = "Read-only map viewer with search, legend, identify, and print handoff.",
                    PublishMode = "Public URL and iframe",
                    StarterWidgets = [AppWidgetKind.Map, AppWidgetKind.Search, AppWidgetKind.Legend],
                    Breakpoints = ["Desktop", "Mobile"]
                },
                new AppTemplate
                {
                    TemplateId = "story-map",
                    Name = "Story map",
                    Description = "Scrolling narrative shell with rich text and embedded map sections.",
                    PublishMode = "Standalone URL",
                    StarterWidgets = [AppWidgetKind.RichText, AppWidgetKind.Map],
                    Breakpoints = ["Desktop", "Mobile"]
                }
            ],
            WidgetLibrary =
            [
                Widget(AppWidgetKind.Map, "Map", "MapLibre map with layer controls.", true, "Parcels feature service"),
                Widget(AppWidgetKind.Chart, "Chart", "Bar, line, pie, and scatter chart shell.", true, "Permit activity by month"),
                Widget(AppWidgetKind.Indicator, "Indicator", "Single-value metric with trend state.", true, "Open permits count"),
                Widget(AppWidgetKind.List, "List", "Sortable feature list bound to a service layer.", true, "Recent inspections"),
                Widget(AppWidgetKind.Filter, "Filter", "Shared filter control for linked widgets.", true, "District selector"),
                Widget(AppWidgetKind.Legend, "Legend", "Layer legend for visible map content.", false, "Visible layers"),
                Widget(AppWidgetKind.Search, "Search", "Geocoding and feature search entry point.", true, "Address locator"),
                Widget(AppWidgetKind.Gauge, "Gauge", "Numeric range visualization.", true, "SLA compliance"),
                Widget(AppWidgetKind.RichText, "Rich text", "Header, narrative, and branded text block.", false, "Section copy")
            ],
            PublishChannels =
            [
                Channel(
                    "standalone-url",
                    AppPublishChannelKind.StandaloneUrl,
                    "Standalone URL",
                    "https://apps.honua.local/{slug}",
                    true,
                    "Pro",
                    "Ready for public or internal app launch."),
                Channel(
                    "iframe-embed",
                    AppPublishChannelKind.IframeEmbed,
                    "Iframe embed",
                    "https://apps.honua.local/embed/{slug}",
                    true,
                    "Pro",
                    "Embeddable viewer output is available for CMS pages."),
                Channel(
                    "custom-domain",
                    AppPublishChannelKind.CustomDomain,
                    "Custom domain",
                    "https://apps.example.gov/{slug}",
                    false,
                    "Enterprise",
                    "Enterprise branding unlocks custom domain publishing.")
            ],
            Quota = new AppQuotaState
            {
                Edition = "Pro",
                PublishedApps = 3,
                AppLimit = 5
            },
            Draft = new AppDraft
            {
                DraftId = "draft-operations-dashboard",
                Name = "Harbor operations dashboard",
                TemplateId = "operations-dashboard",
                ThemeName = "Civic light",
                AutoRefreshSeconds = 60,
                Widgets =
                [
                    Instance(AppWidgetKind.Map, "Operational map", "Harbor assets", 1, 1, 6, 4),
                    Instance(AppWidgetKind.Indicator, "Open incidents", "Incidents count", 7, 1, 3, 2),
                    Instance(AppWidgetKind.Chart, "Incidents by type", "Incident types", 7, 3, 3, 2),
                    Instance(AppWidgetKind.List, "Recent work orders", "Work orders", 1, 5, 9, 2)
                ]
            }
        };

        return Task.FromResult(snapshot);
    }

    public Task<AppPublishResult> PublishAsync(AppDraft draft, CancellationToken cancellationToken)
    {
        var consumedQuotaSlot = !draft.IsPublished && _publishedDraftIds.Add(draft.DraftId);
        return Task.FromResult(new AppPublishResult
        {
            PublishedUrl = $"https://apps.honua.local/{Slug(draft.Name)}",
            EmbedUrl = $"https://apps.honua.local/embed/{Slug(draft.Name)}",
            PublishedAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
            Message = $"{draft.Name} is ready for preview.",
            ConsumedQuotaSlot = consumedQuotaSlot
        });
    }

    private static AppWidgetDefinition Widget(
        AppWidgetKind kind,
        string name,
        string description,
        bool supportsDataBinding,
        string defaultBinding) => new()
        {
            Kind = kind,
            Name = name,
            Description = description,
            SupportsDataBinding = supportsDataBinding,
            DefaultBinding = defaultBinding
        };

    private static AppPublishChannel Channel(
        string channelId,
        AppPublishChannelKind kind,
        string label,
        string target,
        bool enabled,
        string requiredEdition,
        string message) => new()
        {
            ChannelId = channelId,
            Kind = kind,
            Label = label,
            Target = target,
            Enabled = enabled,
            RequiredEdition = requiredEdition,
            Message = message
        };

    private static AppWidgetInstance Instance(
        AppWidgetKind kind,
        string title,
        string binding,
        int column,
        int row,
        int width,
        int height) => new()
        {
            Kind = kind,
            Title = title,
            DataBinding = binding,
            Column = column,
            Row = row,
            Width = width,
            Height = height
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

        return builder.Length == 0 ? "untitled-app" : builder.ToString();
    }
}
