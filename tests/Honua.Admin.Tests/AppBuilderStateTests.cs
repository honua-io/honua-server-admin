// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.AppBuilder;
using Honua.Admin.Services.AppBuilder;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class AppBuilderStateTests
{
    [Fact]
    public async Task LoadAsync_populates_templates_widgets_and_draft()
    {
        var state = new AppBuilderState(new StubAppBuilderClient());

        await state.LoadAsync();

        Assert.Equal(AppBuilderStatus.Idle, state.Status);
        Assert.Contains(state.Templates, template => template.TemplateId == "operations-dashboard");
        Assert.Contains(state.WidgetLibrary, widget => widget.Kind == AppWidgetKind.Map);
        Assert.Contains(state.PublishChannels, channel => channel.Kind == AppPublishChannelKind.StandaloneUrl);
        Assert.Equal("Pro", state.Quota.Edition);
        Assert.True(state.Quota.CanPublishMore);
        Assert.Equal("Harbor operations dashboard", state.Draft.Name);
        Assert.NotEmpty(state.Draft.Widgets);
        Assert.False(state.HasBlockingValidation);
    }

    [Fact]
    public async Task LoadAsync_ignores_stale_snapshot_responses()
    {
        var client = new SequencedLoadAppBuilderClient();
        var state = new AppBuilderState(client);

        var staleLoad = state.LoadAsync();
        var freshLoad = state.LoadAsync();
        client.CompleteLoad(1, Snapshot("Fresh dashboard"));
        await freshLoad;

        Assert.Equal("Fresh dashboard", state.Draft.Name);

        client.CompleteLoad(0, Snapshot("Stale dashboard"));
        await staleLoad;

        Assert.Equal("Fresh dashboard", state.Draft.Name);
        Assert.Equal(AppBuilderStatus.Idle, state.Status);
    }

    [Fact]
    public async Task AddWidget_adds_widget_and_recomputes_binding_validation()
    {
        var state = new AppBuilderState(new FixedAppBuilderClient(new AppBuilderSnapshot
        {
            Templates =
            [
                new AppTemplate
                {
                    TemplateId = "ops",
                    Name = "Operations",
                    Breakpoints = ["Desktop", "Mobile"],
                },
            ],
            WidgetLibrary =
            [
                new AppWidgetDefinition
                {
                    Kind = AppWidgetKind.Chart,
                    Name = "Chart",
                    SupportsDataBinding = true,
                },
            ],
            Draft = new AppDraft
            {
                Name = "Operations",
                TemplateId = "ops",
            },
        }));
        await state.LoadAsync();

        state.AddWidget(AppWidgetKind.Chart);

        var widget = Assert.Single(state.Draft.Widgets);
        Assert.Equal(AppWidgetKind.Chart, widget.Kind);
        var bindings = Assert.Single(state.ValidationChecks, check => check.Key == "bindings");
        Assert.False(bindings.Passed);
        Assert.True(state.HasBlockingValidation);
    }

    [Fact]
    public async Task AddWidget_places_new_widget_in_an_open_canvas_region()
    {
        var state = new AppBuilderState(new StubAppBuilderClient());
        await state.LoadAsync();
        var existing = state.Draft.Widgets;

        state.AddWidget(AppWidgetKind.Search);

        var added = Assert.Single(state.Draft.Widgets, widget => existing.All(item => item.WidgetId != widget.WidgetId));
        Assert.DoesNotContain(existing, widget => Overlaps(widget, added));
    }

    [Fact]
    public async Task PublishAsync_returns_publish_urls_when_valid()
    {
        var state = new AppBuilderState(new StubAppBuilderClient());
        await state.LoadAsync();

        await state.PublishAsync();

        Assert.Equal(AppBuilderStatus.Published, state.Status);
        Assert.NotNull(state.LastPublish);
        Assert.Equal("https://apps.honua.local/harbor-operations-dashboard", state.LastPublish.PublishedUrl);
        Assert.Equal("https://apps.honua.local/embed/harbor-operations-dashboard", state.LastPublish.EmbedUrl);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task Draft_mutators_ignore_changes_while_publish_is_in_flight()
    {
        var client = new BlockingPublishAppBuilderClient();
        var state = new AppBuilderState(client);
        await state.LoadAsync();
        var originalDraft = state.Draft;

        var publishTask = state.PublishAsync();
        await client.PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        state.SetName("Edited during publish");
        state.SetTheme("Dark");
        state.SetAutoRefreshSeconds(15);
        state.SelectTemplate("public-viewer");
        state.AddWidget(AppWidgetKind.Search);
        state.RemoveWidget(originalDraft.Widgets[0].WidgetId);

        Assert.Equal(AppBuilderStatus.Publishing, state.Status);
        Assert.Equal(originalDraft.Name, state.Draft.Name);
        Assert.Equal(originalDraft.ThemeName, state.Draft.ThemeName);
        Assert.Equal(originalDraft.AutoRefreshSeconds, state.Draft.AutoRefreshSeconds);
        Assert.Equal(originalDraft.TemplateId, state.Draft.TemplateId);
        Assert.Equal(originalDraft.Widgets.Count, state.Draft.Widgets.Count);

        client.Complete();
        await publishTask;

        Assert.Equal(AppBuilderStatus.Published, state.Status);
        Assert.Equal(originalDraft.Name, client.PublishedDraft?.Name);
    }

    [Fact]
    public async Task PublishAsync_ignores_reentrant_publish_calls()
    {
        var client = new BlockingPublishAppBuilderClient();
        var state = new AppBuilderState(client);
        await state.LoadAsync();

        var firstPublish = state.PublishAsync();
        await client.PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await state.PublishAsync();

        Assert.Equal(1, client.PublishCalls);

        client.Complete();
        await firstPublish;

        Assert.Equal(AppBuilderStatus.Published, state.Status);
    }

    [Fact]
    public async Task PublishAsync_blocks_when_name_is_missing()
    {
        var client = new StubAppBuilderClient();
        var state = new AppBuilderState(client);
        await state.LoadAsync();

        state.SetName(" ");
        await state.PublishAsync();

        Assert.Equal(AppBuilderStatus.Error, state.Status);
        Assert.Equal("Resolve validation checks before publishing.", state.LastError);
        Assert.Null(state.LastPublish);

        state.SetName("Recovered dashboard");

        Assert.Equal(AppBuilderStatus.Idle, state.Status);
        Assert.Null(state.LastError);
    }

    [Fact]
    public async Task PublishAsync_blocks_when_app_quota_is_full()
    {
        var state = new AppBuilderState(new FixedAppBuilderClient(Snapshot("Quota dashboard") with
        {
            Quota = new AppQuotaState
            {
                Edition = "Pro",
                PublishedApps = 5,
                AppLimit = 5,
            },
        }));
        await state.LoadAsync();

        await state.PublishAsync();

        Assert.Equal(AppBuilderStatus.Error, state.Status);
        Assert.Equal("Resolve validation checks before publishing.", state.LastError);
        Assert.Null(state.LastPublish);
        Assert.Contains(state.PublishReadinessChecks, check => check.Key == "quota" && !check.Passed);
    }

    [Fact]
    public async Task PublishAsync_clears_stale_publish_result_when_retry_fails()
    {
        var state = new AppBuilderState(new FailingRetryAppBuilderClient());
        await state.LoadAsync();
        await state.PublishAsync();
        Assert.NotNull(state.LastPublish);

        await state.PublishAsync();

        Assert.Equal(AppBuilderStatus.Error, state.Status);
        Assert.Equal("publish failed", state.LastError);
        Assert.Null(state.LastPublish);
    }

    [Fact]
    public async Task PublishAsync_notifies_when_publish_is_canceled()
    {
        var state = new AppBuilderState(new CancelingPublishAppBuilderClient());
        await state.LoadAsync();
        var observedStatuses = new List<AppBuilderStatus>();
        state.OnChanged += () => observedStatuses.Add(state.Status);

        await Assert.ThrowsAsync<OperationCanceledException>(() => state.PublishAsync());

        Assert.Equal(AppBuilderStatus.Idle, state.Status);
        Assert.Contains(AppBuilderStatus.Publishing, observedStatuses);
        Assert.Equal(AppBuilderStatus.Idle, observedStatuses[^1]);
    }

    [Fact]
    public async Task StubPublishAsync_sanitizes_preview_url_slug()
    {
        var client = new StubAppBuilderClient();

        var result = await client.PublishAsync(new AppDraft { Name = "Ops/2026?Launch #1" }, CancellationToken.None);

        Assert.Equal("https://apps.honua.local/ops-2026-launch-1", result.PublishedUrl);
        Assert.Equal("https://apps.honua.local/embed/ops-2026-launch-1", result.EmbedUrl);
    }

    private sealed class FixedAppBuilderClient : IAppBuilderClient
    {
        private readonly AppBuilderSnapshot _snapshot;

        public FixedAppBuilderClient(AppBuilderSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<AppBuilderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => Task.FromResult(_snapshot);

        public Task<AppPublishResult> PublishAsync(AppDraft draft, CancellationToken cancellationToken)
            => Task.FromResult(new AppPublishResult
            {
                PublishedUrl = "https://apps.honua.local/test",
                EmbedUrl = "https://apps.honua.local/embed/test",
                PublishedAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
                Message = "Published",
            });
    }

    private sealed class SequencedLoadAppBuilderClient : IAppBuilderClient
    {
        private readonly List<TaskCompletionSource<AppBuilderSnapshot>> _loads = [];

        public Task<AppBuilderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            var load = new TaskCompletionSource<AppBuilderSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            _loads.Add(load);
            return load.Task;
        }

        public Task<AppPublishResult> PublishAsync(AppDraft draft, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void CompleteLoad(int index, AppBuilderSnapshot snapshot)
            => _loads[index].SetResult(snapshot);
    }

    private sealed class FailingRetryAppBuilderClient : IAppBuilderClient
    {
        private readonly StubAppBuilderClient _inner = new();
        private int _publishCount;

        public Task<AppBuilderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => _inner.GetSnapshotAsync(cancellationToken);

        public Task<AppPublishResult> PublishAsync(AppDraft draft, CancellationToken cancellationToken)
        {
            _publishCount++;
            if (_publishCount == 1)
            {
                return _inner.PublishAsync(draft, cancellationToken);
            }

            throw new InvalidOperationException("publish failed");
        }
    }

    private sealed class CancelingPublishAppBuilderClient : IAppBuilderClient
    {
        private readonly StubAppBuilderClient _inner = new();

        public Task<AppBuilderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => _inner.GetSnapshotAsync(cancellationToken);

        public Task<AppPublishResult> PublishAsync(AppDraft draft, CancellationToken cancellationToken)
            => throw new OperationCanceledException(cancellationToken);
    }

    private sealed class BlockingPublishAppBuilderClient : IAppBuilderClient
    {
        private readonly StubAppBuilderClient _inner = new();
        private readonly TaskCompletionSource<AppPublishResult> _publishResult =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> PublishStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AppDraft? PublishedDraft { get; private set; }

        public int PublishCalls { get; private set; }

        public Task<AppBuilderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
            => _inner.GetSnapshotAsync(cancellationToken);

        public Task<AppPublishResult> PublishAsync(AppDraft draft, CancellationToken cancellationToken)
        {
            PublishCalls++;
            PublishedDraft = draft;
            PublishStarted.TrySetResult(true);
            return _publishResult.Task;
        }

        public void Complete()
            => _publishResult.SetResult(new AppPublishResult
            {
                PublishedUrl = "https://apps.honua.local/published",
                EmbedUrl = "https://apps.honua.local/embed/published",
                PublishedAt = DateTimeOffset.Parse("2026-04-28T00:00:00Z"),
                Message = "Published",
            });
    }

    private static AppBuilderSnapshot Snapshot(string draftName)
        => new()
        {
            Templates =
            [
                new AppTemplate
                {
                    TemplateId = "operations-dashboard",
                    Name = "Operations dashboard",
                    Breakpoints = ["Desktop"],
                },
            ],
            WidgetLibrary =
            [
                new AppWidgetDefinition
                {
                    Kind = AppWidgetKind.Map,
                    Name = "Map",
                    SupportsDataBinding = true,
                    DefaultBinding = "Assets",
                },
            ],
            Draft = new AppDraft
            {
                Name = draftName,
                TemplateId = "operations-dashboard",
                Widgets =
                [
                    new AppWidgetInstance
                    {
                        Kind = AppWidgetKind.Map,
                        Title = "Map",
                        DataBinding = "Assets",
                        Column = 1,
                        Row = 1,
                        Width = 6,
                        Height = 4,
                    },
                ],
            },
        };

    private static bool Overlaps(AppWidgetInstance first, AppWidgetInstance second)
    {
        return first.Column < second.Column + second.Width &&
            first.Column + first.Width > second.Column &&
            first.Row < second.Row + second.Height &&
            first.Row + first.Height > second.Row;
    }
}
