// ==========================================================
// Neural-Symbolic Bridge Implementation
// Bridges neural (LLM) and symbolic (MeTTa) reasoning
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

using System.Diagnostics;
using Ouroboros.Agent.MetaAI;

/// <summary>
/// Implementation of neural-symbolic bridge for hybrid reasoning.
/// </summary>
public sealed partial class NeuralSymbolicBridge : INeuralSymbolicBridge
{
    private readonly Ouroboros.Abstractions.Core.IChatCompletionModel _llm;
    private readonly ISymbolicKnowledgeBase _knowledgeBase;
    private readonly ConfidenceConfig _confidenceConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="NeuralSymbolicBridge"/> class.
    /// </summary>
    /// <param name="llm">The language model for neural reasoning.</param>
    /// <param name="knowledgeBase">The symbolic knowledge base.</param>
    /// <param name="confidenceConfig">
    /// Optional confidence scoring configuration. When <c>null</c>, default heuristic values are used.
    /// </param>
    public NeuralSymbolicBridge(
        Ouroboros.Abstractions.Core.IChatCompletionModel llm,
        ISymbolicKnowledgeBase knowledgeBase,
        ConfidenceConfig? confidenceConfig = null)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        _knowledgeBase = knowledgeBase;
        _confidenceConfig = confidenceConfig ?? new ConfidenceConfig();
    }

    /// <inheritdoc/>
    public async Task<Result<List<SymbolicRule>, string>> ExtractRulesFromSkillAsync(
        Skill skill,
        CancellationToken ct = default)
    {
        if (skill == null)
            return Result<List<SymbolicRule>, string>.Failure("Skill cannot be null");

        try
        {
            // Build prompt for rule extraction
            var prompt = BuildRuleExtractionPrompt(skill);
            var response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

            // Parse rules from response
            var rules = ParseExtractedRules(response, skill);

            return Result<List<SymbolicRule>, string>.Success(rules);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<List<SymbolicRule>, string>.Failure($"Rule extraction failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<MeTTaExpression, string>> NaturalLanguageToMeTTaAsync(
        string naturalLanguage,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
            return Result<MeTTaExpression, string>.Failure("Natural language cannot be empty");

        try
        {
            var prompt = $@"Convert the following natural language statement to a MeTTa expression.
Use proper MeTTa syntax with S-expressions.

Natural language: {naturalLanguage}

MeTTa expression:";

            var response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            var expression = ParseMeTTaExpression(response);

            return Result<MeTTaExpression, string>.Success(expression);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<MeTTaExpression, string>.Failure($"Conversion failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> MeTTaToNaturalLanguageAsync(
        MeTTaExpression expression,
        CancellationToken ct = default)
    {
        if (expression == null)
            return Result<string, string>.Failure("Expression cannot be null");

        try
        {
            var prompt = $@"Explain the following MeTTa expression in clear natural language:

MeTTa: {expression.RawExpression}

Natural language explanation:";

            var response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            var explanation = response.Trim();

            return Result<string, string>.Success(explanation);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Explanation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ReasoningResult, string>> HybridReasonAsync(
        string query,
        ReasoningMode mode = ReasoningMode.SymbolicFirst,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Result<ReasoningResult, string>.Failure("Query cannot be empty");

        var startTime = Stopwatch.StartNew();
        var steps = new List<ReasoningStep>();
        bool symbolicSucceeded = false;
        bool neuralSucceeded = false;
        string answer = string.Empty;
        double confidence = 0.0;

        try
        {
            switch (mode)
            {
                case ReasoningMode.SymbolicFirst:
                    // Try symbolic reasoning first
                    var symbolicResult = await TrySymbolicReasoning(query, ct).ConfigureAwait(false);
                    if (symbolicResult.IsSuccess)
                    {
                        symbolicSucceeded = true;
                        answer = symbolicResult.Value.answer;
                        confidence = symbolicResult.Value.confidence;
                        steps.AddRange(symbolicResult.Value.steps);
                    }
                    else
                    {
                        // Fall back to neural
                        var neuralResult = await TryNeuralReasoning(query, ct).ConfigureAwait(false);
                        neuralSucceeded = neuralResult.IsSuccess;
                        answer = neuralResult.IsSuccess ? neuralResult.Value.answer : query;
                        confidence = neuralResult.IsSuccess ? neuralResult.Value.confidence : 0.0;
                        if (neuralResult.IsSuccess)
                            steps.AddRange(neuralResult.Value.steps);
                    }
                    break;

                case ReasoningMode.NeuralFirst:
                    // Try neural first, verify with symbolic
                    var neuralFirst = await TryNeuralReasoning(query, ct).ConfigureAwait(false);
                    neuralSucceeded = neuralFirst.IsSuccess;
                    if (neuralFirst.IsSuccess)
                    {
                        answer = neuralFirst.Value.answer;
                        steps.AddRange(neuralFirst.Value.steps);
                        
                        // Try to verify symbolically
                        var verification = await TrySymbolicReasoning(query, ct).ConfigureAwait(false);
                        symbolicSucceeded = verification.IsSuccess;
                        confidence = symbolicSucceeded
                            ? _confidenceConfig.SymbolicVerifiedNeural
                            : _confidenceConfig.UnverifiedNeural;
                    }
                    break;

                case ReasoningMode.Parallel:
                    // Run both in parallel
                    var symbolicTask = TrySymbolicReasoning(query, ct);
                    var neuralTask = TryNeuralReasoning(query, ct);
                    await Task.WhenAll(symbolicTask, neuralTask).ConfigureAwait(false);
                    
                    var symbolicParallel = await symbolicTask.ConfigureAwait(false);
                    var neuralParallel = await neuralTask.ConfigureAwait(false);
                    
                    symbolicSucceeded = symbolicParallel.IsSuccess;
                    neuralSucceeded = neuralParallel.IsSuccess;
                    
                    if (symbolicSucceeded && neuralSucceeded)
                    {
                        // Combine results
                        answer = $"Symbolic: {symbolicParallel.Value.answer}\nNeural: {neuralParallel.Value.answer}";
                        confidence = _confidenceConfig.ParallelAgreement;
                        steps.AddRange(symbolicParallel.Value.steps);
                        steps.AddRange(neuralParallel.Value.steps);
                    }
                    else if (symbolicSucceeded)
                    {
                        answer = symbolicParallel.Value.answer;
                        confidence = symbolicParallel.Value.confidence;
                        steps.AddRange(symbolicParallel.Value.steps);
                    }
                    else if (neuralSucceeded)
                    {
                        answer = neuralParallel.Value.answer;
                        confidence = neuralParallel.Value.confidence;
                        steps.AddRange(neuralParallel.Value.steps);
                    }
                    break;

                case ReasoningMode.SymbolicOnly:
                    var symbolicOnly = await TrySymbolicReasoning(query, ct).ConfigureAwait(false);
                    symbolicSucceeded = symbolicOnly.IsSuccess;
                    if (symbolicOnly.IsSuccess)
                    {
                        answer = symbolicOnly.Value.answer;
                        confidence = symbolicOnly.Value.confidence;
                        steps.AddRange(symbolicOnly.Value.steps);
                    }
                    break;

                case ReasoningMode.NeuralOnly:
                    var neuralOnly = await TryNeuralReasoning(query, ct).ConfigureAwait(false);
                    neuralSucceeded = neuralOnly.IsSuccess;
                    if (neuralOnly.IsSuccess)
                    {
                        answer = neuralOnly.Value.answer;
                        confidence = neuralOnly.Value.confidence;
                        steps.AddRange(neuralOnly.Value.steps);
                    }
                    break;
            }

            startTime.Stop();

            var result = new ReasoningResult(
                query,
                answer,
                mode,
                steps,
                confidence,
                symbolicSucceeded,
                neuralSucceeded,
                startTime.Elapsed);

            return Result<ReasoningResult, string>.Success(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<ReasoningResult, string>.Failure($"Hybrid reasoning failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ConsistencyReport, string>> CheckConsistencyAsync(
        MetaAI.Hypothesis hypothesis,
        IReadOnlyList<SymbolicRule> knowledgeBase,
        CancellationToken ct = default)
    {
        if (hypothesis == null)
            return Result<ConsistencyReport, string>.Failure("Hypothesis cannot be null");

        try
        {
            var conflicts = new List<LogicalConflict>();
            var missing = new List<string>();
            var suggestions = new List<string>();

            // Build prompt for consistency checking
            var prompt = BuildConsistencyCheckPrompt(hypothesis, knowledgeBase);
            var response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);

            // Parse consistency analysis
            var (isConsistent, parsedConflicts, parsedMissing) = ParseConsistencyAnalysis(response, knowledgeBase);
            conflicts.AddRange(parsedConflicts);
            missing.AddRange(parsedMissing);

            if (!isConsistent)
            {
                suggestions.Add("Resolve logical conflicts between hypothesis and existing rules");
                suggestions.Add("Add missing prerequisites to the knowledge base");
            }

            var score = isConsistent ? 1.0 : Math.Max(0.0, 1.0 - (conflicts.Count * _confidenceConfig.ConflictPenalty));

            var report = new ConsistencyReport(
                isConsistent,
                conflicts,
                missing,
                suggestions,
                score);

            return Result<ConsistencyReport, string>.Success(report);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<ConsistencyReport, string>.Failure($"Consistency check failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<GroundedConcept, string>> GroundConceptAsync(
        string conceptDescription,
        float[] embedding,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conceptDescription))
            return Result<GroundedConcept, string>.Failure("Concept description cannot be empty");

        if (embedding == null || embedding.Length == 0)
            return Result<GroundedConcept, string>.Failure("Embedding cannot be null or empty");

        try
        {
            var prompt = $@"Given the concept: {conceptDescription}

Provide:
1. A MeTTa type for this concept
2. Key properties (list 3-5)
3. Relations to other concepts (list 2-3)

Format your response as:
Type: <type>
Properties: <prop1>, <prop2>, ...
Relations: <rel1>, <rel2>, ...";

            var response = await _llm.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            var (mettaType, properties, relations) = ParseGroundingResponse(response);

            var concept = new GroundedConcept(
                conceptDescription,
                mettaType,
                properties,
                relations,
                embedding,
                _confidenceConfig.BaseGrounding);

            return Result<GroundedConcept, string>.Success(concept);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<GroundedConcept, string>.Failure($"Concept grounding failed: {ex.Message}");
        }
    }

}
