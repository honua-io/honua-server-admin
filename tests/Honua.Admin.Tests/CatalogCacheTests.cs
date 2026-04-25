using Honua.Admin.Models.SpecWorkspace;
using Honua.Admin.Services.SpecWorkspace;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class CatalogCacheTests
{
    [Fact]
    public async Task Cached_resolution_reports_zero_millisecond_lookup()
    {
        var client = new StubSpecWorkspaceClient();
        var cache = new CatalogCache();
        cache.SetPrincipal("operator");

        var query = new ResolveQuery
        {
            Trigger = CatalogTrigger.AtMention,
            Prefix = "par",
            PrincipalId = "operator"
        };

        var first = await cache.GetOrResolveAsync(client, query, CancellationToken.None);
        var second = await cache.GetOrResolveAsync(client, query, CancellationToken.None);

        Assert.False(first.Cached);
        Assert.True(second.Cached);
        Assert.Equal(0, second.ElapsedMillis);
    }
}
