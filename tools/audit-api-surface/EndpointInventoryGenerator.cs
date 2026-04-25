// Copyright (c) Honua. All rights reserved.
// Licensed under the Elastic License 2.0. See LICENSE in the project root.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Honua.Admin.AuditTools;

/// <summary>
/// Walks honua-server feature folders and extracts every HTTP endpoint plus
/// the gRPC service methods. Output is a deterministic, key-sorted inventory
/// suitable for diffing against a hand-edited coverage matrix.
/// </summary>
public static class EndpointInventoryGenerator
{
    private static readonly Regex MapGroupAssignment = new(
        @"var\s+(?<var>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<source>[A-Za-z_][A-Za-z0-9_]*)\s*\.MapGroup\(\s*""(?<route>[^""]+)""\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex MapVerbCall = new(
        @"(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*Map(?<verb>Get|Post|Put|Delete|Patch)\(\s*\$?""(?<route>[^""]*)""",
        RegexOptions.Compiled);

    private static readonly Regex MapMethodsCall = new(
        @"(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*MapMethods\(\s*\$?""(?<route>[^""]*)""\s*,\s*(?<methods>\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    // const-string route declarations like `private const string TaskRoute = "/foo";`
    private static readonly Regex ConstRouteDeclaration = new(
        @"(?:private|internal|public|protected)?\s*const\s+string\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""\s*;",
        RegexOptions.Compiled);

    private static readonly Regex HttpMethodToken = new(
        @"HttpMethods\.(?<method>Get|Post|Put|Delete|Patch|Head|Options)",
        RegexOptions.Compiled);

    private static readonly Regex GrpcOverrideMethod = new(
        @"public\s+override\s+(?:async\s+)?Task(?:<[^>]+>)?\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Generate the endpoint inventory from a honua-server source root.
    /// </summary>
    /// <param name="honuaServerRoot">The honua-server checkout root (must contain src/Honua.Server/Features).</param>
    /// <param name="commitSha">The honua-server commit SHA the inventory was derived from (informational; embedded in output).</param>
    public static EndpointInventory Generate(string honuaServerRoot, string commitSha)
    {
        var featuresRoot = Path.Combine(honuaServerRoot, "src", "Honua.Server", "Features");
        if (!Directory.Exists(featuresRoot))
        {
            throw new DirectoryNotFoundException(
                $"Honua-server features folder not found at '{featuresRoot}'. " +
                "Set the HONUA_SERVER_PATH environment variable or pass --honua-server-root.");
        }

        var endpoints = new List<EndpointEntry>();
        var endpointFiles = Directory
            .EnumerateFiles(featuresRoot, "*Endpoints.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToList();

        foreach (var file in endpointFiles)
        {
            endpoints.AddRange(ExtractFromHttpEndpointFile(file, featuresRoot));
        }

        var grpcServiceFile = Path.Combine(featuresRoot, "Grpc", "HonuaFeatureService.cs");
        if (File.Exists(grpcServiceFile))
        {
            endpoints.AddRange(ExtractFromGrpcServiceFile(grpcServiceFile, featuresRoot));
        }

        var sortedEndpoints = endpoints
            .OrderBy(e => e.Key, System.StringComparer.Ordinal)
            .ToList();

        return new EndpointInventory(commitSha, sortedEndpoints);
    }

    private static IEnumerable<EndpointEntry> ExtractFromHttpEndpointFile(string filePath, string featuresRoot)
    {
        var text = File.ReadAllText(filePath);
        var lines = text.Split('\n');

        var feature = GetFeatureName(filePath, featuresRoot);
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Inline const string declarations so route templates that reference them
        // (e.g. `MapGet($"{TaskRoute}/execute", ...)`) resolve correctly.
        var constants = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (Match m in ConstRouteDeclaration.Matches(text))
        {
            constants[m.Groups["name"].Value] = m.Groups["value"].Value;
        }

        // Resolve `MapGroup` aliases by chasing the receiver chain. We support
        // `var X = receiver.MapGroup("...")` where the receiver is `endpoints`,
        // `app`, or another previously captured group variable.
        var groups = new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            // Bare receivers commonly used in extension methods; both map to no-prefix.
            ["endpoints"] = string.Empty,
            ["app"] = string.Empty,
            ["builder"] = string.Empty,
        };

        foreach (Match m in MapGroupAssignment.Matches(text))
        {
            var variable = m.Groups["var"].Value;
            var source = m.Groups["source"].Value;
            var route = m.Groups["route"].Value;

            var prefix = groups.TryGetValue(source, out var parent) ? parent : string.Empty;
            groups[variable] = NormalizeRoute(prefix, route);
        }

        // `var x = endpoints.MapXxx(...)` style — capture the receiver too so that
        // the assigned variable participates in subsequent receiver lookups (e.g.
        // chained `.RequireAuthorization()` is irrelevant; we only need it for the
        // MapXxx call itself, which is already matched below).
        var seenKeys = new HashSet<string>(System.StringComparer.Ordinal);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            foreach (Match m in MapVerbCall.Matches(line))
            {
                var receiver = m.Groups["receiver"].Value;
                if (!groups.TryGetValue(receiver, out var prefix))
                {
                    continue;
                }

                var route = NormalizeRoute(prefix, ExpandConstantInterpolation(m.Groups["route"].Value, constants));
                var verb = m.Groups["verb"].Value.ToUpperInvariant();
                var entry = EndpointEntry.Http(
                    feature: feature,
                    file: fileName,
                    verb: verb,
                    route: route,
                    sourceFile: GetRelativePath(filePath, featuresRoot),
                    sourceLine: i + 1);
                if (seenKeys.Add(entry.Key))
                {
                    yield return entry;
                }
            }

            foreach (Match m in MapMethodsCall.Matches(line))
            {
                var receiver = m.Groups["receiver"].Value;
                if (!groups.TryGetValue(receiver, out var prefix))
                {
                    continue;
                }

                var route = NormalizeRoute(prefix, ExpandConstantInterpolation(m.Groups["route"].Value, constants));
                var methodsToken = m.Groups["methods"].Value;
                foreach (var method in ResolveMethods(methodsToken, text))
                {
                    var entry = EndpointEntry.Http(
                        feature: feature,
                        file: fileName,
                        verb: method,
                        route: route,
                        sourceFile: GetRelativePath(filePath, featuresRoot),
                        sourceLine: i + 1);
                    if (seenKeys.Add(entry.Key))
                    {
                        yield return entry;
                    }
                }
            }
        }
    }

    private static IEnumerable<EndpointEntry> ExtractFromGrpcServiceFile(string filePath, string featuresRoot)
    {
        var text = File.ReadAllText(filePath);
        var lines = text.Split('\n');
        var feature = GetFeatureName(filePath, featuresRoot);
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (Match m in GrpcOverrideMethod.Matches(line))
            {
                var name = m.Groups["name"].Value;
                yield return EndpointEntry.Grpc(
                    feature: feature,
                    file: fileName,
                    methodName: name,
                    sourceFile: GetRelativePath(filePath, featuresRoot),
                    sourceLine: i + 1);
            }
        }
    }

