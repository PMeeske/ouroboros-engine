// <copyright file="EdgeTtsService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Polly;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Free neural TTS service using Microsoft Edge's TTS API.
/// Provides high-quality neural voices (same as Azure) without API keys.
/// Requires internet connection but no billing/subscription.
/// NOTE: Microsoft may block this unofficial API (403 errors) - use Azure TTS for production.
/// </summary>
public sealed class EdgeTtsService : ITextToSpeechService, IDisposable
{
    private const string TrustedClientToken = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";
    private const string WssUrl = "wss://speech.platform.bing.com/consumer/speech/synthesize/readaloud/edge/v1";
    private const string ChromiumVersion = "143.0.3650.75";

    private readonly string _voice;
    private readonly string _outputFormat;
    private bool _disposed;

    // Circuit breaker - if Edge TTS is blocked, don't spam retries
    private static readonly ResiliencePipeline CircuitBreakerPipeline = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(60),
            MinimumThroughput = 2,
            BreakDuration = TimeSpan.FromMinutes(5), // Stay broken for 5 min if blocked
            OnOpened = args =>
            {
                _isCircuitOpen = true;
                System.Diagnostics.Trace.TraceWarning("[TTS] Edge TTS circuit OPEN - disabled for 5 minutes (likely blocked by Microsoft)");
                return default;
            },
            OnClosed = args =>
            {
                _isCircuitOpen = false;
                return default;
            },
        })
        .Build();

    private static volatile bool _isCircuitOpen;

    /// <summary>
    /// Gets whether the Edge TTS circuit breaker is open (service blocked/unavailable).
    /// </summary>
    public static bool IsCircuitOpen => _isCircuitOpen;

    /// <summary>
    /// Available Edge TTS neural voices.
    /// </summary>
    public static class Voices
    {
        // English (US)
        public const string JennyNeural = "en-US-JennyNeural";
        public const string AriaNeural = "en-US-AriaNeural";
        public const string GuyNeural = "en-US-GuyNeural";
        public const string DavisNeural = "en-US-DavisNeural";
        public const string AmberNeural = "en-US-AmberNeural";
        public const string AnaNeural = "en-US-AnaNeural";
        public const string AshleyNeural = "en-US-AshleyNeural";
        public const string BrandonNeural = "en-US-BrandonNeural";
        public const string ChristopherNeural = "en-US-ChristopherNeural";
        public const string CoraNeural = "en-US-CoraNeural";
        public const string ElizabethNeural = "en-US-ElizabethNeural";
        public const string EricNeural = "en-US-EricNeural";
        public const string JacobNeural = "en-US-JacobNeural";
        public const string MichelleNeural = "en-US-MichelleNeural";
        public const string MonicaNeural = "en-US-MonicaNeural";
        public const string SaraNeural = "en-US-SaraNeural";

        // English (UK)
        public const string SoniaNeural = "en-GB-SoniaNeural";
        public const string RyanNeural = "en-GB-RyanNeural";
        public const string LibbyNeural = "en-GB-LibbyNeural";

        // German
        public const string KatjaNeural = "de-DE-KatjaNeural";
        public const string ConradNeural = "de-DE-ConradNeural";
        public const string AmalaNeural = "de-DE-AmalaNeural";

        // Default Cortana-like voice
        public const string Default = JennyNeural;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeTtsService"/> class.
    /// </summary>
    /// <param name="voice">Voice name (e.g., "en-US-JennyNeural"). Uses Jenny (Cortana-like) by default.</param>
    /// <param name="outputFormat">Audio format. Default is "audio-24khz-96kbitrate-mono-mp3".</param>
    public EdgeTtsService(string? voice = null, string? outputFormat = null)
    {
        _voice = voice ?? Voices.Default;
        _outputFormat = outputFormat ?? "audio-24khz-96kbitrate-mono-mp3";
    }

    /// <inheritdoc/>
    public string ProviderName => "Microsoft Edge TTS (Free Neural)";

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableVoices => new[]
    {
        Voices.JennyNeural,
        Voices.AriaNeural,
        Voices.GuyNeural,
        Voices.SaraNeural,
        Voices.SoniaNeural,
        Voices.KatjaNeural,
    };

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => new[] { "mp3", "wav", "ogg" };

    /// <inheritdoc/>
    public int MaxInputLength => 10000;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(!IsCircuitOpen); // Not available if circuit is open (blocked)

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

        // Fast-fail if circuit is open
        if (IsCircuitOpen)
        {
            return Result<SpeechResult, string>.Failure("Edge TTS temporarily unavailable (circuit open)");
        }

        try
        {
            byte[] audioData = await CircuitBreakerPipeline.ExecuteAsync(
                async token => await SynthesizeInternalAsync(text, options, token).ConfigureAwait(false),
                ct).ConfigureAwait(false);
            return Result<SpeechResult, string>.Success(new SpeechResult(audioData, "mp3"));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<SpeechResult, string>.Failure($"Edge TTS error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToFileAsync(
        string text,
        string outputPath,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        Result<SpeechResult, string> result = await SynthesizeAsync(text, options, ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            try
            {
                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(outputPath, result.Value.AudioData, ct).ConfigureAwait(false);
                return Result<string, string>.Success(outputPath);
            }
            catch (OperationCanceledException) { throw; }
            catch (IOException ex)
            {
                return Result<string, string>.Failure($"Failed to save audio: {ex.Message}");
            }
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
        Result<SpeechResult, string> result = await SynthesizeAsync(text, options, ct).ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await outputStream.WriteAsync(result.Value.AudioData, ct).ConfigureAwait(false);
            return Result<string, string>.Success("mp3");
        }

        return Result<string, string>.Failure(result.Error ?? "Unknown error");
    }

    private Task<byte[]> SynthesizeInternalAsync(
        string text,
        TextToSpeechOptions? options,
        CancellationToken ct)
    {
        double rate = options?.Speed ?? 1.0;
        bool isWhisper = options?.IsWhisper ?? false;

        int ratePercent = (int)((rate - 1.0) * 100);
        string rateString = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";
        string pitchString = isWhisper ? "-10Hz" : "+0Hz";
        string volumeString = isWhisper ? "-20%" : "+0%";

        string ssml = $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
<voice name='{_voice}'>
<prosody rate='{rateString}' pitch='{pitchString}' volume='{volumeString}'>
{System.Security.SecurityElement.Escape(text)}
</prosody>
</voice>
</speak>";

        return SynthesizeSsmlInternalAsync(ssml, ct);
    }

    /// <summary>
    /// Sends pre-built SSML to the Edge TTS WebSocket and returns audio bytes.
    /// </summary>
    private async Task<byte[]> SynthesizeSsmlInternalAsync(string ssml, CancellationToken ct)
    {
        string requestId = Guid.NewGuid().ToString("N");
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        using ClientWebSocket ws = new();
        ws.Options.SetRequestHeader("Pragma", "no-cache");
        ws.Options.SetRequestHeader("Cache-Control", "no-cache");
        ws.Options.SetRequestHeader("Origin", "chrome-extension://jdiccldimpdaibmpdkjnbmckianbfold");
        ws.Options.SetRequestHeader("Accept-Encoding", "gzip, deflate, br");
        ws.Options.SetRequestHeader("Accept-Language", "en-US,en;q=0.9");
        ws.Options.SetRequestHeader("User-Agent",
            $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{ChromiumVersion} Safari/537.36 Edg/{ChromiumVersion}");

        string secGec = GenerateSecMsGec();
        string url = $"{WssUrl}?TrustedClientToken={TrustedClientToken}"
            + $"&Sec-MS-GEC={secGec}"
            + $"&Sec-MS-GEC-Version=1-{ChromiumVersion}"
            + $"&ConnectionId={requestId}";
        await ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);

        string configMessage = $"X-Timestamp:{timestamp}\r\n" +
            "Content-Type:application/json; charset=utf-8\r\n" +
            "Path:speech.config\r\n\r\n" +
            JsonSerializer.Serialize(new
            {
                context = new
                {
                    synthesis = new
                    {
                        audio = new
                        {
                            metadataoptions = new { sentenceBoundaryEnabled = false, wordBoundaryEnabled = false },
                            outputFormat = _outputFormat
                        }
                    }
                }
            });

        await ws.SendAsync(
            Encoding.UTF8.GetBytes(configMessage),
            WebSocketMessageType.Text,
            true,
            ct).ConfigureAwait(false);

        string ssmlMessage = $"X-RequestId:{requestId}\r\n" +
            $"X-Timestamp:{timestamp}\r\n" +
            "Content-Type:application/ssml+xml\r\n" +
            "Path:ssml\r\n\r\n" +
            ssml;

        await ws.SendAsync(
            Encoding.UTF8.GetBytes(ssmlMessage),
            WebSocketMessageType.Text,
            true,
            ct).ConfigureAwait(false);

        using MemoryStream audioStream = new();
        byte[] buffer = new byte[8192];
        bool audioStarted = false;

        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                int headerEnd = FindHeaderEnd(buffer, result.Count);
                if (headerEnd > 0 && headerEnd < result.Count)
                {
                    audioStream.Write(buffer, headerEnd, result.Count - headerEnd);
                    audioStarted = true;
                }
                else if (audioStarted)
                {
                    audioStream.Write(buffer, 0, result.Count);
                }
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (message.Contains("Path:turn.end"))
                {
                    break;
                }
            }
        }

        return audioStream.ToArray();
    }

    /// <summary>
    /// Maps an Azure TTS emotional style name to Edge TTS prosody parameters.
    /// Edge TTS doesn't support &lt;mstts:express-as&gt;, so we approximate
    /// emotional styles with rate/pitch/volume adjustments.
    /// </summary>
    public static (int RatePercent, string Pitch, string Volume) MapStyleToProsody(string? style)
    {
        return style?.ToLowerInvariant() switch
        {
            "cheerful" or "friendly" => (5, "+8%", "+5%"),
            "excited" => (15, "+12%", "+10%"),
            "sad" or "empathetic" => (-10, "-5%", "-10%"),
            "angry" or "shouting" => (10, "+5%", "+15%"),
            "whispering" or "gentle" => (-15, "+3%", "-20%"),
            "calm" or "hopeful" => (-5, "+2%", "-5%"),
            "lyrical" or "poetry-reading" => (-8, "+6%", "0%"),
            "chat" => (0, "+3%", "0%"),
            "newscast-formal" => (-3, "-2%", "+5%"),
            _ => (0, "+5%", "0%")
        };
    }

    /// <summary>
    /// Builds multi-segment SSML where each segment can have its own prosody.
    /// Edge TTS doesn't support mstts:express-as, so styles are approximated via rate/pitch/volume.
    /// </summary>
    public string BuildMultiSegmentSsml(
        IReadOnlyList<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)> segments)
    {
        var sb = new StringBuilder();
        foreach (var (text, style, pitchOff, rateMul) in segments)
        {
            // Break segments contain raw SSML (e.g. <break time='500ms'/>)
            if (string.Equals(style, "break", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(text);
                continue;
            }

            var escaped = System.Security.SecurityElement.Escape(text);
            if (string.IsNullOrWhiteSpace(escaped)) continue;

            // Map voice marker style to Edge-compatible prosody
            var (baseRate, basePitch, baseVolume) = MapStyleToProsody(style);

            // Apply per-segment pitch/rate overrides on top of style defaults
            int ratePercent = rateMul.HasValue
                ? (int)(((rateMul.Value - 1.0f) * 100) + baseRate)
                : baseRate;
            string pitchString = pitchOff.HasValue
                ? $"{(int)(pitchOff.Value * 100) + int.Parse(basePitch.Replace("%", "").Replace("+", ""))}%"
                : basePitch;

            string rateString = ratePercent >= 0 ? $"+{ratePercent}%" : $"{ratePercent}%";

            sb.Append($"<prosody rate='{rateString}' pitch='{pitchString}' volume='{baseVolume}'>");
            sb.Append(escaped);
            sb.Append("</prosody>");
        }

        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
<voice name='{_voice}'>
{sb}
</voice>
</speak>";
    }

    /// <summary>
    /// Synthesizes voice-annotated segments with per-segment prosody.
    /// </summary>
    public async Task<Result<SpeechResult, string>> SynthesizeSegmentsAsync(
        IReadOnlyList<(string Text, string? Style, float? PitchOffset, float? RateMultiplier)> segments,
        CancellationToken ct = default)
    {
        if (segments.Count == 0)
            return Result<SpeechResult, string>.Failure("No segments to synthesize");

        try
        {
            var ssml = BuildMultiSegmentSsml(segments);
            var audioBytes = await SynthesizeSsmlInternalAsync(ssml, ct).ConfigureAwait(false);
            return Result<SpeechResult, string>.Success(new SpeechResult(audioBytes, "mp3"));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<SpeechResult, string>.Failure($"Edge TTS segment synthesis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates the Sec-MS-GEC security token required by the Edge TTS API.
    /// Algorithm: SHA-256 hash of (rounded Windows ticks + TrustedClientToken).
    /// Ticks are rounded to 5-minute intervals for clock tolerance.
    /// See: https://github.com/rany2/edge-tts
    /// </summary>
    private static string GenerateSecMsGec()
    {
        // Convert Unix timestamp (ms) to Windows epoch ticks (100ns intervals since 1601-01-01)
        const long windowsEpochDiff = 116_444_736_000_000_000L; // ticks between 1601 and 1970
        long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long ticks = (unixMs * 10_000) + windowsEpochDiff;

        // Round down to nearest 5-minute (300 second) boundary
        const long fiveMinTicks = 3_000_000_000L; // 300 seconds * 10,000,000 ticks/second
        long rounded = (ticks / fiveMinTicks) * fiveMinTicks;

        // SHA-256 hash of "{rounded_ticks}{token}"
        string data = $"{rounded}{TrustedClientToken}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(data));

        return Convert.ToHexStringLower(hash).ToUpperInvariant();
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        // Look for "Path:audio\r\n" followed by data
        byte[] pattern = Encoding.ASCII.GetBytes("Path:audio\r\n");

        for (int i = 0; i <= length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i + pattern.Length;
            }
        }

        return -1;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
