using System;
using System.Collections.Generic;
using Honua.Admin.Models.DataConnections;

namespace Honua.Admin.Services.DataConnections;

/// <summary>
/// The single seam between the server's free-form <c>Message</c> and the
/// six-cell structured surface. Every page renders the structured cells, never
/// the raw message — keeping the heuristic isolated means the server-side fix
/// (structured per-step codes) reaches every consumer in one change.
/// </summary>
public static class DiagnosticMapper
{
    private static readonly DiagnosticStep[] AllSteps =
    {
        DiagnosticStep.Dns,
        DiagnosticStep.Tcp,
        DiagnosticStep.Auth,
        DiagnosticStep.Ssl,
        DiagnosticStep.Capability,
        DiagnosticStep.Version
    };

    public static ConnectionDiagnostic Map(ConnectionTestOutcome outcome, DataConnectionDetail? detail = null)
    {
        if (outcome.IsHealthy)
        {
            return BuildHealthy(outcome, detail);
        }

        return BuildFailed(outcome);
    }

    private static ConnectionDiagnostic BuildHealthy(ConnectionTestOutcome outcome, DataConnectionDetail? detail)
    {
        var cells = new List<DiagnosticCell>(AllSteps.Length);
        foreach (var step in AllSteps)
        {
            var detailText = step switch
            {
                DiagnosticStep.Capability when detail is not null => $"{detail.SslMode} / managed={detail.StorageType}",
                DiagnosticStep.Version when detail is not null => detail.HealthStatus,
                _ => null
            };
            cells.Add(new DiagnosticCell(step, DiagnosticStatus.Ok, detailText));
        }

        return new ConnectionDiagnostic
        {
            Cells = cells,
            RawOutcome = outcome
        };
    }

    private static ConnectionDiagnostic BuildFailed(ConnectionTestOutcome outcome)
    {
        var message = outcome.Message ?? string.Empty;
        var failed = ClassifyFailure(message);

        var cells = new List<DiagnosticCell>(AllSteps.Length);
        foreach (var step in AllSteps)
        {
            if (step == failed)
            {
                cells.Add(new DiagnosticCell(step, DiagnosticStatus.Failed, message, RemediationKey(step)));
            }
            else
            {
                cells.Add(new DiagnosticCell(step, DiagnosticStatus.NotAssessed));
            }
        }

        return new ConnectionDiagnostic
        {
            Cells = cells,
            RawOutcome = outcome
        };
    }

    /// <summary>
    /// Narrow heuristic — only well-known phrases promote a cell to Failed; an
    /// unmatched string lights only the Auth fallback. Unrelated cells stay
    /// NotAssessed so we do not produce false negatives.
    /// </summary>
    internal static DiagnosticStep ClassifyFailure(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return DiagnosticStep.Auth;
        }

        if (Contains(message, "name resolution") || Contains(message, "dns") || Contains(message, "no such host"))
        {
            return DiagnosticStep.Dns;
        }

        if (Contains(message, "ssl") || Contains(message, "tls") || Contains(message, "certificate") || Contains(message, "handshake"))
        {
            return DiagnosticStep.Ssl;
        }

        if (Contains(message, "authentication") || Contains(message, "password") || Contains(message, "role does not exist") || Contains(message, "permission denied"))
        {
            return DiagnosticStep.Auth;
        }

        if (Contains(message, "timeout") || Contains(message, "unreachable") || Contains(message, "refused") || Contains(message, "connection reset"))
        {
            return DiagnosticStep.Tcp;
        }

        if (Contains(message, "version") || Contains(message, "unsupported server"))
        {
            return DiagnosticStep.Version;
        }

        if (Contains(message, "extension") || Contains(message, "capability") || Contains(message, "feature"))
        {
            return DiagnosticStep.Capability;
        }

        return DiagnosticStep.Auth;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string RemediationKey(DiagnosticStep step) => step switch
    {
        DiagnosticStep.Dns => "remediation.dns",
        DiagnosticStep.Tcp => "remediation.tcp",
        DiagnosticStep.Auth => "remediation.auth",
        DiagnosticStep.Ssl => "remediation.ssl",
        DiagnosticStep.Capability => "remediation.capability",
        DiagnosticStep.Version => "remediation.version",
        _ => "remediation.unknown"
    };
}
