using System.Threading;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Honua.Admin.Services.SpecWorkspace;

public sealed class BrowserStorageService : IBrowserStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public BrowserStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, key);

    public async ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default) =>
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, key, value).ConfigureAwait(false);

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, key).ConfigureAwait(false);
}
