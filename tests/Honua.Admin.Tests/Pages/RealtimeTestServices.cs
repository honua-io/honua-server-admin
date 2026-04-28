// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Services.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Admin.Tests.Pages;

internal static class RealtimeTestServices
{
    public static IServiceCollection AddTestRealtime(this IServiceCollection services)
    {
        services.AddScoped<AdminRealtimeEventBus>();
        services.AddScoped<IAdminRealtimeEvents>(sp => sp.GetRequiredService<AdminRealtimeEventBus>());
        services.AddScoped<IAdminRealtimeEventPublisher>(sp => sp.GetRequiredService<AdminRealtimeEventBus>());
        services.AddScoped<IAdminRealtimeConnection, TestAdminRealtimeConnection>();
        return services;
    }

    private sealed class TestAdminRealtimeConnection : IAdminRealtimeConnection
    {
        public AdminRealtimeConnectionStatus Status { get; private set; } = AdminRealtimeConnectionStatus.Disabled;
        public Uri? HubUri => null;
        public string? LastError => null;
        public event Action? StateChanged;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            Status = AdminRealtimeConnectionStatus.Disabled;
            StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            Status = AdminRealtimeConnectionStatus.Disconnected;
            StateChanged?.Invoke();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
