// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.IO;
using System.Linq;
using Honua.Admin.AuditTools;
using Xunit;

namespace Honua.Admin.Tests.Audit;

/// <summary>
/// Synthetic-fixture regression tests that pin the inventory generator's
/// support for the endpoint declaration shapes prior scanners missed:
///   1. <c>group.Map(route, handler).WithMetadata(new HttpMethodMetadata(...))</c>.
///   2. <c>endpoints.MapPost(route, handler)</c> calls whose argument list
///      wraps onto subsequent lines.
///   3. <c>HttpMethodMetadata([HttpMethods.X])</c> C# 12 collection-expression
///      metadata.
///   4. <c>group.Map(string.Empty, handler)</c> / <c>group.MapGet(string.Empty, ...)</c>
///      where the route argument is the empty-string sentinel rather than a
///      string literal.
/// </summary>
public sealed class EndpointInventoryGeneratorTests
{
    [Fact]
    public void Generic_Map_With_HttpMethodMetadata_Is_Captured()
    {
        var honuaServerRoot = WriteFixtureFeature(
            featureName: "FixtureFeature",
            fileName: "FixtureGenericMapEndpoints.cs",
            source: """
                namespace Honua.Server.Features.FixtureFeature;
                internal static class FixtureGenericMapEndpoints
                {
                    public static void Map(IEndpointRouteBuilder endpoints)
                    {
                        var group = endpoints.MapGroup("/api/v1/admin/widgets");
                        _ = group.Map("/", HandleList)
                            .WithMetadata(new HttpMethodMetadata(new[] { HttpMethods.Get }));
                        _ = group.Map("/{id}/approve", HandleApprove)
                            .WithMetadata(new HttpMethodMetadata(new[] { HttpMethods.Post }));
                    }
                }
                """);

        var inventory = EndpointInventoryGenerator.Generate(honuaServerRoot, commitSha: "fixture");
        var keys = inventory.Endpoints.Select(e => e.Key).ToList();

        Assert.Contains(
            "FixtureFeature/FixtureGenericMapEndpoints:GET:/api/v1/admin/widgets/",
            keys);
        Assert.Contains(
            "FixtureFeature/FixtureGenericMapEndpoints:POST:/api/v1/admin/widgets/{id}/approve",
            keys);
    }

    [Fact]
    public void Multiline_MapPost_Argument_List_Is_Captured()
    {
        var honuaServerRoot = WriteFixtureFeature(
            featureName: "FixtureFeature",
            fileName: "FixtureMultilineEndpoints.cs",
            source: """
                namespace Honua.Server.Features.FixtureFeature;
                internal static class FixtureMultilineEndpoints
                {
                    public static void Map(IEndpointRouteBuilder endpoints)
                    {
                        endpoints.MapPost(
                            "/api/v1/admin/multiline/queryClusters",
                            FixtureHandlers.HandleQueryClustersPost)
                            .WithDisplayName("Query Clusters")
                            .WithTags("FeatureServer")
                            .WithMetadata(new HttpMethodMetadata(new[] { HttpMethods.Post }));
                    }
                }
                """);

        var inventory = EndpointInventoryGenerator.Generate(honuaServerRoot, commitSha: "fixture");
        var keys = inventory.Endpoints.Select(e => e.Key).ToList();

        Assert.Contains(
            "FixtureFeature/FixtureMultilineEndpoints:POST:/api/v1/admin/multiline/queryClusters",
            keys);
    }

