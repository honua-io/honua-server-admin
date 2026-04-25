using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Identity;
using Honua.Admin.Services.Identity;
using Xunit;

namespace Honua.Admin.Tests.Identity;

/// <summary>
/// Pin the wire contract between the admin UI and honua-server's identity admin
/// endpoints. The fake handler verifies request paths, methods, and bodies; the
/// recorded responses verify the client correctly unwraps the
/// <see cref="ApiResponse{T}"/> envelope without leaking secrets back to callers.
/// </summary>
public sealed class HttpIdentityAdminClientTests
{
    private const string ServerBase = "https://server.test/";

    [Fact]
    public async Task ListOidcProviders_calls_correct_path_and_unwraps_envelope()
    {
        var providers = new[]
        {
            new OidcProviderResponse
            {
                ProviderId = Guid.NewGuid(),
                Name = "Acme",
                ProviderType = "Generic",
                Authority = "https://idp.example",
                ClientId = "honua-admin",
                Enabled = true,
                IsHealthy = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/oidc/providers", req.RequestUri!.ToString());
            return JsonOk(new { success = true, data = providers });
        });

        var client = MakeClient(handler);

        var listed = await client.ListOidcProvidersAsync(CancellationToken.None);

        Assert.Single(listed);
        Assert.Equal("Acme", listed[0].Name);
    }

