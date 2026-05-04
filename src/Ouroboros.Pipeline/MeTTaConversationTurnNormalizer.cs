// <copyright file="MeTTaConversationTurnNormalizer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Abstractions.Chat;
using Ouroboros.Core.Memory;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Pipeline;

/// <summary>
/// MeTTa-driven implementation of <see cref="IConversationTurnNormalizer"/>.
/// Reads voice traits and antipatterns from the embedded
/// <c>iaret-voice-contract.metta</c> seed (12 atoms), seeds them into the
/// MeTTa engine on first use, and asks <see cref="IChatRoleClient"/> to
/// rewrite recalled turns so they land in voice for the active LLM mode.
/// Normalized rewrites are cached LRU by (content-hash, mode) so repeated
/// retrievals are free.
/// </summary>
/// <remarks>
/// <para>
/// Active LLM mode is resolved env-first via <c>OUROBOROS_LLM_MODE</c>
/// (set by <c>Program.cs</c> at CLI startup). When no mode is set the
/// normalizer is a no-op and returns the input turn unchanged.
/// </para>
/// <para>
/// Cache: in-process LRU, capacity 1000. Misses cost one
/// <see cref="IChatRoleClient"/> call; hits cost a dictionary lookup. The
/// normalizer also short-circuits without consulting the cache when the
/// input turn is already <c>NormalizedByModel == active</c> within
/// <see cref="FreshnessWindow"/>.
/// </para>
/// </remarks>
public sealed class MeTTaConversationTurnNormalizer : IConversationTurnNormalizer
{
    private const int CacheCapacity = 1000;
    private static readonly TimeSpan FreshnessWindow = TimeSpan.FromDays(7);

