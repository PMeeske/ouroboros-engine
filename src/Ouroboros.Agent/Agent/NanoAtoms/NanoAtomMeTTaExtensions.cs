// <copyright file="NanoAtomMeTTaExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Monads;
using Unit = Ouroboros.Abstractions.Unit;

namespace Ouroboros.Agent.NanoAtoms;

/// <summary>
/// Extension methods for translating NanoOuroborosAtom runtime state into MeTTa symbolic atoms.
/// Follows the MeTTaRepresentation pattern from the MetaAI layer.
/// Enables symbolic reasoning over nano-atom pipelines via the MeTTa knowledge base.
/// </summary>
public static class NanoAtomMeTTaExtensions
{
    /// <summary>
    /// Translates a NanoOuroborosAtom's state into MeTTa atoms and adds them to the knowledge base.
    /// </summary>
    /// <param name="engine">The MeTTa engine.</param>
    /// <param name="atom">The NanoOuroborosAtom to translate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public static async Task<Result<Unit, string>> AddNanoAtomStateAsync(
        this IMeTTaEngine engine,
        NanoOuroborosAtom atom,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(atom);

        try
        {
            StringBuilder sb = new();
            string atomId = $"nano_{atom.AtomId:N}";

            sb.AppendLine($"(NanoAtomInstance \"{atomId}\")");
            sb.AppendLine($"(InPhase (NanoAtomInstance \"{atomId}\") {PhaseToMeTTa(atom.CurrentPhase)})");

            // Circuit breaker state
            string circuitState = atom.IsCircuitOpen ? "CircuitOpen" : "CircuitClosed";
            sb.AppendLine($"(AtomCircuit (NanoAtomInstance \"{atomId}\") {circuitState})");

            var result = await engine.AddFactAsync(sb.ToString(), ct).ConfigureAwait(false);
            return result.Map(_ => Unit.Value).MapError(_ => "Failed to add NanoAtom state to MeTTa");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Unit, string>.Failure($"NanoAtom MeTTa translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates a ThoughtFragment into MeTTa atoms.
    /// </summary>
    /// <param name="engine">The MeTTa engine.</param>
    /// <param name="fragment">The ThoughtFragment to translate.</param>
    /// <param name="streamId">Optional stream identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public static async Task<Result<Unit, string>> AddThoughtFragmentAsync(
        this IMeTTaEngine engine,
        ThoughtFragment fragment,
        string? streamId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(fragment);

        try
        {
            StringBuilder sb = new();
            string content = EscapeMeTTa(fragment.Content.Length > 200
                ? fragment.Content[..200] + "..."
                : fragment.Content);

            sb.AppendLine($"(Fragment \"{content}\" \"{fragment.Source}\" {fragment.EstimatedTokens})");

            // Add tier routing metadata
            string tier = fragment.PreferredTier switch
            {
                Providers.PathwayTier.Local => "LocalTier",
                Providers.PathwayTier.CloudLight => "CloudLightTier",
                Providers.PathwayTier.Specialized => "SpecializedTier",
                Providers.PathwayTier.CloudPremium => "CloudPremiumTier",
                _ => "LocalTier",
            };
            sb.AppendLine($"(PreferredTier (Fragment \"{content}\" \"{fragment.Source}\" {fragment.EstimatedTokens}) {tier})");

            // Link to stream if specified
            if (streamId != null)
            {
                sb.AppendLine($"(FlowsThrough (Fragment \"{content}\" \"{fragment.Source}\" {fragment.EstimatedTokens}) (Stream \"{streamId}\"))");
            }

            var result = await engine.AddFactAsync(sb.ToString(), ct).ConfigureAwait(false);
            return result.Map(_ => Unit.Value).MapError(_ => "Failed to add ThoughtFragment to MeTTa");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Unit, string>.Failure($"ThoughtFragment MeTTa translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates a DigestFragment into MeTTa atoms.
    /// </summary>
    /// <param name="engine">The MeTTa engine.</param>
    /// <param name="digest">The DigestFragment to translate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public static async Task<Result<Unit, string>> AddDigestFragmentAsync(
        this IMeTTaEngine engine,
        DigestFragment digest,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(digest);

        try
        {
            StringBuilder sb = new();
            string content = EscapeMeTTa(digest.Content.Length > 200
                ? digest.Content[..200] + "..."
                : digest.Content);
            string atomId = $"nano_{digest.SourceAtomId:N}";

            sb.AppendLine($"(Digest \"{content}\" \"{atomId}\" {digest.CompressionRatio:F2} {digest.Confidence:F2})");
            sb.AppendLine($"(Digests (NanoAtomInstance \"{atomId}\") (Digest \"{content}\" \"{atomId}\" {digest.CompressionRatio:F2} {digest.Confidence:F2}))");

            var result = await engine.AddFactAsync(sb.ToString(), ct).ConfigureAwait(false);
            return result.Map(_ => Unit.Value).MapError(_ => "Failed to add DigestFragment to MeTTa");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Unit, string>.Failure($"DigestFragment MeTTa translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates a ConsolidatedAction into MeTTa atoms.
    /// </summary>
    /// <param name="engine">The MeTTa engine.</param>
    /// <param name="action">The ConsolidatedAction to translate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public static async Task<Result<Unit, string>> AddConsolidatedActionAsync(
        this IMeTTaEngine engine,
        ConsolidatedAction action,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            StringBuilder sb = new();
            string content = EscapeMeTTa(action.Content.Length > 200
                ? action.Content[..200] + "..."
                : action.Content);

            sb.AppendLine($"(Action \"{content}\" \"{action.ActionType}\" {action.Confidence:F2})");
            sb.AppendLine($"; streams={action.StreamCount} digests={action.SourceDigests.Count} elapsed={action.ElapsedMs}ms");

            var result = await engine.AddFactAsync(sb.ToString(), ct).ConfigureAwait(false);
            return result.Map(_ => Unit.Value).MapError(_ => "Failed to add ConsolidatedAction to MeTTa");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<Unit, string>.Failure($"ConsolidatedAction MeTTa translation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Translates a NanoAtomPhase to its MeTTa representation.
    /// </summary>
    private static string PhaseToMeTTa(NanoAtomPhase phase) => phase switch
    {
        NanoAtomPhase.Idle => "NanoIdle",
        NanoAtomPhase.Receive => "NanoReceive",
        NanoAtomPhase.Process => "NanoProcess",
        NanoAtomPhase.Digest => "NanoDigest",
        NanoAtomPhase.Emit => "NanoEmit",
        _ => "NanoIdle",
    };

    /// <summary>
    /// Escapes special characters for MeTTa string literals.
    /// </summary>
    private static string EscapeMeTTa(string text) =>
        text.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
}
