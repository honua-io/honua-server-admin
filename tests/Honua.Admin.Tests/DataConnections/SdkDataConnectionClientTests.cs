using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.DataConnections;
using Honua.Admin.Services.DataConnections;
using Honua.Sdk.Admin;
using Xunit;

namespace Honua.Admin.Tests.DataConnections;

/// <summary>
/// Pins the UI adapter over Honua.Sdk.Admin for the data-connection workspace.
/// The SDK owns HTTP/envelope parsing; this adapter owns local state-friendly
/// result and error projection.
/// </summary>
public sealed class SdkDataConnectionClientTests
{
    private const string ServerBase = "https://server.test/";

    [Fact]
    public async Task ListAsync_uses_sdk_client_and_projects_summaries()
    {
        var connectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/connections/", req.RequestUri!.ToString());
            return JsonOk(new
            {
                success = true,
                data = new[]
                {
                    SampleSummaryPayload(connectionId)
                }
            });
        });

        var client = MakeClient(handler);
        var result = await client.ListAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(connectionId, result.Value![0].ConnectionId);
        Assert.Equal("postgres", result.Value[0].ProviderId);
    }

    [Fact]
    public async Task GetAsync_projects_detail()
    {
        var connectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/connections/{connectionId}", req.RequestUri!.ToString());
            return JsonOk(new
            {
                success = true,
                data = SampleDetailPayload(connectionId)
            });
        });

        var client = MakeClient(handler);
        var result = await client.GetAsync(connectionId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(connectionId, result.Value!.ConnectionId);
        Assert.Equal("aws:secretsmanager:prod-db-creds", result.Value.CredentialReference);
        Assert.Equal(7, result.Value.EncryptionVersion);
    }

    [Fact]
    public async Task CreateAsync_projects_local_request_to_sdk_request()
    {
        var connectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        string? capturedBody = null;
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/connections/", req.RequestUri!.ToString());
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return JsonCreated(new
            {
                success = true,
                data = SampleSummaryPayload(connectionId)
            });
        });

        var client = MakeClient(handler);
        var result = await client.CreateAsync(new CreateConnectionRequest
        {
            Name = "primary",
            Host = "db.example.com",
            DatabaseName = "honua",
            Username = "honua",
            Password = "secret-1234"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(connectionId, result.Value!.ConnectionId);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"password\":\"secret-1234\"", capturedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateAsync_projects_local_request_to_sdk_request()
    {
        var connectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        string? capturedBody = null;
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/connections/{connectionId}", req.RequestUri!.ToString());
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return JsonOk(new
            {
                success = true,
                data = SampleSummaryPayload(connectionId, port: 6432)
            });
        });

        var client = MakeClient(handler);
        var result = await client.UpdateAsync(connectionId, new UpdateConnectionRequest
        {
            Port = 6432
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(6432, result.Value!.Port);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"port\":6432", capturedBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetActiveAsync_projects_to_update_request(bool active)
    {
        var connectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        string? capturedBody = null;
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/connections/{connectionId}", req.RequestUri!.ToString());
            capturedBody = req.Content!.ReadAsStringAsync().Result;
            return JsonOk(new
            {
                success = true,
                data = SampleSummaryPayload(connectionId, isActive: active)
            });
        });

        var client = MakeClient(handler);
        var result = active
            ? await client.EnableAsync(connectionId, CancellationToken.None)
            : await client.DisableAsync(connectionId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(active, result.Value!.IsActive);
        Assert.NotNull(capturedBody);
        Assert.Contains($"\"isActive\":{active.ToString().ToLowerInvariant()}", capturedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TestExistingAsync_projects_sdk_result()
    {
        var connectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/connections/{connectionId}/test", req.RequestUri!.ToString());
            return JsonOk(new
            {
                success = true,
                data = new
                {
                    connectionId,
                    connectionName = "primary",
                    isHealthy = true,
                    testedAt = DateTimeOffset.UtcNow,
                    message = "Connection is healthy"
                }
            });
        });

        var client = MakeClient(handler);
        var result = await client.TestExistingAsync(connectionId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.IsHealthy);
        Assert.Equal("primary", result.Value.ConnectionName);
    }

    [Fact]
    public async Task DeleteAsync_returns_ok_on_2xx_envelope()
    {
        var connectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Equal($"{ServerBase}api/v1/admin/connections/{connectionId}", req.RequestUri!.ToString());
            return JsonOk(new { success = true, message = "Connection deleted" });
        });

        var client = MakeClient(handler);
        var result = await client.DeleteAsync(connectionId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
    }

    [Fact]
    public async Task ListAsync_surfaces_problem_details_on_500()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                "{\"type\":\"about:blank\",\"title\":\"boom\",\"status\":500,\"detail\":\"engine failure\"}",
                Encoding.UTF8,
                "application/problem+json")
        });

        var client = MakeClient(handler);
        var result = await client.ListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Server, result.Error!.Kind);
        Assert.Equal("engine failure", result.Error.Detail);
    }

    [Fact]
    public async Task GetAsync_returns_NotFound_on_404()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("{\"success\":false,\"message\":\"missing\"}", Encoding.UTF8, "application/json")
        });

        var client = MakeClient(handler);
        var result = await client.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.NotFound, result.Error!.Kind);
        Assert.Equal("missing", result.Error.Detail);
    }

    [Fact]
    public async Task CreateAsync_returns_validation_error_on_400()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"success\":false,\"message\":\"Invalid SSL mode\"}", Encoding.UTF8, "application/json")
        });

        var client = MakeClient(handler);
        var result = await client.CreateAsync(new CreateConnectionRequest
        {
            Name = "x",
            Host = "h",
            DatabaseName = "d",
            Username = "u",
            Password = "p",
            SslMode = "garbage"
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Validation, result.Error!.Kind);
        Assert.Equal("Invalid SSL mode", result.Error.Detail);
    }

    [Fact]
    public async Task DeleteAsync_returns_conflict_with_envelope_message_on_409()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(
                "{\"success\":false,\"message\":\"Connection is in use by services\"}",
                Encoding.UTF8,
                "application/json")
        });

        var client = MakeClient(handler);
        var result = await client.DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal("Connection is in use by services", result.Error.Detail);
    }

    [Fact]
    public async Task ListAsync_returns_typed_error_on_HttpRequestException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueThrow(new HttpRequestException("DNS resolution failed"));

        var client = MakeClient(handler);
        var result = await client.ListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Network, result.Error!.Kind);
        Assert.Contains("DNS resolution failed", result.Error.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListAsync_returns_malformed_response_on_invalid_json()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "application/json")
        });

        var client = MakeClient(handler);
        var result = await client.ListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Server, result.Error!.Kind);
        Assert.Equal("error.malformed_response", result.Error.CopyKey);
    }

    [Fact]
    public async Task GetAsync_returns_empty_response_when_envelope_data_is_null()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(_ => JsonOk(new { success = true, data = (object?)null }));

        var client = MakeClient(handler);
        var result = await client.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Server, result.Error!.Kind);
        Assert.Equal("error.empty_response", result.Error.CopyKey);
    }

    [Fact]
    public async Task ListAsync_returns_timeout_when_handler_throws_OperationCanceledException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueThrow(new TaskCanceledException("simulated request timeout"));

        var client = MakeClient(handler);
        var result = await client.ListAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConnectionErrorKind.Network, result.Error!.Kind);
        Assert.Equal("error.timeout", result.Error.CopyKey);
    }

    private static object SampleSummaryPayload(Guid connectionId, int port = 5432, bool isActive = true) => new
    {
        connectionId,
        name = "primary",
        description = (string?)null,
        host = "db.example.com",
        port,
        databaseName = "honua",
        username = "honua",
        sslRequired = true,
        sslMode = "Require",
        storageType = "managed",
        isActive,
        healthStatus = "Unknown",
        lastHealthCheck = (DateTimeOffset?)null,
        createdAt = DateTimeOffset.UtcNow,
        createdBy = "admin"
    };

    private static object SampleDetailPayload(Guid connectionId) => new
    {
        connectionId,
        name = "primary",
        description = "primary OLAP",
        host = "db.example.com",
        port = 5432,
        databaseName = "honua",
        username = "honua",
        sslRequired = true,
        sslMode = "Require",
        storageType = "external",
        isActive = true,
        healthStatus = "Healthy",
        lastHealthCheck = DateTimeOffset.UtcNow,
        createdAt = DateTimeOffset.UtcNow,
        createdBy = "admin",
        credentialReference = "aws:secretsmanager:prod-db-creds",
        encryptionVersion = 7,
        updatedAt = DateTimeOffset.UtcNow
    };

    private static HttpResponseMessage JsonOk(object payload) => JsonResponse(HttpStatusCode.OK, payload);

    private static HttpResponseMessage JsonCreated(object payload) => JsonResponse(HttpStatusCode.Created, payload);

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, object payload)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(payload, options);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static SdkDataConnectionClient MakeClient(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri(ServerBase) };
        return new SdkDataConnectionClient(new HonuaAdminClient(http));
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
            try
            {
                return Task.FromResult(responder(request));
            }
            catch (Exception ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
