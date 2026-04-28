// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Threading.Tasks;
using Honua.Admin.Auth;
using Honua.Admin.Configuration;
using Honua.Admin.Models.Admin;
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
            new AdminRealtimeEventBus(),
            NullLogger<AdminRealtimeConnection>.Instance);

        await realtime.StartAsync();

        Assert.Equal(AdminRealtimeConnectionStatus.Disabled, realtime.Status);
        Assert.Null(realtime.HubUri);
    }

    [Fact]
    public void EventBus_PublishesTypedRealtimeEvents()
    {
        var bus = new AdminRealtimeEventBus();
        RecentErrorEntry? receivedError = null;
        DeployOperation? receivedDeployOperation = null;
        MigrationObservabilityResponse? receivedMigration = null;
        DataConnectionHealthChangedEvent? receivedHealth = null;
        bus.RecentErrorReceived += entry => receivedError = entry;
        bus.DeployOperationChanged += operation => receivedDeployOperation = operation;
        bus.MigrationStatusChanged += status => receivedMigration = status;
        bus.ConnectionHealthChanged += health => receivedHealth = health;

        var error = new RecentErrorEntry
        {
            Timestamp = DateTimeOffset.Parse("2026-04-28T08:30:00Z"),
            CorrelationId = "corr-1",
            Path = "/api/v1/admin/layers",
            StatusCode = 500,
            Message = "failed"
        };
        var operation = new DeployOperation { OperationId = "op-1", Status = "Reconciling" };
        var migration = new MigrationObservabilityResponse { Status = "Ready", IsReady = true };
        var health = new DataConnectionHealthChangedEvent
        {
            ConnectionId = Guid.NewGuid(),
            HealthStatus = "Healthy"
        };

        bus.PublishRecentError(error);
        bus.PublishDeployOperationChanged(operation);
        bus.PublishMigrationStatusChanged(migration);
        bus.PublishConnectionHealthChanged(health);

        Assert.Same(error, receivedError);
        Assert.Same(operation, receivedDeployOperation);
        Assert.Same(migration, receivedMigration);
        Assert.Same(health, receivedHealth);
        Assert.NotNull(bus.LastEventReceivedAt);
    }

    [Fact]
    public void AddRecentError_TrimsAndDeduplicatesByCorrelationId()
    {
        var current = new RecentErrorsResponse
        {
            Capacity = 2,
            InstanceId = "admin-1",
            Errors =
            [
                Error("corr-1", "2026-04-28T08:00:00Z", "/old"),
                Error("corr-2", "2026-04-28T08:01:00Z", "/keep")
            ]
        };

        var updated = AdminRealtimeReducers.AddRecentError(
            current,
            Error("corr-1", "2026-04-28T08:02:00Z", "/new"));

        Assert.Equal(2, updated.Errors.Count);
        Assert.Equal("admin-1", updated.InstanceId);
        Assert.Collection(
            updated.Errors,
            entry => Assert.Equal("/new", entry.Path),
            entry => Assert.Equal("/keep", entry.Path));
    }

    private static RecentErrorEntry Error(string correlationId, string timestamp, string path)
        => new()
        {
            Timestamp = DateTimeOffset.Parse(timestamp),
            CorrelationId = correlationId,
            Path = path,
            StatusCode = 500,
            Message = "failed"
        };

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
