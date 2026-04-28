// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Honua.Admin.Models.Admin;
using Honua.Admin.Pages.Admin;
using Honua.Admin.Services.Admin;
using Honua.Admin.Services.Annotations;
using Honua.Admin.Services.Publishing;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Honua.Admin.Tests.Pages;

/// <summary>
/// bunit smoke tests for the restored P0 admin pages.
/// </summary>
public sealed class AdminPageRenderTests : TestContext
{
    public AdminPageRenderTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IAdminTelemetry, NullAdminTelemetry>();
    }

    [Fact]
    public void IndexDashboard_RendersFeatureOverview()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Index>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Honua Server Administration");
            cut.Markup.MarkupMatchesContaining("Enterprise");
            cut.Markup.MarkupMatchesContaining("OGC Features");
        });
    }

    [Fact]
    public void ConnectionListPage_RendersConnectionRows()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderWithMudHost<ConnectionListPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Connections");
            cut.Markup.MarkupMatchesContaining("primary-postgis");
            cut.Markup.MarkupMatchesContaining("db.local");
            cut.Markup.MarkupMatchesContaining("Healthy");
        });
    }

    [Fact]
    public void ServerInfoPage_RendersConfigurationDiscoveryTabs()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderWithMudHost<ServerInfoPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Server info");
            cut.Markup.MarkupMatchesContaining("Honua:Database");
            cut.Markup.MarkupMatchesContaining("3 / 3 valid");
        });
    }

    [Theory]
    [InlineData(typeof(ConnectionDetailPage), "primary-postgis")]
    [InlineData(typeof(LayerListPage), "Parcels")]
    [InlineData(typeof(PublishLayerPage), "Publish layer")]
    [InlineData(typeof(LayerStylePage), "MapLibre style JSON")]
    [InlineData(typeof(ServiceListPage), "Default feature service")]
    [InlineData(typeof(ServiceSettingsPage), "Enabled protocols")]
    [InlineData(typeof(DeployControlPage), "Deploy control")]
    [InlineData(typeof(ObservabilityPage), "Sample recent error")]
    public void P0Pages_RenderWithStubClient(Type pageType, string expectedText)
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderAdminPage(pageType);

        cut.WaitForAssertion(() => cut.Markup.MarkupMatchesContaining(expectedText));
    }

    [Fact]
    public void IndexDashboard_SurfacesErrorBannerOnClientFailure()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new ThrowingAdminClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Index>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("simulated client failure");
        });
    }

    [Fact]
    public void AnnotationWorkspace_RendersDrawingLayersCommentsAndExports()
    {
        Services.AddScoped<AnnotationWorkspaceState>();

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.Annotations>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Waikiki field review");
            cut.Markup.MarkupMatchesContaining("Field review annotations");
            cut.Markup.MarkupMatchesContaining("Export");
            cut.Markup.MarkupMatchesContaining("Guest comments");
        });
    }

    [Fact]
    public void PublishingWorkspace_RendersConnectionsIntentAndEnvironmentState()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());
        Services.AddScoped<PublishingWorkspaceState>();

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.PublishingWorkspace>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Publishing workspace");
            cut.Markup.MarkupMatchesContaining("primary-postgis");
            cut.Markup.MarkupMatchesContaining("New");
            cut.Markup.MarkupMatchesContaining("Test");
            cut.Markup.MarkupMatchesContaining("Environment");
            cut.Markup.MarkupMatchesContaining("Parcels");
            cut.Markup.MarkupMatchesContaining("Unpublish all");
        });
    }

    private IRenderedFragment RenderAdminPage(Type pageType)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent(1, pageType);
            if (pageType == typeof(ConnectionDetailPage))
            {
                builder.AddAttribute(2, "ConnectionId", StubHonuaAdminClient.PrimaryConnectionId.ToString("D"));
            }
            else if (pageType == typeof(PublishLayerPage))
            {
                builder.AddAttribute(2, "ConnectionId", StubHonuaAdminClient.PrimaryConnectionId.ToString("D"));
            }
            else if (pageType == typeof(LayerStylePage))
            {
                builder.AddAttribute(2, "LayerId", 101);
            }
            else if (pageType == typeof(ServiceSettingsPage))
            {
                builder.AddAttribute(2, "ServiceName", "default");
            }

            builder.CloseComponent();
        });
    }

    private IRenderedFragment RenderWithMudHost<TComponent>() where TComponent : Microsoft.AspNetCore.Components.IComponent
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TComponent>(1);
            builder.CloseComponent();
        });
    }

    private sealed class ThrowingAdminClient : StubHonuaAdminClient
    {
        public override Task<FeatureOverview> GetFeatureOverviewAsync(CancellationToken cancellationToken)
            => throw new HttpRequestException("simulated client failure");
    }

    private sealed class NullAdminTelemetry : IAdminTelemetry
    {
        public void PageNavigated(string pageRoute, string? principalId) { }
        public void DestructiveAction(string action, string? targetId, string? principalId) { }
        public void ClientRequestFailed(string operation, string error) { }
    }
}

internal static class MarkupExtensions
{
    public static void MarkupMatchesContaining(this string actual, string expectedSubstring)
    {
        if (!actual.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected rendered markup to contain `{expectedSubstring}` but it did not.\nActual:\n{actual}");
        }
    }
}
