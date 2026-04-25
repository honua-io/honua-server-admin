using System;
using Honua.Admin.Models.Identity;
using Honua.Admin.Services.Identity;
using Xunit;

namespace Honua.Admin.Tests.Identity;

/// <summary>
/// Diagnostic-copy mapping is the surface that operators see when reachability
/// fails. These tests pin every documented failure mode against the
/// table in <c>design.md</c> so a server message change doesn't silently regress
/// the actionable copy.
/// </summary>
public sealed class IdentityDiagnosticsTests
{
    [Theory]
    [InlineData("HTTP 404 Not Found", "Discovery URL returned 404", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    [InlineData("HTTP 401 Unauthorized", "Discovery rejected the request", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    [InlineData("HTTP 403 Forbidden", "Discovery rejected the request", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    [InlineData("Identity provider discovery request timed out.", "Discovery request timed out", IdentityDiagnostics.DiagnosticAction.Wait)]
    [InlineData("Identity provider discovery request failed.", "Discovery request failed", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    [InlineData("Provider 'Generic' is not configured or has no authority URL.", "Provider not configured", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    public void IdentityProviderTest_maps_documented_failure_modes_to_actionable_copy(
        string serverMessage,
        string expectedTitle,
        IdentityDiagnostics.DiagnosticAction expectedOutcome)
    {
        var result = new IdentityProviderTestResult
        {
            ProviderType = "Generic",
            IsReachable = false,
            ErrorMessage = serverMessage
        };

        var copy = IdentityDiagnostics.ForIdentityProviderTest(result);

        Assert.Equal(expectedTitle, copy.Title);
        Assert.Equal(expectedOutcome, copy.Outcome);
        Assert.NotEqual(IdentityDiagnostics.DiagnosticSeverity.Healthy, copy.Severity);
    }

    [Fact]
    public void IdentityProviderTest_reachable_returns_healthy_with_response_time()
    {
        var result = new IdentityProviderTestResult
        {
            ProviderType = "Generic",
            IsReachable = true,
            ResponseTimeMs = 12.34,
            DiscoveryUrl = "https://idp.example/.well-known/openid-configuration",
            Issuer = "https://idp.example"
        };

        var copy = IdentityDiagnostics.ForIdentityProviderTest(result);

        Assert.Equal("Reachable", copy.Title);
        Assert.Equal(IdentityDiagnostics.DiagnosticSeverity.Healthy, copy.Severity);
        Assert.Equal(IdentityDiagnostics.DiagnosticAction.None, copy.Outcome);
        Assert.Contains("Reachable in", copy.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void IdentityProviderTest_reachable_with_mismatched_issuer_returns_warning()
    {
        var result = new IdentityProviderTestResult
        {
            ProviderType = "Generic",
            IsReachable = true,
            ResponseTimeMs = 9,
            DiscoveryUrl = "https://idp.example/.well-known/openid-configuration",
            Issuer = "https://other-idp.example"
        };

        var copy = IdentityDiagnostics.ForIdentityProviderTest(result);

        Assert.Equal("Issuer mismatch", copy.Title);
        Assert.Equal(IdentityDiagnostics.DiagnosticSeverity.Warning, copy.Severity);
        Assert.Equal(IdentityDiagnostics.DiagnosticAction.OperatorAction, copy.Outcome);
    }

    [Fact]
    public void IdentityProviderTest_unknown_error_uses_fall_through_with_raw_message()
    {
        var result = new IdentityProviderTestResult
        {
            ProviderType = "Generic",
            IsReachable = false,
            ErrorMessage = "Some unrecognized cosmic ray hit"
        };

        var copy = IdentityDiagnostics.ForIdentityProviderTest(result);

        Assert.Equal("Unreachable", copy.Title);
        Assert.Contains("Some unrecognized cosmic ray hit", copy.Action, StringComparison.Ordinal);
        Assert.Equal(IdentityDiagnostics.DiagnosticAction.Wait, copy.Outcome);
    }

    [Theory]
    [InlineData("HTTP 404", "Discovery URL returned 404", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    [InlineData("HTTP 401", "Discovery rejected the request", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    [InlineData("Connection request failed", "Discovery request failed", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    [InlineData("Provider not configured", "Provider not configured", IdentityDiagnostics.DiagnosticAction.OperatorAction)]
    public void OidcProviderTest_maps_documented_failure_modes_to_actionable_copy(
        string serverMessage,
        string expectedTitle,
        IdentityDiagnostics.DiagnosticAction expectedOutcome)
    {
        var response = new OidcProviderTestResponse
        {
            ProviderId = Guid.NewGuid(),
            IsReachable = false,
            Message = serverMessage,
            TestedAt = DateTimeOffset.UtcNow
        };

        var copy = IdentityDiagnostics.ForOidcProviderTest(response);

        Assert.Equal(expectedTitle, copy.Title);
        Assert.Equal(expectedOutcome, copy.Outcome);
    }

    [Fact]
    public void OidcProviderTest_reachable_renders_last_verified_timestamp()
    {
        var response = new OidcProviderTestResponse
        {
            ProviderId = Guid.NewGuid(),
            IsReachable = true,
            Message = "OK",
            TestedAt = new DateTimeOffset(2026, 04, 25, 12, 00, 00, TimeSpan.Zero)
        };

        var copy = IdentityDiagnostics.ForOidcProviderTest(response);

        Assert.Equal("Reachable", copy.Title);
        Assert.Equal(IdentityDiagnostics.DiagnosticSeverity.Healthy, copy.Severity);
        Assert.Contains("2026-04-25", copy.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderStatus_disabled_provider_marks_operator_action()
    {
        var status = new IdentityProviderStatus
        {
            Type = "Generic",
            DisplayName = "Generic OIDC",
            Enabled = false,
            IsConfigurationValid = true
        };

        var copy = IdentityDiagnostics.ForProviderStatus(status);

        Assert.Equal("Disabled", copy.Title);
        Assert.Equal(IdentityDiagnostics.DiagnosticAction.OperatorAction, copy.Outcome);
    }

    [Fact]
    public void ProviderStatus_invalid_configuration_is_an_error()
    {
        var status = new IdentityProviderStatus
        {
            Type = "Generic",
            DisplayName = "Generic OIDC",
            Enabled = true,
            IsConfigurationValid = false,
            Authority = "https://idp.example"
        };

        var copy = IdentityDiagnostics.ForProviderStatus(status);

        Assert.Equal(IdentityDiagnostics.DiagnosticSeverity.Error, copy.Severity);
        Assert.Equal(IdentityDiagnostics.DiagnosticAction.OperatorAction, copy.Outcome);
    }

    [Fact]
    public void ProviderStatus_configured_returns_healthy()
    {
        var status = new IdentityProviderStatus
        {
            Type = "Generic",
            DisplayName = "Generic OIDC",
            Enabled = true,
            IsConfigurationValid = true,
            Authority = "https://idp.example",
            CallbackPath = "/signin-oidc",
            Scopes = new[] { "openid", "profile", "email" }
        };

        var copy = IdentityDiagnostics.ForProviderStatus(status);

        Assert.Equal("Configured", copy.Title);
        Assert.Equal(IdentityDiagnostics.DiagnosticSeverity.Healthy, copy.Severity);
    }

    [Fact]
    public void PendingDiagnostics_includes_clock_skew_claim_mapping_callback_drift()
    {
        var titles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pending in IdentityDiagnostics.PendingDiagnostics)
        {
            titles.Add(pending.Title);
            Assert.False(string.IsNullOrWhiteSpace(pending.ServerTicket));
            Assert.False(string.IsNullOrWhiteSpace(pending.Description));
        }

        Assert.Contains("Clock skew check", titles);
        Assert.Contains("Claim-mapping coverage", titles);
        Assert.Contains("Callback URL drift", titles);
    }
}
