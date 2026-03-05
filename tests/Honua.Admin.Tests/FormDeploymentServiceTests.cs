using System.Net;
using System.Text;
using Honua.Admin.Configuration;
using Honua.Admin.Models;
using Honua.Admin.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Honua.Admin.Tests;

public sealed class FormDeploymentServiceTests
{
    [Fact]
    public async Task DeployAsync_ReturnsFailure_WhenEndpointMissing()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("should not call")));
        var service = BuildService(client, new HonuaAdminOptions { DeployEndpoint = string.Empty });

        var result = await service.DeployAsync(CreateForm());

        Assert.False(result.Succeeded);
        Assert.Equal("Deployment endpoint is not configured.", result.Message);
    }

    [Fact]
    public async Task DeployAsync_ReturnsFailure_WhenServerRejectsRequest()
    {
        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var service = BuildService(client, new HonuaAdminOptions { DeployEndpoint = "/deploy" });

        var result = await service.DeployAsync(CreateForm());

        Assert.False(result.Succeeded);
        Assert.Equal("Deployment failed. Please verify server status and try again.", result.Message);
    }

    [Fact]
    public async Task DeployAsync_ReturnsSuccess_WhenServerAcceptsRequest()
    {
        const string json = """
                            {
                              "deploymentId": "dep-123",
                              "message": "Queued for deployment"
                            }
                            """;

        using var client = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }));
        var service = BuildService(client, new HonuaAdminOptions { DeployEndpoint = "/deploy" });

        var result = await service.DeployAsync(CreateForm());

        Assert.True(result.Succeeded);
        Assert.Equal("Queued for deployment", result.Message);
        Assert.Equal("dep-123", result.DeploymentId);
    }

    private static FormDeploymentService BuildService(HttpClient client, HonuaAdminOptions options)
    {
        client.BaseAddress ??= new Uri("https://localhost");
        return new FormDeploymentService(client, Options.Create(options), NullLogger<FormDeploymentService>.Instance);
    }

    private static XlsForm CreateForm()
    {
        return new XlsForm
        {
            Name = "Inspection Form",
            Version = "1.0.0",
            Settings = new XlsFormSettings { FormId = "inspection-form" }
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
