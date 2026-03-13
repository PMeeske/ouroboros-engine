// <copyright file="MicrophoneRecorder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Cross-platform microphone recorder using system tools.
/// Uses ffmpeg/sox on all platforms for recording from microphone.
/// </summary>
public static partial class MicrophoneRecorder
{
    /// <summary>
    /// Records audio from the microphone for a specified duration.
    /// </summary>
    /// <param name="durationSeconds">Duration to record in seconds.</param>
    /// <param name="outputPath">Optional output path. If null, creates a temp file.</param>
    /// <param name="format">Audio format (wav, mp3). Default is wav.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the recorded audio file.</returns>
    public static async Task<Result<string, string>> RecordAsync(
        int durationSeconds,
        string? outputPath = null,
        string format = "wav",
        CancellationToken ct = default)
    {
        string filePath = outputPath ?? Path.Combine(Path.GetTempPath(), $"mic_recording_{Guid.NewGuid()}.{format}");

        // Ensure directory exists
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        ProcessStartInfo? startInfo = GetRecorderStartInfo(filePath, durationSeconds, format);
        if (startInfo == null)
        {
            return Result<string, string>.Failure(
                "No suitable audio recorder found. Please install ffmpeg or sox.");
        }

        try
        {
            // SECURITY: safe — GetRecorderStartInfo uses hardcoded commands (ffmpeg,
            // powershell, rec, arecord) with ArgumentList for all parameters.
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start audio recorder");
            }

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync(ct);
                return Result<string, string>.Failure($"Recording failed: {error}");
            }

            if (!File.Exists(filePath))
            {
                return Result<string, string>.Failure("Recording failed: output file not created");
            }

            return Result<string, string>.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Recording cancelled");
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Recording failed: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<string, string>.Failure($"Recording failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Records audio from the microphone until Enter is pressed.
    /// </summary>
    /// <param name="outputPath">Optional output path. If null, creates a temp file.</param>
    /// <param name="format">Audio format (wav, mp3). Default is wav.</param>
    /// <param name="maxDurationSeconds">Maximum recording duration (safety limit). Default 300 seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the recorded audio file.</returns>
    public static async Task<Result<string, string>> RecordUntilKeyPressAsync(
        string? outputPath = null,
        string format = "wav",
        int maxDurationSeconds = 300,
        CancellationToken ct = default)
    {
        string filePath = outputPath ?? Path.Combine(Path.GetTempPath(), $"mic_recording_{Guid.NewGuid()}.{format}");

        // Ensure directory exists
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        ProcessStartInfo? startInfo = GetRecorderStartInfo(filePath, maxDurationSeconds, format);
        if (startInfo == null)
        {
            return Result<string, string>.Failure(
                "No suitable audio recorder found. Please install ffmpeg or sox.");
        }

        try
        {
            // SECURITY: safe — same as RecordAsync above; hardcoded commands with ArgumentList
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start audio recorder");
            }

            // Wait for key press or cancellation
            Console.WriteLine("🎤 Recording... Press Enter to stop.");

            Task keyTask = Task.Run(() =>
            {
                Console.ReadLine();
            }, ct);

            Task processTask = process.WaitForExitAsync(ct);

            // Wait for either key press or process exit
            await Task.WhenAny(keyTask, processTask);

            // If process is still running, kill it
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync(ct);
            }

            // Give a moment for file to be finalized
            await Task.Delay(100, ct);

            if (!File.Exists(filePath))
            {
                return Result<string, string>.Failure("Recording failed: output file not created");
            }

            return Result<string, string>.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Recording cancelled");
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Recording failed: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<string, string>.Failure($"Recording failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Records audio and returns the audio data directly.
    /// </summary>
    /// <param name="durationSeconds">Duration to record in seconds.</param>
    /// <param name="format">Audio format. Default is wav.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recorded audio data.</returns>
    public static async Task<Result<byte[], string>> RecordToMemoryAsync(
        int durationSeconds,
        string format = "wav",
        CancellationToken ct = default)
    {
        Result<string, string> recordResult = await RecordAsync(durationSeconds, null, format, ct);

        return await recordResult.Match<Task<Result<byte[], string>>>(
            async path =>
            {
                try
                {
                    byte[] data = await File.ReadAllBytesAsync(path, ct);

                    // Clean up temp file
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                        // Ignore cleanup errors
                    }

                    return Result<byte[], string>.Success(data);
                }
                catch (IOException ex)
                {
                    return Result<byte[], string>.Failure($"Failed to read recording: {ex.Message}");
                }
            },
            error => Task.FromResult(Result<byte[], string>.Failure(error)));
    }

    /// <summary>
    /// Checks if audio recording is available on this system.
    /// </summary>
    /// <returns>True if recording is possible.</returns>
    public static bool IsRecordingAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            return IsCommandAvailable("ffmpeg") || IsCommandAvailable("sox");
        }
        else if (OperatingSystem.IsMacOS())
        {
            return IsCommandAvailable("ffmpeg") || IsCommandAvailable("sox") || IsCommandAvailable("rec");
        }
        else if (OperatingSystem.IsLinux())
        {
            return IsCommandAvailable("ffmpeg") || IsCommandAvailable("arecord") || IsCommandAvailable("sox");
        }

        return false;
    }

    /// <summary>
    /// Gets information about available recording devices.
    /// </summary>
    /// <returns>Device information string.</returns>
    public static async Task<string> GetDeviceInfoAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            return await RunCommandAsync("ffmpeg", "-list_devices true -f dshow -i dummy 2>&1");
        }
        else if (OperatingSystem.IsMacOS())
        {
            return await RunCommandAsync("ffmpeg", "-f avfoundation -list_devices true -i \"\" 2>&1");
        }
        else if (OperatingSystem.IsLinux())
        {
            return await RunCommandAsync("arecord", "-l");
        }

        return "Unable to list devices on this platform";
    }

}
