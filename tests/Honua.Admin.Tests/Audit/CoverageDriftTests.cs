// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Honua.Admin.AuditTools;
using Xunit;

namespace Honua.Admin.Tests.Audit;

/// <summary>
/// The audit drift guard. Two layers:
///   1. Always: every key in the committed `endpoints.generated.json` must have
///      a matching row in `coverage.yaml`. This catches stale yaml after a
///      regeneration and gives the audit deliverable its diffability promise.
///   2. When honua-server is checked out alongside (HONUA_SERVER_PATH or
///      `../honua-server`): re-run the inventory generator and assert it
///      matches the committed JSON byte-for-byte. This catches API surface
///      drift in the upstream repo.
/// </summary>
public sealed class CoverageDriftTests
{
    private static readonly string DocsRoot = LocateDocsRoot();

    [Fact]
    public void EveryEndpointKeyHasACoverageRow()
    {
        var inventoryPath = Path.Combine(DocsRoot, "endpoints.generated.json");
        var coveragePath = Path.Combine(DocsRoot, "coverage.yaml");
        Assert.True(File.Exists(inventoryPath), $"missing {inventoryPath}");
        Assert.True(File.Exists(coveragePath), $"missing {coveragePath}");

        var inventory = InventorySerializer.Deserialize(File.ReadAllText(inventoryPath));
        var coverage = CoverageYamlSerializer.Load(coveragePath);
        var coverageKeys = coverage.Rows.Select(r => r.Key).ToHashSet(System.StringComparer.Ordinal);

        var missing = inventory.Endpoints
            .Where(e => !coverageKeys.Contains(e.Key))
            .Select(e => e.Key)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"{missing.Count} endpoint key(s) in endpoints.generated.json have no coverage.yaml row. " +
            $"Run `dotnet run --project tools/audit-api-surface -- seed-coverage` to add them. First few: " +
            string.Join("; ", missing.Take(5)));
    }

    [Fact]
    public void EveryCoverageRowReferencesAnInventoryKey()
    {
        var inventoryPath = Path.Combine(DocsRoot, "endpoints.generated.json");
        var coveragePath = Path.Combine(DocsRoot, "coverage.yaml");
        Assert.True(File.Exists(inventoryPath));
        Assert.True(File.Exists(coveragePath));

        var inventory = InventorySerializer.Deserialize(File.ReadAllText(inventoryPath));
        var inventoryKeys = inventory.Endpoints.Select(e => e.Key).ToHashSet(System.StringComparer.Ordinal);
        var coverage = CoverageYamlSerializer.Load(coveragePath);

        var orphaned = coverage.Rows
            .Where(r => !inventoryKeys.Contains(r.Key))
            .Select(r => r.Key)
            .ToList();

        Assert.True(
            orphaned.Count == 0,
            $"{orphaned.Count} coverage.yaml row(s) reference endpoint keys that no longer exist. " +
            $"They must be removed by re-running the seeder. First few: " +
            string.Join("; ", orphaned.Take(5)));
    }

    [Fact]
    public void EveryRowUsesSchemaValidValues()
    {
        var coveragePath = Path.Combine(DocsRoot, "coverage.yaml");
        Assert.True(File.Exists(coveragePath));
        var coverage = CoverageYamlSerializer.Load(coveragePath);

        var errors = new List<string>();
        foreach (var row in coverage.Rows)
        {
            if (!CoverageMatrix.ValidCoverage.Contains(row.Coverage))
            {
                errors.Add($"{row.Key}: invalid coverage `{row.Coverage}`");
            }
            if (!CoverageMatrix.ValidPriorities.Contains(row.Priority))
            {
                errors.Add($"{row.Key}: invalid priority `{row.Priority}`");
            }
            if (row.Coverage == "out-of-scope" && string.IsNullOrWhiteSpace(row.OutOfScopeReason))
            {
                errors.Add($"{row.Key}: out-of-scope rows must have an `out_of_scope_reason`");
            }
            if (row.Coverage == "supported" && string.IsNullOrWhiteSpace(row.AdminPage))
            {
                errors.Add($"{row.Key}: supported rows must name an `admin_page`");
            }
        }

        Assert.True(errors.Count == 0, "coverage.yaml schema violations:\n  " + string.Join("\n  ", errors));
    }

    [Fact]
    public void GeneratedInventoryMatchesHonuaServerSource()
    {
        var honuaServerRoot = LocateHonuaServerRoot();
        if (honuaServerRoot is null)
        {
            // Cannot regenerate without honua-server side-by-side. The first two
            // tests still hold the diffability guarantee against the committed JSON.
            return;
        }

        var inventoryPath = Path.Combine(DocsRoot, "endpoints.generated.json");
        var committed = InventorySerializer.Deserialize(File.ReadAllText(inventoryPath));
        var regenerated = EndpointInventoryGenerator.Generate(honuaServerRoot, committed.HonuaServerCommit);

        var committedKeys = committed.Endpoints.Select(e => e.Key).ToHashSet(System.StringComparer.Ordinal);
        var regeneratedKeys = regenerated.Endpoints.Select(e => e.Key).ToHashSet(System.StringComparer.Ordinal);

        var added = regeneratedKeys.Except(committedKeys, System.StringComparer.Ordinal).Take(5).ToList();
        var removed = committedKeys.Except(regeneratedKeys, System.StringComparer.Ordinal).Take(5).ToList();

        Assert.True(
            added.Count == 0 && removed.Count == 0,
            $"endpoints.generated.json is out of sync with honua-server source. " +
            $"Re-run `dotnet run --project tools/audit-api-surface -- generate`. " +
            $"Added (first 5): {string.Join("; ", added)}; Removed (first 5): {string.Join("; ", removed)}");
    }

    private static string LocateDocsRoot()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var cursor = assemblyDir;
        while (cursor is not null)
        {
            if (File.Exists(Path.Combine(cursor, "Honua.Admin.sln")))
            {
                return Path.Combine(cursor, "docs", "admin-ui-api-coverage");
            }
            cursor = Path.GetDirectoryName(cursor);
        }
        throw new DirectoryNotFoundException("Could not locate Honua.Admin.sln from test assembly directory.");
    }

    private static string? LocateHonuaServerRoot()
    {
        var env = System.Environment.GetEnvironmentVariable("HONUA_SERVER_PATH");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(Path.Combine(env, "src", "Honua.Server", "Features")))
        {
            return env;
        }

        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var cursor = assemblyDir;
        while (cursor is not null)
        {
            var parent = Path.GetDirectoryName(cursor);
            if (parent is not null)
            {
                var sibling = Path.Combine(parent, "honua-server");
                if (Directory.Exists(Path.Combine(sibling, "src", "Honua.Server", "Features")))
                {
                    return sibling;
                }
            }
            cursor = Path.GetDirectoryName(cursor);
        }

        var home = System.Environment.GetEnvironmentVariable("HOME");
        if (!string.IsNullOrEmpty(home))
        {
            var candidate = Path.Combine(home, "honua-server");
            if (Directory.Exists(Path.Combine(candidate, "src", "Honua.Server", "Features")))
            {
                return candidate;
            }
        }

        return null;
    }
}
