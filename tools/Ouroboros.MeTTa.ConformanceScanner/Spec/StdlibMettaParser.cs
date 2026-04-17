// <copyright file="StdlibMettaParser.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace Ouroboros.MeTTa.ConformanceScanner.Spec;

/// <summary>
/// Loads pinned stdlib.metta text, delegates parsing to <see cref="SExpressionParser"/>,
/// and groups parsed atoms by operation name.
/// </summary>
public sealed class StdlibMettaParser
{
    private readonly SExpressionParser parser = new();

    /// <summary>
    /// Parses stdlib.metta source into a <see cref="ParsedSpec"/>.
    /// </summary>
    public Result<ParsedSpec, string> Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return Result<ParsedSpec, string>.Failure("stdlib source was empty");
        }

        var sha256 = ComputeSha256(source);
        var operations = new Dictionary<string, OpAccumulator>(StringComparer.Ordinal);
        var totalForms = 0;
        var unparseable = 0;

        foreach (var rawLine in source.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            Result<Atom> parsed;
            try
            {
                parsed = this.parser.Parse(line);
            }
            catch (ParseException)
            {
                unparseable++;
                continue;
            }

            if (!parsed.IsSuccess)
            {
                unparseable++;
                continue;
            }

            totalForms++;
            Classify(parsed.Value, line, operations);
        }

        var schemas = operations.ToDictionary(
            static kv => kv.Key,
            kv => kv.Value.Freeze(kv.Key),
            StringComparer.Ordinal);

        return Result<ParsedSpec, string>.Success(
            new ParsedSpec(schemas, totalForms, unparseable, sha256));
    }

    private static void Classify(Atom atom, string rawLine, Dictionary<string, OpAccumulator> operations)
    {
        if (atom is not Expression expr || expr.Children.Count == 0)
        {
            return;
        }

        if (expr.Children[0] is not Symbol head)
        {
            return;
        }

        switch (head.Name)
        {
            case ":":
                ClassifySignature(expr, operations);
                break;
            case "=":
                ClassifyDefinition(expr, operations);
                break;
            case "@doc":
                ClassifyDoc(expr, rawLine, operations);
                break;
        }
    }

    private static void ClassifySignature(Expression expr, Dictionary<string, OpAccumulator> ops)
    {
        if (expr.Children.Count < 3)
        {
            return;
        }

        if (expr.Children[1] is not Symbol nameSym)
        {
            return;
        }

        var typeAtom = expr.Children[2];
        var typeText = typeAtom.ToString() ?? string.Empty;
        var arity = ComputeArity(typeAtom);
        var sig = new MeTtaSignature(nameSym.Name, typeText, arity);
        GetOrAdd(ops, nameSym.Name).Signatures.Add(sig);
    }

    private static void ClassifyDefinition(Expression expr, Dictionary<string, OpAccumulator> ops)
    {
        if (expr.Children.Count < 3)
        {
            return;
        }

        if (expr.Children[1] is not Expression head || head.Children.Count == 0)
        {
            return;
        }

        if (head.Children[0] is not Symbol nameSym)
        {
            return;
        }

        var body = expr.Children[2];
        var bodyText = body.ToString() ?? string.Empty;
        var argArity = head.Children.Count - 1;
        var def = new MeTtaDefinition(nameSym.Name, bodyText, argArity);
        GetOrAdd(ops, nameSym.Name).Definitions.Add(def);
    }

    private static void ClassifyDoc(Expression expr, string rawLine, Dictionary<string, OpAccumulator> ops)
    {
        if (expr.Children.Count < 2)
        {
            return;
        }

        if (expr.Children[1] is not Symbol nameSym)
        {
            return;
        }

        GetOrAdd(ops, nameSym.Name).RawForms.Add(rawLine);
    }

    private static int ComputeArity(Atom typeAtom)
    {
        if (typeAtom is not Expression arrowExpr || arrowExpr.Children.Count == 0)
        {
            return -1;
        }

        if (arrowExpr.Children[0] is not Symbol head || head.Name != "->")
        {
            return -1;
        }

        return Math.Max(0, arrowExpr.Children.Count - 2);
    }

    private static OpAccumulator GetOrAdd(Dictionary<string, OpAccumulator> ops, string name)
    {
        if (!ops.TryGetValue(name, out var acc))
        {
            acc = new OpAccumulator();
            ops[name] = acc;
        }

        return acc;
    }

    private static string ComputeSha256(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var digest = SHA256.HashData(bytes);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private sealed class OpAccumulator
    {
        public List<MeTtaSignature> Signatures { get; } = new();

        public List<MeTtaDefinition> Definitions { get; } = new();

        public List<string> RawForms { get; } = new();

        public SpecSchema Freeze(string name) =>
            new(name, this.Signatures, this.Definitions, this.RawForms);
    }
}