    private static IEnumerable<string> ResolveMethods(string methodsToken, string fullFileText)
    {
        var trimmed = methodsToken.Trim();
        if (trimmed.StartsWith('['))
        {
            return ExtractHttpMethods(trimmed);
        }

        // The token is a variable like `_nonGetMethods`. Find its declaration in the file.
        var declarationPattern = new Regex(
            @"\b" + Regex.Escape(trimmed) + @"\s*=\s*(?<arr>\[[^\]]+\]|new\s*\[\][^;]+;)",
            RegexOptions.Singleline);
        var match = declarationPattern.Match(fullFileText);
        if (!match.Success)
        {
            return System.Array.Empty<string>();
        }
        return ExtractHttpMethods(match.Groups["arr"].Value);
    }

    private static IEnumerable<string> ExtractHttpMethods(string token)
    {
        var matches = HttpMethodToken.Matches(token);
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (Match m in matches)
        {
            var verb = m.Groups["method"].Value.ToUpperInvariant();
            if (seen.Add(verb))
            {
                yield return verb;
            }
        }
    }

    private static string GetFeatureName(string filePath, string featuresRoot)
    {
        var relative = Path.GetRelativePath(featuresRoot, filePath).Replace('\\', '/');
        var firstSlash = relative.IndexOf('/');
        return firstSlash > 0 ? relative[..firstSlash] : "Unknown";
    }

    private static string GetRelativePath(string filePath, string featuresRoot)
    {
        return Path.GetRelativePath(featuresRoot, filePath).Replace('\\', '/');
    }

    private static string NormalizeRoute(string prefix, string route)
    {
        var combined = prefix.TrimEnd('/') + "/" + route.TrimStart('/');
        if (!combined.StartsWith('/'))
        {
            combined = "/" + combined;
        }
        return combined;
    }

    private static readonly Regex InterpolatedConstReference = new(
        @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}",
        RegexOptions.Compiled);

    private static string ExpandConstantInterpolation(string route, IReadOnlyDictionary<string, string> constants)
    {
        if (route.Length == 0 || constants.Count == 0)
        {
            return route;
        }
        // Substitute `{NAME}` with the constant's value when NAME is a declared
        // const string. Non-matching tokens (e.g. `{id}` route parameters) are
        // left intact.
        return InterpolatedConstReference.Replace(route, m =>
        {
            var name = m.Groups["name"].Value;
            return constants.TryGetValue(name, out var value) ? value : m.Value;
        });
    }
}
