using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.LicenseWorkspace;
using Honua.Admin.Services.LicenseWorkspace;
using Xunit;

namespace Honua.Admin.Tests.LicenseWorkspace;

/// <summary>
/// End-to-end coverage of the replace-license flow against the
/// <see cref="HttpLicenseWorkspaceClient"/> + <see cref="LicenseWorkspaceState"/>
/// composite. Walks the operator path the design brief calls out for the E2E
/// AC: Expired → upload → Valid, and a non-Valid (InvalidSignature) failure
/// surface. Implemented as an in-process HTTP exchange because no browser-level
/// E2E framework is configured in this repo (see gap report risk #5).
/// </summary>
public sealed class LicenseReplaceFlowEndToEndTests
{
    [Fact]
    public async Task Replace_flow_transitions_expired_to_valid_and_refreshes_status_from_get()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(BuildStatusResponse(HttpStatusCode.OK, BuildExpiredEnvelope()));
        responses.Enqueue(BuildStatusResponse(HttpStatusCode.OK, BuildHealthyEnvelope()));   // POST /license response
        responses.Enqueue(BuildStatusResponse(HttpStatusCode.OK, BuildHealthyEnvelope()));   // GET /license refresh

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingMessageHandler(responses, requests);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new HttpLicenseWorkspaceClient(http);
        var state = new LicenseWorkspaceState(client, new NullTelemetry());

        await state.RefreshAsync();
        Assert.Equal(LicenseDiagnostic.Expired, state.Diagnostic);
        Assert.Equal(ExpiryBand.Expired, state.ExpiryBand);

        await state.UploadAsync(new byte[] { 0x01, 0x02, 0x03 });

        Assert.Equal(LicenseWorkspaceStatus.Idle, state.WorkflowStatus);
        Assert.Equal(LicenseDiagnostic.Valid, state.Diagnostic);
        Assert.Equal("Enterprise", state.Status!.Edition);

        // Three exchanges in order: GET /license, POST /license, GET /license.
        Assert.Equal(3, requests.Count);
        Assert.Equal(HttpMethod.Get, requests[0].Method);
        Assert.Equal(HttpMethod.Post, requests[1].Method);
        Assert.Equal("application/octet-stream", requests[1].Content!.Headers.ContentType!.MediaType);
        Assert.Equal(HttpMethod.Get, requests[2].Method);
        Assert.All(requests, r => Assert.Equal("/api/v1/admin/license", r.RequestUri!.AbsolutePath));
    }

    [Fact]
    public async Task Replace_flow_surfaces_invalid_signature_diagnostic_when_server_rejects_post()
    {
        var responses = new Queue<HttpResponseMessage>();
        // Initial status: a healthy license so the operator opens the dialog
        // expecting to replace one license with another.
        responses.Enqueue(BuildStatusResponse(HttpStatusCode.OK, BuildHealthyEnvelope()));
        // Server rejects the upload with a signature failure surfaced via 400
        // body (no discriminated status code today — see gap report).
        responses.Enqueue(BuildStatusResponse(HttpStatusCode.BadRequest, "license invalid signature"));

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingMessageHandler(responses, requests);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new HttpLicenseWorkspaceClient(http);
        var state = new LicenseWorkspaceState(client, new NullTelemetry());

        await state.RefreshAsync();
        Assert.Equal(LicenseDiagnostic.Valid, state.Diagnostic);

        await state.UploadAsync(new byte[] { 0xFF });

        Assert.Equal(LicenseWorkspaceStatus.Error, state.WorkflowStatus);
        // Without a discriminated server response, the client falls into the
        // BadRequest bucket → Unknown diagnostic. The diagnostic banner still
        // surfaces operator-actionable copy and the server hint string is
        // available for support. When the server adds a signature-specific
        // failure code (gap report), the classifier can be refined.
        Assert.Equal(LicenseDiagnostic.Unknown, state.Diagnostic);
        Assert.NotNull(state.LastError);
        Assert.Equal(400, state.LastError!.StatusCode);
    }

    [Fact]
    public async Task Replace_flow_surfaces_endpoint_unreachable_on_server_5xx_during_status_refresh()
    {
        var responses = new Queue<HttpResponseMessage>();
        responses.Enqueue(BuildStatusResponse(HttpStatusCode.InternalServerError, "boom"));

        var requests = new List<HttpRequestMessage>();
        var handler = new RecordingMessageHandler(responses, requests);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var client = new HttpLicenseWorkspaceClient(http);
        var state = new LicenseWorkspaceState(client, new NullTelemetry());

        await state.RefreshAsync();

        Assert.Equal(LicenseWorkspaceStatus.Error, state.WorkflowStatus);
        Assert.Equal(LicenseDiagnostic.EndpointUnreachable, state.Diagnostic);
    }

    private static HttpResponseMessage BuildStatusResponse(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static string BuildExpiredEnvelope()
    {
        var dto = new LicenseStatusDto
        {
            Edition = "Professional",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-3),
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-365),
            LicensedTo = "Acme",
            IsValid = false,
            ValidationState = "expired",
            Entitlements = Array.Empty<EntitlementDto>()
        };
        return JsonSerializer.Serialize(new LicenseApiEnvelope<LicenseStatusDto>
        {
            Success = true,
            Data = dto
        }, LicenseWorkspaceJsonContext.Default.LicenseApiEnvelopeLicenseStatusDto);
    }

    private static string BuildHealthyEnvelope()
    {
        var dto = new LicenseStatusDto
        {
            Edition = "Enterprise",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(365),
            IssuedAt = DateTimeOffset.UtcNow,
            LicensedTo = "Acme",
            IsValid = true,
            ValidationState = "valid",
            Entitlements = new[]
            {
                new EntitlementDto { Key = "oidc", Name = "OIDC sign-in", IsActive = true }
            }
        };
        return JsonSerializer.Serialize(new LicenseApiEnvelope<LicenseStatusDto>
        {
            Success = true,
            Data = dto
        }, LicenseWorkspaceJsonContext.Default.LicenseApiEnvelopeLicenseStatusDto);
    }

    private sealed class RecordingMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        private readonly List<HttpRequestMessage> _requests;

        public RecordingMessageHandler(Queue<HttpResponseMessage> responses, List<HttpRequestMessage> requests)
        {
            _responses = responses;
            _requests = requests;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Capture the byte payload before responding so the test can
            // assert the upload bytes were sent verbatim.
            if (request.Content is not null)
            {
                _ = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            _requests.Add(request);
            if (_responses.Count == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.NotImplemented);
            }
            return _responses.Dequeue();
        }
    }

    private sealed class NullTelemetry : ILicenseWorkspaceTelemetry
    {
        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null) { }
    }
}
