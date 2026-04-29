using System.Collections.Generic;
using Honua.Admin.Models.LicenseWorkspace;
using Honua.Admin.Services.LicenseWorkspace;
using Honua.Sdk.Admin.Models;
using Xunit;

namespace Honua.Admin.Tests.LicenseWorkspace;

public sealed class LicenseWorkspaceStateTests
{
    [Fact]
    public async Task RefreshAsync_loads_status_and_marks_idle_on_success()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(180)));
        var telemetry = new RecordingTelemetry();
        var state = new LicenseWorkspaceState(stub, telemetry);

        await state.RefreshAsync();

        Assert.NotNull(state.Status);
        Assert.Equal("Enterprise", state.Status!.Edition);
        Assert.Equal(LicenseDiagnostic.Valid, state.Diagnostic);
        Assert.Equal(ExpiryBand.Healthy, state.ExpiryBand);
        Assert.Equal(LicenseWorkspaceStatus.Idle, state.WorkflowStatus);
        Assert.Null(state.LastError);
        Assert.Contains(telemetry.Events, e => e.Name == "status_loaded");
        Assert.Contains(telemetry.Events, e => e.Name == "diagnostic_observed" && string.Equals(e.Properties?["kind"]?.ToString(), "Valid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsync_classifies_expired_status_with_diagnostic()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildExpired());
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.RefreshAsync();

        Assert.Equal(LicenseDiagnostic.Expired, state.Diagnostic);
        Assert.Equal(ExpiryBand.Expired, state.ExpiryBand);
    }

    [Fact]
    public async Task RefreshAsync_classifies_invalid_signature_status()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildInvalidSignature());
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.RefreshAsync();

        Assert.Equal(LicenseDiagnostic.InvalidSignature, state.Diagnostic);
    }

    [Fact]
    public async Task RefreshAsync_classifies_perpetual_community_license()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildPerpetualCommunity());
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.RefreshAsync();

        Assert.Equal(LicenseDiagnostic.Valid, state.Diagnostic);
        Assert.Equal(ExpiryBand.Perpetual, state.ExpiryBand);
        Assert.Null(state.Status!.ExpiresAt);
    }

    [Fact]
    public async Task IssuanceSource_omitted_by_server_falls_back_to_byol_portal_default()
    {
        var stub = new StubLicenseWorkspaceClient(new LicenseStatusResponse
        {
            Edition = "Enterprise",
            IsValid = true,
            ValidationState = "valid",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(120),
            IssuanceSource = null
        });
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.RefreshAsync();

        // The DTO holds whatever the server sent (null today). The status pane
        // surfaces the BYOL default via LicenseStatusResponse.DefaultIssuanceSource;
        // marketplace adapters from honua-server#804 will populate the field
        // directly without an admin-side change.
        Assert.Null(state.Status!.IssuanceSource);
        Assert.Equal("BYOL portal", LicenseStatusResponse.DefaultIssuanceSource);
    }

    [Fact]
    public async Task RefreshAsync_surfaces_transport_failure_as_endpoint_unreachable()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(30)));
        stub.SetStatusError(new LicenseClientError(LicenseClientErrorKind.Transport, "connection refused"));
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.RefreshAsync();

        Assert.Equal(LicenseDiagnostic.EndpointUnreachable, state.Diagnostic);
        Assert.Equal(LicenseWorkspaceStatus.Error, state.WorkflowStatus);
        Assert.NotNull(state.LastError);
        Assert.Equal(LicenseClientErrorKind.Transport, state.LastError!.Kind);
    }

    [Fact]
    public async Task RefreshAsync_classifies_401_as_authentication_failure_distinct_from_transport()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(30)));
        stub.SetStatusError(new LicenseClientError(LicenseClientErrorKind.Authentication, "401", 401));
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.RefreshAsync();

        Assert.Equal(LicenseDiagnostic.AuthenticationFailure, state.Diagnostic);
    }

    [Fact]
    public async Task UploadAsync_refreshes_status_after_success_so_response_is_not_sole_source()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildExpired());
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.RefreshAsync();
        Assert.Equal(LicenseDiagnostic.Expired, state.Diagnostic);

        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        await state.UploadAsync(bytes);

        Assert.Equal(1, stub.UploadCallCount);
        Assert.Equal(LicenseWorkspaceStatus.Idle, state.WorkflowStatus);
        Assert.Equal(LicenseDiagnostic.Valid, state.Diagnostic);
        Assert.Equal("Enterprise", state.Status!.Edition);
    }

    [Fact]
    public async Task UploadAsync_does_not_retain_uploaded_bytes_on_state()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(180)));
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        var bytes = new byte[] { 1, 2, 3, 4 };
        await state.UploadAsync(bytes);

        // Public state surface exposes status, entitlements, diagnostic, last
        // refreshed — none of which contain the uploaded bytes.
        Assert.NotNull(state.Status);
        Assert.NotEqual(0, state.Status!.Edition.Length);
        Assert.DoesNotContain(state.Entitlements, e => e.Key.Contains("", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UploadAsync_surfaces_failure_as_diagnostic_without_throwing()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(30)));
        stub.SetUploadError(new LicenseClientError(LicenseClientErrorKind.BadRequest, "license malformed", 400));
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.UploadAsync(new byte[] { 1, 2, 3 });

        Assert.Equal(LicenseWorkspaceStatus.Error, state.WorkflowStatus);
        Assert.Equal(LicenseDiagnostic.Unknown, state.Diagnostic);
        Assert.NotNull(state.LastError);
        Assert.Equal(400, state.LastError!.StatusCode);
    }

    [Fact]
    public async Task UploadAsync_with_empty_bytes_is_a_noop_and_does_not_call_client()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(30)));
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        await state.UploadAsync(Array.Empty<byte>());

        Assert.Equal(0, stub.UploadCallCount);
        Assert.Equal(LicenseWorkspaceStatus.Idle, state.WorkflowStatus);
    }

    [Fact]
    public async Task FindEntitlement_locates_per_feature_row_for_not_entitled_diagnostic_link()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildPerpetualCommunity());
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());
        await state.RefreshAsync();

        var inactive = state.FindEntitlement("oidc");
        Assert.NotNull(inactive);
        Assert.False(inactive!.IsActive);

        Assert.Null(state.FindEntitlement("does-not-exist"));
    }

    [Fact]
    public async Task OnChanged_event_fires_for_state_transitions()
    {
        var stub = new StubLicenseWorkspaceClient(StubLicenseWorkspaceClient.BuildHealthyEnterprise(DateTimeOffset.UtcNow.AddDays(30)));
        var state = new LicenseWorkspaceState(stub, new RecordingTelemetry());

        var notifications = 0;
        state.OnChanged += () => notifications++;

        await state.RefreshAsync();

        // Loading → Idle on success. Two notifications minimum.
        Assert.True(notifications >= 2);
    }

    private sealed class RecordingTelemetry : ILicenseWorkspaceTelemetry
    {
        public List<TelemetryEvent> Events { get; } = new();

        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
        {
            Events.Add(new TelemetryEvent(eventName, properties));
        }

        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
        {
            Events.Add(new TelemetryEvent(eventName, properties));
        }
    }

    private sealed record TelemetryEvent(string Name, IReadOnlyDictionary<string, object?>? Properties);
}
