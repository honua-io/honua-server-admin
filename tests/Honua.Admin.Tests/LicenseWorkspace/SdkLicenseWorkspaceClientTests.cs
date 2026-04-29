using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.LicenseWorkspace;
using Honua.Admin.Services.LicenseWorkspace;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;
using Xunit;

namespace Honua.Admin.Tests.LicenseWorkspace;

public sealed class SdkLicenseWorkspaceClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public async Task GetStatusAsync_decodes_sdk_response_into_result()
    {
        var dto = new LicenseStatusResponse
        {
            Edition = "Enterprise",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(60),
            IsValid = true,
            ValidationState = "valid",
            LicensedTo = "Acme",
            Entitlements = new[]
            {
                new LicenseEntitlement { Key = "oidc", Name = "OIDC", IsActive = true }
            }
        };
        var handler = new StubMessageHandler(_ => JsonResponse(HttpStatusCode.OK, Envelope(dto)));
        var client = MakeClient(handler);

        var result = await client.GetStatusAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Enterprise", result.Value!.Edition);
        Assert.Equal("Acme", result.Value.LicensedTo);
        Assert.Single(result.Value.Entitlements);
    }

    [Fact]
    public async Task GetEntitlementsAsync_decodes_sdk_response_into_box()
    {
        var entitlements = new[]
        {
            new LicenseEntitlement { Key = "oidc", Name = "OIDC", IsActive = true }
        };
        var handler = new StubMessageHandler(_ => JsonResponse(HttpStatusCode.OK, Envelope(entitlements)));
        var client = MakeClient(handler);

        var result = await client.GetEntitlementsAsync(default);

        Assert.True(result.IsSuccess);
        var entitlement = Assert.Single(result.Value!.Items);
        Assert.Equal("oidc", entitlement.Key);
    }

    [Fact]
    public async Task GetStatusAsync_classifies_500_as_server_error()
    {
        var handler = new StubMessageHandler(_ => JsonResponse(HttpStatusCode.InternalServerError, "server gone"));
        var client = MakeClient(handler);

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
        var client = MakeClient(handler);

        var result = await client.GetStatusAsync(default);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseClientErrorKind.Authentication, result.Error!.Kind);
    }

    [Fact]
    public async Task GetStatusAsync_classifies_transport_exception_as_transport_error()
    {
        var handler = new StubMessageHandler(_ => throw new HttpRequestException("dns"));
        var client = MakeClient(handler);

        var result = await client.GetStatusAsync(default);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseClientErrorKind.Transport, result.Error!.Kind);
    }

    [Fact]
    public async Task UploadLicenseAsync_sends_octet_stream_payload_to_pinned_endpoint()
    {
        HttpRequestMessage? captured = null;
        byte[]? capturedBytes = null;
        var dto = new LicenseStatusResponse
        {
            Edition = "Enterprise",
            IsValid = true,
            ValidationState = "valid"
        };
        var handler = new StubMessageHandler(req =>
        {
            captured = req;
            capturedBytes = req.Content?.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return JsonResponse(HttpStatusCode.OK, Envelope(dto));
        });
        var client = MakeClient(handler);

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
        var handler = new StubMessageHandler(_ => JsonResponse(HttpStatusCode.BadRequest, "license invalid"));
        var client = MakeClient(handler);

        var result = await client.UploadLicenseAsync(new byte[] { 1 }, default);

        Assert.False(result.IsSuccess);
        Assert.Equal(LicenseClientErrorKind.BadRequest, result.Error!.Kind);
        Assert.Equal(400, result.Error.StatusCode);
    }

    private static SdkLicenseWorkspaceClient MakeClient(StubMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        return new SdkLicenseWorkspaceClient(new HonuaAdminClient(http));
    }

    private static string Envelope<T>(T data) => JsonSerializer.Serialize(new { success = true, data }, JsonOptions);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
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
