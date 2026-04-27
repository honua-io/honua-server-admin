// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Threading.Tasks;
using Honua.Admin.Auth;
using Honua.Admin.Configuration;
using Honua.Admin.Services.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class AdminRealtimeConnectionTests
{
    [Fact]
    public void ResolveHubUri_UsesExplicitHubUrl()
    {
        var options = new HonuaAdminOptions
        {
            BaseUrl = "https://server.example",
            HubUrl = "https://push.example/custom/admin",
        };

        var uri = AdminRealtimeConnection.ResolveHubUri(options, runtimeServerUrl: null);

        Assert.Equal("https://push.example/custom/admin", uri?.ToString().TrimEnd('/'));
    }

    [Fact]
    public void ResolveHubUri_DefaultsToAdminHubUnderBaseUrl()
    {
        var options = new HonuaAdminOptions
        {
            BaseUrl = "https://server.example/root/",
        };

        var uri = AdminRealtimeConnection.ResolveHubUri(options, runtimeServerUrl: null);

        Assert.Equal("https://server.example/root/hubs/admin", uri?.ToString().TrimEnd('/'));
    }

    [Fact]
    public void ResolveHubUri_PrefersRuntimeServerUrl()
    {
        var options = new HonuaAdminOptions
        {
            BaseUrl = "https://configured.example",
        };

        var uri = AdminRealtimeConnection.ResolveHubUri(options, "https://operator.example/");

        Assert.Equal("https://operator.example/hubs/admin", uri?.ToString().TrimEnd('/'));
    }

    [Fact]
    public async Task StartAsync_WithoutHubUrlLeavesConnectionDisabled()
    {
        var auth = new AdminAuthStateProvider(ThrowingJsRuntime.Instance);
        var realtime = new AdminRealtimeConnection(
            Options.Create(new HonuaAdminOptions()),
            auth,
            NullLogger<AdminRealtimeConnection>.Instance);

        await realtime.StartAsync();

        Assert.Equal(AdminRealtimeConnectionStatus.Disabled, realtime.Status);
        Assert.Null(realtime.HubUri);
    }

    private sealed class ThrowingJsRuntime : IJSRuntime
    {
        public static readonly ThrowingJsRuntime Instance = new();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new InvalidOperationException("JS interop is not available in this test.");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            System.Threading.CancellationToken cancellationToken,
            object?[]? args)
            => throw new InvalidOperationException("JS interop is not available in this test.");
    }
}
