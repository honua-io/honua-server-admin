using Honua.Admin.Models.LicenseWorkspace;
using Honua.Admin.Services.LicenseWorkspace;
using Honua.Sdk.Admin.Models;
using Xunit;

namespace Honua.Admin.Tests.LicenseWorkspace;

public sealed class LicenseDiagnosticClassifierTests
{
    [Fact]
    public void Valid_status_classifies_as_valid()
    {
        var status = StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(180));
        Assert.Equal(LicenseDiagnostic.Valid, LicenseDiagnosticClassifier.Classify(status));
    }

    [Fact]
    public void Valid_but_expired_status_classifies_as_expired_for_operator_action()
    {
        // Server reports IsValid=true but the date has passed — the UI must
        // surface Expired so the operator sees the renew action.
        var status = StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(-1));
        Assert.Equal(LicenseDiagnostic.Expired, LicenseDiagnosticClassifier.Classify(status));
    }

    [Fact]
    public void Valid_but_expired_earlier_today_classifies_as_expired()
    {
        // Boundary: server reports IsValid=true and the expiry is earlier on
        // the same UTC day. The UTC-date-truncated day delta is 0, so the
        // diagnostic must still resolve to Expired via the precise-instant
        // guard rather than slipping back to Valid.
        var status = StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddHours(-2));
        Assert.Equal(LicenseDiagnostic.Expired, LicenseDiagnosticClassifier.Classify(status));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("EXPIRED")]
    [InlineData("license expiry has passed")]
    public void Expired_validation_state_classifies_as_expired(string state)
    {
        var status = new LicenseStatusResponse
        {
            Edition = "Professional",
            IsValid = false,
            ValidationState = state
        };
        Assert.Equal(LicenseDiagnostic.Expired, LicenseDiagnosticClassifier.Classify(status));
    }

    [Theory]
    [InlineData("invalid signature")]
    [InlineData("Signature mismatch")]
    [InlineData("tamper detected")]
    [InlineData("verification failed")]
    public void Signature_validation_state_classifies_as_invalid_signature(string state)
    {
        var status = new LicenseStatusResponse
        {
            Edition = "Unknown",
            IsValid = false,
            ValidationState = state
        };
        Assert.Equal(LicenseDiagnostic.InvalidSignature, LicenseDiagnosticClassifier.Classify(status));
    }

    [Fact]
    public void Unknown_invalid_state_falls_back_to_unknown()
    {
        var status = new LicenseStatusResponse
        {
            Edition = "Mystery",
            IsValid = false,
            ValidationState = "??? unrecognised"
        };
        Assert.Equal(LicenseDiagnostic.Unknown, LicenseDiagnosticClassifier.Classify(status));
    }

    [Fact]
    public void Transport_error_classifies_as_endpoint_unreachable()
    {
        var error = new LicenseClientError(LicenseClientErrorKind.Transport, "no route to host");
        Assert.Equal(LicenseDiagnostic.EndpointUnreachable, LicenseDiagnosticClassifier.Classify(error));
    }

    [Fact]
    public void Server_error_classifies_as_endpoint_unreachable()
    {
        var error = new LicenseClientError(LicenseClientErrorKind.Server, "500 internal", 500);
        Assert.Equal(LicenseDiagnostic.EndpointUnreachable, LicenseDiagnosticClassifier.Classify(error));
    }

    [Fact]
    public void Authentication_error_classifies_as_authentication_failure_distinct_from_unreachable()
    {
        var error = new LicenseClientError(LicenseClientErrorKind.Authentication, "401", 401);
        Assert.Equal(LicenseDiagnostic.AuthenticationFailure, LicenseDiagnosticClassifier.Classify(error));
    }

    [Fact]
    public void Bad_request_error_classifies_as_unknown()
    {
        var error = new LicenseClientError(LicenseClientErrorKind.BadRequest, "400", 400);
        Assert.Equal(LicenseDiagnostic.Unknown, LicenseDiagnosticClassifier.Classify(error));
    }
}
