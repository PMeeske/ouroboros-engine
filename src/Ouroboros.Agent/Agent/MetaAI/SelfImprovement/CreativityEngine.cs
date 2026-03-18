// ==========================================================
// Creativity Engine
// Fauconnier & Turner conceptual blending, Koestler bisociation,
// and SCAMPER-based divergent thinking
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.SelfImprovement;

/// <summary>
/// Implements computational creativity using Fauconnier and Turner's conceptual blending,
/// Koestler's bisociation theory, and SCAMPER-based divergent thinking for idea generation.
/// </summary>
public sealed class CreativityEngine : ICreativityEngine
{
    private static readonly string[] ScamperOperators =
        ["Substitute", "Combine", "Adapt", "Modify", "Put to another use", "Eliminate", "Reverse"];

    private readonly ConcurrentBag<CreativeIdea> _ideaHistory = new();

    /// <summary>
    /// Generates creative ideas through SCAMPER-based divergent thinking.
    /// </summary>
    /// <param name="problem">The problem to generate ideas for.</param>
    /// <param name="numberOfIdeas">Number of ideas to generate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of creative ideas with novelty, value, and surprise scores.</returns>
    public Task<List<CreativeIdea>> DivergentThinkAsync(
        string problem,
        int numberOfIdeas,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(problem);
        if (numberOfIdeas <= 0) throw new ArgumentOutOfRangeException(nameof(numberOfIdeas));
        ct.ThrowIfCancellationRequested();

        var ideas = new List<CreativeIdea>();
        var rng = new Random(problem.GetHashCode());
        var keywords = ExtractKeywords(problem);

        for (int i = 0; i < numberOfIdeas; i++)
        {
            string op = ScamperOperators[i % ScamperOperators.Length];
            string description = GenerateScamperIdea(problem, keywords, op, rng);

            double novelty = 0.3 + rng.NextDouble() * 0.6;
            double value = 0.2 + rng.NextDouble() * 0.6;
            double surprise = 0.2 + rng.NextDouble() * 0.7;

            // Later ideas tend to be more novel but less immediately practical
            novelty = Math.Min(novelty + i * 0.03, 1.0);
            value = Math.Max(value - i * 0.02, 0.1);

            var idea = new CreativeIdea(
                Id: $"{op}-{i}",
                Description: description,
                NoveltyScore: Math.Round(novelty, 3),
                ValueScore: Math.Round(value, 3),
                SurpriseScore: Math.Round(surprise, 3));

            ideas.Add(idea);
            _ideaHistory.Add(idea);
        }

        return Task.FromResult(ideas);
    }

    /// <summary>
    /// Blends two concepts by finding shared structure and generating an emergent concept.
    /// Based on Fauconnier and Turner's Conceptual Integration Theory.
    /// </summary>
    /// <param name="conceptA">First concept.</param>
    /// <param name="conceptB">Second concept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ConceptualBlend"/> with mappings and emergent concept.</returns>
    public Task<ConceptualBlend> BlendConceptsAsync(
        string conceptA,
        string conceptB,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(conceptA);
        ArgumentNullException.ThrowIfNull(conceptB);
        ct.ThrowIfCancellationRequested();

        var wordsA = ExtractKeywords(conceptA);
        var wordsB = ExtractKeywords(conceptB);

        // Find shared structural elements
        var shared = wordsA.Intersect(wordsB, StringComparer.OrdinalIgnoreCase).ToList();
        var uniqueA = wordsA.Except(wordsB, StringComparer.OrdinalIgnoreCase).Take(3).ToList();
        var uniqueB = wordsB.Except(wordsA, StringComparer.OrdinalIgnoreCase).Take(3).ToList();

        var mappings = new List<string>();
        for (int i = 0; i < Math.Min(uniqueA.Count, uniqueB.Count); i++)
            mappings.Add($"{uniqueA[i]} <-> {uniqueB[i]}");
        foreach (string s in shared)
            mappings.Add($"{s} (shared)");

        // Generate emergent concept by combining unique elements
        string emergent = uniqueA.Count > 0 && uniqueB.Count > 0
            ? $"A system that combines {string.Join(" and ", uniqueA.Take(2))} " +
              $"with {string.Join(" and ", uniqueB.Take(2))}" +
              (shared.Count > 0 ? $", unified by {string.Join(", ", shared.Take(2))}" : "")
            : $"Integration of '{conceptA}' and '{conceptB}'";

        int totalWords = wordsA.Union(wordsB, StringComparer.OrdinalIgnoreCase).Count();
        double strength = totalWords > 0
            ? Math.Round((double)(shared.Count + mappings.Count) / (totalWords + mappings.Count), 3)
            : 0.0;

        return Task.FromResult(new ConceptualBlend(conceptA, conceptB, emergent, mappings, strength));
    }

