using Honua.Admin.Models.SpecWorkspace;
using Honua.Admin.Services.SpecWorkspace;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class SpecWorkspaceStateTests
{
    [Fact]
    public async Task InsertDslTokenAsync_replaces_the_active_editor_selection()
    {
        var storage = new MemoryBrowserStorageService();
        var state = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await state.InitializeAsync("operator");
        await state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels");
        await state.UpdateSectionTextAsync(SpecSectionId.Compute, "aggregate inputs=@parcels by= metric=count");

        state.SetActiveDslSection(SpecSectionId.Compute);
        state.SetDslSelection(SpecSectionId.Compute, "aggregate inputs=@parcels by=".Length, "aggregate inputs=@parcels by=".Length);

        await state.InsertDslTokenAsync("@parcels.county");

        Assert.Contains("@parcels.county", state.GetSectionText(SpecSectionId.Compute), StringComparison.Ordinal);
        Assert.Single(state.Spec.Compute);
        Assert.Equal("@parcels.county", state.Spec.Compute[0].Args["by"]);
    }

    [Fact]
    public async Task InitializeAsync_rehydrates_persisted_workspace_snapshot()
    {
        var storage = new MemoryBrowserStorageService();
        var first = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await first.InitializeAsync("operator");
        first.SetPromptDraft("aggregate count of @parcels by county");
        first.SetLayout(new LayoutWidths(30, 35, 35));
        await first.SubmitPromptAsync("aggregate count of @parcels by county");

        var second = new SpecWorkspaceState(
            new StubSpecWorkspaceClient(),
            storage,
            new NullSpecWorkspaceTelemetry(),
            new CatalogCache());

        await second.InitializeAsync("operator");

        Assert.Equal("aggregate count of @parcels by county", second.PromptDraft);
        Assert.Equal(new LayoutWidths(30, 35, 35), second.Layout);
        Assert.Single(second.Conversation);
        Assert.Contains("@parcels = parcels", second.GetSectionText(SpecSectionId.Sources), StringComparison.Ordinal);
    }

    private sealed class MemoryBrowserStorageService : IBrowserStorageService
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_values.TryGetValue(key, out var value) ? value : null);

        public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NullSpecWorkspaceTelemetry : ISpecWorkspaceTelemetry
    {
        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }
}
