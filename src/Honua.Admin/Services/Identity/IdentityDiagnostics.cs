using System;
using System.Collections.Generic;
using System.Globalization;
using Honua.Sdk.Admin.Models;

namespace Honua.Admin.Services.Identity;

/// <summary>
/// Maps identity-admin server signals to operator-actionable copy. The lookup is
/// kept in one place so the shape of the surface stays auditable as honua-server
/// error strings evolve. A fall-through path renders the raw error so unknown
/// modes remain legible — see the table in <c>design.md</c>.
/// </summary>
public static class IdentityDiagnostics
{
    /// <summary>
    /// Severity tier for a diagnostic. Drives the badge / color in the UI.
    /// </summary>
    public enum DiagnosticSeverity
    {
        /// <summary>Provider is reachable and reporting healthy.</summary>
        Healthy,
        /// <summary>Reachable but the configuration appears inconsistent — soft warning.</summary>
        Warning,
        /// <summary>Provider is unreachable or misconfigured — operator must act.</summary>
        Error,
        /// <summary>Server has not reported on this provider yet.</summary>
        Unknown
    }

    /// <summary>
    /// Whether the failure mode is operator-actionable (e.g. fix configuration)
    /// vs. likely an upstream-IdP outage where the operator can only wait.
    /// </summary>
    public enum DiagnosticAction
    {
        /// <summary>No action required.</summary>
        None,
        /// <summary>Operator should change configuration to resolve.</summary>
        OperatorAction,
        /// <summary>Likely upstream — operator should wait or escalate.</summary>
        Wait
    }

    /// <summary>
    /// A diagnostic mapped from a server signal: what to render, plus whether the
    /// operator can act now or should wait on the upstream IdP.
    /// </summary>
    public readonly record struct DiagnosticCopy(
        string Title,
        string Action,
        DiagnosticSeverity Severity,
        DiagnosticAction Outcome);

    /// <summary>
    /// Map an OIDC provider reachability test (configured provider → discovery test).
    /// </summary>
    public static DiagnosticCopy ForOidcProviderTest(OidcProviderTestResponse response)
    {
        if (response is null)
        {
            return new DiagnosticCopy(
                "Unknown",
                "No reachability test result was reported.",
                DiagnosticSeverity.Unknown,
                DiagnosticAction.None);
        }

        if (response.IsReachable)
        {
            var formattedAt = response.TestedAt.ToString("u", CultureInfo.InvariantCulture);
            return new DiagnosticCopy(
                "Reachable",
                $"Discovery succeeded at {formattedAt}.",
                DiagnosticSeverity.Healthy,
                DiagnosticAction.None);
        }

        return MapErrorMessage(response.Message, providerKnownConfigured: true);
    }

    /// <summary>
    /// Map a catalog provider reachability test (no configured authority → discovery
    /// at runtime via the catalog).
    /// </summary>
    public static DiagnosticCopy ForIdentityProviderTest(IdentityProviderTestResult result)
    {
        if (result is null)
        {
            return new DiagnosticCopy(
                "Unknown",
                "No reachability test result was reported.",
                DiagnosticSeverity.Unknown,
                DiagnosticAction.None);
        }

        if (result.IsReachable)
        {
            var formatted = result.ResponseTimeMs is { } ms
                ? $"Reachable in {ms.ToString("F0", CultureInfo.InvariantCulture)} ms."
                : "Reachable.";
            if (!string.IsNullOrWhiteSpace(result.Issuer)
                && result.DiscoveryUrl is not null
                && !DiscoveryIssuerMatches(result.DiscoveryUrl.ToString(), result.Issuer!))
            {
                return new DiagnosticCopy(
                    "Issuer mismatch",
                    $"Discovery succeeded but issuer '{result.Issuer}' does not match the configured authority — verify the provider's authority URL matches the discovery document.",
                    DiagnosticSeverity.Warning,
                    DiagnosticAction.OperatorAction);
            }
            return new DiagnosticCopy(
                "Reachable",
                formatted,
                DiagnosticSeverity.Healthy,
                DiagnosticAction.None);
        }

        return MapErrorMessage(result.ErrorMessage, providerKnownConfigured: !IsNotConfigured(result.ErrorMessage));
    }

    /// <summary>
    /// Map a catalog provider's configuration-valid flag to a status badge for the
    /// status page (no test response in hand).
    /// </summary>
    public static DiagnosticCopy ForProviderStatus(IdentityProviderStatus provider)
    {
        if (!provider.Enabled)
        {
            return new DiagnosticCopy(
                "Disabled",
                "Provider is disabled — operators must enable it to allow logins.",
                DiagnosticSeverity.Unknown,
                DiagnosticAction.OperatorAction);
        }

        if (!provider.IsConfigurationValid)
        {
            return new DiagnosticCopy(
                "Configuration invalid",
                "Provider is enabled but the server reports the configuration is invalid — verify authority URL, callback path, and client credentials.",
                DiagnosticSeverity.Error,
                DiagnosticAction.OperatorAction);
        }

        if (string.IsNullOrWhiteSpace(provider.Authority))
        {
            return new DiagnosticCopy(
                "Authority missing",
                "Provider is enabled but no authority URL is configured — add an authority URL.",
                DiagnosticSeverity.Error,
                DiagnosticAction.OperatorAction);
        }

        return new DiagnosticCopy(
            "Configured",
            "Provider configuration is valid. Run a reachability test to verify upstream availability.",
            DiagnosticSeverity.Healthy,
            DiagnosticAction.None);
    }

