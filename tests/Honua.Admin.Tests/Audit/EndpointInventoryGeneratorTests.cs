// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.IO;
using System.Linq;
using Honua.Admin.AuditTools;
using Xunit;

namespace Honua.Admin.Tests.Audit;

/// <summary>
/// Synthetic-fixture regression tests that pin the inventory generator's
/// support for the two endpoint declaration shapes the prior
/// line-at-a-time scanner missed:
///   1. <c>group.Map(route, handler).WithMetadata(new HttpMethodMetadata(...))</c>
///   2. <c>endpoints.MapPost(route, handler)</c> calls whose argument list
///      wraps onto subsequent lines.
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
