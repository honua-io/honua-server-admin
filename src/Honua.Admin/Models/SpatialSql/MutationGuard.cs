using System;
using System.Text;

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
        "MERGE",
        // SELECT ... INTO target FROM source creates a new table and fills it
        // (https://www.postgresql.org/docs/current/sql-selectinto.html); the
        // bare INTO keyword catches that. INSERT INTO and MERGE INTO are
        // already flagged via INSERT/MERGE so the additional keyword does
        // not affect them.
        "INTO",
        // EXECUTE runs a previously prepared statement whose body is opaque
        // to the client, and is also the PL/pgSQL dynamic-SQL primitive.
        // EXPLAIN ANALYZE EXECUTE actually executes the prepared statement
        // (https://www.postgresql.org/docs/current/sql-explain.html), so
        // the client guard rejects it conservatively; the per-query
        // operator override remains available on the Run path.
        "EXECUTE"
    };

    /// <summary>
    /// True when the SQL appears to mutate data or schema. False for SELECT, WITH-only,
    /// EXPLAIN, and SHOW statements. Whitespace-only SQL is treated as non-mutating
    /// since the server will reject it on its own grounds. Also flags
    /// <c>SELECT ... INTO</c> (creates a table) and <c>EXECUTE</c> (opaque
    /// prepared-statement / dynamic-SQL execution) — both forms run as writes
    /// under <c>EXPLAIN ANALYZE</c>, so the conservative check applies on both
    /// the Run and EXPLAIN paths.
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

    /// <summary>
    /// Single-pass scanner that drops the contents of comments and string literals
    /// before keyword detection so the guard does not trip on benign mentions
    /// inside <c>'...'</c>, <c>"..."</c>, <c>--</c> / <c>/*...*/</c> comments, or
    /// PostgreSQL dollar-quoted string constants (<c>$$...$$</c> /
    /// <c>$tag$...$tag$</c>). A scanner is used over a stack of regex passes so
    /// that quote/comment regions are recognized in source order — otherwise a
    /// dollar-quoted body could leak its contents back into a later pass and a
    /// quoted-string body could swallow a real comment opener.
    /// </summary>
    internal static string StripCommentsAndLiterals(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];

            // Block comment: /* ... */
            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }
                i = Math.Min(i + 2, sql.Length);
                sb.Append(' ');
                continue;
            }

            // Line comment: -- ...
            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n' && sql[i] != '\r')
                {
                    i++;
                }
                sb.Append(' ');
                continue;
            }

            // Single-quoted string literal (with '' escape).
            if (c == '\'')
            {
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                sb.Append(' ');
                continue;
            }

            // Double-quoted identifier (with "" escape).
            if (c == '"')
            {
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == '"')
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == '"')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                sb.Append(' ');
                continue;
            }

            // PostgreSQL dollar-quoted string: $$...$$ or $tag$...$tag$.
            // The tag follows identifier rules (starts with letter or underscore,
            // then letter/underscore/digit). If the sequence after $ does not form
            // a valid opener (e.g. positional parameter $1), the $ is treated as a
            // literal character.
            if (c == '$' && TryConsumeDollarQuote(sql, ref i))
            {
                sb.Append(' ');
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static bool TryConsumeDollarQuote(string sql, ref int i)
    {
        // i points at the opening '$'. The opener is $tag$ where tag is empty or
        // starts with a letter/underscore followed by letter/underscore/digit.
        var tagEnd = i + 1;
        if (tagEnd < sql.Length && IsTagStartChar(sql[tagEnd]))
        {
            tagEnd++;
            while (tagEnd < sql.Length && IsTagChar(sql[tagEnd]))
            {
                tagEnd++;
            }
        }

        if (tagEnd >= sql.Length || sql[tagEnd] != '$')
        {
            // Not a dollar-quote opener (e.g. $1 positional parameter, or stray $).
            return false;
        }

        var tagLength = tagEnd - i + 1; // includes both surrounding $ chars
        var openerStart = i;
        var bodyStart = tagEnd + 1;
        var scan = bodyStart;
        while (scan + tagLength <= sql.Length)
        {
            if (sql[scan] == '$' &&
                string.CompareOrdinal(sql, scan, sql, openerStart, tagLength) == 0)
            {
                i = scan + tagLength;
                return true;
            }
            scan++;
        }

        // Unterminated dollar-quote — consume to end of string so trailing
        // keywords inside an unclosed body are not falsely flagged.
        i = sql.Length;
        return true;
    }

    private static bool IsTagStartChar(char c) => char.IsLetter(c) || c == '_';

    private static bool IsTagChar(char c) => char.IsLetterOrDigit(c) || c == '_';

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
