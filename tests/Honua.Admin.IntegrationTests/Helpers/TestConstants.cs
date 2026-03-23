namespace Honua.Admin.IntegrationTests.Helpers;

/// <summary>
/// Shared constants for seeded test data and common test values.
/// </summary>
internal static class TestConstants
{
    /// <summary>
    /// Name of the seeded parks table (Point geometry).
    /// </summary>
    public const string ParksTable = "parks";

    /// <summary>
    /// Name of the seeded roads table (LineString geometry).
    /// </summary>
    public const string RoadsTable = "roads";

    /// <summary>
    /// Schema for test tables.
    /// </summary>
    public const string PublicSchema = "public";

    /// <summary>
    /// SRID used for test geometries (WGS 84).
    /// </summary>
    public const int TestSrid = 4326;

    /// <summary>
    /// API key used for admin authentication in tests.
    /// </summary>
    public const string TestApiKey = "integration-test-api-key-2024";

    /// <summary>
    /// Database name for the test PostGIS instance.
    /// </summary>
    public const string TestDatabase = "honua_test";

    /// <summary>
    /// Username for the test PostGIS instance.
    /// </summary>
    public const string TestDbUser = "honua";

    /// <summary>
    /// Password for the test PostGIS instance.
    /// </summary>
    public const string TestDbPassword = "honua_test_pass";

    /// <summary>
    /// Metadata resource kind used in tests.
    /// </summary>
    public const string TestMetadataKind = "Layer";

    /// <summary>
    /// Metadata resource namespace used in tests.
    /// </summary>
    public const string TestMetadataNamespace = "integration-tests";

    /// <summary>
    /// Metadata API version used in test resources.
    /// </summary>
    public const string TestApiVersion = "honua.io/v1alpha1";

    /// <summary>
    /// Docker network name shared between PostGIS and Honua Server containers.
    /// </summary>
    public const string DockerNetworkName = "honua-integration-test";

    /// <summary>
    /// Internal hostname for PostGIS within the Docker network.
    /// </summary>
    public const string PostGisNetworkAlias = "postgis";

    /// <summary>
    /// Port Honua Server listens on inside its container.
    /// </summary>
    public const int HonuaServerInternalPort = 8080;

    /// <summary>
    /// Generates a unique name with a GUID suffix for test isolation.
    /// </summary>
    public static string UniqueName(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(63, prefix.Length + 33)];
}
