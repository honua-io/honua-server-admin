using System.Collections.Generic;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// One row in the managed-Postgres certification matrix (Aurora / Azure DB).
/// The renderer pairs each row with remediation copy keyed by
/// <see cref="RemediationKey"/>.
/// </summary>
public sealed record CapabilityCheck(
    string CheckId,
    string DisplayName,
    string Category,
    string RemediationKey)
{
    public DiagnosticStatus Status { get; init; } = DiagnosticStatus.NotAssessed;

    public string? Detail { get; init; }
}

/// <summary>
/// Provider-level matrix of expected checks; each provider returns its own
/// list. Until <c>honua-server#644</c> lands an endpoint that drives these,
/// rows render as <c>NotAssessed</c> with a "needs #644" hover. False
/// negatives would erode trust, so the renderer never synthesizes a "fail".
/// </summary>
public sealed class ProviderCapabilityMatrix
{
    public required string ProviderId { get; init; }

    public required IReadOnlyList<CapabilityCheck> Checks { get; init; }

    /// <summary>
    /// True when the matrix data came from a real server endpoint. Today this
    /// is always false; once <c>#644</c> lands we flip it per-row.
    /// </summary>
    public bool IsServerSourced { get; init; }
}
