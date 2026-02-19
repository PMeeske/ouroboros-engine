// <copyright file="CommonsenseKnowledgeBase.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1101 // Prefix local calls with this

namespace Ouroboros.Pipeline.Reasoning;

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Loads the curated Commonsense Knowledge Base (CommonsenseKB.metta) into
/// a Hyperon AtomSpace and exposes typed query helpers for symbolic commonsense
/// reasoning.
///
/// The KB covers:
///   - Spatial relations (Above, Below, Inside, Near …)
///   - Temporal / causal relations (Before, After, Causes, Enables …)
///   - Physical properties of objects (Solid, Liquid, Edible, Fragile …)
///   - Agent affordances (CanLift, CanOpen, CanEat …)
///   - Natural categories / taxonomies (Animal, Vehicle, Tool, Food …)
///   - Inference rules that derive new facts from existing ones
/// </summary>
public sealed class CommonsenseKnowledgeBase : IDisposable
{
    // Path to the bundled MeTTa schema, resolved at runtime.
    private const string SchemaFileName = "CommonsenseKB.metta";
    private const string SchemaRelativePath = "MeTTa/Schemas/" + SchemaFileName;

    private readonly HyperonReasoningStep _step;
    private bool _loaded;
    private bool _disposed;

    /// <summary>
    /// Initialises a new CommonsenseKnowledgeBase backed by a fresh reasoning step.
    /// </summary>
    public CommonsenseKnowledgeBase()
        : this(new HyperonReasoningStep("commonsense-kb"))
    {
    }

    /// <summary>
    /// Initialises a CommonsenseKnowledgeBase that shares an existing
    /// <see cref="HyperonMeTTaEngine"/> so its facts are accessible from other
    /// reasoning steps in the same pipeline.
    /// </summary>
    /// <param name="engine">Shared engine / AtomSpace to load facts into.</param>
    public CommonsenseKnowledgeBase(HyperonMeTTaEngine engine)
        : this(new HyperonReasoningStep("commonsense-kb", engine))
    {
    }

    private CommonsenseKnowledgeBase(HyperonReasoningStep step)
    {
        _step = step;
    }

    /// <summary>Gets whether the KB has been loaded into AtomSpace.</summary>
    public bool IsLoaded => _loaded;

    /// <summary>Gets the underlying engine for advanced usage.</summary>
    public HyperonMeTTaEngine Engine => _step.Engine;

    // ─────────────────────────────────────────────────────────────────────────
    // Loading
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads CommonsenseKB.metta into the AtomSpace.  Idempotent — safe to
    /// call multiple times; subsequent calls are no-ops.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (_loaded) return;

