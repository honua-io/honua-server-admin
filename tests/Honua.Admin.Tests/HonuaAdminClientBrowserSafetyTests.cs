// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Services.Admin;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class HonuaAdminClientBrowserSafetyTests
{
    [Fact]
    public async Task GetRecentErrorsAsync_uses_sdk_client_over_supplied_http_pipeline()
    {
        using var handler = new RecordingHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("https://admin.example.test/api/v1/admin/observability/errors", req.RequestUri!.ToString());
            Assert.False(req.Headers.Contains("X-API-Key"));
            Assert.Null(req.Headers.Authorization);

            return JsonOk(
                """
                {
                  "capacity": 10,
                  "instanceId": "browser-node",
                  "errors": [
                    {
                      "timestamp": "2026-04-29T12:00:00Z",
                      "correlationId": "corr-browser",
                      "path": "/api/v1/admin/config",
                      "statusCode": 500,
                      "message": "boom"
                    }
                  ]
                }
                """);
        });
        IHonuaAdminClient client = MakeClient(handler);

        var result = await client.GetRecentErrorsAsync(CancellationToken.None);

        Assert.Equal(10, result.Capacity);
        Assert.Equal("browser-node", result.InstanceId);
        var error = Assert.Single(result.Errors);
        Assert.Equal("corr-browser", error.CorrelationId);
        Assert.Equal("/api/v1/admin/config", error.Path);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetDeployPreflightAsync_uses_sdk_diagnostics_query_over_supplied_http_pipeline()
    {
        using var handler = new RecordingHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal(
                "https://admin.example.test/api/v1/admin/deploy/preflight?includeDiagnostics=true",
                req.RequestUri!.ToString());
            Assert.False(req.Headers.Contains("X-API-Key"));
            Assert.Null(req.Headers.Authorization);

            return JsonOk(
                """
                {
                  "status": "ready",
                  "readyForCoordinatedDeploy": true,
                  "message": "Instance is ready for coordinated deployment.",
                  "serverVersion": "0.1.0",
                  "environment": "Development",
                  "deploymentMode": "SingleInstance",
                  "instanceName": "browser-node",
                  "generatedAt": "2026-04-29T12:00:00Z",
                  "readiness": {
                    "isReady": true,
                    "statusCode": 200,
                    "message": "ready"
                  },
                  "migration": null,
                  "databaseCompatibility": null
                }
                """);
        });
        IHonuaAdminClient client = MakeClient(handler);

        var result = await client.GetDeployPreflightAsync(CancellationToken.None);

        Assert.Equal("ready", result.Status);
        Assert.True(result.ReadyForCoordinatedDeploy);
        Assert.Equal("browser-node", result.InstanceName);
        Assert.Equal(200, result.Readiness?.StatusCode);
        Assert.Single(handler.Requests);
    }

    private static HonuaAdminClient MakeClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://admin.example.test/"),
        };
        return new HonuaAdminClient(http, new NullTelemetry());
    }

    private static HttpResponseMessage JsonOk(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class NullTelemetry : IAdminTelemetry
    {
        public void PageNavigated(string pageRoute, string? principalId) { }
        public void DestructiveAction(string action, string? targetId, string? principalId) { }
        public void ClientRequestFailed(string operation, string error) { }
    }
}
