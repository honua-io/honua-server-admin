using System.Collections.Generic;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// Minimal RFC 7807 mirror used by the HTTP client to translate server error
/// bodies into typed <see cref="ConnectionOperationError"/> values.
/// </summary>
public sealed class ProblemDetailsPayload
{
    public string? Type { get; init; }

    public string? Title { get; init; }

    public int? Status { get; init; }

    public string? Detail { get; init; }

    public string? Instance { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Errors { get; init; }
}
