using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Honua.Admin.Models.SpecWorkspace;

namespace Honua.Admin.Services.SpecWorkspace;

internal static class SpecSectionTextTranslator
{
    private static readonly string[] ComputeOps = ["aggregate", "filter", "join", "buffer"];

    public static IReadOnlyDictionary<SpecSectionId, string> Serialize(SpecDocument document)
    {
        var sections = new Dictionary<SpecSectionId, string>();
        foreach (var section in Enum.GetValues<SpecSectionId>())
        {
            sections[section] = SerializeSection(document, section);
        }

        return sections;
    }

    public static string SerializeSection(SpecDocument document, SpecSectionId section) => section switch
    {
        SpecSectionId.Sources => SerializeSources(document),
        SpecSectionId.Scope => SerializeScope(document),
        SpecSectionId.Parameters => SerializeParameters(document),
        SpecSectionId.Compute => SerializeCompute(document),
        SpecSectionId.Map => SerializeMap(document),
        SpecSectionId.Output => SerializeOutput(document),
        _ => string.Empty
    };

    public static SectionParseResult ParseSection(SpecDocument current, SpecSectionId section, string? text)
    {
        var normalized = Normalize(text);
        var diagnostics = new List<ValidationDiagnostic>();

        switch (section)
        {
            case SpecSectionId.Sources:
                return new SectionParseResult(current with { Sources = ParseSources(normalized, diagnostics) }, diagnostics);

            case SpecSectionId.Scope:
                return new SectionParseResult(current with { Scope = ParseScope(normalized, diagnostics) }, diagnostics);

            case SpecSectionId.Parameters:
                return new SectionParseResult(current with { Parameters = ParseParameters(normalized, diagnostics) }, diagnostics);

            case SpecSectionId.Compute:
                return new SectionParseResult(current with { Compute = ParseCompute(normalized, diagnostics) }, diagnostics);

            case SpecSectionId.Map:
                return new SectionParseResult(current with { Map = ParseMap(normalized, diagnostics) }, diagnostics);

            case SpecSectionId.Output:
                return new SectionParseResult(current with { Output = ParseOutput(normalized, diagnostics) }, diagnostics);

            default:
                return new SectionParseResult(current, diagnostics);
        }
    }

    private static IReadOnlyList<SpecSourceEntry> ParseSources(string text, List<ValidationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<SpecSourceEntry>();
        }

