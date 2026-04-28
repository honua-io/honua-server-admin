using Honua.Admin.Models.AppBuilder;

namespace Honua.Admin.Services.AppBuilder;

public sealed class AppBuilderState
{
    private const int CanvasColumns = 9;

    private readonly IAppBuilderClient _client;
    private int _loadRequestVersion;

    public AppBuilderState(IAppBuilderClient client)
    {
        _client = client;
    }

    public AppBuilderStatus Status { get; private set; } = AppBuilderStatus.Idle;

    public string? LastError { get; private set; }

    public IReadOnlyList<AppTemplate> Templates { get; private set; } = Array.Empty<AppTemplate>();

    public IReadOnlyList<AppWidgetDefinition> WidgetLibrary { get; private set; } = Array.Empty<AppWidgetDefinition>();

    public IReadOnlyList<AppPublishChannel> PublishChannels { get; private set; } = Array.Empty<AppPublishChannel>();

    public AppQuotaState Quota { get; private set; } = new();

    public AppDraft Draft { get; private set; } = new();

    public AppPublishResult? LastPublish { get; private set; }

    public AppTemplate? SelectedTemplate =>
        Templates.FirstOrDefault(template => string.Equals(template.TemplateId, Draft.TemplateId, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<AppValidationCheck> ValidationChecks
    {
        get
        {
            var hasName = !string.IsNullOrWhiteSpace(Draft.Name);
            var hasWidgets = Draft.Widgets.Count > 0;
            var dataBoundWidgets = Draft.Widgets.Where(widget => WidgetRequiresBinding(widget.Kind)).ToArray();
            var allBindings = dataBoundWidgets.All(widget => !string.IsNullOrWhiteSpace(widget.DataBinding));
            var widgetIds = Draft.Widgets.Select(widget => widget.WidgetId).ToHashSet(StringComparer.Ordinal);
            var interactionsReferenceWidgets = Draft.Interactions.All(interaction =>
                widgetIds.Contains(interaction.SourceWidgetId) && widgetIds.Contains(interaction.TargetWidgetId));
            var interactionMessage = Draft.Interactions.Count == 0
                ? "No cross-widget interactions configured."
                : interactionsReferenceWidgets
                    ? $"{Draft.Interactions.Count} cross-widget interaction(s) configured."
                    : "Interaction source and target widgets must remain on the canvas.";

            return
            [
                new AppValidationCheck
                {
                    Key = "name",
                    Label = "App name",
                    Passed = hasName,
                    Message = hasName ? Draft.Name : "Name is required before publish."
                },
                new AppValidationCheck
                {
                    Key = "widgets",
                    Label = "Widget layout",
                    Passed = hasWidgets,
                    Message = hasWidgets ? $"{Draft.Widgets.Count} widget(s) on canvas." : "Add at least one widget."
                },
                new AppValidationCheck
                {
                    Key = "bindings",
                    Label = "Data bindings",
                    Passed = allBindings,
                    Message = allBindings ? $"{dataBoundWidgets.Length} data-bound widget(s) configured." : "Data-bound widgets need bindings."
                },
                new AppValidationCheck
                {
                    Key = "responsive",
                    Label = "Responsive breakpoints",
                    Passed = SelectedTemplate?.Breakpoints.Count > 0,
                    Message = SelectedTemplate is null ? "Select a template." : string.Join(", ", SelectedTemplate.Breakpoints)
                },
                new AppValidationCheck
                {
                    Key = "interactions",
                    Label = "Cross-widget interactions",
                    Passed = interactionsReferenceWidgets,
                    Message = interactionMessage
                }
            ];
        }
    }

    public IReadOnlyList<AppValidationCheck> PublishReadinessChecks
    {
        get
        {
            var standalone = PublishChannels.FirstOrDefault(channel => channel.Kind == AppPublishChannelKind.StandaloneUrl);
            var embed = PublishChannels.FirstOrDefault(channel => channel.Kind == AppPublishChannelKind.IframeEmbed);
            var customDomain = PublishChannels.FirstOrDefault(channel => channel.Kind == AppPublishChannelKind.CustomDomain);
            var quotaLimit = Quota.AppLimit?.ToString() ?? "unlimited";
            var isEnterprise = string.Equals(Quota.Edition, "Enterprise", StringComparison.OrdinalIgnoreCase);
            var quotaPassed = Draft.IsPublished || Quota.CanPublishMore;
            var quotaMessage = quotaPassed
                ? $"{Quota.PublishedApps} of {quotaLimit} {Quota.Edition} app slots used."
                : $"{Quota.Edition} app limit reached.";

            if (Draft.IsPublished && !Quota.CanPublishMore)
            {
                quotaMessage = $"{Quota.Edition} app limit reached; updates to this published app do not use a new slot.";
            }

            return
            [
                new AppValidationCheck
                {
                    Key = "quota",
                    Label = "Published app quota",
                    Passed = quotaPassed,
                    Message = quotaMessage
                },
                new AppValidationCheck
                {
                    Key = "standalone",
                    Label = "Standalone URL",
                    Passed = standalone?.Enabled == true,
                    Message = standalone?.Message ?? "Standalone URL publishing channel is not configured."
                },
                new AppValidationCheck
                {
                    Key = "embed",
                    Label = "Iframe embed",
                    Passed = embed?.Enabled == true,
                    Message = embed?.Message ?? "Embed publishing channel is not configured."
                },
                new AppValidationCheck
                {
                    Key = "custom-domain",
                    Label = "Custom domain",
                    Passed = !isEnterprise || customDomain?.Enabled == true,
                    Message = customDomain?.Message ?? (isEnterprise ? "Custom domain publishing channel is not configured." : "Custom domains can be configured from Enterprise branding.")
                }
            ];
        }
    }

    public bool HasBlockingValidation => ValidationChecks.Any(check => !check.Passed) || (!Draft.IsPublished && !Quota.CanPublishMore);

    public event Action? OnChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var loadVersion = Interlocked.Increment(ref _loadRequestVersion);
        Status = AppBuilderStatus.Loading;
        LastError = null;
        Notify();

        try
        {
            var snapshot = await _client.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (!IsCurrentLoad(loadVersion))
            {
                return;
            }

            Templates = snapshot.Templates;
            WidgetLibrary = snapshot.WidgetLibrary;
            PublishChannels = snapshot.PublishChannels;
            Quota = snapshot.Quota;
            Draft = snapshot.Draft;
            LastPublish = null;
            Status = AppBuilderStatus.Idle;
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentLoad(loadVersion))
            {
                Status = AppBuilderStatus.Idle;
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

            Status = AppBuilderStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    public void SelectTemplate(string templateId)
    {
        if (IsDraftLocked)
        {
            return;
        }

        if (!Templates.Any(template => string.Equals(template.TemplateId, templateId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Draft = Draft with { TemplateId = templateId };
        ResetTransientPublishState();
        Notify();
    }

    public void SetName(string? name)
    {
        if (IsDraftLocked)
        {
            return;
        }

        Draft = Draft with { Name = name?.Trim() ?? string.Empty };
        ResetTransientPublishState();
        Notify();
    }

    public void SetTheme(string? themeName)
    {
        if (IsDraftLocked)
        {
            return;
        }

        Draft = Draft with { ThemeName = string.IsNullOrWhiteSpace(themeName) ? "Civic light" : themeName.Trim() };
        ResetTransientPublishState();
        Notify();
    }

    public void SetAutoRefreshSeconds(int seconds)
    {
        if (IsDraftLocked)
        {
            return;
        }

        Draft = Draft with { AutoRefreshSeconds = Math.Clamp(seconds, 15, 3600) };
        ResetTransientPublishState();
        Notify();
    }

    public void AddWidget(AppWidgetKind kind)
    {
        if (IsDraftLocked)
        {
            return;
        }

        var definition = WidgetLibrary.FirstOrDefault(widget => widget.Kind == kind);
        if (definition is null || string.IsNullOrWhiteSpace(definition.Name))
        {
            return;
        }

        var width = kind == AppWidgetKind.Map ? 6 : 3;
        var height = kind == AppWidgetKind.Map ? 4 : 2;
        var (column, row) = FindOpenSlot(width, height);
        var instance = new AppWidgetInstance
        {
            Kind = kind,
            Title = definition.Name,
            DataBinding = definition.DefaultBinding,
            Column = column,
            Row = row,
            Width = width,
            Height = height
        };

        Draft = Draft with { Widgets = [.. Draft.Widgets, instance] };
        ResetTransientPublishState();
        Notify();
    }

    public void RemoveWidget(string widgetId)
    {
        if (IsDraftLocked)
        {
            return;
        }

        var widgets = Draft.Widgets
            .Where(widget => !string.Equals(widget.WidgetId, widgetId, StringComparison.Ordinal))
            .ToArray();
        var widgetIds = widgets.Select(widget => widget.WidgetId).ToHashSet(StringComparer.Ordinal);

        Draft = Draft with
        {
            Widgets = widgets,
            Interactions = Draft.Interactions
                .Where(interaction => widgetIds.Contains(interaction.SourceWidgetId) && widgetIds.Contains(interaction.TargetWidgetId))
                .ToArray()
        };
        ResetTransientPublishState();
        Notify();
    }

    public async Task PublishAsync(CancellationToken cancellationToken = default)
    {
        if (Status is AppBuilderStatus.Loading or AppBuilderStatus.Publishing)
        {
            return;
        }

        if (HasBlockingValidation)
        {
            Status = AppBuilderStatus.Error;
            LastError = "Resolve validation checks before publishing.";
            LastPublish = null;
            Notify();
            return;
        }

        Status = AppBuilderStatus.Publishing;
        LastError = null;
        LastPublish = null;
        Notify();

        try
        {
            LastPublish = await _client.PublishAsync(Draft, cancellationToken).ConfigureAwait(false);
            if (LastPublish.ConsumedQuotaSlot)
            {
                Quota = Quota with { PublishedApps = Quota.PublishedApps + 1 };
            }

            Draft = Draft with { IsPublished = true };
            Status = AppBuilderStatus.Published;
        }
        catch (OperationCanceledException)
        {
            Status = AppBuilderStatus.Idle;
            Notify();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Status = AppBuilderStatus.Error;
            LastError = ex.Message;
        }

        Notify();
    }

    private bool WidgetRequiresBinding(AppWidgetKind kind)
        => WidgetLibrary.FirstOrDefault(widget => widget.Kind == kind)?.SupportsDataBinding == true;

    private bool IsCurrentLoad(int loadVersion) => loadVersion == Volatile.Read(ref _loadRequestVersion);

    private bool IsDraftLocked => Status is AppBuilderStatus.Loading or AppBuilderStatus.Publishing;

    private (int Column, int Row) FindOpenSlot(int width, int height)
    {
        var clampedWidth = Math.Clamp(width, 1, CanvasColumns);
        var clampedHeight = Math.Max(1, height);
        for (var row = 1; row < 200; row++)
        {
            for (var column = 1; column <= CanvasColumns - clampedWidth + 1; column++)
            {
                if (!Draft.Widgets.Any(widget => Overlaps(column, row, clampedWidth, clampedHeight, widget)))
                {
                    return (column, row);
                }
            }
        }

        return (1, Draft.Widgets.Count * clampedHeight + 1);
    }

    private static bool Overlaps(int column, int row, int width, int height, AppWidgetInstance existing)
    {
        var existingColumn = Math.Clamp(existing.Column, 1, CanvasColumns);
        var existingWidth = Math.Clamp(existing.Width, 1, CanvasColumns - existingColumn + 1);
        var existingRow = Math.Max(1, existing.Row);
        var existingHeight = Math.Max(1, existing.Height);

        return column < existingColumn + existingWidth &&
            column + width > existingColumn &&
            row < existingRow + existingHeight &&
            row + height > existingRow;
    }

    private void ResetTransientPublishState()
    {
        LastPublish = null;
        LastError = null;
        if (Status is AppBuilderStatus.Error or AppBuilderStatus.Published)
        {
            Status = AppBuilderStatus.Idle;
        }
    }

    private void Notify() => OnChanged?.Invoke();
}
