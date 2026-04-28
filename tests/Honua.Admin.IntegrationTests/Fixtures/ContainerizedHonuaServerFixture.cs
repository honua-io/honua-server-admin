// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Honua.Admin.Models.Admin;
using Honua.Admin.Services.Admin;
using Testcontainers.PostgreSql;

namespace Honua.Admin.IntegrationTests.Fixtures;

/// <summary>
/// Opt-in Docker fixture for the issue #19 containerized E2E lane. The fixture
/// starts PostGIS and Honua Server on a private Docker network, then points the
/// real admin HTTP client at the mapped Honua Server port.
/// </summary>
public sealed class ContainerizedHonuaServerFixture : IAsyncDisposable
{
    private readonly ContainerizedHonuaServerOptions _options;
    private readonly INetwork _network;
    private readonly PostgreSqlContainer _postgis;
    private readonly IContainer _honuaServer;
    private readonly HttpClient _httpClient;

    private ContainerizedHonuaServerFixture(ContainerizedHonuaServerOptions options)
    {
        _options = options;
        _network = new NetworkBuilder()
            .WithName($"honua-admin-e2e-{Guid.NewGuid():N}")
            .Build();

        _postgis = new PostgreSqlBuilder()
            .WithImage(options.PostgisImage)
            .WithDatabase(options.PostgisDatabase)
            .WithUsername(options.PostgisUsername)
            .WithPassword(options.PostgisPassword)
            .WithNetwork(_network)
            .WithNetworkAliases("postgis")
            .Build();

        _honuaServer = new ContainerBuilder()
            .WithImage(options.HonuaServerImage)
            .WithNetwork(_network)
            .WithPortBinding(options.HonuaServerPort, assignRandomHostPort: true)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("ASPNETCORE_URLS", $"http://+:{options.HonuaServerPort}")
            .WithEnvironment("ConnectionStrings__DefaultConnection", options.PostgisNetworkConnectionString)
            .WithEnvironment("HONUA_DEV_AUTH", "false")
            .WithEnvironment("HONUA_ADMIN_PASSWORD", options.AdminApiKey)
            .WithEnvironment("Security__ConnectionEncryption__MasterKey", options.ConnectionEncryptionMasterKey)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPort((ushort)options.HonuaServerPort)
                .ForPath(options.WaitPath)
                .WithHeader("X-API-Key", options.AdminApiKey)))
            .Build();

        _httpClient = new HttpClient();
        if (!string.IsNullOrWhiteSpace(options.AdminApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", options.AdminApiKey);
        }
    }

    public string BaseUrl { get; private set; } = string.Empty;

    public IHonuaAdminClient AdminClient { get; private set; } = null!;

    public string SeedSchema => "public";

    public string SeedTable => "admin_e2e_parcels";

    public static bool IsEnabled => ContainerizedHonuaServerOptions.Load().Enabled;

    public static async Task<ContainerizedHonuaServerFixture> StartAsync(CancellationToken cancellationToken = default)
    {
        var fixture = new ContainerizedHonuaServerFixture(ContainerizedHonuaServerOptions.Load());
        await fixture.StartCoreAsync(cancellationToken).ConfigureAwait(false);
        return fixture;
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        await _network.CreateAsync(cancellationToken).ConfigureAwait(false);
        await _postgis.StartAsync(cancellationToken).ConfigureAwait(false);
        await _honuaServer.StartAsync(cancellationToken).ConfigureAwait(false);

        var mappedPort = _honuaServer.GetMappedPublicPort(_options.HonuaServerPort);
        BaseUrl = new UriBuilder("http", _honuaServer.Hostname, mappedPort).Uri.ToString();
        _httpClient.BaseAddress = new Uri(BaseUrl, UriKind.Absolute);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
        AdminClient = new HonuaAdminClient(_httpClient, new NullTelemetry());
    }

    public CreateConnectionRequest CreatePostgisConnectionRequest(string name) => new()
    {
        Name = name,
        Description = "Container E2E PostGIS connection",
        Host = "postgis",
        Port = 5432,
        DatabaseName = _options.PostgisDatabase,
        Username = _options.PostgisUsername,
        Password = _options.PostgisPassword,
        SslRequired = false,
        SslMode = "Disable",
    };

    public async Task SeedSpatialCatalogAsync(CancellationToken cancellationToken)
    {
        var result = await _postgis.ExecScriptAsync(
            """
            CREATE EXTENSION IF NOT EXISTS postgis;

            DROP TABLE IF EXISTS public.admin_e2e_parcels;

            CREATE TABLE public.admin_e2e_parcels
            (
                id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
                name text NOT NULL,
                category text NOT NULL,
                geom geometry(Polygon, 4326) NOT NULL
            );

            INSERT INTO public.admin_e2e_parcels (name, category, geom)
            VALUES
                ('Ala Wai field block', 'field-review', ST_GeomFromText('POLYGON((-157.8350 21.2830,-157.8320 21.2830,-157.8320 21.2860,-157.8350 21.2860,-157.8350 21.2830))', 4326)),
                ('Kapahulu service area', 'service-area', ST_GeomFromText('POLYGON((-157.8200 21.2750,-157.8160 21.2750,-157.8160 21.2790,-157.8200 21.2790,-157.8200 21.2750))', 4326));

            CREATE INDEX admin_e2e_parcels_geom_idx
                ON public.admin_e2e_parcels
                USING gist (geom);
            """,
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to seed PostGIS test data: {result.Stderr}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _httpClient.Dispose();

        await DisposeContainerAsync(_honuaServer).ConfigureAwait(false);
        await DisposeContainerAsync(_postgis).ConfigureAwait(false);

        await _network.DeleteAsync().ConfigureAwait(false);
        await _network.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task DisposeContainerAsync(IAsyncDisposable disposable)
    {
        try
        {
            await disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cleanup failures should not hide the original test failure.
        }
    }

    private sealed class NullTelemetry : IAdminTelemetry
    {
        public void PageNavigated(string pageRoute, string? principalId) { }
        public void DestructiveAction(string action, string? targetId, string? principalId) { }
        public void ClientRequestFailed(string operation, string error) { }
    }
}

public sealed record ContainerizedHonuaServerOptions
{
    public bool Enabled { get; init; }
    public string HonuaServerImage { get; init; } = "ghcr.io/honua-io/honua-server:latest";
    public string PostgisImage { get; init; } = "postgis/postgis:18-3.6";
    public string PostgisDatabase { get; init; } = "honua_integration";
    public string PostgisUsername { get; init; } = "honua_test";
    public string PostgisPassword { get; init; } = "honua_test";
    public string AdminApiKey { get; init; } = "test-admin-key";
    public string ConnectionEncryptionMasterKey { get; init; } = "container-e2e-master-key-0123456789";
    public int HonuaServerPort { get; init; } = 8080;
    public int RequestTimeoutSeconds { get; init; } = 30;
    public string WaitPath { get; init; } = "/api/v1/admin/features/";

    public string PostgisNetworkConnectionString
        => $"Host=postgis;Port=5432;Database={PostgisDatabase};Username={PostgisUsername};Password={PostgisPassword};";

    public static ContainerizedHonuaServerOptions Load() => new()
    {
        Enabled = IsTruthy(Environment.GetEnvironmentVariable("HONUA_ADMIN_CONTAINER_E2E")),
        HonuaServerImage = GetValue("HONUA_SERVER_IMAGE", "ghcr.io/honua-io/honua-server:latest"),
        PostgisImage = GetValue("HONUA_POSTGIS_IMAGE", "postgis/postgis:18-3.6"),
        PostgisDatabase = GetValue("HONUA_POSTGIS_DATABASE", "honua_integration"),
        PostgisUsername = GetValue("HONUA_POSTGIS_USERNAME", "honua_test"),
        PostgisPassword = GetValue("HONUA_POSTGIS_PASSWORD", "honua_test"),
        AdminApiKey = GetValue("HONUA_ADMIN_CONTAINER_API_KEY", "test-admin-key"),
        ConnectionEncryptionMasterKey = GetValue("HONUA_ADMIN_CONTAINER_ENCRYPTION_MASTER_KEY", "container-e2e-master-key-0123456789"),
        HonuaServerPort = GetInt("HONUA_SERVER_CONTAINER_PORT", 8080),
        RequestTimeoutSeconds = GetInt("HONUA_ADMIN_CONTAINER_TIMEOUT_SECONDS", 30),
        WaitPath = GetValue("HONUA_SERVER_WAIT_PATH", "/api/v1/admin/features/"),
    };

    private static string GetValue(string key, string fallback)
        => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key))
            ? fallback
            : Environment.GetEnvironmentVariable(key)!;

    private static int GetInt(string key, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(key), out var value) && value > 0
            ? value
            : fallback;

    private static bool IsTruthy(string? value)
        => string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}
