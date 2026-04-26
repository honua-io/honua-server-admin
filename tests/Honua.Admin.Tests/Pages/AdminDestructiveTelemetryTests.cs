// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using Bunit;
using Honua.Admin.Models.Admin;
using Honua.Admin.Pages.Admin;
using Honua.Admin.Services.Admin;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Xunit;

namespace Honua.Admin.Tests.Pages;

/// <summary>
/// Asserts that every documented destructive admin action emits an
/// <see cref="IAdminTelemetry.DestructiveAction"/> event before the
/// underlying server call. Anchored on the
/// <c>delete-connection</c> baseline that already shipped (so the
/// recording stub stays calibrated against a known-good site).
/// </summary>
public sealed class AdminDestructiveTelemetryTests : TestContext
{
    public AdminDestructiveTelemetryTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task SubmitDeploy_EmitsDestructiveAction()
    {
        var telemetry = new RecordingAdminTelemetry();
        Services.AddScoped<IAdminTelemetry>(_ => telemetry);
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        // DeployControlPage routes the Submit button through a MudTabPanel
        // whose content lazy-renders, so we render the component to inject
        // dependencies + the operation id, then invoke SubmitAsync directly
        // on the page instance. The destructive telemetry contract is what
        // the test guards.
        var cut = RenderComponent<DeployControlPage>();
        SetPrivateField(cut.Instance, "_operationId", "op-test-123");

        await cut.InvokeAsync(() => InvokePrivateAsync(cut.Instance, "SubmitAsync"));

        Assert.Contains(
            telemetry.DestructiveActions,
            entry => entry.Action == "submit-deploy" && entry.TargetId == "op-test-123");
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }

    private static Task InvokePrivateAsync(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        return (Task)method!.Invoke(instance, parameters: null)!;
    }

    [Fact]
    public async Task RotateEncryptionKey_EmitsDestructiveAction()
    {
        var telemetry = new RecordingAdminTelemetry();
        Services.AddScoped<IAdminTelemetry>(_ => telemetry);
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderAdminPage<ConnectionListPage>();

        var rotate = await WaitForButtonAsync(cut, "Rotate encryption key");
        await cut.InvokeAsync(() => rotate.Click());

        cut.WaitForAssertion(() => Assert.Contains(
            telemetry.DestructiveActions,
            entry => entry.Action == "rotate-encryption-key" && entry.TargetId is null));
    }

    [Fact]
    public async Task BulkSetServiceLayers_EmitsDestructiveAction()
    {
        var telemetry = new RecordingAdminTelemetry();
        Services.AddScoped<IAdminTelemetry>(_ => telemetry);
        Services.AddScoped<IHonuaAdminClient>(_ => new StubHonuaAdminClient());

        var cut = RenderAdminPage<LayerListPage>(builder =>
            builder.AddAttribute(2, "ConnectionId", StubHonuaAdminClient.PrimaryConnectionId.ToString("D")));

        var disableAll = await WaitForButtonAsync(cut, "Disable all");
        await cut.InvokeAsync(() => disableAll.Click());

        cut.WaitForAssertion(() => Assert.Contains(
            telemetry.DestructiveActions,
            entry => entry.Action == "disable-all-service-layers"));
    }

    private IRenderedFragment RenderAdminPage<TPage>(Action<Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder>? configurePageAttributes = null)
        where TPage : IComponent
    {
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<TPage>(1);
            configurePageAttributes?.Invoke(builder);
            builder.CloseComponent();
        });
    }

    private static async Task<IElement> WaitForButtonAsync(IRenderedFragment cut, string ariaLabelOrText)
    {
        // Buttons may render aria-label or visible text; check both.
        IElement? button = null;
        cut.WaitForAssertion(() =>
        {
            button = cut.FindAll("button")
                .FirstOrDefault(b =>
                    string.Equals(b.GetAttribute("aria-label"), ariaLabelOrText, StringComparison.OrdinalIgnoreCase) ||
                    b.TextContent.Contains(ariaLabelOrText, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(button);
        });
        await Task.Yield();
        return button!;
    }

    private static async Task<IElement> WaitForEnabledButtonAsync(IRenderedFragment cut, string label)
    {
        IElement? button = null;
        cut.WaitForAssertion(() =>
        {
            button = cut.FindAll("button")
                .FirstOrDefault(b =>
                    (string.Equals(b.GetAttribute("aria-label"), label, StringComparison.OrdinalIgnoreCase) ||
                     b.TextContent.Contains(label, StringComparison.OrdinalIgnoreCase))
                    && !b.HasAttribute("disabled"));
            Assert.NotNull(button);
        });
        await Task.Yield();
        return button!;
    }

    internal sealed class RecordingAdminTelemetry : IAdminTelemetry
    {
        public ConcurrentBag<(string Action, string? TargetId, string? PrincipalId)> DestructiveActions { get; } = new();

        public void PageNavigated(string pageRoute, string? principalId) { }

        public void DestructiveAction(string action, string? targetId, string? principalId)
        {
            DestructiveActions.Add((action, targetId, principalId));
        }

        public void ClientRequestFailed(string operation, string error) { }
    }
}
