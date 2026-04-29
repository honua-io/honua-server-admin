using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;
using Honua.Admin.Services.Identity;
using Xunit;

namespace Honua.Admin.Tests.Identity;

/// <summary>
/// End-to-end test exercising the OIDC provider lifecycle through the typed
/// SDK-backed admin client against a fake honua-server. Substitutes for a Playwright /
/// browser-driven E2E (the admin repo has no harness yet — see
/// <c>docs/identity-admin-gaps.md</c>) by routing through the same wire contract
/// the page UI uses. Per-user API key lifecycle is excluded; the corresponding
/// server endpoints don't exist yet.
/// </summary>
public sealed class IdentityAdminEndToEndTests
{
    [Fact]
    public async Task Provider_create_test_delete_walks_the_full_lifecycle()
    {
        var server = new FakeIdentityServer();
        var client = new SdkIdentityAdminClient(
            new HonuaAdminClient(new HttpClient(server) { BaseAddress = new Uri("https://server.test/") }),
            new NullTelemetry());

        // 1. Empty starting state.
        var initial = await client.ListOidcProvidersAsync(CancellationToken.None);
        Assert.Empty(initial);

        // 2. Operator creates a provider with a plaintext secret.
        var created = await client.CreateOidcProviderAsync(new CreateOidcProviderRequest
        {
            Name = "Acme",
            ProviderType = "Generic",
            Authority = "https://idp.example",
            ClientId = "honua-admin",
            ClientSecret = "s3cret",
            Enabled = true
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.ProviderId);
        Assert.Equal("Acme", created.Name);

        // 3. Listing now reports the new provider.
        var listed = await client.ListOidcProvidersAsync(CancellationToken.None);
        Assert.Single(listed);
        Assert.Equal(created.ProviderId, listed[0].ProviderId);

        // 4. Reachability test against the configured provider.
        server.NextTestResult = new OidcProviderTestResponse
        {
            ProviderId = created.ProviderId,
            IsReachable = false,
            Message = "HTTP 404 Not Found",
            TestedAt = DateTimeOffset.UtcNow
        };
        var testResult = await client.TestOidcProviderAsync(created.ProviderId, CancellationToken.None);
        Assert.False(testResult.IsReachable);

        var diagnostic = IdentityDiagnostics.ForOidcProviderTest(testResult);
        Assert.Equal("Discovery URL returned 404", diagnostic.Title);
        Assert.Equal(IdentityDiagnostics.DiagnosticAction.OperatorAction, diagnostic.Outcome);

        // 5. Edit without rotating the secret — server must not see clientSecret.
        var updated = await client.UpdateOidcProviderAsync(created.ProviderId, new UpdateOidcProviderRequest
        {
            Name = "Acme (renamed)"
        }, CancellationToken.None);
        Assert.Equal("Acme (renamed)", updated.Name);
        Assert.False(server.LastUpdateBodyContainedSecret);

        // 6. Delete the provider; subsequent list is empty.
        await client.DeleteOidcProviderAsync(created.ProviderId, CancellationToken.None);

        var afterDelete = await client.ListOidcProvidersAsync(CancellationToken.None);
        Assert.Empty(afterDelete);
    }

    private sealed class FakeIdentityServer : HttpMessageHandler
    {
        private readonly Dictionary<Guid, OidcProviderResponse> _providers = new();

        public OidcProviderTestResponse? NextTestResult { get; set; }

        public bool LastUpdateBodyContainedSecret { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            var method = request.Method;

            if (method == HttpMethod.Get && path == "/api/v1/admin/oidc/providers")
            {
                return JsonOk(new { success = true, data = _providers.Values.ToArray() });
            }

            if (method == HttpMethod.Post && path == "/api/v1/admin/oidc/providers")
            {
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(body);
                var name = doc.RootElement.GetProperty("name").GetString()!;
                var providerType = doc.RootElement.GetProperty("providerType").GetString()!;
                var authority = doc.RootElement.GetProperty("authority").GetString()!;
                var clientId = doc.RootElement.GetProperty("clientId").GetString()!;
                var enabled = doc.RootElement.TryGetProperty("enabled", out var en) ? en.GetBoolean() : true;
                Assert.True(doc.RootElement.TryGetProperty("clientSecret", out _), "Create payload must include the plaintext secret exactly once.");

                var id = Guid.NewGuid();
                var record = new OidcProviderResponse
                {
                    ProviderId = id,
                    Name = name,
                    ProviderType = providerType,
                    Authority = authority,
                    ClientId = clientId,
                    Enabled = enabled,
                    IsHealthy = false,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _providers[id] = record;
                return JsonOk(new { success = true, data = record });
            }

            if (path.StartsWith("/api/v1/admin/oidc/providers/", StringComparison.Ordinal))
            {
                var remainder = path.Substring("/api/v1/admin/oidc/providers/".Length);
                if (remainder.EndsWith("/test", StringComparison.Ordinal))
                {
                    var id = Guid.Parse(remainder[..^"/test".Length]);
                    Assert.True(_providers.ContainsKey(id));
                    return JsonOk(new { success = true, data = NextTestResult });
                }
                var providerId = Guid.Parse(remainder);
                if (method == HttpMethod.Get)
                {
                    return _providers.TryGetValue(providerId, out var existing)
                        ? JsonOk(new { success = true, data = existing })
                        : new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent("{\"success\":false}", Encoding.UTF8, "application/json")
                        };
                }
                if (method == HttpMethod.Put)
                {
                    var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                    LastUpdateBodyContainedSecret = body.Contains("clientSecret", StringComparison.OrdinalIgnoreCase);
                    using var doc = JsonDocument.Parse(body);
                    var current = _providers[providerId];
                    var updated = new OidcProviderResponse
                    {
                        ProviderId = providerId,
                        Name = doc.RootElement.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                            ? nameEl.GetString()!
                            : current.Name,
                        ProviderType = current.ProviderType,
                        Authority = current.Authority,
                        ClientId = current.ClientId,
                        Enabled = current.Enabled,
                        IsHealthy = current.IsHealthy,
                        CreatedAt = current.CreatedAt,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        LastHealthCheck = current.LastHealthCheck
                    };
                    _providers[providerId] = updated;
                    return JsonOk(new { success = true, data = updated });
                }
                if (method == HttpMethod.Delete)
                {
                    _providers.Remove(providerId);
                    return JsonOk(new { success = true, message = "OIDC provider deleted" });
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"unexpected request {method} {path}", Encoding.UTF8, "text/plain")
            };
        }

        private static HttpResponseMessage JsonOk(object payload)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, options), Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class NullTelemetry : IIdentityAdminTelemetry
    {
        public void Record(string eventName, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void RecordLatency(string eventName, long elapsedMillis, IReadOnlyDictionary<string, object?>? properties = null) { }
        public void RecordError(string eventName, Exception exception, long elapsedMillis) { }
    }
}
