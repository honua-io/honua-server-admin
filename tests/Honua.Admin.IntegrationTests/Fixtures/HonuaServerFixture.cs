// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Honua.Admin.Auth;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Honua.Admin.IntegrationTests.Fixtures;

/// <summary>
/// xUnit fixture that hosts a fake honua-server admin API in-process and wires
/// the real <see cref="HonuaAdminClient"/> against it. The fake mirrors the
/// JSON shapes documented in the coverage matrix for representative P0
/// endpoints (features, configuration summary, connections).
///
/// This is the scaffold for the full Testcontainers-backed E2E suite cherry-
/// picked from PR #20: that suite needs <c>Honua.Sdk.Admin</c> + Docker and
/// is deferred until the SDK is published. Today's fixture exercises the
/// HTTP client + serializer + auth handler chain end-to-end against a real
/// HTTP server, which is the operationally significant path.
/// </summary>
public sealed class HonuaServerFixture : IAsyncLifetime
{
    private const string ApiKey = "test-admin-key";

    private IHost? _host;
    public string ServerBaseUrl { get; private set; } = string.Empty;
    public IHonuaAdminClient AdminClient { get; private set; } = null!;

    public Task InitializeAsync()
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/v1/admin/features/", () => AdminApiResponse<FeatureOverview>.CreateSuccess(new FeatureOverview
                        {
                            CurrentEdition = "Enterprise",
                            Features =
                            [
                                new FeatureOverviewItem { Key = "ogc-features", DisplayName = "OGC Features", Category = "Protocol", Description = "OGC API Features support", IsEnabled = true, MinimumEdition = "Community" },
                                new FeatureOverviewItem { Key = "tiles", DisplayName = "Tiles", Category = "Protocol", Description = "Tile serving", IsEnabled = true, MinimumEdition = "Enterprise" },
                            ],
                        }));

                        endpoints.MapGet("/api/v1/admin/configuration/summary", () => new ConfigurationSummary
                        {
                            Environment = "Integration",
                            TotalTypes = 4,
                            RegisteredTypes = 4,
                            TotalProperties = 22,
                            SecretProperties = 2,
                            RequiredProperties = 3,
                            TypesWithValidation = 4,
                            TotalSecrets = 2,
                            ValidSecrets = 2,
                            InvalidSecrets = 0,
                            DiscoveryDurationMs = 7,
                            LastDiscovery = DateTimeOffset.Parse("2026-04-25T00:00:00Z"),
                        });

                        endpoints.MapGet("/api/v1/admin/connections/", () =>
                        {
                            var rows = new[]
                            {
                                new ConnectionSummary
                                {
                                    ConnectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                                    Name = "live-postgis",
                                    Host = "db.integration",
                                    Port = 5432,
                                    DatabaseName = "honua",
                                    Username = "honua",
                                    SslRequired = true,
                                    SslMode = "Require",
                                    StorageType = "managed",
                                    IsActive = true,
                                    HealthStatus = "Healthy",
                                    CreatedAt = DateTimeOffset.Parse("2026-04-25T00:00:00Z"),
                                    CreatedBy = "integration",
                                },
                            };

                            return AdminApiResponse<ConnectionSummary[]>.CreateSuccess(rows);
                        });

                        // 401-respond when the API key is missing so the auth handler
                        // chain is exercised end-to-end.
                        endpoints.MapGet("/api/v1/admin/secured-probe", (HttpContext ctx) =>
                        {
                            return ctx.Request.Headers.TryGetValue("X-API-Key", out var key) && key == ApiKey
                                ? Results.Ok(new { ok = true })
                                : Results.Unauthorized();
                        });
                    });
                });
                web.ConfigureServices(s => s.AddRouting());
            });

        _host = builder.Start();
        var server = _host.GetTestServer();
        var httpClient = server.CreateClient();
        ServerBaseUrl = server.BaseAddress.ToString();

        // Build the admin client via the same DI shape the WebAssembly host uses.
        var services = new ServiceCollection();
        services.AddSingleton(httpClient);
        services.AddSingleton<IAdminTelemetry, NullTelemetry>();
        services.AddSingleton<IHonuaAdminClient>(sp => new HonuaAdminClient(httpClient, sp.GetRequiredService<IAdminTelemetry>()));

        AdminClient = services.BuildServiceProvider().GetRequiredService<IHonuaAdminClient>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync().ConfigureAwait(false);
            _host.Dispose();
        }
    }

    private sealed class NullTelemetry : IAdminTelemetry
    {
        public void PageNavigated(string pageRoute, string? principalId) { }
        public void DestructiveAction(string action, string? targetId, string? principalId) { }
        public void ClientRequestFailed(string operation, string error) { }
    }
}
