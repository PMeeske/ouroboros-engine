// <copyright file="OuroborosOrchestrator.Planning.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class OuroborosOrchestrator
{
    /// <summary>
    /// Executes the PLAN phase - goal decomposition and strategy formulation.
    /// </summary>
    private async Task<PhaseResult> ExecutePlanPhaseAsync(string goal, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            if (!_atom.IsSafeAction(goal))
            {
                return new PhaseResult(
                    ImprovementPhase.Plan,
                    Success: false,
                    Output: string.Empty,
                    Error: "Goal violates safety constraints",
                    Duration: sw.Elapsed);
            }

            OuroborosConfidence confidence = _atom.AssessConfidence(goal);
            string selfReflection = _atom.SelfReflect();
            string prompt = BuildPlanPrompt(goal, selfReflection, confidence);

            string planText = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

            sw.Stop();
            RecordPhaseMetric("plan", sw.ElapsedMilliseconds, true);

            return new PhaseResult(
                ImprovementPhase.Plan,
                Success: true,
                Output: planText,
                Error: null,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["confidence"] = confidence.ToString(),
                    ["self_reflection_included"] = true,
                });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            RecordPhaseMetric("plan", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Plan,
                Success: false,
                Output: string.Empty,
                Error: $"Planning failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    private string BuildPlanPrompt(string goal, string selfReflection, OuroborosConfidence confidence)
    {
        string confidenceNote = confidence switch
        {
            OuroborosConfidence.High => "I have high confidence in achieving this goal based on past experience.",
            OuroborosConfidence.Medium => "I have moderate confidence in this goal - proceed with verification.",
            OuroborosConfidence.Low => "I have low confidence in this goal - careful planning required.",
            _ => string.Empty,
        };

        double planningDepth = _atom.GetStrategyWeight("PlanningDepth", 0.5);
        double decompositionGranularity = _atom.GetStrategyWeight("DecompositionGranularity", 0.5);

        string planningGuidance = planningDepth < 0.3
            ? "a concise high-level plan"
            : planningDepth > 0.7
                ? "a detailed plan with sub-steps and contingencies"
                : "a structured plan with clear steps";

        int suggestedSteps = (int)(MinPlanSteps + (decompositionGranularity * PlanStepsRange));
        string stepGuidance = $"Aim for approximately {suggestedSteps} steps";

        return $@"Create a plan to achieve: {goal}

Self-Assessment:
{selfReflection}

Confidence: {confidenceNote}

Available tools: {string.Join(", ", _tools.All.Select(t => t.Name))}

Provide {planningGuidance}. {stepGuidance}. Each step should be actionable and specific.";
    }

    private static List<string> ParsePlanSteps(string plan)
    {
        List<string> steps = plan.Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Where(l => l.TrimStart().StartsWith('-') ||
                       l.TrimStart().StartsWith('*') ||
                       char.IsDigit(l.TrimStart().FirstOrDefault()))
            .Select(l => l.TrimStart('-', '*', ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.'))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        if (steps.Count == 0)
        {
            steps.Add(plan.Trim());
        }

        return steps;
    }

    /// <summary>
    /// Projects the current affective state into the MeTTa knowledge base.
    /// </summary>
    private async Task ProjectAffectiveStateToMeTTaAsync(
        Affect.AffectiveState affect, Affect.Urge? dominantUrge, CancellationToken ct)
    {
        string id = _atom.InstanceId;

        await _mettaEngine.AddFactAsync(
            $"(AffectiveState (OuroborosInstance \"{id}\") " +
            $"(Valence {affect.Valence:F2}) " +
            $"(Stress {affect.Stress:F2}) " +
            $"(Arousal {affect.Arousal:F2}) " +
            $"(Confidence {affect.Confidence:F2}) " +
            $"(Curiosity {affect.Curiosity:F2}))", ct).ConfigureAwait(false);

        if (dominantUrge != null)
        {
            await _mettaEngine.AddFactAsync(
                $"(DominantUrge (OuroborosInstance \"{id}\") " +
                $"(Urge \"{dominantUrge.Name}\" {dominantUrge.Intensity:F2}))", ct).ConfigureAwait(false);
        }

        if (_urgeSystem != null)
        {
            string urgesMeTTa = _urgeSystem.ToMeTTa(id);
            await _mettaEngine.AddFactAsync(urgesMeTTa, ct).ConfigureAwait(false);
        }

        if (_spreading != null)
        {
            foreach (var (atomKey, activation) in _spreading.GetActivatedAtoms().Take(10))
            {
                await _mettaEngine.AddFactAsync(
                    $"(Primed (OuroborosInstance \"{id}\") (Concept \"{EscapeMeTTa(atomKey)}\" {activation:F2}))", ct).ConfigureAwait(false);
            }
        }
    }
}