        var entries = new List<SpecSourceEntry>();
        foreach (var line in EnumerateLines(text))
        {
            var match = Regex.Match(line.Trim(), @"^@?(?<id>[\w-]+)(?:\s*=\s*@?(?<dataset>[\w-]+))?(?:\s+pin=(?<pin>\S+))?$");
            if (!match.Success)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Sources,
                    ValidationSeverity.Red,
                    "invalid-source",
                    $"Could not parse source line `{line}`.",
                    line.Trim()));
                continue;
            }

            var id = match.Groups["id"].Value;
            var dataset = match.Groups["dataset"].Success ? match.Groups["dataset"].Value : id;
            var pin = match.Groups["pin"].Success ? match.Groups["pin"].Value : null;
            entries.Add(new SpecSourceEntry(id, dataset, pin));
        }

        return entries;
    }

    private static SpecScope ParseScope(string text, List<ValidationDiagnostic> diagnostics)
    {
        var scope = new SpecScope();
        foreach (var line in EnumerateLines(text))
        {
            var trimmed = line.Trim();
            var separator = trimmed.Contains(':', StringComparison.Ordinal) ? ':' : '=';
            var parts = trimmed.Split(separator, 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Scope,
                    ValidationSeverity.Red,
                    "invalid-scope",
                    $"Could not parse scope line `{line}`.",
                    line.Trim()));
                continue;
            }

            var key = parts[0].ToLowerInvariant();
            var value = parts[1];
            switch (key)
            {
                case "crs":
                    scope = scope with { Crs = string.IsNullOrWhiteSpace(value) ? null : value };
                    break;

                case "bbox":
                    var numbers = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (numbers.Length != 4
                        || numbers.Any(n => !double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            SpecSectionId.Scope,
                            ValidationSeverity.Red,
                            "invalid-bbox",
                            $"bbox requires four comma-separated numbers: `{line}`.",
                            "bbox"));
                        break;
                    }

                    scope = scope with
                    {
                        Bbox = numbers
                            .Select(n => double.Parse(n, NumberStyles.Float, CultureInfo.InvariantCulture))
                            .ToArray()
                    };
                    break;

                default:
                    diagnostics.Add(new ValidationDiagnostic(
                        SpecSectionId.Scope,
                        ValidationSeverity.Yellow,
                        "unknown-scope-key",
                        $"Unknown scope field `{parts[0]}` is ignored.",
                        parts[0]));
                    break;
            }
        }

        return scope;
    }

    private static IReadOnlyList<SpecParameterEntry> ParseParameters(string text, List<ValidationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<SpecParameterEntry>();
        }

        var entries = new List<SpecParameterEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in EnumerateLines(text))
        {
            var trimmed = line.Trim();
            if (!TryParseParameterLine(trimmed, diagnostics, out var entry))
            {
                continue;
            }

            if (!Regex.IsMatch(entry.Name, @"^[a-zA-Z_][\w-]*$"))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Parameters,
                    ValidationSeverity.Red,
                    "invalid-parameter-name",
                    $"Parameter name `{entry.Name}` must start with a letter or underscore.",
                    entry.Name));
                continue;
            }

            if (!seen.Add(entry.Name))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Parameters,
                    ValidationSeverity.Red,
                    "duplicate-parameter",
                    $"Parameter `{entry.Name}` is defined more than once.",
                    entry.Name));
                continue;
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static bool TryParseParameterLine(
        string line,
        List<ValidationDiagnostic> diagnostics,
        out SpecParameterEntry entry)
    {
        entry = new SpecParameterEntry(string.Empty, "string");

        if (Regex.IsMatch(line, @"^\$?[a-zA-Z_][\w-]*\s*:")
            && TryParseColonParameterLine(line, diagnostics, out entry))
        {
            return true;
        }

        var tokens = TokenizeComputeLine(line);
        if (tokens.Count == 0)
        {
            return false;
        }

        var name = tokens[0].TrimStart('$');
        string? type = null;
        string? defaultValue = null;
        var required = false;
        foreach (var token in tokens.Skip(1))
        {
            var equalsIndex = token.IndexOf('=');
            if (equalsIndex < 0)
            {
                if (type is null)
                {
                    type = token;
                    continue;
                }

                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Parameters,
                    ValidationSeverity.Red,
                    "invalid-parameter-token",
                    $"Expected key=value token in `{line}`.",
                    token));
                continue;
            }

            var key = token[..equalsIndex];
            var value = token[(equalsIndex + 1)..];
            switch (key.ToLowerInvariant())
            {
                case "type":
                    type = value;
                    break;

                case "default":
                    defaultValue = value;
                    break;

                case "required":
                    if (bool.TryParse(value, out var parsed))
                    {
                        required = parsed;
                    }
                    else
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            SpecSectionId.Parameters,
                            ValidationSeverity.Red,
                            "invalid-parameter-required",
                            $"Parameter `{name}` has invalid required flag `{value}`.",
                            value));
                    }
                    break;

                default:
                    diagnostics.Add(new ValidationDiagnostic(
                        SpecSectionId.Parameters,
                        ValidationSeverity.Yellow,
                        "unknown-parameter-key",
                        $"Unknown parameter field `{key}` is ignored.",
                        key));
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(type))
        {
            diagnostics.Add(new ValidationDiagnostic(
                SpecSectionId.Parameters,
                ValidationSeverity.Red,
                "missing-parameter-type",
                $"Parameter `{name}` is missing type=.",
                name));
            type = "string";
        }

        entry = new SpecParameterEntry(name, NormalizeParameterType(type), defaultValue, required);
        return true;
    }

    private static bool TryParseColonParameterLine(
        string line,
        List<ValidationDiagnostic> diagnostics,
        out SpecParameterEntry entry)
    {
        entry = new SpecParameterEntry(string.Empty, "string");
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0)
        {
            return false;
        }

        var name = line[..colonIndex].Trim().TrimStart('$');
        var rest = line[(colonIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(rest))
        {
            diagnostics.Add(new ValidationDiagnostic(
                SpecSectionId.Parameters,
                ValidationSeverity.Red,
                "invalid-parameter",
                $"Could not parse parameter line `{line}`.",
                line));
            return false;
        }

        var equalsIndex = rest.IndexOf('=');
        var type = equalsIndex >= 0 ? rest[..equalsIndex].Trim() : rest;
        var defaultValue = equalsIndex >= 0 ? UnquoteParameterValue(rest[(equalsIndex + 1)..].Trim()) : null;
        entry = new SpecParameterEntry(name, NormalizeParameterType(type), defaultValue);
        return true;
    }

    private static IReadOnlyList<SpecComputeStep> ParseCompute(string text, List<ValidationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<SpecComputeStep>();
        }

        var steps = new List<SpecComputeStep>();
        foreach (var line in EnumerateLines(text))
        {
            var trimmed = line.Trim();
            var tokens = TokenizeComputeLine(trimmed);
            if (tokens.Count == 0)
            {
                continue;
            }

            var op = tokens[0];
            if (!ComputeOps.Contains(op, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Compute,
                    ValidationSeverity.Red,
                    "unknown-op",
                    $"Op `{op}` is not in the S1 grammar.",
                    op));
            }

            var inputs = Array.Empty<string>();
            var args = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in tokens.Skip(1))
            {
                var equalsIndex = token.IndexOf('=');
                if (equalsIndex < 0)
                {
                    diagnostics.Add(new ValidationDiagnostic(
                        SpecSectionId.Compute,
                        ValidationSeverity.Red,
                        "invalid-compute-token",
                        $"Expected key=value token in `{line}`.",
                        token));
                    continue;
                }

                var key = token[..equalsIndex];
                var value = token[(equalsIndex + 1)..];
                if (key.Equals("inputs", StringComparison.OrdinalIgnoreCase))
                {
                    inputs = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.TrimStart('@'))
                        .ToArray();
                    continue;
                }

                args[key] = value;
            }

            steps.Add(new SpecComputeStep(op, inputs, args));
        }

        return steps;
    }

    private static IReadOnlyList<string> TokenizeComputeLine(string line)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(line))
        {
            return tokens;
        }

        var builder = new StringBuilder();
        var inQuotes = false;
        var escape = false;
        foreach (var ch in line)
        {
            if (escape)
            {
                builder.Append(ch);
                escape = false;
                continue;
            }

            if (inQuotes && ch == '\\')
            {
                escape = true;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (builder.Length > 0)
                {
                    tokens.Add(builder.ToString());
                    builder.Clear();
                }
                continue;
            }

            builder.Append(ch);
        }

        if (builder.Length > 0)
        {
            tokens.Add(builder.ToString());
        }

        return tokens;
    }

    private static SpecMap ParseMap(string text, List<ValidationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SpecMap();
        }

        var layers = new List<SpecMapLayer>();
        foreach (var line in EnumerateLines(text))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("layer", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Map,
                    ValidationSeverity.Red,
                    "invalid-map-line",
                    $"Map lines must begin with `layer`: `{line}`.",
                    trimmed));
                continue;
            }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var token in trimmed.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                var parts = token.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    continue;
                }

                if (!fields.TryAdd(parts[0], parts[1]))
                {
                    // Duplicate fields used to throw from ToDictionary and crash the
                    // editor; emit a red diagnostic instead and keep the first value so
                    // downstream parsing can still surface other issues.
                    diagnostics.Add(new ValidationDiagnostic(
                        SpecSectionId.Map,
                        ValidationSeverity.Red,
                        "duplicate-map-field",
                        $"Map layer field `{parts[0]}` is specified more than once in `{line}`.",
                        parts[0]));
                }
            }

            if (!fields.TryGetValue("source", out var source))
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Map,
                    ValidationSeverity.Red,
                    "missing-required-param",
                    $"Map layer `{line}` is missing `source=`.",
                    "source"));
                continue;
            }

            layers.Add(new SpecMapLayer(source.TrimStart('@'), fields.GetValueOrDefault("symbology") ?? "viridis"));
        }

        return new SpecMap { Layers = layers };
    }

    private static SpecOutput ParseOutput(string text, List<ValidationDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SpecOutput();
        }

        SpecOutputKind kind = SpecOutputKind.None;
        string? target = null;
        foreach (var line in EnumerateLines(text))
        {
            var trimmed = line.Trim();
            var separator = trimmed.Contains(':', StringComparison.Ordinal) ? ':' : '=';
            var parts = trimmed.Split(separator, 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                diagnostics.Add(new ValidationDiagnostic(
                    SpecSectionId.Output,
                    ValidationSeverity.Red,
                    "invalid-output",
                    $"Could not parse output line `{line}`.",
                    trimmed));
                continue;
            }

            switch (parts[0].ToLowerInvariant())
            {
                case "kind":
                    if (!Enum.TryParse<SpecOutputKind>(parts[1], true, out kind))
                    {
                        diagnostics.Add(new ValidationDiagnostic(
                            SpecSectionId.Output,
                            ValidationSeverity.Red,
                            "unknown-output-kind",
                            $"Unknown output kind `{parts[1]}`.",
                            parts[1]));
                        kind = SpecOutputKind.None;
                    }
                    break;

                case "target":
                    target = parts[1];
                    break;

                default:
                    diagnostics.Add(new ValidationDiagnostic(
                        SpecSectionId.Output,
                        ValidationSeverity.Yellow,
                        "unknown-output-key",
                        $"Unknown output field `{parts[0]}` is ignored.",
                        parts[0]));
                    break;
            }
        }

        return new SpecOutput { Kind = kind, Target = target };
    }

    private static string SerializeSources(SpecDocument document)
    {
        if (document.Sources.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var source in document.Sources)
        {
            builder.Append('@').Append(source.Id).Append(" = ").Append(source.Dataset);
            if (!string.IsNullOrWhiteSpace(source.Pin))
            {
                builder.Append(" pin=").Append(source.Pin);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string SerializeScope(SpecDocument document)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(document.Scope.Crs))
        {
            lines.Add($"crs: {document.Scope.Crs}");
        }

        if (document.Scope.Bbox is { Length: 4 } bbox)
        {
            lines.Add("bbox: " + string.Join(",", bbox.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture))));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string SerializeParameters(SpecDocument document)
    {
        if (document.Parameters.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            document.Parameters.Select(parameter =>
            {
                var tokens = new List<string> { $"${parameter.Name}", $"type={parameter.Type}" };
                if (!string.IsNullOrWhiteSpace(parameter.Default))
                {
                    tokens.Add($"default={QuoteComputeValue(parameter.Default)}");
                }

                if (parameter.Required)
                {
                    tokens.Add("required=true");
                }

                return string.Join(" ", tokens);
            }));
    }

    private static string SerializeCompute(SpecDocument document)
    {
        if (document.Compute.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            document.Compute.Select(step =>
            {
                var tokens = new List<string> { step.Op };
                if (step.Inputs.Count > 0)
                {
                    tokens.Add("inputs=" + string.Join(",", step.Inputs.Select(i => $"@{i}")));
                }

                tokens.AddRange(step.Args.Select(kv => $"{kv.Key}={QuoteComputeValue(kv.Value)}"));
                return string.Join(" ", tokens);
            }));
    }

    private static string QuoteComputeValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var needsQuoting = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch) || ch == '"' || ch == '\\')
            {
                needsQuoting = true;
                break;
            }
        }

        if (!needsQuoting)
        {
            return value;
        }

        var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string SerializeMap(SpecDocument document)
    {
        if (document.Map.Layers.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            document.Map.Layers.Select(layer => $"layer source=@{layer.Source} symbology={layer.Symbology}"));
    }

    private static string SerializeOutput(SpecDocument document)
    {
        if (document.Output.Kind == SpecOutputKind.None && string.IsNullOrWhiteSpace(document.Output.Target))
        {
            return string.Empty;
        }

        var lines = new List<string> { $"kind: {document.Output.Kind}" };
        if (!string.IsNullOrWhiteSpace(document.Output.Target))
        {
            lines.Add($"target: {document.Output.Target}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> EnumerateLines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'));

    private static string NormalizeParameterType(string value) => value.Trim().ToLowerInvariant();

    private static string UnquoteParameterValue(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1]
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        return value;
    }

    private static string Normalize(string? text) => text?.Replace("\r\n", "\n", StringComparison.Ordinal) ?? string.Empty;
}

internal sealed record SectionParseResult(SpecDocument Document, IReadOnlyList<ValidationDiagnostic> Diagnostics);
