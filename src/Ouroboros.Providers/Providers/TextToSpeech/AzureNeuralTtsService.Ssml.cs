// <copyright file="AzureNeuralTtsService.Ssml.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using R3;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// SSML construction and voice configuration partial.
/// Contains voice selection, SSML building, and multi-segment SSML helpers.
/// Synthesis execution lives in AzureNeuralTtsService.cs.
/// </summary>
public sealed partial class AzureNeuralTtsService
{
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
            _ => isGerman ? "de-DE-KatjaNeural" : "en-US-JennyNeural",
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
    /// Gets the voice's primary locale for the SSML &lt;speak&gt; element.
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
    private string BuildSsml(string text, bool isWhisper, string? cultureOverride = null, double rate = 1.0)
    {
        var escaped = System.Security.SecurityElement.Escape(text);
        string culture = cultureOverride ?? _culture;
        string voiceLoc = SpeakLang;                              // voice's own primary locale

        bool isEnglish = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        bool isCrossLingual = voiceLoc.Length >= 2 && culture.Length >= 2
            && !string.Equals(voiceLoc[..2], culture[..2], StringComparison.OrdinalIgnoreCase);

        int normalRate = SelfVectorRateMultiplier.HasValue
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
    /// <returns></returns>
    public string BuildMultiSegmentSsml(
        IReadOnlyList<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)> segments,
        string? cultureOverride = null)
    {
        string culture = cultureOverride ?? _culture;
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
            if (string.IsNullOrWhiteSpace(escaped))
            {
                continue;
            }

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
        _ => null,
    };
}
