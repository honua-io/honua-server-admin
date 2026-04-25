// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System;
using System.Diagnostics;
using System.IO;
using Honua.Admin.AuditTools;

var (command, options) = ParseArgs(args);

return command switch
{
    "generate" => Generate(options),
    "seed-coverage" => SeedCoverage(options),
    "render" => Render(options),
    _ => Usage(),
};

static int Generate(Options options)
{
    var honuaServerRoot = options.HonuaServerRoot ?? DiscoverHonuaServerRoot();
    if (honuaServerRoot is null)
    {
        Console.Error.WriteLine(
            "honua-server checkout not found. Pass --honua-server-root or set HONUA_SERVER_PATH.");
        return 2;
    }

    var commit = options.HonuaServerCommit ?? TryReadGitSha(honuaServerRoot) ?? "unknown";
    var inventory = EndpointInventoryGenerator.Generate(honuaServerRoot, commit);
    var json = InventorySerializer.Serialize(inventory);

    var outputPath = options.OutputPath ?? Path.Combine(
        DiscoverAdminRepoRoot() ?? Directory.GetCurrentDirectory(),
        "docs",
        "admin-ui-api-coverage",
        "endpoints.generated.json");

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    File.WriteAllText(outputPath, json);
    Console.WriteLine($"Wrote {inventory.Endpoints.Count} endpoints from honua-server@{commit} to {outputPath}");
    return 0;
}

static int SeedCoverage(Options options)
{
    var docsRoot = options.DocsRoot ?? Path.Combine(
        DiscoverAdminRepoRoot() ?? Directory.GetCurrentDirectory(),
        "docs",
        "admin-ui-api-coverage");
    var inventoryPath = Path.Combine(docsRoot, "endpoints.generated.json");
    var coveragePath = Path.Combine(docsRoot, "coverage.yaml");

    if (!File.Exists(inventoryPath))
    {
        Console.Error.WriteLine($"endpoints.generated.json not found at {inventoryPath}. Run `generate` first.");
        return 2;
    }

    var inventory = InventorySerializer.Deserialize(File.ReadAllText(inventoryPath));
    var existing = File.Exists(coveragePath)
        ? CoverageYamlSerializer.Load(coveragePath).Rows.ToDictionary(r => r.Key, System.StringComparer.Ordinal)
        : new System.Collections.Generic.Dictionary<string, CoverageRow>(System.StringComparer.Ordinal);

    var rows = new System.Collections.Generic.List<CoverageRow>(inventory.Endpoints.Count);
    var unclassified = 0;
    foreach (var endpoint in inventory.Endpoints)
    {
        if (existing.TryGetValue(endpoint.Key, out var row))
        {
            // Preserve existing hand-tuned rows verbatim.
            rows.Add(row);
            continue;
        }

        var rule = CoverageRules.Resolve(endpoint);
        if (rule is null)
        {
            unclassified++;
            rows.Add(new CoverageRow(
                Key: endpoint.Key,
                Coverage: "missing",
                AdminPage: null,
                Priority: "deferred",
                OutOfScopeReason: null,
                FollowUpTicket: null,
                Notes: "TODO: classify this endpoint family in CoverageRules.cs"));
            continue;
        }

        rows.Add(new CoverageRow(
            Key: endpoint.Key,
            Coverage: rule.Coverage,
            AdminPage: rule.AdminPage,
            Priority: rule.Priority,
            OutOfScopeReason: rule.OutOfScopeReason,
            FollowUpTicket: rule.FollowUpTicket,
            Notes: rule.Notes));
    }

    var snapshot = System.DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    var sb = new System.Text.StringBuilder();
    sb.AppendLine("# Admin UI ↔ honua-server API coverage matrix.");
    sb.AppendLine("# Authored by `dotnet run --project tools/audit-api-surface -- seed-coverage`.");
    sb.AppendLine("# Hand-edit rows for per-endpoint classifications; the seeder preserves them on re-run.");
    sb.Append("snapshot_date: ").AppendLine(snapshot);
    sb.Append("honua_server_commit: ").AppendLine(inventory.HonuaServerCommit);
    sb.AppendLine("rows:");
    sb.Append(CoverageYamlSerializer.SerializeRows(rows));

    File.WriteAllText(coveragePath, sb.ToString());
    Console.WriteLine($"Seeded coverage.yaml at {coveragePath} ({rows.Count} rows, {unclassified} unclassified).");
    return 0;
}

