// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.IntegrationTests.Fixtures;
using Xunit;

namespace Honua.Admin.IntegrationTests.Tests;

/// <summary>
/// Representative end-to-end test for the typed <see cref="HonuaAdminClient"/>
/// against the in-process fake honua-server admin API. Each test exercises the
/// full path: route resolution → JSON deserialization → DTO assertion.
/// </summary>
public sealed class AdminClientEndToEndTests : IClassFixture<HonuaServerFixture>
{
    private readonly HonuaServerFixture _fixture;

    public AdminClientEndToEndTests(HonuaServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetFeatureOverview_ReturnsFeatureRowsFromOverviewEndpoint()
    {
        var overview = await _fixture.AdminClient.GetFeatureOverviewAsync(CancellationToken.None);

        Assert.Equal("Enterprise", overview.CurrentEdition);
        Assert.Equal(2, overview.Features.Count);
        Assert.Contains(overview.Features, feature => feature.Key == "ogc-features" && feature.IsEnabled);
    }

    [Fact]
    public async Task GetConfigurationSummary_ReturnsConfigurationSummary()
    {
        var info = await _fixture.AdminClient.GetConfigurationSummaryAsync(CancellationToken.None);

        Assert.Equal("Integration", info.Environment);
        Assert.Equal(4, info.TotalTypes);
        Assert.Equal(22, info.TotalProperties);
        Assert.Equal(2, info.ValidSecrets);
    }

    [Fact]
    public async Task ListConnections_ReturnsRowsFromConnectionsEndpoint()
    {
        var rows = await _fixture.AdminClient.ListConnectionsAsync(CancellationToken.None);

        Assert.Single(rows);
        Assert.Equal("live-postgis", rows[0].Name);
        Assert.Equal("managed", rows[0].Provider);
        Assert.Equal("Healthy", rows[0].Status);
        Assert.Equal("db.integration", rows[0].Host);
    }
}
