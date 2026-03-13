// <copyright file="GrammarEvolutionStep.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.Pipeline.Grammar;

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;
using Microsoft.Extensions.Logging;

/// <summary>
/// A pipeline step that evolves an ANTLR grammar from a natural language description,
/// integrating with the Ouroboros pipeline's Kleisli arrow composition pattern.
/// </summary>
/// <remarks>
/// This step records grammar evolution events in the local C# AtomSpace for
/// observability and integration with the engine's existing neuro-symbolic
/// reasoning infrastructure. The actual grammar validation and correction
/// is delegated to the upstream Hyperon sidecar via the <see cref="AdaptiveParserPipeline"/>.
/// </remarks>
public sealed class GrammarEvolutionStep : IDisposable
{
    private readonly AdaptiveParserPipeline _pipeline;
    private readonly HyperonMeTTaEngine? _engine;
    private readonly ILogger<GrammarEvolutionStep>? _logger;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GrammarEvolutionStep"/> class.
    /// </summary>
    /// <param name="pipeline">The adaptive parser pipeline.</param>
    /// <param name="engine">Optional Hyperon engine for recording evolution events.</param>
    /// <param name="logger">Optional logger.</param>
    public GrammarEvolutionStep(
        AdaptiveParserPipeline pipeline,
        HyperonMeTTaEngine? engine = null,
        ILogger<GrammarEvolutionStep>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Creates a Kleisli arrow that evolves a grammar from a description and sample input.
    /// </summary>
    /// <param name="maxAttempts">Maximum evolution attempts.</param>
    /// <returns>A step function from (description, sampleInput) to CompiledGrammar.</returns>
    public Func<GrammarRequest, Task<CompiledGrammar>> CreateArrow(int maxAttempts = 5)
    {
        return async request =>
        {
            RecordEvent("GrammarEvolutionStarted", request.Description);

            try
            {
                var compiled = await _pipeline.EvolveGrammarAsync(
                    request.Description,
                    request.SampleInput,
                    maxAttempts);

                RecordEvent("GrammarEvolutionSucceeded", compiled.GrammarName);
                return compiled;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                RecordEvent("GrammarEvolutionFailed", ex.Message);
                throw;
            }
        };
    }

    /// <summary>
    /// Evolves a grammar directly from a request.
    /// </summary>
    /// <param name="request">The grammar evolution request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A compiled grammar ready for use.</returns>
    public async Task<CompiledGrammar> EvolveAsync(GrammarRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        RecordEvent("GrammarEvolutionStarted", request.Description);

        try
        {
            var compiled = await _pipeline.EvolveGrammarAsync(
                request.Description,
                request.SampleInput,
                request.MaxAttempts,
                ct);

            RecordEvent("GrammarEvolutionSucceeded", compiled.GrammarName);
            return compiled;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            RecordEvent("GrammarEvolutionFailed", ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pipeline.Dispose();
    }

    private void RecordEvent(string eventName, string details)
    {
        if (_engine == null) return;

        _engine.AddAtom(Atom.Expr(
            Atom.Sym(eventName),
            Atom.Sym(details),
            Atom.Sym(DateTime.UtcNow.Ticks.ToString())));
    }
}

/// <summary>
/// Request to evolve a grammar from a natural language description.
/// </summary>
/// <param name="Description">What the grammar should parse.</param>
/// <param name="SampleInput">Optional sample input for validation.</param>
/// <param name="MaxAttempts">Maximum evolution attempts (default 5).</param>
public sealed record GrammarRequest(
    string Description,
    string? SampleInput = null,
    int MaxAttempts = 5);
