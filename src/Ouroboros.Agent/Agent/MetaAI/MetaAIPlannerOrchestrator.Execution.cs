
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ouroboros.Agent.MetaAI;

public sealed partial class MetaAIPlannerOrchestrator
{
    /// <summary>
    /// Executes a plan step by step with monitoring and safety checks.
    /// Supports optional parallel execution of independent steps.
    /// </summary>
    public async Task<Result<PlanExecutionResult, string>> ExecuteAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        if (plan == null)
            return Result<PlanExecutionResult, string>.Failure("Plan cannot be null");

        Stopwatch stopwatch = Stopwatch.StartNew();

        // Check if parallel execution is beneficial
        ParallelExecutor parallelExecutor = new ParallelExecutor(_safety, ExecuteStepAsync);
        double estimatedSpeedup = parallelExecutor.EstimateSpeedup(plan);

        List<StepResult> stepResults;
        bool overallSuccess;
        string finalOutput;

        try
        {
            // Use parallel execution if speedup is significant
            if (estimatedSpeedup > 1.5)
            {
                (stepResults, overallSuccess, finalOutput) = await parallelExecutor.ExecuteParallelAsync(plan, ct);
            }
            else
            {
                // Sequential execution
                stepResults = new List<StepResult>();
                overallSuccess = true;
                finalOutput = "";

                foreach (PlanStep step in plan.Steps)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    // Apply safety sandbox
                    PlanStep sandboxedStep = _safety.SandboxStep(step);

                    // Execute step
                    StepResult stepResult = await ExecuteStepAsync(sandboxedStep, ct);
                    stepResults.Add(stepResult);

                    if (!stepResult.Success)
                    {
                        overallSuccess = false;
                        // Continue with remaining steps even if one fails
                    }

                    finalOutput += stepResult.Output + "\n";
                }

                finalOutput = finalOutput.Trim();
            }

            stopwatch.Stop();

            PlanExecutionResult execution = new PlanExecutionResult(
                plan,
                stepResults,
                overallSuccess,
                finalOutput,
                new Dictionary<string, object>
                {
                    ["steps_completed"] = stepResults.Count,
                    ["steps_total"] = plan.Steps.Count,
                    ["parallel_execution"] = estimatedSpeedup > 1.5,
                    ["estimated_speedup"] = estimatedSpeedup
                },
                stopwatch.Elapsed);

            RecordMetric("executor", stopwatch.Elapsed.TotalMilliseconds, overallSuccess);
            return Result<PlanExecutionResult, string>.Success(execution);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordMetric("executor", stopwatch.Elapsed.TotalMilliseconds, false);
            return Result<PlanExecutionResult, string>.Failure($"Execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies execution results and provides feedback for improvement.
    /// </summary>
    public async Task<Result<PlanVerificationResult, string>> VerifyAsync(
        PlanExecutionResult execution,
        CancellationToken ct = default)
    {
        if (execution == null)
            return Result<PlanVerificationResult, string>.Failure("Execution cannot be null");

        try
        {
            // Build verification prompt
            string verifyPrompt = BuildVerificationPrompt(execution);
            string verificationText = await _llm.GenerateTextAsync(verifyPrompt, ct);

            // Parse verification result
            PlanVerificationResult verification = ParseVerification(execution, verificationText);

            RecordMetric("verifier", 1.0, verification.Verified);
            return Result<PlanVerificationResult, string>.Success(verification);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            RecordMetric("verifier", 1.0, false);
            return Result<PlanVerificationResult, string>.Failure($"Verification failed: {ex.Message}");
        }
    }

    private async Task<StepResult> ExecuteStepAsync(PlanStep step, CancellationToken ct)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate step action is not empty
            if (string.IsNullOrWhiteSpace(step.Action))
            {
                stopwatch.Stop();
                return new StepResult(
                    step,
                    false,
                    "",
                    "Step action/tool name cannot be empty",
                    stopwatch.Elapsed,
                    new Dictionary<string, object> { ["error"] = "empty_action" });
            }

            // Check if this is a tool invocation
            ITool? tool = _tools.Get(step.Action);
            if (tool != null)
            {
                string args = JsonSerializer.Serialize(step.Parameters);
                Result<string, string> toolResult = await tool.InvokeAsync(args);

                stopwatch.Stop();

                return toolResult.Match(
                    output => new StepResult(
                        step,
                        true,
                        output,
                        null,
                        stopwatch.Elapsed,
                        new Dictionary<string, object>
                        {
                            ["tool"] = step.Action,
                            ["success"] = true
                        }),
                    error => new StepResult(
                        step,
                        false,
                        "",
                        error,
                        stopwatch.Elapsed,
                        new Dictionary<string, object>
                        {
                            ["tool"] = step.Action,
                            ["success"] = false
                        }));
            }

            // If not a tool, try to execute as LLM task
            string prompt = $"Execute the following task: {step.Action}\nParameters: {JsonSerializer.Serialize(step.Parameters)}";
            string output = await _llm.GenerateTextAsync(prompt, ct);

            stopwatch.Stop();

            return new StepResult(
                step,
                true,
                output,
                null,
                stopwatch.Elapsed,
                new Dictionary<string, object>
                {
                    ["type"] = "llm_task"
                });
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new StepResult(
                step,
                false,
                "",
                ex.Message,
                stopwatch.Elapsed,
                new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
    }

    private string BuildVerificationPrompt(PlanExecutionResult execution)
    {
        return $@"Verify the following execution result:

GOAL: {execution.Plan.Goal}

PLAN:
{string.Join("\n", execution.Plan.Steps.Select((s, i) => $"{i + 1}. {s.Action}"))}

EXECUTION RESULTS:
{string.Join("\n", execution.StepResults.Select((r, i) =>
    $"{i + 1}. {(r.Success ? "\u2713" : "\u2717")} {r.Output.Substring(0, Math.Min(100, r.Output.Length))}"))}

FINAL OUTPUT:
{execution.FinalOutput}

Please verify if:
1. The goal was accomplished
2. All steps executed correctly
3. The output quality is acceptable

Provide:
- VERIFIED: yes/no
- QUALITY_SCORE: 0-1
- ISSUES: (list any problems)
- IMPROVEMENTS: (list suggestions)
- REVISED_PLAN: (if needed)
";
    }

    private PlanVerificationResult ParseVerification(PlanExecutionResult execution, string verificationText)
    {
        bool verified = verificationText.Contains("VERIFIED: yes", StringComparison.OrdinalIgnoreCase);

        // Extract quality score
        double qualityScore = 0.7;
        Match qualityMatch = QualityScoreRegex().Match(verificationText);
        if (qualityMatch.Success && double.TryParse(qualityMatch.Groups[1].Value, out double score))
        {
            qualityScore = score;
        }

                List<string> issues = new List<string>();
        List<string> improvements = new List<string>();

        // Simple parsing - in production use more robust approach
        return new PlanVerificationResult(
            Execution: execution,
            Verified: verified,
            QualityScore: qualityScore,
            Issues: issues,
            Improvements: improvements,
            Timestamp: DateTime.UtcNow);
    }

    [GeneratedRegex(@"QUALITY_SCORE:\s*([0-9.]+)")]
    private static partial Regex QualityScoreRegex();
}
