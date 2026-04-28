// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AngleSharp.Dom;
using Bunit;
using Honua.Admin.Models.Admin;
using Honua.Admin.Pages.Admin;
using Honua.Admin.Services.Admin;
using Honua.Admin.Services.Annotations;
using Honua.Admin.Services.AppBuilder;
using Honua.Admin.Services.Operations;
using Honua.Admin.Services.PrintService;
using Honua.Admin.Services.Publishing;
using Honua.Admin.Services.SpecWorkspace;
using Honua.Admin.Services.UsageAnalytics;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Honua.Admin.Tests.Pages;

/// <summary>
/// Baseline quality gates for the top admin workflows tracked by issue #9.
/// These are intentionally lightweight bunit checks so they can run on every PR.
/// </summary>
public sealed class AdminQualityGateTests : TestContext
{
    private static readonly TimeSpan StubRenderBudget = TimeSpan.FromSeconds(10);

    public AdminQualityGateTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddScoped<IAdminTelemetry, NullAdminTelemetry>();
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());
        Services.AddScoped<AnnotationWorkspaceState>();
        Services.AddScoped<OperationsConsoleState>();
        Services.AddScoped<PublishingWorkspaceState>();
        Services.AddScoped<IUsageAnalyticsClient, StubUsageAnalyticsClient>();
        Services.AddScoped<UsageAnalyticsState>();
        Services.AddScoped<IPrintServiceClient, StubPrintServiceClient>();
        Services.AddScoped<PrintServiceState>();
        Services.AddScoped<IAppBuilderClient, StubAppBuilderClient>();
        Services.AddScoped<AppBuilderState>();
        Services.AddScoped<CatalogCache>();
        Services.AddScoped<IBrowserStorageService, BrowserStorageService>();
        Services.AddScoped<ISpecWorkspaceTelemetry, NullSpecWorkspaceTelemetry>();
        Services.AddScoped<ISpecWorkspaceClient, StubSpecWorkspaceClient>();
        Services.AddScoped<SpecWorkspaceState>();
        Services.AddTestRealtime();
    }

    public static IEnumerable<object[]> TopWorkflows()
    {
        yield return
        [
            "Dashboard",
            typeof(Honua.Admin.Pages.Index),
            "Honua Server Administration",
            new[] { "[aria-label='Feature overview']", "a[href='/connections']" },
        ];
        yield return
        [
            "Control center",
            typeof(Honua.Admin.Pages.Operator.ControlCenter),
            "Governance queue",
            new[] { "[aria-label='Control center surfaces']", "[aria-label='Governance queue']", "[aria-label='Release evidence control center']" },
        ];
        yield return
        [
            "Admin readiness",
            typeof(Honua.Admin.Pages.Operator.AdminReadiness),
            "Administration readiness",
            new[] { "[aria-label='Administration readiness surfaces']", "[aria-label='Administration readiness checklist']", "[aria-label='Readiness connection inventory']", "[aria-label='Gated feature diagnostics']" },
        ];
        yield return
        [
            "Spec workspace",
            typeof(Honua.Admin.Pages.Operator.SpecWorkspace),
            "Spec workspace S1",
            new[] { "[aria-label='Spec workspace S1 readiness']", "[data-testid='spec-workspace-root']", "[data-testid='spec-nl-pane']", "[data-testid='spec-dsl-pane']", "[data-testid='spec-preview-pane']" },
        ];
        yield return
        [
            "Connections",
            typeof(ConnectionListPage),
            "primary-postgis",
            new[] { "[aria-label='Connections list']", "a[href='/connections/new']" },
        ];
        yield return
        [
            "Layers",
            typeof(LayerListPage),
            "Parcels",
            new[] { "[aria-label='Published layers']", "a[href$='/publish']" },
        ];
        yield return
        [
            "Services",
            typeof(ServiceListPage),
            "Default feature service",
            new[] { "[aria-label='Services']", "a[href='/services/default/settings']" },
        ];
        yield return
        [
            "Deploy",
            typeof(DeployControlPage),
            "Run preflight",
            new[] { "button" },
        ];
        yield return
        [
            "Observability",
            typeof(ObservabilityPage),
            "Sample recent error",
            new[] { "[aria-label='Recent errors']" },
        ];
        yield return
        [
            "Server info",
            typeof(ServerInfoPage),
            "Honua:Database",
            new[] { "[aria-label='Configuration metadata']" },
        ];
        yield return
        [
            "Annotations",
            typeof(Honua.Admin.Pages.Operator.Annotations),
            "Waikiki field review",
            new[] { "[aria-label='Annotation workspace toolbar']", "[aria-label='Annotation layers']" },
        ];
        yield return
        [
            "Publishing",
            typeof(Honua.Admin.Pages.Operator.PublishingWorkspace),
            "primary-postgis",
            new[] { "[aria-label='Publishing workspace toolbar']", "[aria-label='Publishing validation checks']" },
        ];
        yield return
        [
            "Usage analytics",
            typeof(Honua.Admin.Pages.Operator.UsageAnalytics),
            "Queries per second",
            new[] { "[aria-label='Usage analytics toolbar']", "[aria-label='Popular layers']" },
        ];
        yield return
        [
            "Print service",
            typeof(Honua.Admin.Pages.Operator.PrintService),
            "Letter portrait",
            new[] { "[aria-label='Print service toolbar']", "[aria-label='Print preview']", "[aria-label='Print job queue']" },
        ];
        yield return
        [
            "App builder",
            typeof(Honua.Admin.Pages.Operator.AppBuilder),
            "Harbor operations dashboard",
            new[] { "[aria-label='App builder toolbar']", "[aria-label='Widget library']", "[aria-label='App layout canvas']", "[aria-label='App builder validation checks']" },
        ];
    }

    [Theory]
    [MemberData(nameof(TopWorkflows))]
    public void TopWorkflowPages_RenderWithinBudgetAndMeetAccessibilityBaseline(
        string workflowName,
        Type pageType,
        string expectedText,
        string[] requiredSelectors)
    {
        var stopwatch = Stopwatch.StartNew();

        var cut = RenderAdminPage(pageType);

        cut.WaitForAssertion(() => cut.Markup.MarkupMatchesContaining(expectedText), StubRenderBudget);
        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed <= StubRenderBudget,
            $"{workflowName} exceeded the {StubRenderBudget.TotalSeconds:0}s stub render budget. Actual: {stopwatch.Elapsed.TotalMilliseconds:0} ms.");

        foreach (var selector in requiredSelectors)
        {
            cut.Find(selector);
        }

        AssertInteractiveElementsHaveAccessibleNames(cut);
        AssertTablesHaveAccessibleNames(cut);
    }

    private IRenderedFragment RenderAdminPage(Type pageType)
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent(1, pageType);
            builder.CloseComponent();
        });
    }

    private static void AssertInteractiveElementsHaveAccessibleNames(IRenderedFragment cut)
    {
        var unlabeled = cut.FindAll("a[href], button, input:not([type='hidden']), select, textarea")
            .Where(element => !HasAccessibleName(element))
            .Select(Describe)
            .ToArray();

        Assert.True(
            unlabeled.Length == 0,
            "Interactive elements must expose accessible names: " + string.Join(", ", unlabeled));
    }

    private static void AssertTablesHaveAccessibleNames(IRenderedFragment cut)
    {
        var unnamed = cut.FindAll("table")
            .Where(table => !HasTableName(table))
            .Select(Describe)
            .ToArray();

        Assert.True(
            unnamed.Length == 0,
            "Tables must have aria-label, aria-labelledby, or caption: " + string.Join(", ", unnamed));
    }

    private static bool HasAccessibleName(IElement element)
    {
        if (HasTextAttribute(element, "aria-label") ||
            HasTextAttribute(element, "title") ||
            HasTextAttribute(element, "placeholder") ||
            !string.IsNullOrWhiteSpace(element.TextContent))
        {
            return true;
        }

        if (HasLabelledByText(element))
        {
            return true;
        }

        var id = element.GetAttribute("id");
        if (!string.IsNullOrWhiteSpace(id) &&
            element.Owner?.QuerySelector($"label[for='{id}']") is { TextContent: var labelText } &&
            !string.IsNullOrWhiteSpace(labelText))
        {
            return true;
        }

        return element.ParentElement?.LocalName.Equals("label", StringComparison.OrdinalIgnoreCase) == true &&
            !string.IsNullOrWhiteSpace(element.ParentElement.TextContent);
    }

    private static bool HasTableName(IElement table)
    {
        return HasTextAttribute(table, "aria-label") ||
            HasLabelledByText(table) ||
            table.QuerySelector("caption") is { TextContent: var caption } &&
            !string.IsNullOrWhiteSpace(caption) ||
            HasNamedMudTableHost(table);
    }

    private static bool HasNamedMudTableHost(IElement table)
    {
        for (var element = table.ParentElement; element is not null; element = element.ParentElement)
        {
            if ((element.ClassList.Contains("mud-table") || element.ClassList.Contains("mud-simple-table")) &&
                (HasTextAttribute(element, "aria-label") || HasLabelledByText(element)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLabelledByText(IElement element)
    {
        var labelledBy = element.GetAttribute("aria-labelledby");
        if (string.IsNullOrWhiteSpace(labelledBy) || element.Owner is null)
        {
            return false;
        }

        return labelledBy.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => element.Owner.GetElementById(id))
            .Any(label => !string.IsNullOrWhiteSpace(label?.TextContent));
    }

    private static bool HasTextAttribute(IElement element, string attributeName)
        => !string.IsNullOrWhiteSpace(element.GetAttribute(attributeName));

    private static string Describe(IElement element)
    {
        var id = element.GetAttribute("id");
        var classes = element.GetAttribute("class");
        return $"<{element.LocalName}{DescribeAttribute("id", id)}{DescribeAttribute("class", classes)}>";
    }

    private static string DescribeAttribute(string name, string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : $" {name}='{value}'";

    private sealed class NullAdminTelemetry : IAdminTelemetry
    {
        public void PageNavigated(string pageRoute, string? principalId) { }
        public void DestructiveAction(string action, string? targetId, string? principalId) { }
        public void ClientRequestFailed(string operation, string error) { }
    }

    private sealed class NullSpecWorkspaceTelemetry : ISpecWorkspaceTelemetry
    {
        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null) { }
    }
}
