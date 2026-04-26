namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// Typed error funnel for every connection client call. Razor pages branch on
/// <see cref="Kind"/> and render copy keyed by <see cref="CopyKey"/> — the
/// raw server message is for debug, never the primary signal.
/// </summary>
public sealed record ConnectionOperationError(
    ConnectionErrorKind Kind,
    string CopyKey,
    string? Detail = null);

public enum ConnectionErrorKind
{
    Network,
    Auth,
    Validation,
    Server,
    Conflict,
    NotFound
}
