// <copyright file="OuroborosOrchestrator.Execution.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class OuroborosOrchestrator
{
    /// <summary>
    /// Executes the EXECUTE phase - carrying out planned actions.
    /// </summary>
    private async Task<PhaseResult> ExecuteExecutePhaseAsync(string plan, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            List<string> steps = ParsePlanSteps(plan);
            StringBuilder outputBuilder = new StringBuilder();
            bool allStepsSucceeded = true;
            string? lastError = null;

            foreach (string step in steps)
            {
                Result<string, string> stepResult = await ExecuteStepAsync(step, ct).ConfigureAwait(false);

                stepResult.Match(
                    success =>
                    {
                        outputBuilder.AppendLine($"Step completed: {success}");
                    },
                    error =>
                    {
                        outputBuilder.AppendLine($"Step failed: {error}");
                        allStepsSucceeded = false;
                        lastError = error;
                    });

                if (!allStepsSucceeded)
                {
                    break;
                }
            }

            sw.Stop();
            RecordPhaseMetric("execute", sw.ElapsedMilliseconds, allStepsSucceeded);

            return new PhaseResult(
                ImprovementPhase.Execute,
                Success: allStepsSucceeded,
                Output: outputBuilder.ToString(),
                Error: lastError,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["steps_count"] = steps.Count,
                    ["steps_completed"] = allStepsSucceeded ? steps.Count : steps.Count - 1,
                });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            RecordPhaseMetric("execute", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Execute,
                Success: false,
                Output: string.Empty,
                Error: $"Execution failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    /// <summary>
    /// Executes the VERIFY phase - checking results against expectations.
    /// </summary>
    private async Task<PhaseResult> ExecuteVerifyPhaseAsync(string goal, string output, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            double verificationStrictness = _atom.GetStrategyWeight("VerificationStrictness", 0.6);
            double qualityThreshold = BaseQualityThreshold + (verificationStrictness * QualityThresholdRange);

            string prompt = BuildVerificationPrompt(goal, output);
            string verificationText = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

            (bool verified, double qualityScore) = await ParsePlanVerificationResult(verificationText, goal, output, ct).ConfigureAwait(false);

            bool meetsQualityThreshold = qualityScore >= qualityThreshold;

            string planMetta = $"(plan (goal \"{EscapeMeTTa(goal)}\") (output \"{EscapeMeTTa(output.Substring(0, Math.Min(MeTTaOutputTruncationLength, output.Length)))}\"))";
            Result<bool, string> mettaResult = await _mettaEngine.VerifyPlanAsync(planMetta, ct).ConfigureAwait(false);

            bool mettaVerified = mettaResult.Match(
                v => v,
                err =>
                {
                    _logger.LogWarning("[VERIFY] MeTTa verification failed: {Error}. Treating as unverified.", err);
                    return false;
                });

            bool overallSuccess = verified && meetsQualityThreshold && mettaVerified;

            string? errorMessage = null;
            if (!overallSuccess)
            {
                List<string> failures = new List<string>();
                if (!verified) failures.Add("LLM verification failed");
                if (!meetsQualityThreshold) failures.Add($"quality {qualityScore:F2} below threshold {qualityThreshold:F2}");
                if (!mettaVerified) failures.Add("MeTTa verification failed");
                errorMessage = $"Verification failed: {string.Join(", ", failures)}";
            }

            sw.Stop();
            RecordPhaseMetric("verify", sw.ElapsedMilliseconds, overallSuccess);

            return new PhaseResult(
                ImprovementPhase.Verify,
                Success: overallSuccess,
                Output: verificationText,
                Error: errorMessage,
                Duration: sw.Elapsed,
                Metadata: new Dictionary<string, object>
                {
                    ["quality_score"] = qualityScore,
                    ["quality_threshold"] = qualityThreshold,
                    ["verification_strictness"] = verificationStrictness,
                    ["metta_verified"] = mettaVerified,
                    ["llm_verified"] = verified,
                    ["meets_quality_threshold"] = meetsQualityThreshold,
                });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            RecordPhaseMetric("verify", sw.ElapsedMilliseconds, false);
            return new PhaseResult(
                ImprovementPhase.Verify,
                Success: false,
                Output: string.Empty,
                Error: $"Verification failed: {ex.Message}",
                Duration: sw.Elapsed);
        }
    }

    private static string BuildVerificationPrompt(string goal, string output)
    {
        return $@"Verify if the following execution achieved the goal.

Goal: {goal}

Output:
{output}

Provide verification in JSON format:
{{
  ""verified"": true/false,
  ""quality_score"": 0.0-1.0,
  ""reasoning"": ""brief explanation""
}}";
    }

    private async Task<(bool Verified, double QualityScore)> ParsePlanVerificationResult(string verificationText, string goal, string output, CancellationToken ct)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(verificationText);
            bool verified = doc.RootElement.GetProperty("verified").GetBoolean();
            double qualityScore = doc.RootElement.GetProperty("quality_score").GetDouble();
            return (verified, qualityScore);
        }
        catch (JsonException jsonEx)
        {
            _logger.LogWarning(
                jsonEx,
                "[VERIFY] Failed to parse verification result (JSON error), attempting retry. ExceptionMessage: {ExceptionMessage}",
                jsonEx.Message);
            return await RetryVerificationParsingAsync(goal, output, ct).ConfigureAwait(false);
        }
        catch (KeyNotFoundException keyEx)
        {
            _logger.LogWarning(
                keyEx,
                "[VERIFY] Missing required property in verification result. ExceptionMessage: {ExceptionMessage}",
                keyEx.Message);
            return await RetryVerificationParsingAsync(goal, output, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException invalidOpEx)
        {
            _logger.LogWarning(
                invalidOpEx,
                "[VERIFY] Invalid property type in verification result. ExceptionMessage: {ExceptionMessage}",
                invalidOpEx.Message);
            return await RetryVerificationParsingAsync(goal, output, ct).ConfigureAwait(false);
        }
    }

    private async Task<(bool Verified, double QualityScore)> RetryVerificationParsingAsync(string goal, string output, CancellationToken ct)
    {
        try
        {
            string retryResponse = await RequestStructuredVerificationAsync(goal, output, ct).ConfigureAwait(false);
            using JsonDocument retryDoc = JsonDocument.Parse(retryResponse);
            bool verified = retryDoc.RootElement.GetProperty("verified").GetBoolean();
            double qualityScore = retryDoc.RootElement.GetProperty("quality_score").GetDouble();
            _logger.LogInformation("[VERIFY] Retry successful: verified={Verified}, quality={QualityScore}", verified, qualityScore);
            return (verified, qualityScore);
        }
        catch (JsonException retryJsonEx)
        {
            _logger.LogWarning(
                retryJsonEx,
                "[VERIFY] Failed to parse verification result after retry, treating as failed. ExceptionMessage: {ExceptionMessage}",
                retryJsonEx.Message);
            return (false, 0.0);
        }
        catch (KeyNotFoundException retryKeyEx)
        {
            _logger.LogWarning(
                retryKeyEx,
                "[VERIFY] Missing required property after retry, treating as failed. ExceptionMessage: {ExceptionMessage}",
                retryKeyEx.Message);
            return (false, 0.0);
        }
        catch (InvalidOperationException retryInvalidOpEx)
        {
            _logger.LogWarning(
                retryInvalidOpEx,
                "[VERIFY] Invalid property type after retry, treating as failed. ExceptionMessage: {ExceptionMessage}",
                retryInvalidOpEx.Message);
            return (false, 0.0);
        }
    }

    private async Task<string> RequestStructuredVerificationAsync(string goal, string output, CancellationToken ct)
    {
        const int maxLength = 2000;
        string truncatedGoal = goal.Length > maxLength ? goal.Substring(0, maxLength) + "..." : goal;
        string truncatedOutput = output.Length > maxLength ? output.Substring(0, maxLength) + "..." : output;

        string prompt = $"Verify if the output achieves the goal. " +
                        $"Respond ONLY with JSON: {{\"verified\": true/false, \"quality_score\": 0.0-1.0}}\n" +
                        $"Goal: {truncatedGoal}\nOutput: {truncatedOutput}";
        return await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a single step from the plan using LLM-based tool selection.
    /// </summary>
    private async Task<Result<string, string>> ExecuteStepAsync(string step, CancellationToken ct)
    {
        double toolWeight = _atom.GetStrategyWeight("ToolVsLLMWeight", 0.7);

        if (toolWeight < 0.3)
        {
            try
            {
                string llmResponse = await _llm.GenerateTextAsync($"Process this step: {step}", ct).ConfigureAwait(false);
                return Result<string, string>.Success(llmResponse);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Result<string, string>.Failure($"LLM processing failed: {ex.Message}");
            }
        }

        ToolSelection? selection = await _toolSelector.SelectToolAsync(step, ct).ConfigureAwait(false);

        if (selection != null)
        {
            ITool? tool = _tools.All.FirstOrDefault(t => t.Name.Equals(selection.ToolName, StringComparison.OrdinalIgnoreCase));

            if (tool != null)
            {
                SafetyCheckResult safetyCheck = await _safety.CheckActionSafetyAsync(
                    tool.Name,
                    new Dictionary<string, object> { ["step"] = step, ["arguments"] = selection.ArgumentsJson },
                    context: null,
                    ct).ConfigureAwait(false);

                if (!safetyCheck.IsAllowed)
                {
                    return Result<string, string>.Failure($"Safety violation: {safetyCheck.Reason}");
                }

                return await tool.InvokeAsync(selection.ArgumentsJson, ct).ConfigureAwait(false);
            }
        }

        Option<ITool> toolOption = _tools.All
            .FirstOrDefault(t => step.Contains(t.Name, StringComparison.OrdinalIgnoreCase))
            .ToOption();

        if (toolOption.HasValue && toolOption.Value != null)
        {
            ITool tool = toolOption.Value;

            SafetyCheckResult safetyCheck = await _safety.CheckActionSafetyAsync(
                tool.Name,
                new Dictionary<string, object> { ["step"] = step },
                context: null,
                ct).ConfigureAwait(false);

            if (!safetyCheck.IsAllowed)
            {
                return Result<string, string>.Failure($"Safety violation: {safetyCheck.Reason}");
            }

            return await tool.InvokeAsync(step, ct).ConfigureAwait(false);
        }

        string response = await _llm.GenerateTextAsync($"Process this step: {step}", ct).ConfigureAwait(false);
        return Result<string, string>.Success(response);
    }
}
