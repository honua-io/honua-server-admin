using System;
using System.Linq;
using Honua.Admin.Models.SpecWorkspace;

namespace Honua.Admin.Services.SpecWorkspace;

public sealed record SpecCompletionRequest(ResolveQuery Query, int ReplaceStart, int ReplaceEnd);

public static class SpecCompletionQueryBuilder
{
    public static SpecCompletionRequest? Build(string? text, int cursor, SpecSectionId section, string principalId)
    {
        text ??= string.Empty;
        if (cursor < 0 || cursor > text.Length)
        {
            return null;
        }

        var atMention = BuildAtMentionRequest(text, cursor, principalId);
        if (atMention is not null)
        {
            return atMention;
        }

        if (section == SpecSectionId.Map)
        {
            var symbology = BuildSymbologyRequest(text, cursor, principalId);
            if (symbology is not null)
            {
                return symbology;
            }
        }

        return section == SpecSectionId.Compute
            ? BuildParamListRequest(text, cursor, principalId)
            : null;
    }

    public static string GetInsertionText(CatalogCandidate candidate, SpecCompletionRequest? completion) =>
        completion?.Query.Trigger switch
        {
            CatalogTrigger.AtMention when candidate.Kind == CatalogCandidateKind.Dataset => $"@{candidate.Id}",
            CatalogTrigger.DotMember when candidate.Kind == CatalogCandidateKind.Column => $"@{candidate.Parent}.{candidate.Label}",
            CatalogTrigger.ParamList => $"{candidate.Label}=",
            CatalogTrigger.SymbologyRamp => candidate.Label,
            _ => candidate.Kind switch
            {
                CatalogCandidateKind.Dataset => $"@{candidate.Id}",
                CatalogCandidateKind.Column => $"@{candidate.Parent}.{candidate.Label}",
                _ => candidate.Label
            }
        };

    private static SpecCompletionRequest? BuildAtMentionRequest(string text, int cursor, string principalId)
    {
        var prefixText = text[..cursor];
        var atIndex = prefixText.LastIndexOf('@');
        if (atIndex < 0)
        {
            return null;
        }

        var tail = prefixText[atIndex..];
        if (tail.Any(ch => char.IsWhiteSpace(ch) || ch == ',' || ch == ')' || ch == '(' || ch == ':'))
        {
            return null;
        }

        var raw = prefixText[(atIndex + 1)..];
        if (raw.Length == 0)
        {
            return new SpecCompletionRequest(
                new ResolveQuery
                {
                    Trigger = CatalogTrigger.AtMention,
                    Prefix = string.Empty,
                    PrincipalId = principalId
                },
                atIndex,
                cursor);
        }

        var dotIndex = raw.IndexOf('.');
        if (dotIndex >= 0)
        {
            var parent = raw[..dotIndex];
            var memberPrefix = raw[(dotIndex + 1)..];
            if (parent.Length == 0)
            {
                return null;
            }

            return new SpecCompletionRequest(
                new ResolveQuery
                {
                    Trigger = CatalogTrigger.DotMember,
                    Parent = parent,
                    Prefix = memberPrefix,
                    PrincipalId = principalId
                },
                atIndex,
                cursor);
        }

        return new SpecCompletionRequest(
            new ResolveQuery
            {
                Trigger = CatalogTrigger.AtMention,
                Prefix = raw,
                PrincipalId = principalId
            },
            atIndex,
            cursor);
    }

    private static SpecCompletionRequest? BuildSymbologyRequest(string text, int cursor, string principalId)
    {
        var prefixText = text[..cursor];
        var lineStart = LastLineStart(prefixText);
        var linePrefix = prefixText[lineStart..];

        var valueStart = LastTokenValueStart(linePrefix, "symbology=");
        valueStart ??= LastCallArgumentStart(linePrefix, "symbology(");
        if (valueStart is not { } localStart)
        {
            return null;
        }

        var replaceStart = lineStart + localStart;
        var prefix = text[replaceStart..cursor];
        if (!IsCompletionWord(prefix))
        {
            return null;
        }

        return new SpecCompletionRequest(
            new ResolveQuery
            {
                Trigger = CatalogTrigger.SymbologyRamp,
                Prefix = prefix,
                PrincipalId = principalId
            },
            replaceStart,
            cursor);
    }

    private static SpecCompletionRequest? BuildParamListRequest(string text, int cursor, string principalId)
    {
        var prefixText = text[..cursor];
        var lineStart = LastLineStart(prefixText);
        var linePrefix = prefixText[lineStart..];
        var tokenStart = LastTokenStart(linePrefix);
        var prefix = linePrefix[tokenStart..];
        if (!HasPriorToken(linePrefix, tokenStart)
            || prefix.Contains('=', StringComparison.Ordinal)
            || prefix.Contains('@', StringComparison.Ordinal)
            || prefix.Contains('.', StringComparison.Ordinal)
            || !IsIdentifierPrefix(prefix))
        {
            return null;
        }

        var replaceStart = lineStart + tokenStart;
        return new SpecCompletionRequest(
            new ResolveQuery
            {
                Trigger = CatalogTrigger.ParamList,
                Prefix = prefix,
                PrincipalId = principalId
            },
            replaceStart,
            cursor);
    }

    private static int LastLineStart(string text)
    {
        var newline = text.LastIndexOfAny(['\n', '\r']);
        return newline < 0 ? 0 : newline + 1;
    }

    private static int LastTokenStart(string linePrefix)
    {
        var index = linePrefix.Length - 1;
        while (index >= 0 && !char.IsWhiteSpace(linePrefix[index]) && linePrefix[index] is not ',' and not '(' and not ')')
        {
            index--;
        }

        return index + 1;
    }

    private static int? LastTokenValueStart(string linePrefix, string marker)
    {
        var markerIndex = linePrefix.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var valueStart = markerIndex + marker.Length;
        var valuePrefix = linePrefix[valueStart..];
        return valuePrefix.Any(ch => char.IsWhiteSpace(ch) || ch == ',' || ch == ')' || ch == '(')
            ? null
            : valueStart;
    }

    private static int? LastCallArgumentStart(string linePrefix, string marker)
    {
        var markerIndex = linePrefix.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var valueStart = markerIndex + marker.Length;
        var valuePrefix = linePrefix[valueStart..];
        if (valuePrefix.Contains(')', StringComparison.Ordinal))
        {
            return null;
        }

        var commaIndex = valuePrefix.LastIndexOf(',');
        if (commaIndex >= 0)
        {
            valueStart += commaIndex + 1;
        }

        while (valueStart < linePrefix.Length && char.IsWhiteSpace(linePrefix[valueStart]))
        {
            valueStart++;
        }

        return valueStart;
    }

    private static bool HasPriorToken(string linePrefix, int tokenStart) =>
        linePrefix[..tokenStart].Any(ch => !char.IsWhiteSpace(ch) && ch is not ',' and not '(' and not ')');

    private static bool IsCompletionWord(string value) =>
        value.All(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-');

    private static bool IsIdentifierPrefix(string value) =>
        value.All(ch => char.IsLetter(ch) || ch == '_');
}
