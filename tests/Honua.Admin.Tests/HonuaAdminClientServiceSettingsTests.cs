// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Services.Admin;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class HonuaAdminClientServiceSettingsTests
{
    [Fact]
    public async Task GetServiceSettingsAsync_preserves_admin_defaults_for_partial_sdk_payloads()
    {
        using var handler = new RecordingHandler(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal(
                "https://admin.example.test/api/v1/admin/services/default/settings",
                req.RequestUri!.ToString());

            return JsonOk(
                """
                {
                  "success": true,
                  "data": {
                    "serviceName": "default",
                    "enabledProtocols": null,
                    "availableProtocols": null,
                    "mapServer": null
                  }
                }
                """);
        });
        var client = MakeClient(handler);

        var settings = await client.GetServiceSettingsAsync("default", CancellationToken.None);

        Assert.Equal("default", settings.ServiceName);
        Assert.Empty(settings.EnabledProtocols);
        Assert.Empty(settings.AvailableProtocols);
        Assert.NotNull(settings.MapServer);
        Assert.Null(settings.MapServer.MaxImageWidth);
        Assert.Null(settings.MapServer.DefaultFormat);
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class NullTelemetry : IAdminTelemetry
    {
        public void PageNavigated(string pageRoute, string? principalId) { }
        public void DestructiveAction(string action, string? targetId, string? principalId) { }
        public void ClientRequestFailed(string operation, string error) { }
    }
}
