// <copyright file="MeTTaLineParser.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;

namespace Ouroboros.MeTTa.Ethics;

/// <summary>
/// A line-oriented MeTTa S-expression parser used by the ethics loaders.
/// Tolerates blank lines, comments (lines starting with <c>;</c>), and
/// expressions that span multiple lines via balanced parentheses.
/// </summary>
/// <remarks>
/// This parser is intentionally minimal — its job is to lift parsed
/// expressions into <see cref="Atom"/> records so an <see cref="IAtomSpace"/>
/// can ingest them. Full MeTTa execution semantics are NOT implemented here.
/// </remarks>
public static class MeTTaLineParser
{
    /// <summary>
    /// Parses a stream of MeTTa source text into a list of top-level atoms.
    /// </summary>
    /// <param name="source">Raw MeTTa source. May contain comments and blank lines.</param>
    /// <returns>An immutable list of parsed atoms.</returns>
    public static IReadOnlyList<Atom> Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        List<Atom> result = new();

        // Strip line comments while preserving structure across multi-line expressions.
        string stripped = StripLineComments(source);

        int idx = 0;
        while (idx < stripped.Length)
        {
            // Skip whitespace.
            while (idx < stripped.Length && char.IsWhiteSpace(stripped[idx]))
            {
                idx++;
            }

            if (idx >= stripped.Length)
            {
                break;
            }

            if (stripped[idx] == '(')
            {
                int end = FindMatchingParen(stripped, idx);
                if (end < 0)
                {
                    // Unbalanced — bail out gracefully.
                    break;
                }

                string slice = stripped.Substring(idx, end - idx + 1);
                Atom? atom = ParseAtom(slice);
                if (atom is not null)
                {
                    result.Add(atom);
                }

                idx = end + 1;
            }
            else
            {
                // Bare token at top level (rare but possible).
                int tokenEnd = idx;
                while (tokenEnd < stripped.Length
                       && !char.IsWhiteSpace(stripped[tokenEnd])
                       && stripped[tokenEnd] != '('
                       && stripped[tokenEnd] != ')')
                {
                    tokenEnd++;
                }

                string token = stripped.Substring(idx, tokenEnd - idx);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    result.Add(MakeSymbolOrVariable(token));
                }

                idx = tokenEnd;
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a single S-expression slice into an atom. Slice MUST include the
    /// outermost parentheses (or be a bare token).
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <returns>The parsed atom, or <c>null</c> if invalid.</returns>
    public static Atom? ParseAtom(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        text = text.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (text[0] != '(' || text[^1] != ')')
        {
            return MakeSymbolOrVariable(text);
        }

        string inner = text[1..^1].Trim();
        List<string> tokens = TokenizeRespectingParens(inner);
        if (tokens.Count == 0)
        {
            return Atom.Expr(ImmutableList<Atom>.Empty);
        }

        ImmutableList<Atom>.Builder builder = ImmutableList.CreateBuilder<Atom>();
        foreach (string token in tokens)
        {
            Atom? child = token.StartsWith('(')
                ? ParseAtom(token)
                : MakeSymbolOrVariable(token);
            if (child is not null)
            {
                builder.Add(child);
            }
        }

        return Atom.Expr(builder.ToImmutable());
    }

    private static Atom MakeSymbolOrVariable(string token)
    {
        return token.StartsWith('$')
            ? Atom.Var(token[1..])
            : Atom.Sym(token);
    }

    private static string StripLineComments(string source)
    {
        StringBuilder sb = new(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            if (c == ';')
            {
                // Skip until end of line.
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    private static int FindMatchingParen(string s, int start)
    {
        int depth = 0;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static List<string> TokenizeRespectingParens(string input)
    {
        List<string> tokens = new();
        StringBuilder current = new();
        int depth = 0;

        foreach (char ch in input)
        {
            if (ch == '(')
            {
                depth++;
                current.Append(ch);
            }
            else if (ch == ')')
            {
                depth--;
                current.Append(ch);
            }
            else if (char.IsWhiteSpace(ch) && depth == 0)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
