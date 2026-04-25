using System.Threading;
using System.Threading.Tasks;

namespace Honua.Admin.Services.SpecWorkspace;

/// <summary>
/// Typed wrapper around browser localStorage. Separated from direct
/// <c>IJSRuntime</c> usage so tests can drop in an in-memory fake.
/// </summary>
public interface IBrowserStorageService
{
    ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default);
}