    [Fact]
    public async Task GetOidcProvider_returns_null_on_404_without_throwing()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"success\":false,\"message\":\"OIDC provider not found\"}", Encoding.UTF8, "application/json")
        });

        var client = MakeClient(handler);

        var result = await client.GetOidcProviderAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateOidcProvider_posts_secret_in_request_body_and_returns_response_without_one()
    {
        var providerId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        string? capturedBody = null;
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/oidc/providers", req.RequestUri!.ToString());
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return JsonOk(new
            {
                success = true,
                data = new
                {
                    providerId = providerId,
                    name = "Acme",
                    providerType = "Generic",
                    authority = "https://idp.example",
                    clientId = "honua-admin",
                    enabled = true,
                    isHealthy = false,
                    createdAt = DateTimeOffset.UtcNow,
                    updatedAt = DateTimeOffset.UtcNow
                }
            });
        });

        var client = MakeClient(handler);

        var created = await client.CreateOidcProviderAsync(new CreateOidcProviderRequest
        {
            Name = "Acme",
            ProviderType = "Generic",
            Authority = "https://idp.example",
            ClientId = "honua-admin",
            ClientSecret = "secret-value",
            Enabled = true
        }, CancellationToken.None);

        Assert.Equal(providerId, created.ProviderId);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"clientSecret\":\"secret-value\"", capturedBody, StringComparison.Ordinal);
        // The response shape we crafted on the wire has no clientSecret field, so
        // the deserialized response surface cannot leak one back into UI state.
        var responseJson = JsonSerializer.Serialize(created);
        Assert.DoesNotContain("clientSecret", responseJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateOidcProvider_omits_secret_when_request_has_none()
    {
        var providerId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        string? capturedBody = null;
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/oidc/providers/{providerId:D}", req.RequestUri!.ToString());
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return JsonOk(new
            {
                success = true,
                data = new
                {
                    providerId,
                    name = "Renamed",
                    providerType = "Generic",
                    authority = "https://idp.example",
                    clientId = "honua-admin",
                    enabled = true,
                    isHealthy = true,
                    createdAt = DateTimeOffset.UtcNow,
                    updatedAt = DateTimeOffset.UtcNow
                }
            });
        });

        var client = MakeClient(handler);

        var updated = await client.UpdateOidcProviderAsync(providerId, new UpdateOidcProviderRequest
        {
            Name = "Renamed",
            // ClientSecret intentionally null — operator did not opt in to rotate.
        }, CancellationToken.None);

        Assert.Equal("Renamed", updated.Name);
        Assert.NotNull(capturedBody);
        Assert.DoesNotContain("clientSecret", capturedBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestOidcProvider_posts_to_test_endpoint_and_returns_response()
    {
        var providerId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/oidc/providers/{providerId:D}/test", req.RequestUri!.ToString());
            return JsonOk(new
            {
                success = true,
                data = new
                {
                    providerId,
                    isReachable = false,
                    message = "HTTP 404 Not Found",
                    testedAt = DateTimeOffset.UtcNow
                }
            });
        });

        var client = MakeClient(handler);

        var result = await client.TestOidcProviderAsync(providerId, CancellationToken.None);

        Assert.False(result.IsReachable);
        Assert.StartsWith("HTTP 404", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteOidcProvider_uses_delete_verb_at_provider_path()
    {
        var providerId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/oidc/providers/{providerId:D}", req.RequestUri!.ToString());
            return JsonOk(new { success = true, message = "OIDC provider deleted" });
        });

        var client = MakeClient(handler);

        await client.DeleteOidcProviderAsync(providerId, CancellationToken.None);
    }

    [Fact]
    public async Task GetIdentityProviders_returns_envelope_data()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/identity/providers", req.RequestUri!.ToString());
            return JsonOk(new
            {
                success = true,
                data = new
                {
                    enabled = true,
                    providers = new[]
                    {
                        new
                        {
                            type = "Generic",
                            enabled = true,
                            displayName = "Generic OIDC",
                            authority = (string?)"https://idp.example",
                            callbackPath = (string?)"/signin-oidc",
                            scopes = new[] { "openid", "profile", "email" },
                            isConfigurationValid = true
                        }
                    }
                }
            });
        });

        var client = MakeClient(handler);

        var response = await client.GetIdentityProvidersAsync(CancellationToken.None);

        Assert.True(response.Enabled);
        var provider = Assert.Single(response.Providers);
        Assert.Equal("Generic", provider.Type);
        Assert.True(provider.IsConfigurationValid);
    }

    [Fact]
    public async Task TestIdentityProvider_uses_get_with_url_escaped_type()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            // AbsoluteUri preserves percent-encoding; ToString() would unescape.
            Assert.Equal($"{ServerBase}api/v1/admin/identity/providers/Azure%20Ad/test", req.RequestUri!.AbsoluteUri);
            return JsonOk(new
            {
                success = true,
                data = new
                {
                    providerType = "Azure Ad",
                    isReachable = true,
                    responseTimeMs = 12.5,
                    discoveryUrl = "https://idp.example/.well-known/openid-configuration",
                    issuer = "https://idp.example"
                }
            });
        });

        var client = MakeClient(handler);

        var result = await client.TestIdentityProviderAsync("Azure Ad", CancellationToken.None);

        Assert.True(result.IsReachable);
        Assert.Equal("https://idp.example", result.Issuer);
    }

    [Fact]
    public async Task TestIdentityProvider_records_error_telemetry_when_handler_throws()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueThrow(new HttpRequestException("DNS lookup failed"));
        var telemetry = new RecordingTelemetry();
        var client = MakeClient(handler, telemetry);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.TestIdentityProviderAsync("Generic", CancellationToken.None));

        Assert.Contains(telemetry.Errors, e => e.Event == "identity_admin_test_provider");
    }

    private static HttpResponseMessage JsonOk(object payload)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(payload, options);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpIdentityAdminClient MakeClient(FakeHttpMessageHandler handler, IIdentityAdminTelemetry? telemetry = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(ServerBase) };
        return new HttpIdentityAdminClient(http, telemetry ?? new NullTelemetry());
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _queue = new();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _queue.Enqueue(responder);
        }

        public void EnqueueThrow(Exception exception)
        {
            _queue.Enqueue(_ => throw exception);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_queue.Count == 0)
            {
                return Task.FromException<HttpResponseMessage>(new InvalidOperationException(
                    $"Unexpected HTTP request: {request.Method} {request.RequestUri}"));
            }
            var responder = _queue.Dequeue();
            return Task.FromResult(responder(request));
        }
    }

    private sealed class NullTelemetry : IIdentityAdminTelemetry
    {
        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void RecordError(string eventName, Exception exception, long elapsedMillis) { }
    }

    private sealed class RecordingTelemetry : IIdentityAdminTelemetry
    {
        public List<(string Event, IReadOnlyDictionary<string, object?>? Props)> Events { get; } = new();
        public List<(string Event, long ElapsedMs, IReadOnlyDictionary<string, object?>? Props)> Latency { get; } = new();
        public List<(string Event, Exception Exception, long ElapsedMs)> Errors { get; } = new();

        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
        {
            Events.Add((eventName, properties));
        }

        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null)
        {
            Latency.Add((eventName, elapsedMillis, properties));
        }

        public void RecordError(string eventName, Exception exception, long elapsedMillis)
        {
            Errors.Add((eventName, exception, elapsedMillis));
        }
    }
}