    /// <summary>
    /// The set of "pending — see #NNN" cards rendered on the diagnostics page so
    /// the surface communicates the full diagnostic intent without claiming
    /// capability the server does not yet have. Each card resolves once the
    /// linked honua-server child ticket lands.
    /// </summary>
    public static IReadOnlyList<PendingDiagnostic> PendingDiagnostics { get; } = new[]
    {
        new PendingDiagnostic(
            "Clock skew check",
            "Compare server clock to the discovery document's timestamps. Surfaces when skew >5s.",
            "honua-server: extend IdentityProviderTestResult with clock-skew measurement"),
        new PendingDiagnostic(
            "Claim-mapping coverage",
            "Validate the configured claim mapping against the provider's discovery document and report missing claims.",
            "honua-server: add claim-mapping validation endpoint"),
        new PendingDiagnostic(
            "Callback URL drift",
            "Compare the configured callback path against the provider's registered redirect URIs.",
            "honua-server: add callback-URL drift check endpoint")
    };

    /// <summary>
    /// Diagnostic capability the server does not yet expose; used to render
    /// "Pending — see #NNN" cards on the diagnostics page.
    /// </summary>
    public sealed record PendingDiagnostic(string Title, string Description, string ServerTicket);

    private static DiagnosticCopy MapErrorMessage(string? rawMessage, bool providerKnownConfigured)
    {
        var message = rawMessage?.Trim() ?? string.Empty;
        if (message.Length == 0)
        {
            return new DiagnosticCopy(
                "Unreachable",
                "Discovery failed but the server returned no error detail — re-run the reachability test.",
                DiagnosticSeverity.Error,
                DiagnosticAction.Wait);
        }

        if (message.StartsWith("HTTP 404", StringComparison.OrdinalIgnoreCase))
        {
            return new DiagnosticCopy(
                "Discovery URL returned 404",
                "Verify the authority hostname and base path — the OIDC discovery document was not found.",
                DiagnosticSeverity.Error,
                DiagnosticAction.OperatorAction);
        }

        if (message.StartsWith("HTTP 401", StringComparison.OrdinalIgnoreCase)
            || message.StartsWith("HTTP 403", StringComparison.OrdinalIgnoreCase))
        {
            return new DiagnosticCopy(
                "Discovery rejected the request",
                "Verify the configured client credentials are correct; the provider rejected the discovery request.",
                DiagnosticSeverity.Error,
                DiagnosticAction.OperatorAction);
        }

        if (message.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new DiagnosticCopy(
                "Discovery request timed out",
                "Check network egress to the provider and confirm the upstream is online.",
                DiagnosticSeverity.Error,
                DiagnosticAction.Wait);
        }

        if (message.IndexOf("request failed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new DiagnosticCopy(
                "Discovery request failed",
                "Likely a DNS or TLS issue between the server and the provider — verify hostname resolution and TLS chain.",
                DiagnosticSeverity.Error,
                DiagnosticAction.OperatorAction);
        }

        if (IsNotConfigured(message))
        {
            return new DiagnosticCopy(
                "Provider not configured",
                "Add an authority URL for this provider before it can authenticate operators.",
                DiagnosticSeverity.Warning,
                DiagnosticAction.OperatorAction);
        }

        if (!providerKnownConfigured)
        {
            return new DiagnosticCopy(
                "Provider not configured",
                "Add an authority URL for this provider before it can authenticate operators.",
                DiagnosticSeverity.Warning,
                DiagnosticAction.OperatorAction);
        }

        if (message.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase))
        {
            return new DiagnosticCopy(
                "Discovery rejected the request",
                $"Provider responded with `{message}`. Verify provider configuration and try again.",
                DiagnosticSeverity.Error,
                DiagnosticAction.Wait);
        }

        return new DiagnosticCopy(
            "Unreachable",
            $"Discovery failed: {message}",
            DiagnosticSeverity.Error,
            DiagnosticAction.Wait);
    }

    private static bool IsNotConfigured(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }
        return message.IndexOf("not configured", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool DiscoveryIssuerMatches(string discoveryUrl, string issuer)
    {
        var trimmedDiscovery = discoveryUrl.TrimEnd('/');
        var marker = "/.well-known/openid-configuration";
        if (trimmedDiscovery.EndsWith(marker, StringComparison.OrdinalIgnoreCase))
        {
            trimmedDiscovery = trimmedDiscovery[..^marker.Length];
        }
        var trimmedIssuer = issuer.TrimEnd('/');
        return string.Equals(trimmedDiscovery, trimmedIssuer, StringComparison.OrdinalIgnoreCase);
    }
}
