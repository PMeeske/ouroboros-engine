// <copyright file="TaskAnalyzer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Analyzes tasks to determine optimal routing within the ConsolidatedMind.
/// Uses pattern matching and heuristics for fast, local analysis.
/// </summary>
public static partial class TaskAnalyzer
{
    // Complexity indicators
    private static readonly string[] HighComplexityIndicators = new[]
    {
        "complex", "difficult", "challenging", "comprehensive", "detailed", "thorough",
        "multi-step", "advanced", "sophisticated", "intricate", "elaborate"
    };

    private static readonly string[] LowComplexityIndicators = new[]
    {
        "simple", "quick", "brief", "short", "basic", "easy", "straightforward"
    };

    // Pattern definitions for task classification - built lazily using [GeneratedRegex] methods
    private static readonly Dictionary<SpecializedRole, (Func<string, bool>[] Patterns, string[] Keywords)> RolePatterns = new()
    {
        [SpecializedRole.CodeExpert] = (
            new Func<string, bool>[]
            {
                s => CodeBlockPattern().IsMatch(s),
                s => CodeKeywordsPattern().IsMatch(s),
                s => CodeTaskPattern().IsMatch(s),
                s => NeuralArchPattern().IsMatch(s),
            },
            new[] { "code", "implement", "function", "class", "method", "debug", "fix", "refactor", "programming", "syntax", "compile", "api", "library", "bug", "error", "exception", "neural", "embedding", "vector", "tensor", "architecture", "pipeline", "codebase" }
        ),

        [SpecializedRole.DeepReasoning] = (
            new Func<string, bool>[]
            {
                s => WhyQuestionPattern().IsMatch(s),
                s => AnalyzePattern().IsMatch(s),
                s => FrequencyAnalysisPattern().IsMatch(s),
            },
            new[] { "reason", "why", "because", "therefore", "analyze", "evaluate", "explain", "logic", "think", "understand", "deduce", "infer", "consequence", "implication", "cognitive", "consciousness", "metacognition", "self-aware", "introspect" }
        ),

        [SpecializedRole.Mathematical] = (
            new Func<string, bool>[]
            {
                s => MathExpressionPattern().IsMatch(s),
                s => MathKeywordsPattern().IsMatch(s),
                s => LatexMathPattern().IsMatch(s),
                s => FrequencyMeasurePattern().IsMatch(s),
                s => DimensionPattern().IsMatch(s),
            },
            new[] { "calculate", "compute", "math", "equation", "formula", "solve", "proof", "theorem", "algebra", "calculus", "statistics", "probability", "derivative", "integral", "frequency", "hertz", "hz", "decibel", "db", "spectrum", "fourier", "transform", "dimension" }
        ),

        [SpecializedRole.Creative] = (
            new Func<string, bool>[]
            {
                s => CreativeWritePattern().IsMatch(s),
                s => ImaginativePattern().IsMatch(s),
                s => SoundscapePattern().IsMatch(s),
            },
            new[] { "create", "write", "story", "poem", "creative", "imagine", "brainstorm", "ideate", "generate", "compose", "narrative", "fiction", "artistic", "soundscape", "ambient", "atmosphere", "mood", "aesthetic" }
        ),

        [SpecializedRole.Planner] = (
            new Func<string, bool>[]
            {
                s => PlanPattern().IsMatch(s),
                s => DecomposePattern().IsMatch(s),
                s => ImprovePattern().IsMatch(s),
            },
            new[] { "plan", "steps", "strategy", "approach", "decompose", "break down", "outline", "roadmap", "schedule", "procedure", "workflow", "architecture", "design", "improve", "enhance", "upgrade", "level up", "optimize", "better" }
        ),

        [SpecializedRole.Synthesizer] = (
            new Func<string, bool>[]
            {
                s => SummarizePattern().IsMatch(s),
                s => KeyPointsPattern().IsMatch(s),
                s => MemoryPattern().IsMatch(s),
            },
            new[] { "summarize", "summary", "brief", "tldr", "overview", "condense", "extract", "key points", "main ideas", "essence", "remember", "recall", "memory", "memories", "context", "history" }
        ),

        [SpecializedRole.Analyst] = (
            new Func<string, bool>[]
            {
                s => AnalysePattern().IsMatch(s),
                s => ProsConsPattern().IsMatch(s),
                s => AudioAnalysisPattern().IsMatch(s),
            },
            new[] { "analyze", "critique", "review", "evaluate", "assess", "examine", "pros", "cons", "strengths", "weaknesses", "comparison", "acoustic", "audio", "sound", "signal", "urban", "noise", "pattern" }
        ),

        [SpecializedRole.Verifier] = (
            new Func<string, bool>[]
            {
                s => VerifyPattern().IsMatch(s),
                s => FactCheckPattern().IsMatch(s),
            },
            new[] { "verify", "check", "validate", "confirm", "fact-check", "correct", "accurate", "true", "false" }
        ),

        [SpecializedRole.MetaCognitive] = (
            new Func<string, bool>[]
            {
                s => SelfImprovePattern().IsMatch(s),
                s => CapabilitiesPattern().IsMatch(s),
                s => VectorStorePattern().IsMatch(s),
            },
            new[] { "self-improve", "capabilities", "abilities", "features", "memory", "collection", "vector store", "neural", "model", "personality", "behavior", "mode", "configuration", "settings" }
        )
    };

