using System;
using System.Collections.Generic;

namespace Honua.Admin.Models.DataConnections;

/// <summary>
/// One of six fixed diagnostic steps rendered in deterministic order by the
/// preflight surface. Adding a step would change the contract; the order and
/// the set are intentionally frozen so the matcher and the renderer stay in
/// sync.
/// </summary>
public enum DiagnosticStep
{
    Dns,
    Tcp,
    Auth,
    Ssl,
    Capability,
    Version
}

public enum DiagnosticStatus
{
    NotAssessed,
    Ok,
    Failed
}

public sealed record DiagnosticCell(
    DiagnosticStep Step,
    DiagnosticStatus Status,
    string? Detail = null,
    string? RemediationKey = null);

/// <summary>
/// Full preflight result projected into the six-cell structured surface, plus
/// the raw test outcome so callers can log / debug without re-fetching.
/// </summary>
public sealed class ConnectionDiagnostic
{
    public required IReadOnlyList<DiagnosticCell> Cells { get; init; }

    public required ConnectionTestOutcome RawOutcome { get; init; }

    public bool AnyFailed
    {
        get
        {
            foreach (var cell in Cells)
            {
                if (cell.Status == DiagnosticStatus.Failed)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public DiagnosticCell GetCell(DiagnosticStep step)
    {
        foreach (var cell in Cells)
        {
            if (cell.Step == step)
            {
                return cell;
            }
        }
        return new DiagnosticCell(step, DiagnosticStatus.NotAssessed);
    }
}

/// <summary>
/// Wire shape for <c>POST /api/v1/admin/connections/test</c> and
/// <c>POST /api/v1/admin/connections/{id}/test</c>. Today the server returns
/// only <c>IsHealthy + Message</c>; the diagnostic mapper distributes that
/// signal across the six cells. Server gap recorded in the gap report.
/// </summary>
public sealed class ConnectionTestOutcome
{
    public required Guid ConnectionId { get; init; }

    public required string ConnectionName { get; init; }

    public bool IsHealthy { get; init; }

    public DateTimeOffset TestedAt { get; init; }

    public string? Message { get; init; }
}
