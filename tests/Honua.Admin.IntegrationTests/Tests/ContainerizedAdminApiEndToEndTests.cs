// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.IntegrationTests.Fixtures;
using Xunit;

namespace Honua.Admin.IntegrationTests.Tests;

/// <summary>
/// Docker-backed E2E smoke coverage for issue #19. The test is opt-in until
/// the repo has a stable Honua Server image configured in CI.
/// </summary>
public sealed class ContainerizedAdminApiEndToEndTests
{
    [Fact]
    [Trait("Category", "ContainerE2E")]
    public async Task HonuaServerContainer_ReturnsAdminFeatureOverview()
    {
        if (!ContainerizedHonuaServerFixture.IsEnabled)
        {
            return;
        }

        await using var fixture = await ContainerizedHonuaServerFixture.StartAsync(CancellationToken.None);

        var overview = await fixture.AdminClient.GetFeatureOverviewAsync(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(fixture.BaseUrl));
        Assert.NotNull(overview);
        Assert.NotEmpty(overview.CurrentEdition);
        Assert.NotEmpty(overview.Features);
    }
}
