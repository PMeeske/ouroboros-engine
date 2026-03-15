// <copyright file="AzureNeuralTtsService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Microsoft.CognitiveServices.Speech;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Azure Cognitive Services Neural TTS service.
/// Provides high-quality, natural-sounding voices including Jenny (Cortana-like).
/// Implements streaming TTS for real-time voice synthesis.
/// </summary>
public sealed partial class AzureNeuralTtsService : IStreamingTtsService, IDisposable
{
    private volatile bool _isSynthesizing;
    private CancellationTokenSource? _currentSynthesisCts;
    private readonly SemaphoreSlim _speechLock = new(1, 1);
    private readonly string _subscriptionKey;
    private readonly string _region;
    private readonly string _persona;
    private string _voiceName;
    private string _culture;
    private SpeechSynthesizer? _synthesizer;
    private SpeechConfig? _config;
    private bool _disposed;

    // Lazy Edge TTS fallback (Jenny voice) — shared across all Azure TTS instances.
    // Activated only when the Azure circuit breaker opens or Azure TTS throws.
    private static readonly Lazy<EdgeTtsService> EdgeTtsFallback = new(
        () => new EdgeTtsService(EdgeTtsService.Voices.JennyNeural),
        LazyThreadSafetyMode.ExecutionAndPublication);