        var facts = await ReadSchemaFacts(ct);
        await _step.LoadContextAsync("commonsense-kb", facts, ct);
        _loaded = true;
    }

    /// <summary>
    /// Loads a supplementary set of domain-specific facts on top of the base KB.
    /// </summary>
    /// <param name="domainName">Logical name for this fact-set (used for tracing).</param>
    /// <param name="facts">MeTTa fact strings, e.g. <c>"(HasProperty (Entity "robot") Solid)"</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task LoadDomainFactsAsync(
        string domainName,
        IEnumerable<string> facts,
        CancellationToken ct = default)
    {
        if (!_loaded)
            await LoadAsync(ct);

        await _step.LoadContextAsync(domainName, facts, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Spatial queries
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Queries all objects that are directly <em>above</em> <paramref name="reference"/>.</summary>
    public Task<IReadOnlyList<string>> GetObjectsAboveAsync(string reference, CancellationToken ct = default)
        => QueryEntities($"(match &self (Above $obj (Entity \"{reference}\")) $obj)", ct);

    /// <summary>Queries all objects that are <em>inside</em> <paramref name="container"/>.</summary>
    public Task<IReadOnlyList<string>> GetObjectsInsideAsync(string container, CancellationToken ct = default)
        => QueryEntities($"(match &self (Inside $obj (Entity \"{container}\")) $obj)", ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Causal queries
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all events / states that <paramref name="cause"/> causes.</summary>
    public Task<IReadOnlyList<string>> GetEffectsOfAsync(string cause, CancellationToken ct = default)
        => QueryEntities($"(match &self (Causes (Event \"{cause}\") $effect) $effect)", ct);

    /// <summary>Returns all events / states that cause <paramref name="effect"/>.</summary>
    public Task<IReadOnlyList<string>> GetCausesOfAsync(string effect, CancellationToken ct = default)
        => QueryEntities($"(match &self (Causes $cause (Event \"{effect}\")) $cause)", ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Property queries
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all physical properties of <paramref name="entityName"/>.
    /// </summary>
    public Task<IReadOnlyList<string>> GetPropertiesOfAsync(string entityName, CancellationToken ct = default)
        => QueryEntities($"(match &self (HasProperty (Entity \"{entityName}\") $prop) $prop)", ct);

    /// <summary>
    /// Returns all entities that have <paramref name="property"/>.
    /// </summary>
    public Task<IReadOnlyList<string>> GetEntitiesWithPropertyAsync(string property, CancellationToken ct = default)
        => QueryEntities($"(match &self (HasProperty $obj {property}) $obj)", ct);

    /// <summary>
    /// Checks whether <paramref name="entityName"/> has the given <paramref name="property"/>.
    /// </summary>
    public async Task<bool> HasPropertyAsync(string entityName, string property, CancellationToken ct = default)
    {
        var results = await _step.InferAsync(
            $"(match &self (HasProperty (Entity \"{entityName}\") {property}) True)",
            ct);
        return results.Any(r => r.Contains("True"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Affordance queries
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all affordances (things an agent can do) that
    /// <paramref name="agentName"/> has with <paramref name="objectName"/>.
    /// </summary>
    public Task<IReadOnlyList<string>> GetAffordancesAsync(
        string agentName,
        string objectName,
        CancellationToken ct = default)
        => QueryEntities(
            $"(match &self (AgentCanDo (Entity \"{agentName}\") (Entity \"{objectName}\") $aff) $aff)",
            ct);

    /// <summary>
    /// Checks whether <paramref name="agentName"/> can perform <paramref name="affordance"/>
    /// on <paramref name="objectName"/>.
    /// </summary>
    public async Task<bool> CanAgentDoAsync(
        string agentName,
        string objectName,
        string affordance,
        CancellationToken ct = default)
    {
        var results = await _step.InferAsync(
            $"(match &self (AgentCanDo (Entity \"{agentName}\") (Entity \"{objectName}\") {affordance}) True)",
            ct);
        return results.Any(r => r.Contains("True"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Category queries
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns all categories that <paramref name="entityName"/> belongs to.</summary>
    public Task<IReadOnlyList<string>> GetCategoriesOfAsync(string entityName, CancellationToken ct = default)
        => QueryEntities($"(match &self (IsA (Entity \"{entityName}\") $cat) $cat)", ct);

    /// <summary>Returns all entities in the given <paramref name="category"/>.</summary>
    public Task<IReadOnlyList<string>> GetEntitiesInCategoryAsync(string category, CancellationToken ct = default)
        => QueryEntities($"(match &self (IsA $obj {category}) $obj)", ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Free-form inference
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a raw MeTTa query against the commonsense knowledge base and
    /// returns the matched bindings as strings.
    /// </summary>
    /// <param name="mettaQuery">
    ///   A well-formed MeTTa match expression, e.g.
    ///   <c>"(match &amp;self (IsA $x Animal) $x)"</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of result atoms as strings.</returns>
    public Task<IReadOnlyList<string>> QueryAsync(string mettaQuery, CancellationToken ct = default)
        => _step.InferAsync(mettaQuery, ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<string>> QueryEntities(string query, CancellationToken ct)
    {
        if (!_loaded)
            await LoadAsync(ct);

        return await _step.InferAsync(query, ct);
    }

    /// <summary>
    /// Resolves and reads CommonsenseKB.metta, returning each non-comment,
    /// non-blank line as an individual fact string.
    /// </summary>
    private static async Task<IReadOnlyList<string>> ReadSchemaFacts(CancellationToken ct)
    {
        string? schemaPath = ResolveSchemaPath();

        if (schemaPath != null)
        {
            var lines = await File.ReadAllLinesAsync(schemaPath, ct);
            return ParseMeTTaFacts(lines);
        }

        // Fallback: return the minimal set of bootstrap facts embedded as a
        // constant so the class is functional even if the .metta file is not
        // present on disk (e.g., unit-test environments without content files).
        return GetEmbeddedBootstrapFacts();
    }

    private static string? ResolveSchemaPath()
    {
        // 1. Relative to the executing assembly (deployed content files)
        var assemblyDir = Path.GetDirectoryName(typeof(CommonsenseKnowledgeBase).Assembly.Location) ?? ".";
        var candidate1 = Path.Combine(assemblyDir, SchemaRelativePath);
        if (File.Exists(candidate1)) return candidate1;

        // 2. Relative to the current working directory (development / test runs)
        var candidate2 = Path.Combine(Directory.GetCurrentDirectory(), SchemaRelativePath);
        if (File.Exists(candidate2)) return candidate2;

        // 3. Walk up the directory tree looking for the schema (monorepo dev layout)
        var dir = new DirectoryInfo(assemblyDir);
        while (dir != null)
        {
            var candidate3 = Path.Combine(dir.FullName, "src", "Ouroboros.Pipeline", SchemaRelativePath);
            if (File.Exists(candidate3)) return candidate3;
            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Parses a MeTTa source file into individual top-level expression strings.
    /// Each continuous block of non-comment, non-blank lines that forms a
    /// single S-expression is returned as one fact.
    /// </summary>
    private static IReadOnlyList<string> ParseMeTTaFacts(IEnumerable<string> lines)
    {
        var facts = new List<string>();
        var current = new System.Text.StringBuilder();
        int depth = 0;

        foreach (var raw in lines)
        {
            // Strip inline and full-line comments
            int commentIdx = raw.IndexOf(';');
            var line = commentIdx >= 0 ? raw[..commentIdx] : raw;
            line = line.Trim();

            if (string.IsNullOrEmpty(line)) continue;

            // Accumulate characters tracking parenthesis depth
            foreach (char ch in line)
            {
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                current.Append(ch);
            }

            current.Append(' ');

            // A complete top-level expression ends when depth returns to 0
            if (depth == 0 && current.Length > 1)
            {
                var fact = current.ToString().Trim();
                if (fact.Length > 0)
                    facts.Add(fact);
                current.Clear();
            }
        }

        return facts;
    }

    /// <summary>
    /// Minimal embedded facts used as a fallback when the .metta file cannot
    /// be located on disk.  Covers the most commonly queried assertions.
    /// </summary>
    private static IReadOnlyList<string> GetEmbeddedBootstrapFacts() =>
    [
        // Properties
        "(= (HasProperty (Entity \"water\")  Liquid)  True)",
        "(= (HasProperty (Entity \"ice\")    Solid)   True)",
        "(= (HasProperty (Entity \"stone\")  Heavy)   True)",
        "(= (HasProperty (Entity \"glass\")  Fragile) True)",
        "(= (HasProperty (Entity \"bread\")  Edible)  True)",
        "(= (HasProperty (Entity \"apple\")  Edible)  True)",
        "(= (HasProperty (Entity \"knife\")  Sharp)   True)",
        // Categories
        "(= (IsA (Entity \"dog\")   Animal)    True)",
        "(= (IsA (Entity \"cat\")   Animal)    True)",
        "(= (IsA (Entity \"car\")   Vehicle)   True)",
        "(= (IsA (Entity \"bread\") Food)      True)",
        "(= (IsA (Entity \"chair\") Furniture) True)",
        // Affordances
        "(= (AgentCanDo (Entity \"human\") (Entity \"chair\") CanSit)  True)",
        "(= (AgentCanDo (Entity \"human\") (Entity \"bread\") CanEat)  True)",
        "(= (AgentCanDo (Entity \"human\") (Entity \"water\") CanDrink) True)",
        "(= (AgentCanDo (Entity \"human\") (Entity \"door\")  CanOpen) True)",
        "(= (AgentCanDo (Entity \"human\") (Entity \"knife\") CanCut)  True)",
        // Causality
        "(= (Causes (Event \"heating\")  (Event \"melting\"))    True)",
        "(= (Causes (Event \"freezing\") (Event \"solidifying\")) True)",
        "(= (Causes (Event \"eating\")   (Event \"satiation\"))  True)",
    ];

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _step.Dispose();
    }
}
