// <copyright file="WhisperNetService.Audio.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Whisper.net;
using Whisper.net.Ggml;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Partial class containing audio processing helpers: WAV reading,
/// resampling, format conversion, and model path utilities.
/// </summary>
public sealed partial class WhisperNetService
{
    private static async Task<float[]?> ReadAudioSamplesAsync(string wavPath, CancellationToken ct)
    {
        try
        {
            // Read WAV file and convert to float samples
            await using var fileStream = File.OpenRead(wavPath);
            using var reader = new BinaryReader(fileStream);

            // Read WAV header
            string riff = new(reader.ReadChars(4));
            if (riff != "RIFF")
            {
                // Try to convert with ffmpeg first
                string convertedPath = Path.Combine(Path.GetTempPath(), $"whisper_converted_{Guid.NewGuid()}.wav");
                var convertResult = await ConvertToWavAsync(wavPath, ct, convertedPath);
                if (convertResult.IsSuccess)
                {
                    var samples = await ReadAudioSamplesAsync(convertedPath, ct);
                    try { File.Delete(convertedPath); } catch (IOException) { /* Intentional: best-effort temp file cleanup */ }
                    return samples;
                }
                return null;
            }

            reader.ReadInt32(); // File size
            string wave = new(reader.ReadChars(4));
            if (wave != "WAVE") return null;

            // Find fmt chunk
            int channels = 1;
            int sampleRate = 16000;
            int bitsPerSample = 16;

            while (fileStream.Position < fileStream.Length)
            {
                string chunkId = new(reader.ReadChars(4));
                int chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                {
                    reader.ReadInt16(); // Audio format
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    reader.ReadInt32(); // Byte rate
                    reader.ReadInt16(); // Block align
                    bitsPerSample = reader.ReadInt16();
                    if (chunkSize > 16) reader.ReadBytes(chunkSize - 16);
                }
                else if (chunkId == "data")
                {
                    // Read audio data
                    int bytesPerSample = bitsPerSample / 8;
                    int numSamples = chunkSize / bytesPerSample / channels;
                    float[] samples = new float[numSamples];

                    for (int i = 0; i < numSamples && fileStream.Position < fileStream.Length; i++)
                    {
                        float sample = 0;
                        for (int c = 0; c < channels; c++)
                        {
                            float channelSample = bitsPerSample switch
                            {
                                8 => (reader.ReadByte() - 128) / 128f,
                                16 => reader.ReadInt16() / 32768f,
                                24 => (reader.ReadByte() | (reader.ReadByte() << 8) | (reader.ReadSByte() << 16)) / 8388608f,
                                32 => reader.ReadInt32() / 2147483648f,
                                _ => 0f
                            };
                            sample += channelSample;
                        }
                        samples[i] = sample / channels; // Average channels to mono
                    }

                    // Resample to 16kHz if needed
                    if (sampleRate != 16000)
                    {
                        samples = Resample(samples, sampleRate, 16000);
                    }

                    return samples;
                }
                else
                {
                    // Skip unknown chunk
                    if (chunkSize > 0 && fileStream.Position + chunkSize <= fileStream.Length)
                    {
                        reader.ReadBytes(chunkSize);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static float[] Resample(float[] samples, int fromRate, int toRate)
    {
        double ratio = (double)toRate / fromRate;
        int newLength = (int)(samples.Length * ratio);
        float[] resampled = new float[newLength];

        for (int i = 0; i < newLength; i++)
        {
            double srcIndex = i / ratio;
            int srcIndexInt = (int)srcIndex;
            double frac = srcIndex - srcIndexInt;

            if (srcIndexInt + 1 < samples.Length)
            {
                resampled[i] = (float)(samples[srcIndexInt] * (1 - frac) + samples[srcIndexInt + 1] * frac);
            }
            else if (srcIndexInt < samples.Length)
            {
                resampled[i] = samples[srcIndexInt];
            }
        }

        return resampled;
    }

    private static async Task<Result<string, string>> ConvertToWavAsync(string inputPath, CancellationToken ct, string? outputPath = null)
    {
        outputPath ??= Path.Combine(Path.GetTempPath(), $"whisper_converted_{Guid.NewGuid()}.wav");

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add("16000");
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("wav");
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add(outputPath);

            // SECURITY: safe — hardcoded "ffmpeg" with ArgumentList for WAV conversion
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start ffmpeg");
            }

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                return Result<string, string>.Failure("FFmpeg conversion failed");
            }

            return Result<string, string>.Success(outputPath);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Conversion failed: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<string, string>.Failure($"Conversion failed: {ex.Message}");
        }
    }

    private static string GetDefaultModelDirectory()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".whisper", "models");
    }

    private static string GetModelFileName(GgmlType modelType)
    {
        return modelType switch
        {
            GgmlType.Tiny => "ggml-tiny.bin",
            GgmlType.TinyEn => "ggml-tiny.en.bin",
            GgmlType.Base => "ggml-base.bin",
            GgmlType.BaseEn => "ggml-base.en.bin",
            GgmlType.Small => "ggml-small.bin",
            GgmlType.SmallEn => "ggml-small.en.bin",
            GgmlType.Medium => "ggml-medium.bin",
            GgmlType.MediumEn => "ggml-medium.en.bin",
            GgmlType.LargeV1 => "ggml-large-v1.bin",
            GgmlType.LargeV2 => "ggml-large-v2.bin",
            GgmlType.LargeV3 => "ggml-large-v3.bin",
            _ => "ggml-base.bin"
        };
    }
}