    // ================================================================
    // [GeneratedRegex] partial methods
    // ================================================================

    [GeneratedRegex(@"```[\w]*\s*[\s\S]*?```")]
    private static partial Regex CodeBlockPattern();

    [GeneratedRegex(@"\b(def|class|function|const|let|var|import|export|public|private|async|await)\s+\w+", RegexOptions.IgnoreCase)]
    private static partial Regex CodeKeywordsPattern();

    [GeneratedRegex(@"\b(implement|code|program|debug|fix|refactor|optimize)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CodeTaskPattern();

    [GeneratedRegex(@"\b(neural|embedding|vector|tensor|model)\s*(architecture|layer|network)", RegexOptions.IgnoreCase)]
    private static partial Regex NeuralArchPattern();

    [GeneratedRegex(@"\b(why|how come|explain why|reason)\b.*\?", RegexOptions.IgnoreCase)]
    private static partial Regex WhyQuestionPattern();

    [GeneratedRegex(@"\b(analyze|evaluate|assess|consider)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AnalyzePattern();

    [GeneratedRegex(@"\b(frequency|spectrum|harmonic|waveform|acoustic)\s*(analysis|pattern)", RegexOptions.IgnoreCase)]
    private static partial Regex FrequencyAnalysisPattern();

    [GeneratedRegex(@"[\d]+\s*[\+\-\*\/\^]\s*[\d]+")]
    private static partial Regex MathExpressionPattern();

    [GeneratedRegex(@"\b(calculate|compute|solve|equation|formula)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MathKeywordsPattern();

    [GeneratedRegex(@"\$.*\$")]
    private static partial Regex LatexMathPattern();

    [GeneratedRegex(@"\b(\d+)\s*(hz|khz|mhz|ghz|db|decibel)", RegexOptions.IgnoreCase)]
    private static partial Regex FrequencyMeasurePattern();

    [GeneratedRegex(@"\b(dimension|vector|matrix|768|384|1536)\s*(dimension|embed)?", RegexOptions.IgnoreCase)]
    private static partial Regex DimensionPattern();

    [GeneratedRegex(@"\b(write|create|generate|compose)\s+(a|an|the)?\s*(story|poem|song|essay|article)", RegexOptions.IgnoreCase)]
    private static partial Regex CreativeWritePattern();

    [GeneratedRegex(@"\b(imagine|creative|brainstorm)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ImaginativePattern();

    [GeneratedRegex(@"\b(soundscape|ambient|atmosphere|mood)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SoundscapePattern();

    [GeneratedRegex(@"\b(plan|steps|how to|strategy)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlanPattern();

    [GeneratedRegex(@"\b(break down|decompose|outline)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DecomposePattern();

    [GeneratedRegex(@"\b(improve|enhance|upgrade|level up|optimize)\s*(my|the|your)?", RegexOptions.IgnoreCase)]
    private static partial Regex ImprovePattern();

    [GeneratedRegex(@"\b(summarize|summary|tldr|brief)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SummarizePattern();

    [GeneratedRegex(@"\b(key points|main ideas|overview)\b", RegexOptions.IgnoreCase)]
    private static partial Regex KeyPointsPattern();

    [GeneratedRegex(@"\b(remember|recall|memory|memories)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MemoryPattern();

    [GeneratedRegex(@"\b(analyze|analyse|critique|review|evaluate)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AnalysePattern();

    [GeneratedRegex(@"\b(pros and cons|strengths and weaknesses)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProsConsPattern();

    [GeneratedRegex(@"\b(acoustic|audio|sound|signal)\s*(analysis|processing|pattern)", RegexOptions.IgnoreCase)]
    private static partial Regex AudioAnalysisPattern();

    [GeneratedRegex(@"\b(verify|check|validate|confirm)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VerifyPattern();

    [GeneratedRegex(@"\b(is it true|fact check|correct)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FactCheckPattern();

    [GeneratedRegex(@"\b(self|yourself|your own)\s*(improve|enhance|modify|change)", RegexOptions.IgnoreCase)]
    private static partial Regex SelfImprovePattern();

    [GeneratedRegex(@"\b(capabilities|abilities|features)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CapabilitiesPattern();

    [GeneratedRegex(@"\b(memory|collection|vector\s*store)\s*(system|database)?", RegexOptions.IgnoreCase)]
    private static partial Regex VectorStorePattern();

    [GeneratedRegex(@"\b(first|second|third|then|next|finally)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MultiStepPattern();

    // ================================================================
    // Public API
    // ================================================================

    /// <summary>
    /// Analyzes a prompt to determine optimal routing.
    /// </summary>
    /// <param name="prompt">The user prompt to analyze.</param>
    /// <returns>Analysis result with routing recommendations.</returns>
    public static TaskAnalysis Analyze(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new TaskAnalysis(
                SpecializedRole.QuickResponse,
                Array.Empty<SpecializedRole>(),
                Array.Empty<string>(),
                EstimatedComplexity: 0.0,
                RequiresThinking: false,
                RequiresVerification: false,
                Confidence: 1.0);
        }

        string lowerPrompt = prompt.ToLowerInvariant();
        int promptLength = prompt.Length;

        var roleScores = new Dictionary<SpecializedRole, double>();
        var matchedCapabilities = new HashSet<string>();

        foreach (var (role, (patterns, keywords)) in RolePatterns)
        {
            double score = 0.0;

            foreach (var pattern in patterns.Where(pattern => pattern(prompt)))
            {
                score += 2.0;
            }

            foreach (var keyword in keywords.Where(keyword => lowerPrompt.Contains(keyword)))
            {
                score += 1.0;
                matchedCapabilities.Add(keyword);
            }

            if (score > 0)
            {
                roleScores[role] = score;
            }
        }

        SpecializedRole primaryRole;
        double confidence;

        if (roleScores.Count == 0)
        {
            primaryRole = promptLength < 200 ? SpecializedRole.QuickResponse : SpecializedRole.DeepReasoning;
            confidence = 0.5;
        }
        else
        {
            var sorted = roleScores.OrderByDescending(kvp => kvp.Value).ToList();
            primaryRole = sorted[0].Key;
            double maxScore = sorted[0].Value;
            double totalScore = sorted.Sum(kvp => kvp.Value);
            confidence = Math.Min(1.0, maxScore / Math.Max(totalScore * 0.5, 1.0));
        }

        var secondaryRoles = roleScores
            .Where(kvp => kvp.Key != primaryRole && kvp.Value > 1.0)
            .OrderByDescending(kvp => kvp.Value)
            .Take(2)
            .Select(kvp => kvp.Key)
            .ToArray();

        double complexity = EstimateComplexity(prompt, lowerPrompt, promptLength);

        bool requiresThinking = complexity > 0.6 ||
            primaryRole == SpecializedRole.DeepReasoning ||
            primaryRole == SpecializedRole.Mathematical ||
            primaryRole == SpecializedRole.Planner;

        bool requiresVerification = complexity > 0.7 ||
            primaryRole == SpecializedRole.CodeExpert ||
            primaryRole == SpecializedRole.Mathematical ||
            lowerPrompt.Contains("important") ||
            lowerPrompt.Contains("critical") ||
            lowerPrompt.Contains("production");

        return new TaskAnalysis(
            primaryRole,
            secondaryRoles,
            matchedCapabilities.ToArray(),
            complexity,
            requiresThinking,
            requiresVerification,
            confidence);
    }

    /// <summary>
    /// Estimates the complexity of a task.
    /// </summary>
    private static double EstimateComplexity(string prompt, string lowerPrompt, int length)
    {
        double complexity = 0.0;

        complexity += Math.Min(0.3, length / 2000.0);

        foreach (var indicator in HighComplexityIndicators.Where(indicator => lowerPrompt.Contains(indicator)))
        {
            complexity += 0.15;
        }

        foreach (var indicator in LowComplexityIndicators.Where(indicator => lowerPrompt.Contains(indicator)))
        {
            complexity -= 0.1;
        }

        int questionMarks = prompt.Count(c => c == '?');
        if (questionMarks == 1 && length < 100)
        {
            complexity -= 0.1;
        }

        if (lowerPrompt.Contains("and then") || lowerPrompt.Contains("after that") ||
            MultiStepPattern().IsMatch(lowerPrompt))
        {
            complexity += 0.2;
        }

        if (prompt.Contains("```"))
        {
            complexity += 0.2;
        }

        return Math.Clamp(complexity, 0.0, 1.0);
    }

    /// <summary>
    /// Determines if a task should be split into sub-tasks.
    /// </summary>
    /// <param name="analysis">The task analysis result.</param>
    /// <returns>True if the task should be decomposed.</returns>
    public static bool ShouldDecompose(TaskAnalysis analysis)
    {
        return analysis.EstimatedComplexity > 0.7 ||
               analysis.SecondaryRoles.Length >= 2 ||
               analysis.RequiredCapabilities.Length > 5;
    }
}
