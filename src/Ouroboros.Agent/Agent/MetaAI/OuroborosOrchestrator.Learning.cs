// <copyright file="OuroborosOrchestrator.Learning.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class OuroborosOrchestrator
{
    /// <summary>
    /// Executes the LEARN phase - extracting insights and updating capabilities.
    /// </summary>
    private async Task<PhaseResult> ExecuteLearnPhaseAsync(string goal, List<PhaseResult> phaseResults, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            bool overallSuccess = phaseResults.All(p => p.Success);
            double averageQuality = phaseResults
                .Where(p => p.Metadata.ContainsKey("quality_score"))
                .Select(p => (double)p.Metadata["quality_score"])
                .DefaultIfEmpty(0.5)
                .Average();

            List<string> insights = await ExtractInsightsAsync(goal, phaseResults, ct).ConfigureAwait(false);

            TimeSpan cycleDuration = phaseResults.Aggregate(TimeSpan.Zero, (sum, p) => sum + p.Duration);
            OuroborosExperience experience = new OuroborosExperience(
                Id: Guid.NewGuid(),
                Goal: goal,
                Success: overallSuccess,
                QualityScore: averageQuality,
                Insights: insights,
                Timestamp: DateTime.UtcNow,
                Duration: cycleDuration);

            _atom.RecordExperience(experience);

            await StoreExperienceInMemoryAsync(experience, ct).ConfigureAwait(false);
            await UpdateMeTTaKnowledgeAsync(experience, ct).ConfigureAwait(false);

            if (overallSuccess && averageQuality > QualityThresholdForCapabilityUpdate)
            {
                IEnumerable<string> relevantCapabilities = goal.ToLower().Split(' ')
                    .Where(word => _atom.Capabilities.Any(c => c.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));

                foreach (string capName in relevantCapabilities.Take(2))
                {
                    OuroborosCapability? existingCap = _atom.Capabilities.FirstOrDefault(c =>
                        c.Name.Contains(capName, StringComparison.OrdinalIgnoreCase));

                    if (existingCap != null)
                    {
                        double newConfidence = Math.Min(1.0, existingCap.ConfidenceLevel + ConfidenceBoostIncrement);
                        _atom.AddCapability(existingCap with { ConfidenceLevel = newConfidence });
                    }
                }
            }

            await TryEvolveStrategiesAsync(ct).ConfigureAwait(false);

            sw.Stop();
            RecordPhaseMetric("learn", sw.ElapsedMilliseconds, true);

            return new PhaseResult(
                ImprovementPhase.Learn,
                Success: true,
                Output: $"Learned {insights.Count} insights. Success rate: {_atom.Experiences.Count(e => e.Success) / (double)_atom.Experiences.Count:P0}",
                Error: null,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["insights_count"] = insights.Count,
                    ["cycle_count"] = _atom.CycleCount,
                    ["total_experiences"] = _atom.Experiences.Count,
                });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            RecordPhaseMetric("learn", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Learn,
                Success: false,
                Output: string.Empty,
                Error: $"Learning failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Extracts insights from the execution.
    /// </summary>
    private async Task<List<string>> ExtractInsightsAsync(string goal, List<PhaseResult> phases, CancellationToken ct)
    {
        StringBuilder context = new StringBuilder();
        context.AppendLine($"Goal: {goal}");

        foreach (PhaseResult phase in phases)
        {
            context.AppendLine($"Phase {phase.Phase}: Success={phase.Success}");
            if (!string.IsNullOrEmpty(phase.Error))
            {
                context.AppendLine($"  Error: {phase.Error}");
            }
        }

        string prompt = $@"Extract 2-3 key insights from this execution:
{context}

Provide insights as a bullet list, each starting with '-'. Focus on:
- What worked well
- What could be improved
- Patterns to remember";

        string response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

        List<string> insights = response.Split('\n')
            .Where(l => l.TrimStart().StartsWith('-'))
            .Select(l => l.TrimStart('-', ' '))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        return insights;
    }

    /// <summary>
    /// Stores experience in the memory store.
    /// </summary>
    private async Task StoreExperienceInMemoryAsync(OuroborosExperience ouroborosExp, CancellationToken ct)
    {
        Plan plan = new Plan(
            ouroborosExp.Goal,
            new List<PlanStep>(),
            new Dictionary<string, double> { ["quality"] = ouroborosExp.QualityScore },
            ouroborosExp.Timestamp);

        PlanExecutionResult execution = new PlanExecutionResult(
            plan,
            new List<StepResult>(),
            ouroborosExp.Success,
            string.Join("; ", ouroborosExp.Insights),
            new Dictionary<string, object>(),
            TimeSpan.Zero);

        PlanVerificationResult verification = new PlanVerificationResult(
            execution,
            ouroborosExp.Success,
            ouroborosExp.QualityScore,
            new List<string>(),
            ouroborosExp.Insights.ToList(),
            null);

        Experience experience = new Experience(
            ouroborosExp.Id.ToString(),
            ouroborosExp.Timestamp,
            Context: ouroborosExp.Goal,
            Action: JsonSerializer.Serialize(plan),
            Outcome: execution.Success ? "Success" : "Failed",
            Success: execution.Success,
            Tags: new List<string> { "ouroboros", "orchestrator" },
            Goal: ouroborosExp.Goal,
            Execution: execution,
            Verification: verification);

        var result = await _memory.StoreExperienceAsync(experience, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            // Log error but don't fail the execution
        }

        // If the execution was successful and a SkillExtractor is available, attempt skill extraction
        if (_skillExtractor != null && execution.Success && verification.Verified
            && verification.QualityScore >= 0.7)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    bool shouldExtract = await _skillExtractor.ShouldExtractSkillAsync(verification).ConfigureAwait(false);
                    if (shouldExtract)
                    {
                        Result<Skill, string> skillResult = await _skillExtractor.ExtractSkillAsync(
                            execution,
                            verification,
                            ct: CancellationToken.None).ConfigureAwait(false);

                        skillResult.Match(
                            skill => _logger.LogInformation(
                                "[SkillExtractor] Extracted skill '{SkillName}' (quality: {Quality:P0})",
                                skill.Name, skill.SuccessRate),
                            error => _logger.LogDebug(
                                "[SkillExtractor] Skill extraction skipped: {Reason}", error));
                    }
                }
                catch (OperationCanceledException) { /* fire-and-forget, ignore cancellation */ }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug("[SkillExtractor] Non-fatal error during skill extraction: {Message}", ex.Message);
                }
            }, ct);
        }
    }

    /// <summary>
    /// Updates MeTTa knowledge base with new experience.
    /// </summary>
    private async Task UpdateMeTTaKnowledgeAsync(OuroborosExperience experience, CancellationToken ct)
    {
        StringBuilder metta = new StringBuilder();

        string expType = experience.Success ? "SuccessfulExperience" : "FailedExperience";
        metta.AppendLine($"(LearnedFrom (OuroborosInstance \"{_atom.InstanceId}\") ({expType} \"{EscapeMeTTa(experience.Goal)}\" {experience.QualityScore:F2}))");

        foreach (string insight in experience.Insights)
        {
            metta.AppendLine($"(Insight \"{experience.Id}\" \"{EscapeMeTTa(insight)}\")");
        }

        await _mettaEngine.AddFactAsync(metta.ToString(), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Tries to evolve planning strategies using the genetic algorithm, if configured.
    /// </summary>
    private async Task TryEvolveStrategiesAsync(CancellationToken ct)
    {
        if (_strategyEvolver == null || _atom.Experiences.Count < 5)
        {
            return;
        }

        try
        {
            var random = new Random();
            var initialPopulation = new List<Genetic.Abstractions.IChromosome<Evolution.PlanStrategyGene>>
            {
                Evolution.PlanStrategyChromosome.FromAtom(_atom),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
                Evolution.PlanStrategyChromosome.CreateRandom(random),
            };

            Result<Genetic.Abstractions.IChromosome<Evolution.PlanStrategyGene>, string> evolutionResult =
                await _strategyEvolver.EvolveAsync(initialPopulation, generations: 3, cancellationToken: ct).ConfigureAwait(false);

            if (evolutionResult.IsSuccess)
            {
                var bestChromosome = evolutionResult.Match(
                    success => success as Evolution.PlanStrategyChromosome,
                    _ => null);

                if (bestChromosome != null && bestChromosome.Fitness > 0.6)
                {
                    foreach (var gene in bestChromosome.Genes)
                    {
                        var capability = new OuroborosCapability(
                            Name: $"Strategy_{gene.StrategyName}",
                            Description: gene.Description,
                            ConfidenceLevel: gene.Weight);
                        _atom.AddCapability(capability);
                    }

                    Trace.TraceInformation("[GA] Evolved planning strategy with fitness {0}: {1}", bestChromosome.Fitness.ToString("F3"), bestChromosome);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceWarning("[GA] Strategy evolution failed (non-fatal): {0}", ex.Message);
        }
    }
}
