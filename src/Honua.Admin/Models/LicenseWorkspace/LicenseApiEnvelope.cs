namespace Honua.Admin.Models.LicenseWorkspace;

/// <summary>
/// Mirror of the honua-server <c>ApiResponse&lt;T&gt;</c> envelope used by the
/// admin license endpoints. Captured here (instead of taking a dependency on
/// the server assembly) so this project remains shippable as a standalone
/// Blazor WASM admin UI.
/// </summary>
public sealed class LicenseApiEnvelope<T> where T : class
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string? Error { get; init; }
}
