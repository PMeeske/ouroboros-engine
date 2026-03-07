// <copyright file="ConsolidatedMind.Processing.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using Ouroboros.Agent.NeuralSymbolic;

namespace Ouroboros.Agent.ConsolidatedMind;

public sealed partial class ConsolidatedMind
{
    /// <summary>
    /// Executes a complex task by decomposing it and coordinating multiple specialists.
    /// </summary>
    public async Task<MindResponse> ProcessComplexAsync(string prompt, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var usedRoles = new List<SpecializedRole>();

        if (!_specialists.TryGetValue(SpecializedRole.Planner, out var planner))
        {
            return await ProcessAsync(prompt, ct);
        }

        usedRoles.Add(SpecializedRole.Planner);

        string planPrompt = $@"Decompose this task into clear sub-tasks that can be handled by specialists.
Output a numbered list of sub-tasks, each on a new line.

Task: {prompt}

Sub-tasks:";

        string plan = await planner.Model.GenerateTextAsync(planPrompt, ct);

        var subTasks = plan.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '-', ' '))
            .Where(line => line.Length > 5)
            .ToList();

        if (subTasks.Count == 0)
        {
            return await ProcessAsync(prompt, ct);
        }

        var subResults = new List<(string Task, string Result, SpecializedRole Role)>();

        if (_config.EnableParallelExecution && subTasks.Count > 1)
        {
            var semaphore = new SemaphoreSlim(_config.MaxParallelism);
            var tasks = subTasks.Select(async subTask =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var subAnalysis = TaskAnalyzer.Analyze(subTask);
                    var specialist = _specialists.GetValueOrDefault(subAnalysis.PrimaryRole)
                        ?? _specialists.Values.First();

                    var result = await specialist.Model.GenerateTextAsync(subTask, ct);
                    return (subTask, result, specialist.Role);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            subResults.AddRange(results);
        }
        else
        {
            foreach (var subTask in subTasks)
            {
                var subAnalysis = TaskAnalyzer.Analyze(subTask);
                var specialist = _specialists.GetValueOrDefault(subAnalysis.PrimaryRole)
                    ?? _specialists.Values.First();

                var result = await specialist.Model.GenerateTextAsync(subTask, ct);
                subResults.Add((subTask, result, specialist.Role));
                usedRoles.Add(specialist.Role);
            }
        }

        usedRoles.AddRange(subResults.Select(r => r.Role).Distinct());

        if (_specialists.TryGetValue(SpecializedRole.Synthesizer, out var synthesizer))
        {
            usedRoles.Add(SpecializedRole.Synthesizer);

            string synthesisPrompt = $@"Synthesize a coherent response from these sub-task results.

Original task: {prompt}

Sub-task results:
{string.Join("\n\n", subResults.Select((r, i) => $"[{i + 1}] {r.Task}\nResult: {r.Result}"))}

Synthesized response:";

            var finalResponse = await synthesizer.Model.GenerateTextAsync(synthesisPrompt, ct);

            return new MindResponse(
                finalResponse,
                ThinkingContent: null,
                usedRoles.Distinct().ToArray(),
                stopwatch.ElapsedMilliseconds,
                WasVerified: false,
                Confidence: 0.8);
        }

        var combinedResponse = string.Join("\n\n", subResults.Select(r => r.Result));
        return new MindResponse(
            combinedResponse,
            ThinkingContent: null,
            usedRoles.Distinct().ToArray(),
            stopwatch.ElapsedMilliseconds,
            WasVerified: false,
            Confidence: 0.6);
    }

    private async Task<(bool IsValid, string Feedback)> VerifyResponseAsync(
        string originalPrompt,
        string response,
        CancellationToken ct)
    {
        if (!_specialists.TryGetValue(SpecializedRole.Verifier, out var verifier))
        {
            return (true, string.Empty);
        }

        string verifyPrompt = $@"Verify this response for accuracy and completeness.

Original question: {originalPrompt}

Response to verify: {response}

Is this response accurate and complete? Reply with:
VALID: [reason]
or
INVALID: [specific issues and suggestions]";

        var verificationResult = await verifier.Model.GenerateTextAsync(verifyPrompt, ct);

        bool isValid = verificationResult.StartsWith("VALID", StringComparison.OrdinalIgnoreCase);
        return (isValid, verificationResult);
    }

    private async Task<string> RefineResponseAsync(
        string originalPrompt,
        string originalResponse,
        string feedback,
        CancellationToken ct)
    {
        var refiner = _specialists.GetValueOrDefault(SpecializedRole.DeepReasoning)
            ?? _specialists.GetValueOrDefault(SpecializedRole.Analyst)
            ?? _specialists.Values.FirstOrDefault();

        if (refiner == null)
            return originalResponse;

        string refinePrompt = $@"Improve this response based on the verification feedback.

Original question: {originalPrompt}

Original response: {originalResponse}

Feedback: {feedback}

Improved response:";

        return await refiner.Model.GenerateTextAsync(refinePrompt, ct);
    }

    private SpecializedModel? GetFallbackSpecialist(SpecializedRole failedRole)
    {
        var fallbackChain = failedRole switch
        {
            SpecializedRole.QuickResponse => new[] { SpecializedRole.DeepReasoning, SpecializedRole.Analyst, SpecializedRole.SymbolicReasoner },
            SpecializedRole.DeepReasoning => new[] { SpecializedRole.Analyst, SpecializedRole.QuickResponse, SpecializedRole.SymbolicReasoner },
            SpecializedRole.CodeExpert => new[] { SpecializedRole.DeepReasoning, SpecializedRole.QuickResponse, SpecializedRole.SymbolicReasoner },
            SpecializedRole.Mathematical => new[] { SpecializedRole.DeepReasoning, SpecializedRole.CodeExpert, SpecializedRole.SymbolicReasoner },
            SpecializedRole.Creative => new[] { SpecializedRole.QuickResponse, SpecializedRole.DeepReasoning, SpecializedRole.SymbolicReasoner },
            SpecializedRole.Planner => new[] { SpecializedRole.DeepReasoning, SpecializedRole.Analyst, SpecializedRole.SymbolicReasoner },
            SpecializedRole.Analyst => new[] { SpecializedRole.DeepReasoning, SpecializedRole.QuickResponse, SpecializedRole.SymbolicReasoner },
            SpecializedRole.Synthesizer => new[] { SpecializedRole.DeepReasoning, SpecializedRole.QuickResponse, SpecializedRole.SymbolicReasoner },
            SpecializedRole.Verifier => new[] { SpecializedRole.DeepReasoning, SpecializedRole.Analyst, SpecializedRole.SymbolicReasoner },
            SpecializedRole.MetaCognitive => new[] { SpecializedRole.DeepReasoning, SpecializedRole.Analyst, SpecializedRole.SymbolicReasoner },
            SpecializedRole.SymbolicReasoner => Array.Empty<SpecializedRole>(),
            _ => new[] { SpecializedRole.SymbolicReasoner }
        };

        foreach (var role in fallbackChain)
        {
            if (_specialists.TryGetValue(role, out var specialist))
            {
                return specialist;
            }
        }

        return failedRole == SpecializedRole.SymbolicReasoner ? null : _specialists.Values.FirstOrDefault();
    }

    private void UpdateMetrics(string modelName, double latencyMs, bool success)
    {
        _metrics.AddOrUpdate(
            modelName,
            key => new PerformanceMetrics(
                key,
                ExecutionCount: 1,
                AverageLatencyMs: latencyMs,
                SuccessRate: success ? 1.0 : 0.0,
                LastUsed: DateTime.UtcNow,
                CustomMetrics: new Dictionary<string, double>()),
            (key, existing) =>
            {
                int newCount = existing.ExecutionCount + 1;
                double newLatency = (existing.AverageLatencyMs * existing.ExecutionCount + latencyMs) / newCount;
                double newSuccessRate = (existing.SuccessRate * existing.ExecutionCount + (success ? 1.0 : 0.0)) / newCount;

                return existing with
                {
                    ExecutionCount = newCount,
                    AverageLatencyMs = newLatency,
                    SuccessRate = newSuccessRate,
                    LastUsed = DateTime.UtcNow
                };
            });
    }
}
