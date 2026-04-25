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

public sealed class HttpLicenseWorkspaceClientTests
{
    private static HttpClient BuildClient(StubMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://localhost/") };

    [Fact]
    public async Task GetStatusAsync_decodes_envelope_into_dto()
    {
        var dto = new LicenseStatusDto
        {
            Edition = "Enterprise",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(60),
            IsValid = true,
            ValidationState = "valid",
            LicensedTo = "Acme",
            Entitlements = new[]
            {
                new EntitlementDto { Key = "oidc", Name = "OIDC", IsActive = true }
            }
        };
        var envelope = new LicenseApiEnvelope<LicenseStatusDto>
        {
            Success = true,
            Data = dto
        };
        var body = JsonSerializer.Serialize(envelope, LicenseWorkspaceJsonContext.Default.LicenseApiEnvelopeLicenseStatusDto);
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
        var http = BuildClient(handler);
        var client = new HttpLicenseWorkspaceClient(http);

        var result = await client.GetStatusAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Enterprise", result.Value!.Edition);
        Assert.Equal("Acme", result.Value.LicensedTo);
        Assert.Single(result.Value.Entitlements);
    }

    [Fact]
    public async Task GetStatusAsync_classifies_500_as_server_error()
    {
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("server gone")
        });
        var client = new HttpLicenseWorkspaceClient(BuildClient(handler));

        var result = await client.GetStatusAsync(default);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseClientErrorKind.Server, result.Error!.Kind);
        Assert.Equal(500, result.Error.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetStatusAsync_classifies_auth_status_codes_as_authentication(HttpStatusCode status)
    {
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(status));
        var client = new HttpLicenseWorkspaceClient(BuildClient(handler));

        var result = await client.GetStatusAsync(default);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseClientErrorKind.Authentication, result.Error!.Kind);
    }

    [Fact]
    public async Task GetStatusAsync_classifies_transport_exception_as_transport_error()
    {
        var handler = new StubMessageHandler(_ => throw new HttpRequestException("dns"));
        var client = new HttpLicenseWorkspaceClient(BuildClient(handler));

        var result = await client.GetStatusAsync(default);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseClientErrorKind.Transport, result.Error!.Kind);
    }

    [Fact]
    public async Task UploadLicenseAsync_sends_octet_stream_payload_to_pinned_endpoint()
    {
        HttpRequestMessage? captured = null;
        byte[]? capturedBytes = null;
        var dto = new LicenseStatusDto
        {
            Edition = "Enterprise",
            IsValid = true,
            ValidationState = "valid"
        };
        var body = JsonSerializer.Serialize(new LicenseApiEnvelope<LicenseStatusDto>
        {
            Success = true,
            Data = dto
        }, LicenseWorkspaceJsonContext.Default.LicenseApiEnvelopeLicenseStatusDto);
        var handler = new StubMessageHandler(req =>
        {
            captured = req;
            capturedBytes = req.Content?.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpLicenseWorkspaceClient(BuildClient(handler));

        var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var result = await client.UploadLicenseAsync(bytes, default);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("http://localhost/api/v1/admin/license", captured.RequestUri!.ToString());
        Assert.Equal("application/octet-stream", captured.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, capturedBytes);
    }

    [Fact]
    public async Task UploadLicenseAsync_propagates_400_as_bad_request_kind()
    {
        var handler = new StubMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("license invalid")
        });
        var client = new HttpLicenseWorkspaceClient(BuildClient(handler));

        var result = await client.UploadLicenseAsync(new byte[] { 1 }, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseClientErrorKind.BadRequest, result.Error!.Kind);
        Assert.Equal(400, result.Error.StatusCode);
    }

    private sealed class StubMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responder(request));
        }
    }
}