static int Render(Options options)
{
    var docsRoot = options.DocsRoot ?? Path.Combine(
        DiscoverAdminRepoRoot() ?? Directory.GetCurrentDirectory(),
        "docs",
        "admin-ui-api-coverage");

    var inventoryPath = Path.Combine(docsRoot, "endpoints.generated.json");
    var coveragePath = Path.Combine(docsRoot, "coverage.yaml");
    var matrixPath = Path.Combine(docsRoot, "matrix.md");

    if (!File.Exists(inventoryPath))
    {
        Console.Error.WriteLine($"endpoints.generated.json not found at {inventoryPath}. Run `generate` first.");
        return 2;
    }
    if (!File.Exists(coveragePath))
    {
        Console.Error.WriteLine($"coverage.yaml not found at {coveragePath}. Author it first.");
        return 2;
    }

    var inventory = InventorySerializer.Deserialize(File.ReadAllText(inventoryPath));
    var coverage = CoverageYamlSerializer.Load(coveragePath);
    var rendered = MatrixRenderer.Render(inventory, coverage);
    File.WriteAllText(matrixPath, rendered);
    Console.WriteLine($"Wrote {matrixPath}");
    return 0;
}

static int Usage()
{
    Console.Error.WriteLine("Usage: audit-api-surface <command> [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Commands:");
    Console.Error.WriteLine("  generate    Walk honua-server feature folders and emit docs/admin-ui-api-coverage/endpoints.generated.json.");
    Console.Error.WriteLine("  render      Render docs/admin-ui-api-coverage/matrix.md from coverage.yaml + endpoints.generated.json.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options (generate):");
    Console.Error.WriteLine("  --honua-server-root <path>    Override the honua-server checkout path.");
    Console.Error.WriteLine("  --honua-server-commit <sha>   Override the captured commit SHA (default: git rev-parse HEAD).");
    Console.Error.WriteLine("  --output <path>               Override the output JSON path.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Options (render):");
    Console.Error.WriteLine("  --docs-root <path>            Override docs/admin-ui-api-coverage/.");
    return 1;
}

static (string command, Options options) ParseArgs(string[] args)
{
    if (args.Length == 0)
    {
        return ("help", new Options());
    }

    var command = args[0];
    var options = new Options();
    for (var i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "--honua-server-root":
                options.HonuaServerRoot = args[++i];
                break;
            case "--honua-server-commit":
                options.HonuaServerCommit = args[++i];
                break;
            case "--output":
                options.OutputPath = args[++i];
                break;
            case "--docs-root":
                options.DocsRoot = args[++i];
                break;
            default:
                Console.Error.WriteLine($"Unknown argument: {arg}");
                Environment.Exit(1);
                break;
        }
    }
    return (command, options);
}

static string? DiscoverHonuaServerRoot()
{
    var env = Environment.GetEnvironmentVariable("HONUA_SERVER_PATH");
    if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
    {
        return env;
    }

    var here = Directory.GetCurrentDirectory();
    string? cursor = here;
    while (cursor is not null)
    {
        var sibling = Path.Combine(Path.GetDirectoryName(cursor)!, "honua-server");
        if (Directory.Exists(Path.Combine(sibling, "src", "Honua.Server", "Features")))
        {
            return sibling;
        }
        cursor = Path.GetDirectoryName(cursor);
    }

    var home = Environment.GetEnvironmentVariable("HOME");
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

static string? DiscoverAdminRepoRoot()
{
    var here = Directory.GetCurrentDirectory();
    string? cursor = here;
    while (cursor is not null)
    {
        if (File.Exists(Path.Combine(cursor, "Honua.Admin.sln")))
        {
            return cursor;
        }
        cursor = Path.GetDirectoryName(cursor);
    }
    return null;
}

static string? TryReadGitSha(string repoRoot)
{
    try
    {
        var psi = new ProcessStartInfo("git", "rev-parse HEAD")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi);
        if (process is null)
        {
            return null;
        }
        var sha = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 ? sha : null;
    }
    catch
    {
        return null;
    }
}

internal sealed class Options
{
    public string? HonuaServerRoot { get; set; }
    public string? HonuaServerCommit { get; set; }
    public string? OutputPath { get; set; }
    public string? DocsRoot { get; set; }
}
