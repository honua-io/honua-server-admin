// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Honua.Admin.Auth;

/// <summary>
/// Holds the operator's server URL + API key and persists them to localStorage.
/// Real OIDC-backed auth is owned by honua-server-admin#22; until that ships,
/// this scaffold gives the typed admin HTTP client a credential source. In dev
/// the values are hydrated from <c>HonuaAdminOptions</c> at startup.
/// </summary>
public sealed class AdminAuthStateProvider
{
    private const string ServerUrlKey = "honua_admin_server_url";
    private const string ApiKeyKey = "honua_admin_api_key";

    private readonly IJSRuntime _js;
    private string? _serverUrl;
    private string? _apiKey;
    private bool _initialized;

    public AdminAuthStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(_serverUrl) && !string.IsNullOrEmpty(_apiKey);

    public string ServerUrl => _serverUrl ?? string.Empty;

    public string ApiKey => _apiKey ?? string.Empty;

    public async Task InitializeAsync(string? defaultServerUrl = null, string? defaultApiKey = null)
    {
        if (_initialized)
        {
            return;
        }
        try
        {
            _serverUrl = await _js.InvokeAsync<string?>("localStorage.getItem", ServerUrlKey).ConfigureAwait(false);
            _apiKey = await _js.InvokeAsync<string?>("localStorage.getItem", ApiKeyKey).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // bunit / SSR hosts may not expose localStorage; fall back to defaults.
        }

        if (string.IsNullOrEmpty(_serverUrl) && !string.IsNullOrEmpty(defaultServerUrl))
        {
            _serverUrl = defaultServerUrl;
        }
        if (string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(defaultApiKey))
        {
            _apiKey = defaultApiKey;
        }

        _initialized = true;
    }

    public async Task LoginAsync(string serverUrl, string apiKey)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _apiKey = apiKey;
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", ServerUrlKey, _serverUrl).ConfigureAwait(false);
            await _js.InvokeVoidAsync("localStorage.setItem", ApiKeyKey, _apiKey).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // ignore in non-browser hosts
        }
    }

    public async Task LogoutAsync()
    {
        _serverUrl = null;
        _apiKey = null;
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", ServerUrlKey).ConfigureAwait(false);
            await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyKey).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // ignore in non-browser hosts
        }
    }
}
