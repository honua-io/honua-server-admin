// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;
using Xunit;

namespace Honua.Admin.Tests;

public sealed class HonuaAdminClientMetadataTests
{
    [Fact]
    public async Task UpdateMetadataResourceAsync_requires_if_match_before_sending_request()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        var client = MakeClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.UpdateMetadataResourceAsync(
            "Layer",
            "default",
            "parcels",
            CreateResource("parcels"),
            ifMatch: "",
            CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DeleteMetadataResourceAsync_requires_if_match_before_sending_request()
    {
        var handler = new RecordingHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        var client = MakeClient(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => client.DeleteMetadataResourceAsync(
            "Layer",
            "default",
            "parcels",
            ifMatch: " ",
            CancellationToken.None));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdateMetadataResourceAsync_sends_if_match_header_and_returns_response_etag()
    {
        var resource = CreateResource("parcels");
        var handler = new RecordingHandler(_ => JsonOk(resource, "\"etag-updated\""));
        var client = MakeClient(handler);

        var result = await client.UpdateMetadataResourceAsync(
            "Layer",
            "default",
            "parcels",
            resource,
            ifMatch: "\"etag-original\"",
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("\"etag-original\"", string.Join(",", request.Headers.GetValues("If-Match")));
        Assert.Equal("\"etag-updated\"", result.ETag);
    }

    private static HonuaAdminClient MakeClient(HttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://admin.example.test/"),
        };
        return new HonuaAdminClient(http, new NullTelemetry());
    }

    private static MetadataResource CreateResource(string name)
        => new()
        {
            ApiVersion = "honua.io/v1alpha1",
            Kind = "Layer",
            Metadata = new ResourceMetadata
            {
                Name = name,
                Namespace = "default",
            },
            Spec = JsonSerializer.SerializeToElement(new
            {
                schemaName = "public",
                tableName = name,
                geometryType = "Polygon",
                srid = 4326,
            }),
        };

    private static HttpResponseMessage JsonOk(MetadataResource resource, string etag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(AdminApiResponse<MetadataResource>.CreateSuccess(resource), JsonOptions),
                Encoding.UTF8,
                "application/json"),
        };
        response.Headers.TryAddWithoutValidation("ETag", etag);
        return response;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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
