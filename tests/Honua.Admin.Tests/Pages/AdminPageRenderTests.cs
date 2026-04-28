// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Honua.Admin.Models.Admin;
using Honua.Admin.Models.SpecWorkspace;
using Honua.Admin.Pages.Admin;
using Honua.Admin.Services.Admin;
using Honua.Admin.Services.Annotations;
using Honua.Admin.Services.AppBuilder;
using Honua.Admin.Services.Operations;
using Honua.Admin.Services.OpenDataHub;
using Honua.Admin.Services.PrintService;
using Honua.Admin.Services.Publishing;
using Honua.Admin.Services.SpecWorkspace;
using Honua.Admin.Services.UsageAnalytics;
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
        Services.AddScoped<IUsageAnalyticsClient, StubUsageAnalyticsClient>();
        Services.AddScoped<UsageAnalyticsState>();
        Services.AddScoped<IPrintServiceClient, StubPrintServiceClient>();
        Services.AddScoped<PrintServiceState>();
        Services.AddScoped<IAppBuilderClient, StubAppBuilderClient>();
        Services.AddScoped<AppBuilderState>();
        Services.AddScoped<IOpenDataHubClient, StubOpenDataHubClient>();
        Services.AddScoped<OpenDataHubState>();
        Services.AddScoped<OperationsConsoleState>();
        Services.AddScoped<CatalogCache>();
        Services.AddScoped<IBrowserStorageService, MemoryBrowserStorageService>();
        Services.AddScoped<ISpecWorkspaceTelemetry, NullSpecWorkspaceTelemetry>();
        Services.AddScoped<ISpecWorkspaceClient, StubSpecWorkspaceClient>();
        Services.AddScoped<SpecWorkspaceState>();
        Services.AddTestRealtime();
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

    [Fact]
    public void OperationsConsole_RendersReleaseDriftAndTroubleshootingState()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.OperationsConsole>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Operations console");
            cut.Markup.MarkupMatchesContaining("Rollout health");
            cut.Markup.MarkupMatchesContaining("Drift");
            cut.Markup.MarkupMatchesContaining("Release evidence");
            cut.Markup.MarkupMatchesContaining("Troubleshooting");
            cut.Markup.MarkupMatchesContaining("Sample recent error");
        });
    }

    [Fact]
    public void OperationsConsole_RendersRecentErrorsFailureInsteadOfEmptyState()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new RecentErrorsUnavailableClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.OperationsConsole>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Recent errors unavailable");
            Assert.False(cut.Markup.Contains("No recent errors loaded.", StringComparison.OrdinalIgnoreCase), cut.Markup);
        });
    }

    [Fact]
    public void ControlCenter_RendersGovernanceEvidenceAndTroubleshootingHandoff()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.ControlCenter>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Admin control center");
            cut.Markup.MarkupMatchesContaining("Governance queue");
            cut.Markup.MarkupMatchesContaining("Release evidence");
            cut.Markup.MarkupMatchesContaining("Troubleshooting handoff");
            cut.Markup.MarkupMatchesContaining("Promote staging manifest");
            cut.Markup.MarkupMatchesContaining("0123456789ab - pending_approval");
        });
    }

    [Fact]
    public void AdminReadiness_RendersIdentityLicenseAndConnectionOverview()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.AdminReadiness>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Administration readiness");
            cut.Markup.MarkupMatchesContaining("Identity and access");
            cut.Markup.MarkupMatchesContaining("Enterprise");
            cut.Markup.MarkupMatchesContaining("primary-postgis");
            cut.Markup.MarkupMatchesContaining("Encryption service is working correctly");
            cut.Markup.MarkupMatchesContaining("Advanced Observability");
        });
    }

    [Fact]
    public void AdminReadiness_RendersEditionFailureAsUnavailableDiagnostics()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new EditionUnavailableClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.AdminReadiness>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("edition unavailable");
            Assert.DoesNotContain("No gated features reported", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void AdminReadiness_MarksConnectionReadinessFromHealth()
    {
        Services.AddScoped<IHonuaAdminClient>(_ => new UnhealthyConnectionClient());

        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.AdminReadiness>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("0 healthy connection(s)");
            var readiness = cut.Find("[data-testid='connection-readiness-state']");
            Assert.Equal("error", readiness.GetAttribute("data-readiness"));
        });
    }

    [Fact]
    public async Task SpecWorkspace_RendersS1ReadinessEvidence()
    {
        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.SpecWorkspace>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Spec workspace S1");
            cut.Markup.MarkupMatchesContaining("grammar v1");
            cut.Markup.MarkupMatchesContaining("5 sections");
            cut.Markup.MarkupMatchesContaining("plan/apply");
            cut.Markup.MarkupMatchesContaining("map/table/app preview");
            cut.Find("[aria-label='Spec workspace S1 readiness']");
        });

        var state = Services.GetRequiredService<SpecWorkspaceState>();
        await cut.InvokeAsync(() => state.UpdateSectionTextAsync(SpecSectionId.Sources, "@parcels = parcels"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("1/5 drafted");
        });
    }

    [Fact]
    public void UsageAnalytics_RendersMetricsDrilldownsAndExports()
    {
        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.UsageAnalytics>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Usage analytics");
            cut.Markup.MarkupMatchesContaining("Queries per second");
            cut.Markup.MarkupMatchesContaining("Popular layers");
            cut.Markup.MarkupMatchesContaining("Parcels");
            cut.Markup.MarkupMatchesContaining("CSV");
            cut.Markup.MarkupMatchesContaining("PDF");
        });
    }

    [Fact]
    public void PrintService_RendersTemplatesPreviewAndQueue()
    {
        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.PrintService>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Print service");
            cut.Markup.MarkupMatchesContaining("Letter portrait");
            cut.Markup.MarkupMatchesContaining("Preview");
            cut.Markup.MarkupMatchesContaining("Queue export");
            cut.Markup.MarkupMatchesContaining("Planning commission packet");
        });
    }

    [Fact]
    public void AppBuilder_RendersTemplatesWidgetsCanvasAndValidation()
    {
        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.AppBuilder>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("App builder");
            cut.Markup.MarkupMatchesContaining("Operations dashboard");
            cut.Markup.MarkupMatchesContaining("Widget library");
            cut.Markup.MarkupMatchesContaining("Harbor operations dashboard");
            cut.Markup.MarkupMatchesContaining("Open incidents");
            cut.Markup.MarkupMatchesContaining("Publishing readiness");
            cut.Markup.MarkupMatchesContaining("Standalone URL");
            cut.Markup.MarkupMatchesContaining("Custom domain");
            cut.Markup.MarkupMatchesContaining("App builder validation checks");
        });
    }

    [Fact]
    public void OpenDataHub_RendersCatalogDeliveryAndValidation()
    {
        var cut = RenderWithMudHost<Honua.Admin.Pages.Operator.OpenDataHub>();

        cut.WaitForAssertion(() =>
        {
            cut.Markup.MarkupMatchesContaining("Open data hub");
            cut.Markup.MarkupMatchesContaining("Published datasets");
            cut.Markup.MarkupMatchesContaining("Harbor assets");
            cut.Markup.MarkupMatchesContaining("GeoJSON");
            cut.Markup.MarkupMatchesContaining("Civic tech API");
            cut.Markup.MarkupMatchesContaining("Readiness checks");
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

    private sealed class RecentErrorsUnavailableClient : StubHonuaAdminClient
    {
        public override Task<RecentErrorsResponse> GetRecentErrorsAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("recent errors unavailable");
    }

    private sealed class EditionUnavailableClient : StubHonuaAdminClient
    {
        public override Task<FeatureOverview> GetFeatureOverviewAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("edition unavailable");
    }

    private sealed class UnhealthyConnectionClient : StubHonuaAdminClient
    {
        public override Task<IReadOnlyList<ConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<ConnectionSummary>>(
                [Connections[0] with { HealthStatus = "Unhealthy" }]);
    }

    private sealed class NullAdminTelemetry : IAdminTelemetry
    {
        public void PageNavigated(string pageRoute, string? principalId) { }
        public void DestructiveAction(string action, string? targetId, string? principalId) { }
        public void ClientRequestFailed(string operation, string error) { }
    }

    private sealed class NullSpecWorkspaceTelemetry : ISpecWorkspaceTelemetry
    {
        public void Record(string eventName, IReadOnlyDictionary<string, object?>? dimensions = null) { }
        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? dimensions = null) { }
    }

    private sealed class MemoryBrowserStorageService : IBrowserStorageService
    {
        private readonly Dictionary<string, string> _items = new(StringComparer.Ordinal);

        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(key, out var value);
            return ValueTask.FromResult<string?>(value);
        }

        public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _items[key] = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            _items.Remove(key);
            return ValueTask.CompletedTask;
        }
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
