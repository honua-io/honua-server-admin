// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Admin.AuditTools;

/// <summary>
/// Serializes <see cref="EndpointInventory"/> to a deterministic JSON layout
/// (keys sorted, lines stable) so the committed file diffs cleanly.
/// </summary>
public static class InventorySerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static string Serialize(EndpointInventory inventory)
    {
        var payload = new
        {
            schema_version = 1,
            honua_server_commit = inventory.HonuaServerCommit,
            generated_count = inventory.Endpoints.Count,
            endpoints = inventory.Endpoints,
        };

        var json = JsonSerializer.Serialize(payload, Options);
        // JsonSerializer normalizes line endings to the platform; force LF for stable diffs.
        return json.Replace("\r\n", "\n") + "\n";
    }

    public static EndpointInventory Deserialize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var commit = root.GetProperty("honua_server_commit").GetString() ?? "unknown";
        var endpoints = new List<EndpointEntry>();
        foreach (var element in root.GetProperty("endpoints").EnumerateArray())
        {
            endpoints.Add(new EndpointEntry(
                Key: element.GetProperty("key").GetString()!,
                Feature: element.GetProperty("feature").GetString()!,
                File: element.GetProperty("file").GetString()!,
                Kind: element.GetProperty("kind").GetString()!,
                Verb: element.GetProperty("verb").GetString()!,
                Route: element.GetProperty("route").GetString()!,
                SourceFile: element.GetProperty("source_file").GetString()!,
                SourceLine: element.GetProperty("source_line").GetInt32()));
        }
        return new EndpointInventory(commit, endpoints);
    }
}
