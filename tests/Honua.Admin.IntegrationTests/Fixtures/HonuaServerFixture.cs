using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Honua.Admin.IntegrationTests.Helpers;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Honua.Admin.IntegrationTests.Fixtures;

/// <summary>
/// xUnit fixture that orchestrates PostGIS and Honua Server containers for integration testing.
/// </summary>
public sealed class HonuaServerFixture : IAsyncLifetime
{
    private INetwork _network = null!;
    private PostgreSqlContainer _postGisContainer = null!;
    private IContainer _honuaServerContainer = null!;

    /// <summary>
    /// The authenticated admin client built via the SDK's AddHonuaAdmin() registration.
    /// </summary>
    public IHonuaAdminClient Client { get; private set; } = null!;

    /// <summary>
    /// Base URL of the Honua Server (mapped to localhost).
    /// </summary>
    public string ServerBaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// API key used for admin authentication.
    /// </summary>
    public string ApiKey => TestConstants.TestApiKey;

    /// <summary>
    /// External (host-mapped) port for the PostGIS container.
    /// </summary>
    public int PostGisExternalPort { get; private set; }

    /// <summary>
    /// Connection string for the PostGIS container accessible from the host.
    /// </summary>
    public string PostGisConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// The internal Docker network hostname for PostGIS.
    /// </summary>
    public string PostGisInternalHost => TestConstants.PostGisNetworkAlias;

    /// <summary>
    /// The internal Docker network port for PostGIS (always 5432).
    /// </summary>
    public int PostGisInternalPort => 5432;

    /// <summary>
    /// The service provider used to resolve the client. Kept alive for the fixture lifetime.
    /// </summary>
    private ServiceProvider? _serviceProvider;

    public async Task InitializeAsync()
    {
        // Create a shared Docker network
        _network = new NetworkBuilder()
            .WithName($"{TestConstants.DockerNetworkName}-{Guid.NewGuid():N}"[..48])
            .Build();

        await _network.CreateAsync();

        // Start PostGIS container
        _postGisContainer = new PostgreSqlBuilder()
            .WithImage("postgis/postgis:18-3.6")
            .WithDatabase(TestConstants.TestDatabase)
            .WithUsername(TestConstants.TestDbUser)
            .WithPassword(TestConstants.TestDbPassword)
            .WithNetwork(_network)
            .WithNetworkAliases(TestConstants.PostGisNetworkAlias)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
            .Build();

        await _postGisContainer.StartAsync();

        PostGisExternalPort = _postGisContainer.GetMappedPublicPort(5432);
        PostGisConnectionString = _postGisContainer.GetConnectionString();

        // Seed test tables
        await SeedTestDataAsync();

        // Start Honua Server container
        _honuaServerContainer = new ContainerBuilder()
            .WithImage("honuaio/honua-server:latest")
            .WithNetwork(_network)
            .WithPortBinding(TestConstants.HonuaServerInternalPort, true)
            .WithEnvironment("HONUA__Database__Host", TestConstants.PostGisNetworkAlias)
            .WithEnvironment("HONUA__Database__Port", "5432")
            .WithEnvironment("HONUA__Database__Name", TestConstants.TestDatabase)
            .WithEnvironment("HONUA__Database__Username", TestConstants.TestDbUser)
            .WithEnvironment("HONUA__Database__Password", TestConstants.TestDbPassword)
            .WithEnvironment("HONUA__Admin__ApiKey", TestConstants.TestApiKey)
            .WithEnvironment("HONUA__Admin__Enabled", "true")
            .WithEnvironment("ASPNETCORE_URLS", $"http://+:{TestConstants.HonuaServerInternalPort}")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPath("/api/v1/admin/version")
                    .ForPort(TestConstants.HonuaServerInternalPort)
                    .WithHeader("X-Api-Key", TestConstants.TestApiKey)))
            .Build();

        await _honuaServerContainer.StartAsync();

        var mappedPort = _honuaServerContainer.GetMappedPublicPort(TestConstants.HonuaServerInternalPort);
        ServerBaseUrl = $"http://localhost:{mappedPort}";

        // Build the SDK client via DI using AddHonuaAdmin()
        var services = new ServiceCollection();
        services.AddHonuaAdmin(options =>
        {
            options.BaseAddress = new Uri(ServerBaseUrl);
            options.ApiKey = TestConstants.TestApiKey;
            options.EnableRetry = false;
        });

        _serviceProvider = services.BuildServiceProvider();
        Client = _serviceProvider.GetRequiredService<IHonuaAdminClient>();
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();

        if (_honuaServerContainer is not null)
        {
            await _honuaServerContainer.DisposeAsync();
        }

        if (_postGisContainer is not null)
        {
            await _postGisContainer.DisposeAsync();
        }

        if (_network is not null)
        {
            await _network.DeleteAsync();
            await _network.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a fresh HttpClient pointed at the Honua Server with no authentication headers.
    /// Useful for testing authentication failure scenarios.
    /// </summary>
    public HttpClient CreateUnauthenticatedHttpClient()
    {
        return new HttpClient { BaseAddress = new Uri(ServerBaseUrl) };
    }

    /// <summary>
    /// Creates an HttpClient with a specific API key header.
    /// </summary>
    public HttpClient CreateHttpClientWithApiKey(string apiKey)
    {
        var client = new HttpClient { BaseAddress = new Uri(ServerBaseUrl) };
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private async Task SeedTestDataAsync()
    {
        await using var connection = new NpgsqlConnection(PostGisConnectionString);
        await connection.OpenAsync();

        // Ensure PostGIS extension is available
        await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS postgis;", connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Create parks table with Point geometry
        await using (var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS parks (
                id SERIAL PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                geom GEOMETRY(Point, 4326) NOT NULL
            );
            """, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert sample park data
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO parks (name, description, geom) VALUES
                ('Central Park', 'A large public park in NYC', ST_SetSRID(ST_MakePoint(-73.9654, 40.7829), 4326)),
                ('Golden Gate Park', 'Urban park in San Francisco', ST_SetSRID(ST_MakePoint(-122.4862, 37.7694), 4326)),
                ('Griffith Park', 'Large urban park in LA', ST_SetSRID(ST_MakePoint(-118.3004, 34.1365), 4326))
            ON CONFLICT DO NOTHING;
            """, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Create roads table with LineString geometry
        await using (var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS roads (
                id SERIAL PRIMARY KEY,
                name VARCHAR(255) NOT NULL,
                road_type VARCHAR(50),
                geom GEOMETRY(LineString, 4326) NOT NULL
            );
            """, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert sample road data
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO roads (name, road_type, geom) VALUES
                ('Main Street', 'primary', ST_SetSRID(ST_MakeLine(ST_MakePoint(-73.99, 40.73), ST_MakePoint(-73.98, 40.74)), 4326)),
                ('Broadway', 'primary', ST_SetSRID(ST_MakeLine(ST_MakePoint(-73.98, 40.76), ST_MakePoint(-73.97, 40.77)), 4326))
            ON CONFLICT DO NOTHING;
            """, connection))
        {
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

/// <summary>
/// xUnit collection definition that ensures tests sharing the Honua Server fixture run serially.
/// </summary>
[CollectionDefinition("HonuaServer")]
public class HonuaServerCollection : ICollectionFixture<HonuaServerFixture>
{
}