    [Fact]
    public void Generic_Map_With_CollectionExpression_HttpMethodMetadata_Is_Captured()
    {
        // C# 12+ collection-expression form: `[HttpMethods.Get]` instead of
        // the classic `new[] { HttpMethods.Get }`. honua-server uses this in
        // FeatureChangeEventsEndpoints.cs and FeatureStreamEndpoints.cs.
        var honuaServerRoot = WriteFixtureFeature(
            featureName: "FixtureFeature",
            fileName: "FixtureCollectionExpressionEndpoints.cs",
            source: """
                namespace Honua.Server.Features.FixtureFeature;
                internal static class FixtureCollectionExpressionEndpoints
                {
                    public static void Map(IEndpointRouteBuilder endpoints)
                    {
                        var group = endpoints.MapGroup("/api/v1/admin/feature-events");
                        _ = group.Map("/replay", HandleReplay)
                            .WithName("ReplayFeatureEvents")
                            .WithDescription("Replays feature events.")
                            .WithMetadata(new HttpMethodMetadata([HttpMethods.Get]));
                    }
                }
                """);

        var inventory = EndpointInventoryGenerator.Generate(honuaServerRoot, commitSha: "fixture");
        var keys = inventory.Endpoints.Select(e => e.Key).ToList();

        Assert.Contains(
            "FixtureFeature/FixtureCollectionExpressionEndpoints:GET:/api/v1/admin/feature-events/replay",
            keys);
    }

    [Fact]
    public void Generic_Map_With_StringEmpty_Route_Is_Captured()
    {
        // honua-server's MetadataResourceEndpoints maps list/create on the
        // group root via `group.Map(string.Empty, ...)`. Without
        // string.Empty support those rows silently never enter the
        // inventory and the drift guard passes against an incomplete set.
        var honuaServerRoot = WriteFixtureFeature(
            featureName: "FixtureFeature",
            fileName: "FixtureStringEmptyEndpoints.cs",
            source: """
                namespace Honua.Server.Features.FixtureFeature;
                internal static class FixtureStringEmptyEndpoints
                {
                    public static void Map(IEndpointRouteBuilder endpoints)
                    {
                        var group = endpoints.MapGroup("/api/v1/admin/metadata/resources");
                        _ = group.Map(string.Empty, HandleList)
                            .WithMetadata(new HttpMethodMetadata(new[] { HttpMethods.Get }));
                        _ = group.Map(string.Empty, HandleCreate)
                            .WithMetadata(new HttpMethodMetadata([HttpMethods.Post]));
                    }
                }
                """);

        var inventory = EndpointInventoryGenerator.Generate(honuaServerRoot, commitSha: "fixture");
        var keys = inventory.Endpoints.Select(e => e.Key).ToList();

        Assert.Contains(
            "FixtureFeature/FixtureStringEmptyEndpoints:GET:/api/v1/admin/metadata/resources/",
            keys);
        Assert.Contains(
            "FixtureFeature/FixtureStringEmptyEndpoints:POST:/api/v1/admin/metadata/resources/",
            keys);
    }

    [Fact]
    public void Verb_Map_With_StringEmpty_Route_Is_Captured()
    {
        // OgcMapsEndpoints uses `group.MapGet(string.Empty, ...)` for the
        // landing page. Sibling pattern to the generic-Map case above; both
        // share the same RoutePattern in EndpointInventoryGenerator.
        var honuaServerRoot = WriteFixtureFeature(
            featureName: "FixtureFeature",
            fileName: "FixtureVerbStringEmptyEndpoints.cs",
            source: """
                namespace Honua.Server.Features.FixtureFeature;
                internal static class FixtureVerbStringEmptyEndpoints
                {
                    public static void Map(IEndpointRouteBuilder endpoints)
                    {
                        var group = endpoints.MapGroup("/ogc/maps");
                        group.MapGet(string.Empty, GetLandingPage);
                    }
                }
                """);

        var inventory = EndpointInventoryGenerator.Generate(honuaServerRoot, commitSha: "fixture");
        var keys = inventory.Endpoints.Select(e => e.Key).ToList();

        Assert.Contains(
            "FixtureFeature/FixtureVerbStringEmptyEndpoints:GET:/ogc/maps/",
            keys);
    }

    private static string WriteFixtureFeature(string featureName, string fileName, string source)
    {
        // Each invocation gets its own throwaway honua-server checkout under
        // the test temp directory; the generator only inspects the
        // src/Honua.Server/Features tree so we materialise just that.
        var root = Path.Combine(Path.GetTempPath(), "honua-admin-audit-fixture-" + System.Guid.NewGuid().ToString("N"));
        var featureDir = Path.Combine(root, "src", "Honua.Server", "Features", featureName);
        Directory.CreateDirectory(featureDir);
        File.WriteAllText(Path.Combine(featureDir, fileName), source);
        return root;
    }
}