    /// <summary>
    /// Matches MeTTa seed atoms of shape <c>(CoreVoice Iaret &lt;trait&gt;)</c>.
    /// Trait names are kebab-case symbols (no quotes, no parens).
    /// </summary>
    private static readonly Regex CoreVoiceRegex = new(
        @"^\s*\(\s*CoreVoice\s+Iaret\s+([\w\-]+)\s*\)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Matches MeTTa seed atoms of shape <c>(Avoid Iaret &lt;antipattern&gt;)</c>.
    /// </summary>
    private static readonly Regex AvoidRegex = new(
        @"^\s*\(\s*Avoid\s+Iaret\s+([\w\-]+)\s*\)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IMeTTaEngine _engine;
    private readonly IChatRoleClient _chat;
    private readonly ILogger<MeTTaConversationTurnNormalizer> _logger;
    private readonly object _cacheLock = new();
    private readonly Dictionary<(string Hash, string Mode), string> _cache = new();
    private readonly LinkedList<(string Hash, string Mode)> _lru = new();

    private List<string>? _cachedTraits;
    private List<string>? _cachedAntipatterns;
    private int _voiceContractLoaded; // 0 = not loaded, 1 = loaded

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaConversationTurnNormalizer"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine for fact ingestion.</param>
    /// <param name="chat">The chat-role client used to rewrite turns.</param>
    /// <param name="logger">Optional logger; defaults to NullLogger.</param>
    public MeTTaConversationTurnNormalizer(
        IMeTTaEngine engine,
        IChatRoleClient chat,
        ILogger<MeTTaConversationTurnNormalizer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(chat);
        _engine = engine;
        _chat = chat;
        _logger = logger ?? NullLogger<MeTTaConversationTurnNormalizer>.Instance;
    }

    /// <inheritdoc />
    public async Task<ConversationTurn> NormalizeAsync(ConversationTurn turn, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(turn);

        var activeMode = ResolveActiveLlmMode();
        if (string.IsNullOrEmpty(activeMode))
        {
            return turn;
        }

        if (string.IsNullOrWhiteSpace(turn.AiResponse))
        {
            return turn;
        }

        // Fast path: turn is already normalized for active mode within freshness window.
        if (string.Equals(turn.NormalizedByModel, activeMode, StringComparison.OrdinalIgnoreCase)
            && turn.NormalizedAt.HasValue
            && DateTimeOffset.UtcNow - turn.NormalizedAt.Value < FreshnessWindow)
        {
            return turn;
        }

        await EnsureVoiceContractLoadedAsync(ct).ConfigureAwait(false);

        var traits = _cachedTraits ?? new List<string>();
        var antipatterns = _cachedAntipatterns ?? new List<string>();

        if (traits.Count == 0 && antipatterns.Count == 0)
        {
            _logger.LogDebug("Voice contract empty; skipping normalization for mode {Mode}.", activeMode);
            return turn;
        }

        var hash = HashContent(turn.AiResponse);
        if (TryGetCached(hash, activeMode, out var cached))
        {
            return turn with
            {
                AiResponse = cached,
                NormalizedByModel = activeMode,
                NormalizedAt = DateTimeOffset.UtcNow,
            };
        }

        var prompt = BuildRewritePrompt(turn.AiResponse, traits, antipatterns);
        ChatResponse response;
        try
        {
            response = await _chat
                .GetResponseAsync(prompt, options: null, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Normalizer must degrade gracefully — chat failure returns original turn.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Normalizer chat call failed; returning original turn.");
            return turn;
        }

        var rewritten = response?.Text?.Trim();
        if (string.IsNullOrEmpty(rewritten))
        {
            return turn;
        }

        StoreCached(hash, activeMode, rewritten);
        return turn with
        {
            AiResponse = rewritten,
            NormalizedByModel = activeMode,
            NormalizedAt = DateTimeOffset.UtcNow,
        };
    }

    private static string? ResolveActiveLlmMode()
    {
        // Env-first per spec; the CLI sets OUROBOROS_LLM_MODE at startup.
        var fromEnv = Environment.GetEnvironmentVariable("OUROBOROS_LLM_MODE")?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(fromEnv) ? null : fromEnv;
    }

    private static string HashContent(string aiResponse)
    {
        var bytes = Encoding.UTF8.GetBytes(aiResponse);
        var sha = SHA256.HashData(bytes);
        return Convert.ToHexString(sha, 0, 8); // 16 hex chars
    }

    private bool TryGetCached(string hash, string mode, out string value)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue((hash, mode), out var cached))
            {
                value = cached;
                _lru.Remove((hash, mode));
                _lru.AddFirst((hash, mode));
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private void StoreCached(string hash, string mode, string normalized)
    {
        lock (_cacheLock)
        {
            var key = (hash, mode);
            if (_cache.ContainsKey(key))
            {
                _cache[key] = normalized;
                _lru.Remove(key);
                _lru.AddFirst(key);
                return;
            }

            if (_cache.Count >= CacheCapacity)
            {
                var evict = _lru.Last!.Value;
                _lru.RemoveLast();
                _cache.Remove(evict);
            }

            _cache[key] = normalized;
            _lru.AddFirst(key);
        }
    }

    /// <summary>
    /// Loads the embedded <c>iaret-voice-contract.metta</c> seed exactly once,
    /// extracts traits + antipatterns by regex, and ingests each atom into the
    /// MeTTa engine for downstream rule-based reasoning.
    /// </summary>
    private async Task EnsureVoiceContractLoadedAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _voiceContractLoaded, 1, 0) != 0)
        {
            return; // Already loaded (or in flight) — second caller short-circuits.
        }

        try
        {
            var seedText = LoadVoiceContractResource();
            if (string.IsNullOrWhiteSpace(seedText))
            {
                _logger.LogWarning("iaret-voice-contract.metta resource not found in Ouroboros.Tools assembly.");
                _cachedTraits = new List<string>();
                _cachedAntipatterns = new List<string>();
                return;
            }

            var traits = new List<string>();
            var antipatterns = new List<string>();

            foreach (var rawLine in seedText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(';'))
                {
                    continue;
                }

                var coreMatch = CoreVoiceRegex.Match(line);
                if (coreMatch.Success)
                {
                    traits.Add(coreMatch.Groups[1].Value);
                }
                else
                {
                    var avoidMatch = AvoidRegex.Match(line);
                    if (avoidMatch.Success)
                    {
                        antipatterns.Add(avoidMatch.Groups[1].Value);
                    }
                }

                // Ingest each atom as a MeTTa fact so future rules can reason
                // over the contract. AddFactAsync is best-effort — a parse
                // failure on an unrecognized atom must not break normalization.
                try
                {
                    await _engine.AddFactAsync(line, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
#pragma warning disable CA1031 // Best-effort fact ingestion.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogDebug(ex, "MeTTa AddFactAsync failed for atom: {Atom}", line);
                }
            }

            _cachedTraits = traits.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _cachedAntipatterns = antipatterns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            _logger.LogInformation(
                "Iaret voice contract loaded: {TraitCount} traits, {AntipatternCount} antipatterns.",
                _cachedTraits.Count,
                _cachedAntipatterns.Count);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Exchange(ref _voiceContractLoaded, 0); // Allow retry on next call.
            throw;
        }
#pragma warning disable CA1031 // Defensive — voice contract failure must not crash recall.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Failed to load Iaret voice contract; normalization disabled.");
            _cachedTraits = new List<string>();
            _cachedAntipatterns = new List<string>();
        }
    }

    private static string LoadVoiceContractResource()
    {
        // The .metta seed is embedded into Ouroboros.Tools.dll with the
        // logical name "iaret-voice-contract.metta" (see Tools.csproj). We
        // reach the assembly via any IMeTTaEngine implementation type.
        var asm = typeof(IMeTTaEngine).Assembly;
        const string ResourceName = "iaret-voice-contract.metta";
        using var stream = asm.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            // Some build pipelines prefix with default namespace — try a fuzzy match.
            var match = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(ResourceName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return string.Empty;
            }

            using var alt = asm.GetManifestResourceStream(match);
            if (alt is null)
            {
                return string.Empty;
            }

            using var altReader = new StreamReader(alt, Encoding.UTF8);
            return altReader.ReadToEnd();
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string BuildRewritePrompt(string original, List<string> traits, List<string> antipatterns)
    {
        var sb = new StringBuilder();
        sb.AppendLine("System: You are a voice-fidelity rewriter. Rewrite the assistant turn below to honor these voice traits and avoid these antipatterns. Preserve all factual content, names, dates, and decisions verbatim — only change voice and phrasing. If the original is already in voice, return it unchanged.");
        sb.AppendLine();
        sb.AppendLine($"Voice traits: {string.Join(", ", traits)}");
        sb.AppendLine($"Antipatterns: {string.Join(", ", antipatterns)}");
        sb.AppendLine();
        sb.AppendLine($"Original: {original}");
        sb.AppendLine();
        sb.Append("Rewrite (voice only, no preamble):");
        return sb.ToString();
    }
}
