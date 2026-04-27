// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Auth;
using Honua.Admin.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Admin.Services.Admin;

public enum AdminRealtimeConnectionStatus
{
    Disabled,
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Unavailable,
}

public interface IAdminRealtimeConnection : IAsyncDisposable
{
    AdminRealtimeConnectionStatus Status { get; }
    Uri? HubUri { get; }
    string? LastError { get; }
    event Action? StateChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminRealtimeConnection : IAdminRealtimeConnection
{
    private readonly IOptions<HonuaAdminOptions> _options;
    private readonly AdminAuthStateProvider _auth;
    private readonly ILogger<AdminRealtimeConnection> _logger;
    private HubConnection? _connection;

    public AdminRealtimeConnection(
        IOptions<HonuaAdminOptions> options,
        AdminAuthStateProvider auth,
        ILogger<AdminRealtimeConnection> logger)
    {
        _options = options;
        _auth = auth;
        _logger = logger;
    }

    public AdminRealtimeConnectionStatus Status { get; private set; } = AdminRealtimeConnectionStatus.Disabled;
    public Uri? HubUri { get; private set; }
    public string? LastError { get; private set; }
    public event Action? StateChanged;

    public static Uri? ResolveHubUri(HonuaAdminOptions options, string? runtimeServerUrl)
    {
        if (Uri.TryCreate(options.HubUrl, UriKind.Absolute, out var explicitHubUri))
        {
            return explicitHubUri;
        }

        var baseUrl = !string.IsNullOrWhiteSpace(runtimeServerUrl)
            ? runtimeServerUrl
            : options.BaseUrl;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return null;
        }

        return new Uri(baseUri, "hubs/admin");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Status is AdminRealtimeConnectionStatus.Connected or AdminRealtimeConnectionStatus.Connecting)
        {
            return;
        }

        HubUri = ResolveHubUri(_options.Value, _auth.ServerUrl);
        if (HubUri is null)
        {
            SetStatus(AdminRealtimeConnectionStatus.Disabled);
            return;
        }

        _connection ??= BuildConnection(HubUri);

        try
        {
            SetStatus(AdminRealtimeConnectionStatus.Connecting);
            await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
            LastError = null;
            SetStatus(AdminRealtimeConnectionStatus.Connected);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastError = ex.Message;
            _logger.LogWarning(ex, "Admin SignalR hub unavailable at {HubUri}", HubUri);
            SetStatus(AdminRealtimeConnectionStatus.Unavailable);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            SetStatus(AdminRealtimeConnectionStatus.Disabled);
            return;
        }

        await _connection.StopAsync(cancellationToken).ConfigureAwait(false);
        SetStatus(AdminRealtimeConnectionStatus.Disconnected);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private HubConnection BuildConnection(Uri hubUri)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = ResolveAccessTokenAsync;
            })
            .WithAutomaticReconnect()
            .Build();

        connection.Reconnecting += ex =>
        {
            LastError = ex?.Message;
            SetStatus(AdminRealtimeConnectionStatus.Reconnecting);
            return Task.CompletedTask;
        };
        connection.Reconnected += _ =>
        {
            LastError = null;
            SetStatus(AdminRealtimeConnectionStatus.Connected);
            return Task.CompletedTask;
        };
        connection.Closed += ex =>
        {
            LastError = ex?.Message;
            SetStatus(AdminRealtimeConnectionStatus.Disconnected);
            return Task.CompletedTask;
        };

        return connection;
    }

    private Task<string?> ResolveAccessTokenAsync()
    {
        var token = !string.IsNullOrWhiteSpace(_auth.ApiKey)
            ? _auth.ApiKey
            : _options.Value.ApiKey;

        return Task.FromResult(string.IsNullOrWhiteSpace(token) ? null : token);
    }

    private void SetStatus(AdminRealtimeConnectionStatus status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;
        StateChanged?.Invoke();
    }
}
