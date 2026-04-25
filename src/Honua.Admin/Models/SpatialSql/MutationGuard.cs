using System;
using System.Text.RegularExpressions;

namespace Honua.Admin.Models.SpatialSql;

/// <summary>
/// Best-effort client-side detection of mutating SQL verbs. The server is the
/// source of truth — this is purely UX so the operator gets an immediate prompt
/// rather than a round-trip rejection. Comments and string literals are stripped
/// before keyword scanning so the guard does not trip on benign content.
/// </summary>
public static class MutationGuard
{
    private static readonly string[] MutatingKeywords = new[]
    {
        "INSERT",
        "UPDATE",
        "DELETE",
        "TRUNCATE",
        "DROP",
        "CREATE",
        "ALTER",
        "GRANT",
        "REVOKE",
        "COPY",
        "VACUUM",
        "REINDEX",
        "CLUSTER",
        "COMMENT",
        "MERGE"
    };

    private static readonly Regex BlockComment = new(@"/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex LineComment = new(@"--[^\r\n]*", RegexOptions.Compiled);
    private static readonly Regex SingleQuoted = new(@"'(?:''|[^'])*'", RegexOptions.Compiled);
    private static readonly Regex DoubleQuoted = new(@"""(?:""""|[^""])*""", RegexOptions.Compiled);

    /// <summary>
    /// True when the SQL appears to mutate data or schema. False for SELECT, WITH-only,
    /// EXPLAIN, and SHOW statements. Whitespace-only SQL is treated as non-mutating
    /// since the server will reject it on its own grounds.
    /// </summary>
    public static bool IsMutating(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var stripped = StripCommentsAndLiterals(sql);
        foreach (var keyword in MutatingKeywords)
        {
            if (HasKeyword(stripped, keyword))
            {
                return true;
            }
        }

        return false;
    }

    internal static string StripCommentsAndLiterals(string sql)
    {
        var pass1 = BlockComment.Replace(sql, " ");
        var pass2 = LineComment.Replace(pass1, " ");
        var pass3 = SingleQuoted.Replace(pass2, " ");
        return DoubleQuoted.Replace(pass3, " ");
    }

    private static bool HasKeyword(string sql, string keyword)
    {
        var index = 0;
        while (index < sql.Length)
        {
            var hit = sql.IndexOf(keyword, index, StringComparison.OrdinalIgnoreCase);
            if (hit < 0)
            {
                return false;
            }

            var leftOk = hit == 0 || !IsWordChar(sql[hit - 1]);
            var rightOk = hit + keyword.Length >= sql.Length || !IsWordChar(sql[hit + keyword.Length]);
            if (leftOk && rightOk)
            {
                return true;
            }

            index = hit + 1;
        }
        return false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
