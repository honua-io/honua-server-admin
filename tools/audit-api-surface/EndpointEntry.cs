// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

namespace Honua.Admin.AuditTools;

/// <summary>
/// One endpoint discovered in the honua-server source. The <see cref="Key"/>
/// is stable across runs and is what `coverage.yaml` rows are keyed by.
/// </summary>
public sealed record EndpointEntry(
    string Key,
    string Feature,
    string File,
    string Kind,
    string Verb,
    string Route,
    string SourceFile,
    int SourceLine)
{
    public static EndpointEntry Http(string feature, string file, string verb, string route, string sourceFile, int sourceLine)
        => new(
            Key: $"{feature}/{file}:{verb}:{route}",
            Feature: feature,
            File: file,
            Kind: "http",
            Verb: verb,
            Route: route,
            SourceFile: sourceFile,
            SourceLine: sourceLine);

    public static EndpointEntry Grpc(string feature, string file, string methodName, string sourceFile, int sourceLine)
        => new(
            Key: $"{feature}/{file}:GRPC:{methodName}",
            Feature: feature,
            File: file,
            Kind: "grpc",
            Verb: "GRPC",
            Route: methodName,
            SourceFile: sourceFile,
            SourceLine: sourceLine);
}

/// <summary>
/// Top-level inventory output. The honua-server commit SHA is captured so a
/// future audit can pinpoint what state the inventory was derived from.
/// </summary>
public sealed record EndpointInventory(
    string HonuaServerCommit,
    System.Collections.Generic.IReadOnlyList<EndpointEntry> Endpoints);