    /// <summary>
    /// Finds bisociative connections between two domains — surprising links between
    /// matrices of thought that are normally separate (Koestler).
    /// </summary>
    /// <param name="domainA">First domain description.</param>
    /// <param name="domainB">Second domain description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="BisociationResult"/> with surprising connections.</returns>
    public Task<BisociationResult> FindBisociationsAsync(
        string domainA,
        string domainB,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(domainA);
        ArgumentNullException.ThrowIfNull(domainB);
        ct.ThrowIfCancellationRequested();

        var wordsA = ExtractKeywords(domainA);
        var wordsB = ExtractKeywords(domainB);

        var connections = new List<string>();

        // Direct shared concepts (low surprise but valid connections)
        var directShared = wordsA.Intersect(wordsB, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string s in directShared)
            connections.Add($"Shared concept: '{s}' bridges both domains");

        // Distant analogical connections (higher surprise)
        foreach (string a in wordsA.Take(4))
        {
            foreach (string b in wordsB.Take(4))
            {
                if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (SharesStructuralSimilarity(a, b))
                    connections.Add($"Analogical link: '{a}' in {domainA.Split(' ')[0]} " +
                                   $"mirrors '{b}' in {domainB.Split(' ')[0]}");
            }
        }

        // Bisociation strength: higher when domains are distant but connections exist
        double domainDistance = 1.0 - JaccardSimilarity(wordsA, wordsB);
        double connectionDensity = connections.Count > 0
            ? Math.Min(connections.Count / 5.0, 1.0)
            : 0.0;
        double strength = Math.Round(domainDistance * connectionDensity, 3);

        string mostNovel = connections.Count > 0
            ? connections[^1]
            : "No connections found";
        return Task.FromResult(new BisociationResult(domainA, domainB, connections, strength, mostNovel));
    }

    /// <summary>
    /// Evaluates the creativity of an idea using weighted scoring:
    /// novelty (0.4) + value (0.3) + surprise (0.3).
    /// </summary>
    /// <param name="idea">The idea to evaluate.</param>
    /// <param name="context">Context for evaluation.</param>
    /// <returns>A <see cref="CreativityScore"/> with component and overall scores.</returns>
    public CreativityScore EvaluateCreativity(CreativeIdea idea, string context)
    {
        ArgumentNullException.ThrowIfNull(idea);

        double overall = idea.NoveltyScore * 0.4 + idea.ValueScore * 0.3 + idea.SurpriseScore * 0.3;
        return new CreativityScore(idea.NoveltyScore, idea.ValueScore, idea.SurpriseScore, Math.Round(overall, 3));
    }

    /// <summary>
    /// Returns the total number of ideas generated.
    /// </summary>
    public int TotalIdeasGenerated => _ideaHistory.Count;

    private static string GenerateScamperIdea(string problem, HashSet<string> keywords, string op, Random rng)
    {
        string[] keyArray = [.. keywords.Take(5)];
        string focus = keyArray.Length > 0 ? keyArray[rng.Next(keyArray.Length)] : "approach";

        return op switch
        {
            "Substitute" => $"Replace '{focus}' with an unconventional alternative to address: {Truncate(problem)}",
            "Combine" => $"Merge '{focus}' with a different discipline's approach to: {Truncate(problem)}",
            "Adapt" => $"Borrow and adapt '{focus}' from nature or another field for: {Truncate(problem)}",
            "Modify" => $"Amplify or minimize the role of '{focus}' in solving: {Truncate(problem)}",
            "Put to another use" => $"Repurpose '{focus}' for an entirely different aspect of: {Truncate(problem)}",
            "Eliminate" => $"Remove '{focus}' entirely and see what remains of: {Truncate(problem)}",
            "Reverse" => $"Invert assumptions about '{focus}' to reframe: {Truncate(problem)}",
            _ => $"Transform '{focus}' using {op} for: {Truncate(problem)}"
        };
    }

    private static string Truncate(string text, int maxLen = 60)
    {
        return text.Length <= maxLen ? text : text[..maxLen] + "...";
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var words = text.Split([' ', ',', '.', ';', ':', '-', '_', '/', '\\', '(', ')'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return [.. words.Where(w => w.Length > 2).Select(w => w.ToLowerInvariant())];
    }

    private static bool SharesStructuralSimilarity(string a, string b)
    {
        // Simple heuristic: similar length and shared character trigrams
        if (Math.Abs(a.Length - b.Length) > 3)
            return false;
        int sharedChars = a.ToLowerInvariant().Intersect(b.ToLowerInvariant()).Count();
        return sharedChars >= Math.Min(a.Length, b.Length) / 2;
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        int intersection = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        int union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
        return union > 0 ? (double)intersection / union : 0.0;
    }
}
