using Microsoft.JSInterop;

namespace Honua.Admin.Auth;

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

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _serverUrl = await _js.InvokeAsync<string?>("localStorage.getItem", ServerUrlKey);
        _apiKey = await _js.InvokeAsync<string?>("localStorage.getItem", ApiKeyKey);
        _initialized = true;
    }

    public async Task LoginAsync(string serverUrl, string apiKey)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _apiKey = apiKey;
        await _js.InvokeVoidAsync("localStorage.setItem", ServerUrlKey, _serverUrl);
        await _js.InvokeVoidAsync("localStorage.setItem", ApiKeyKey, _apiKey);
    }

    public async Task LogoutAsync()
    {
        _serverUrl = null;
        _apiKey = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", ServerUrlKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", ApiKeyKey);
    }
}