    // Circuit breaker state - shared across all instances for global rate limit protection
    private static readonly ResiliencePipeline CircuitBreakerPipeline = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 2,
            BreakDuration = TimeSpan.FromSeconds(60),
            OnOpened = args =>
            {
                IsCircuitOpen = true;
                System.Diagnostics.Trace.TraceWarning("[TTS] Circuit OPEN - Azure TTS disabled for 60s due to rate limiting");
                return default;
            },
            OnClosed = args =>
            {
                IsCircuitOpen = false;
                System.Diagnostics.Trace.TraceInformation("[TTS] Circuit CLOSED - Azure TTS re-enabled");
                return default;
            },
            OnHalfOpened = args =>
            {
                System.Diagnostics.Trace.TraceInformation("[TTS] Circuit HALF-OPEN - Testing Azure TTS...");
                return default;
            },
        })
        .Build();

    // Polly retry policy for 429 rate limiting with exponential backoff
    private static readonly ResiliencePipeline RetryPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = args =>
            {
                System.Diagnostics.Trace.TraceWarning("[TTS] Rate limited, retrying in {0:F1}s (attempt {1}/3)...", args.RetryDelay.TotalSeconds, args.AttemptNumber + 1);
                return default;
            },
        })
        .Build();

    /// <summary>
    /// Gets whether the circuit breaker is currently open (Azure TTS disabled).
    /// </summary>
    public static bool IsCircuitOpen { get; private set; }

    /// <summary>
    /// Raised after every successful synthesis with the raw audio data (WAV format).
    /// Subscribers can use this to feed an FFT-based voice fingerprint detector
    /// for self-echo suppression in ambient microphone capture.
    /// </summary>
    public event Action<byte[]>? OnAudioSynthesized;

    /// <summary>
    /// Gets or sets the culture for voice selection and language.
    /// </summary>
    public string Culture
    {
        get => _culture;
        set
        {
            _culture = value;
            UpdateVoiceForCulture();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureNeuralTtsService"/> class.
    /// </summary>
    /// <param name="subscriptionKey">Azure Speech subscription key.</param>
    /// <param name="region">Azure region (e.g., "eastus").</param>
    /// <param name="persona">Persona name for voice selection.</param>
    /// <param name="culture">Culture for language selection (e.g., "de-DE").</param>
    public AzureNeuralTtsService(string subscriptionKey, string region, string persona = "Ouroboros", string? culture = null)
    {
        ArgumentNullException.ThrowIfNull(subscriptionKey);
        ArgumentNullException.ThrowIfNull(region);
        _subscriptionKey = subscriptionKey;
        _region = region;
        _persona = persona;
        _culture = culture ?? "en-US";

        // Select voice based on persona and culture
        _voiceName = SelectVoice(_persona, _culture);

        InitializeSynthesizer();
    }

    private static string SelectVoice(string persona, string culture)
    {
        bool isGerman = culture.StartsWith("de", StringComparison.OrdinalIgnoreCase);

        return persona.ToUpperInvariant() switch
        {
            // Iaret uses en-US-JennyMultilingualNeural (Cortana voice) for all languages.
            // Cross-lingual synthesis is triggered by <lang xml:lang='xx-XX'> in BuildSsml.
            "IARET" => "en-US-JennyMultilingualNeural",
            "OUROBOROS" => isGerman ? "de-DE-KatjaNeural" : "en-US-JennyNeural",
            "ARIA" => isGerman ? "de-DE-AmalaNeural" : "en-US-AriaNeural",
            "ECHO" => isGerman ? "de-DE-LouisaNeural" : "en-GB-SoniaNeural",
            "SAGE" => isGerman ? "de-DE-ElkeNeural" : "en-US-SaraNeural",
            "ATLAS" => isGerman ? "de-DE-ConradNeural" : "en-US-GuyNeural",
            _ => isGerman ? "de-DE-KatjaNeural" : "en-US-JennyNeural"
        };
    }

    private void UpdateVoiceForCulture()
    {
        // Select the culture-appropriate voice for the stored persona.
        // For IARET this always stays en-US-AvaMultilingualNeural (cross-lingual via <lang>).
        // For other personas this picks the locale-specific voice (e.g. de-DE-KatjaNeural).
        _voiceName = SelectVoice(_persona, _culture);

        // Reinitialize synthesizer with the new voice and updated culture.
        _synthesizer?.Dispose();
        InitializeSynthesizer();
    }

    /// <summary>
    /// Extracts the primary BCP-47 locale from a voice name.
    /// E.g. "en-US-AvaMultilingualNeural" → "en-US", "de-DE-KatjaNeural" → "de-DE".
    /// </summary>
    private static string VoicePrimaryLocale(string voiceName)
    {
        // Voice names follow the pattern: {locale}-{VoiceName}, e.g. en-US-Ava...
        // The locale is always the first two dash-separated segments.
        var parts = voiceName.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : "en-US";
    }

    /// <summary>
    /// The voice's primary locale for the SSML &lt;speak&gt; element.
    /// For cross-lingual synthesis the &lt;speak&gt; element must carry the voice's
    /// OWN locale (e.g. en-US for AvaMultilingualNeural); the target language is
    /// declared on the inner &lt;lang&gt; element instead.
    /// </summary>
    private string SpeakLang => VoicePrimaryLocale(_voiceName);

    /// <summary>
    /// Builds an SSML document for the given text, style, and target culture.
    /// Centralises all language/payload logic — the single source of truth for SSML construction.
    /// <para>
    /// Cross-lingual synthesis (e.g. en-US-AvaMultilingualNeural speaking German) is triggered
    /// via a &lt;lang xml:lang='de-DE'&gt; element inside &lt;voice&gt; — the Azure-documented
    /// format for cross-lingual neural voices. The caller may supply an explicit
    /// <paramref name="cultureOverride"/> to drive language without mutating service state.
    /// </para>
    /// </summary>
    /// <param name="text">Text to synthesise.</param>
    /// <param name="isWhisper">Use whispering style.</param>
    /// <param name="cultureOverride">Override culture for this utterance only (e.g. "de-DE").</param>
    /// <param name="rate">Speed multiplier (1.0 = normal).</param>
    /// <summary>
    /// Current emotional speech style (e.g. "cheerful", "sad", "excited").
    /// Set externally by the personality/embodiment system.
    /// When set, overrides the default "assistant" style in SSML.
    /// </summary>
    public string? EmotionalStyle { get; set; }

    /// <summary>
    /// Pitch offset from the SelfVector (-0.2 to +0.2). Overrides default when set.
    /// </summary>
    public float? SelfVectorPitchOffset { get; set; }

    /// <summary>
    /// Rate multiplier from the SelfVector (0.7 to 1.4). Overrides default when set.
    /// </summary>
    public float? SelfVectorRateMultiplier { get; set; }

    private string BuildSsml(string text, bool isWhisper, string? cultureOverride = null, double rate = 1.0)
    {
        var escaped     = System.Security.SecurityElement.Escape(text);
        string culture  = cultureOverride ?? _culture;
        string voiceLoc = SpeakLang;                              // voice's own primary locale

        bool isEnglish     = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        bool isCrossLingual = voiceLoc.Length >= 2 && culture.Length >= 2
            && !string.Equals(voiceLoc[..2], culture[..2], StringComparison.OrdinalIgnoreCase);

        int normalRate  = SelfVectorRateMultiplier.HasValue
            ? (int)((SelfVectorRateMultiplier.Value - 1.0f) * 50)
            : -5 + (int)((rate - 1.0) * 50);
        int whisperRate = -8 + (int)((rate - 1.0) * 50);

        string content;
        if (isWhisper)
        {
            // Whispering style + optional cross-lingual wrapper.
            var inner = isCrossLingual ? $"<lang xml:lang='{culture}'>{escaped}</lang>" : escaped;
            content = $"<mstts:express-as style='whispering' styledegree='0.6'>"
                    + $"<prosody rate='{whisperRate:+0;-0;0}%' pitch='+3%' volume='-15%'>{inner}</prosody>"
                    + $"</mstts:express-as>";
        }
        else if (isEnglish)
        {
            // Emotional style or default Cortana-style assistant
            var style = EmotionalStyle ?? "assistant";
            var degree = EmotionalStyle != null ? "1.5" : "1.2";
            var pitchStr = SelfVectorPitchOffset.HasValue
                ? $"{(int)(SelfVectorPitchOffset.Value * 100):+0;-0;0}%"
                : "+5%";
            content = $"<mstts:express-as style='{style}' styledegree='{degree}'>"
                    + $"<prosody rate='{normalRate:+0;-0;0}%' pitch='{pitchStr}'>{escaped}</prosody>"
                    + $"</mstts:express-as>";
        }
        else if (isCrossLingual)
        {
            // Cross-lingual non-English: <lang> element selects the target language
            // from a multilingual voice (e.g. AvaMultilingualNeural → German).
            content = $"<lang xml:lang='{culture}'>"
                    + $"<prosody rate='{normalRate:+0;-0;0}%' pitch='+5%' volume='+5%'>{escaped}</prosody>"
                    + $"</lang>";
        }
        else
        {
            // Native-voice non-English (e.g. de-DE-KatjaNeural): plain prosody.
            content = $"<prosody rate='{normalRate:+0;-0;0}%' pitch='+5%' volume='+5%'>{escaped}</prosody>";
        }

        return $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' "
             + $"xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{voiceLoc}'>"
             + $"<voice name='{_voiceName}'>{content}</voice>"
             + $"</speak>";
    }

    /// <summary>
    /// Builds a multi-segment SSML document where each segment can have its own
    /// express-as style and prosody. Used by VoiceAnnotatedText for inline voice markers.
    /// </summary>
    /// <param name="segments">Voice segments with per-segment style/prosody overrides.</param>
    /// <param name="cultureOverride">Override culture for this utterance only.</param>
    public string BuildMultiSegmentSsml(
        IReadOnlyList<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)> segments,
        string? cultureOverride = null)
    {
        string culture  = cultureOverride ?? _culture;
        string voiceLoc = SpeakLang;
        bool isCrossLingual = voiceLoc.Length >= 2 && culture.Length >= 2
            && !string.Equals(voiceLoc[..2], culture[..2], StringComparison.OrdinalIgnoreCase);

        var sb = new System.Text.StringBuilder();
        foreach (var (text, style, pitchOff, rateMul) in segments)
        {
            // Break segments contain raw SSML (e.g. <break time='500ms'/>)
            if (string.Equals(style, "break", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(text); // Already SSML — don't escape
                continue;
            }

            var escaped = System.Security.SecurityElement.Escape(text);
            if (string.IsNullOrWhiteSpace(escaped)) continue;

            var inner = isCrossLingual ? $"<lang xml:lang='{culture}'>{escaped}</lang>" : escaped;

            int ratePercent = pitchOff.HasValue || rateMul.HasValue
                ? (int)(((rateMul ?? 1.0f) - 1.0f) * 50) - 5
                : -5;
            string pitchStr = pitchOff.HasValue ? $"{pitchOff.Value * 100:+0;-0;0}%" : "+5%";

            // Map voice marker styles to Azure TTS express-as styles
            var segStyle = MapToAzureStyle(style) ?? EmotionalStyle ?? "assistant";
            var degree = style != null || EmotionalStyle != null ? "1.5" : "1.2";

            sb.Append($"<mstts:express-as style='{segStyle}' styledegree='{degree}'>");
            sb.Append($"<prosody rate='{ratePercent:+0;-0;0}%' pitch='{pitchStr}'>{inner}</prosody>");
            sb.Append("</mstts:express-as>");
        }

        return $"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' "
             + $"xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='{voiceLoc}'>"
             + $"<voice name='{_voiceName}'>{sb}</voice>"
             + $"</speak>";
    }

    /// <summary>
    /// Maps voice marker style names to Azure Neural TTS express-as styles.
    /// Jenny/AvaMultilingual support: cheerful, sad, angry, excited, friendly,
    /// hopeful, shouting, terrified, unfriendly, whispering, chat.
    /// </summary>
    private static string? MapToAzureStyle(string? style) => style?.ToLowerInvariant() switch
    {
        "whisper" or "whispering" => "whispering",
        "excited" => "excited",
        "cheerful" or "happy" => "cheerful",
        "sad" or "melancholy" => "sad",
        "gentle" or "tender" => "friendly",
        "emphasis" => "chat",
        "sing" or "lyrical" => "poetry-reading",
        _ => null
    };

    private void InitializeSynthesizer()
    {
        _config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
        _config.SpeechSynthesisVoiceName = _voiceName;
        // Do NOT set SpeechSynthesisLanguage — SSML xml:lang controls language.
        // Setting it to a different locale than the voice's primary can conflict.
        _synthesizer = new SpeechSynthesizer(_config);
    }

    /// <inheritdoc/>
    public string ProviderName => "Azure Neural TTS";

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableVoices => new[]
    {
        "en-US-JennyMultilingualNeural",
        "en-US-JennyNeural",
        "en-US-AriaNeural",
        "en-US-GuyNeural",
        "en-US-SaraNeural",
        "en-GB-SoniaNeural",
        "de-DE-KatjaNeural",
        "de-DE-ConradNeural",
        "de-DE-AmalaNeural",
        "de-DE-ElkeNeural",
        "de-DE-LouisaNeural",
    };

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => new[] { "wav", "mp3", "ogg" };

    /// <inheritdoc/>
    public int MaxInputLength => 10000;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(!string.IsNullOrEmpty(_subscriptionKey) && !string.IsNullOrEmpty(_region));

    /// <summary>
    /// Stops any currently playing speech.
    /// </summary>
    public async Task StopSpeakingAsync()
    {
        if (_isSynthesizing && _synthesizer != null)
        {
            try
            {
                _currentSynthesisCts?.Cancel();
                await _synthesizer.StopSpeakingAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Ignore errors during stop
            }
        }
    }

    /// <summary>
    /// Speaks text to the default audio output using the service's current culture.
    /// </summary>
    public Task SpeakAsync(string text, CancellationToken ct = default)
        => SpeakCoreAsync(text, isWhisper: false, cultureOverride: null, ct);

    /// <summary>
    /// Speaks text to the default audio output in an explicitly specified culture,
    /// without mutating the service's default culture state.
    /// Iaret uses this overload to pass the detected response language directly.
    /// </summary>
    /// <param name="text">Text to synthesise.</param>
    /// <param name="culture">BCP-47 culture for this utterance (e.g. "de-DE").</param>
    /// <param name="ct">Cancellation token.</param>
    public Task SpeakAsync(string text, string culture, CancellationToken ct = default)
        => SpeakCoreAsync(text, isWhisper: false, cultureOverride: culture, ct);

    /// <summary>
    /// Speaks text to the default audio output with optional whisper style.
    /// </summary>
    /// <param name="text">Text to speak.</param>
    /// <param name="isWhisper">Use whispering style for inner thoughts.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task SpeakAsync(string text, bool isWhisper, CancellationToken ct = default)
        => SpeakCoreAsync(text, isWhisper, cultureOverride: null, ct);

    /// <summary>
    /// Speaks voice-annotated segments with per-segment prosody (pitch, rate, style).
    /// Each segment can have its own voice style (whisper, excited, etc.) and pitch/rate overrides.
    /// </summary>
    /// <param name="segments">Voice segments with per-segment style/prosody.</param>
    /// <param name="cultureOverride">Override culture for this utterance.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task SpeakSegmentsAsync(
        IReadOnlyList<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)> segments,
        string? cultureOverride = null,
        CancellationToken ct = default)
    {
        if (segments.Count == 0) return;

        await StopSpeakingAsync().ConfigureAwait(false);
        await _speechLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _isSynthesizing = true;
            _currentSynthesisCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (_synthesizer == null) InitializeSynthesizer();

            var ssml = BuildMultiSegmentSsml(segments, cultureOverride);

            await CircuitBreakerPipeline.ExecuteAsync(async _ =>
            {
                await RetryPipeline.ExecuteAsync(async token =>
                {
                    using SpeechSynthesisResult result = await _synthesizer!.SpeakSsmlAsync(ssml).ConfigureAwait(false);
                    if (result.Reason == ResultReason.Canceled)
                    {
                        var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                        string errorDetails = cancellation.ErrorDetails ?? string.Empty;
                        if (errorDetails.Contains("429") || errorDetails.Contains("Too many requests"))
                            throw new HttpRequestException($"Rate limited: {errorDetails}");
                        throw new InvalidOperationException(
                            $"Azure TTS canceled: {cancellation.Reason} - {errorDetails}");
                    }
                    else if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Azure TTS] Segment synthesis complete, {result.AudioData.Length} bytes");
                        try { OnAudioSynthesized?.Invoke(result.AudioData); } catch (Exception ex) when (ex is not OperationCanceledException) { }
                    }
                }, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            System.Diagnostics.Trace.TraceWarning("[TTS] Circuit open -- segment speech falling back to Edge TTS");
            // Fallback: concatenate segments and speak as plain text
            var plainText = string.Join(" ", segments.Select(s => s.Text));
            await FallbackSpeakWithEdgeTtsAsync(plainText, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Trace.TraceWarning("[TTS] Segment speech failed: {0} -- falling back to Edge TTS", ex.Message);
            var plainText = string.Join(" ", segments.Select(s => s.Text));
            await FallbackSpeakWithEdgeTtsAsync(plainText, ct).ConfigureAwait(false);
        }
        finally
        {
            _isSynthesizing = false;
            _currentSynthesisCts?.Dispose();
            _currentSynthesisCts = null;
            _speechLock.Release();
        }
    }

    private async Task SpeakCoreAsync(string text, bool isWhisper, string? cultureOverride, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        await StopSpeakingAsync().ConfigureAwait(false);
        await _speechLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _isSynthesizing = true;
            _currentSynthesisCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (_synthesizer == null) InitializeSynthesizer();

            var ssml = BuildSsml(text, isWhisper, cultureOverride);

            await CircuitBreakerPipeline.ExecuteAsync(async _ =>
            {
                await RetryPipeline.ExecuteAsync(async token =>
                {
                    using SpeechSynthesisResult result = await _synthesizer!.SpeakSsmlAsync(ssml).ConfigureAwait(false);
                    if (result.Reason == ResultReason.Canceled)
                    {
                        var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                        string errorDetails = cancellation.ErrorDetails ?? string.Empty;
                        if (errorDetails.Contains("429") || errorDetails.Contains("Too many requests"))
                            throw new HttpRequestException($"Rate limited: {errorDetails}");
                        // Throw so callers can fall back to Edge/Local TTS
                        throw new InvalidOperationException(
                            $"Azure TTS canceled: {cancellation.Reason} - {errorDetails}");
                    }
                    else if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Azure TTS] Synthesis complete, {result.AudioData.Length} bytes");
                        try { OnAudioSynthesized?.Invoke(result.AudioData); } catch (Exception ex) when (ex is not OperationCanceledException) { }
                    }
                }, ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        catch (BrokenCircuitException)
        {
            System.Diagnostics.Trace.TraceWarning("[TTS] Circuit open -- falling back to Edge TTS Jenny");
            await FallbackSpeakWithEdgeTtsAsync(text, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Rate limited"))
        {
            System.Diagnostics.Trace.TraceWarning("[TTS] Rate limit (429) -- falling back to Edge TTS Jenny");
            await FallbackSpeakWithEdgeTtsAsync(text, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Trace.TraceWarning("[TTS] {0}: {1} -- falling back to Edge TTS Jenny", ex.GetType().Name, ex.Message);
            await FallbackSpeakWithEdgeTtsAsync(text, ct).ConfigureAwait(false);
        }
        finally
        {
            _isSynthesizing = false;
            _currentSynthesisCts?.Dispose();
            _currentSynthesisCts = null;
            _speechLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<Result<SpeechResult, string>> SynthesizeAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<SpeechResult, string>.Failure("Text cannot be empty");
        }

        if (_synthesizer == null) InitializeSynthesizer();

        try
        {
            var rate      = options?.Speed ?? 1.0;
            var isWhisper = options?.IsWhisper ?? false;
            var ssml      = BuildSsml(text, isWhisper, cultureOverride: null, rate);

            using var result = await _synthesizer!.SpeakSsmlAsync(ssml).ConfigureAwait(false);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                return Result<SpeechResult, string>.Success(new SpeechResult(
                    result.AudioData,
                    "audio/wav",
                    result.AudioDuration.TotalSeconds));
            }

            if (result.Reason == ResultReason.Canceled)
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                System.Diagnostics.Trace.TraceWarning("[Azure TTS] Canceled: {0} -- falling back to Edge TTS Jenny", cancellation.ErrorDetails);
                return await FallbackSynthesizeWithEdgeTtsAsync(text, options, ct).ConfigureAwait(false);
            }

            System.Diagnostics.Trace.TraceWarning("[Azure TTS] Synthesis failed -- falling back to Edge TTS Jenny");
            return await FallbackSynthesizeWithEdgeTtsAsync(text, options, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Trace.TraceWarning("[Azure TTS] {0}: {1} -- falling back to Edge TTS Jenny", ex.GetType().Name, ex.Message);
            return await FallbackSynthesizeWithEdgeTtsAsync(text, options, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToFileAsync(
        string text,
        string outputPath,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        var result = await SynthesizeAsync(text, options, ct).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await File.WriteAllBytesAsync(outputPath, result.Value.AudioData, ct).ConfigureAwait(false);
            return Result<string, string>.Success(outputPath);
        }

        return Result<string, string>.Failure(result.Error ?? "Unknown error");
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        var result = await SynthesizeAsync(text, options, ct).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await outputStream.WriteAsync(result.Value.AudioData, ct).ConfigureAwait(false);
            return Result<string, string>.Success("audio/wav");
        }

        return Result<string, string>.Failure(result.Error ?? "Unknown error");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _currentSynthesisCts?.Cancel();
        _currentSynthesisCts?.Dispose();
        _synthesizer?.Dispose();
        _synthesizer = null;
        _speechLock.Dispose();
    }

    /// <summary>
    /// Falls back to Edge TTS (Jenny voice) for direct speaker output.
    /// Synthesizes audio via Edge TTS, then plays it through <see cref="AudioPlayer"/>.
    /// </summary>
    private static async Task FallbackSpeakWithEdgeTtsAsync(string text, CancellationToken ct)
    {
        try
        {
            var edgeResult = await EdgeTtsFallback.Value.SynthesizeAsync(text, null, ct).ConfigureAwait(false);
            if (edgeResult.IsSuccess)
            {
                await AudioPlayer.PlayAsync(edgeResult.Value, ct).ConfigureAwait(false);
            }
            else
            {
                System.Diagnostics.Trace.TraceWarning("[TTS] Edge TTS fallback also failed: {0}", edgeResult.Error);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Trace.TraceWarning("[TTS] Edge TTS fallback error: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Falls back to Edge TTS (Jenny voice) for audio synthesis.
    /// Returns the Edge TTS result, or a failure if both services are unavailable.
    /// </summary>
    private static async Task<Result<SpeechResult, string>> FallbackSynthesizeWithEdgeTtsAsync(
        string text,
        TextToSpeechOptions? options,
        CancellationToken ct)
    {
        try
        {
            return await EdgeTtsFallback.Value.SynthesizeAsync(text, options, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<SpeechResult, string>.Failure(
                $"Both Azure TTS and Edge TTS fallback failed: {ex.Message}");
        }
    }
}
