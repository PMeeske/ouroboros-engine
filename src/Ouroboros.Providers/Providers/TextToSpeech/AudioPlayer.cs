// <copyright file="AudioPlayer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Cross-platform audio player for playing audio data directly.
/// Uses system audio players (Windows Media Player, afplay on macOS, mpv/ffplay on Linux).
/// </summary>
public static class AudioPlayer
{
    /// <summary>
    /// Plays audio data directly without requiring a permanent file.
    /// </summary>
    /// <param name="audioData">The audio data to play.</param>
    /// <param name="format">The audio format (mp3, wav, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static async Task<Result<bool, string>> PlayAsync(
        byte[] audioData,
        string format = "mp3",
        CancellationToken ct = default)
    {
        // Create a temporary file for playback
        // Sanitize MIME-type format strings like "audio/wav" → "wav"
        string ext = format.Contains('/') ? format[(format.LastIndexOf('/') + 1)..] : format;
        string tempFile = Path.Combine(Path.GetTempPath(), $"tts_playback_{Guid.NewGuid()}.{ext}");

        try
        {
            await File.WriteAllBytesAsync(tempFile, audioData, ct).ConfigureAwait(false);
            return await PlayFileAsync(tempFile, ct).ConfigureAwait(false);
        }
        finally
        {
            // Clean up temp file after a delay to ensure playback started
            _ = Task.Run(async () =>
            {
                await Task.Delay(30000, CancellationToken.None).ConfigureAwait(false); // Wait 30 seconds
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore cleanup errors
                }
            }, ct);
        }
    }

    /// <summary>
    /// Plays an audio file using the system's default audio player.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static async Task<Result<bool, string>> PlayFileAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<bool, string>.Failure($"File not found: {filePath}");
        }

        try
        {
            ProcessStartInfo? startInfo = GetPlayerStartInfo(filePath);
            if (startInfo == null)
            {
                return Result<bool, string>.Failure("No suitable audio player found on this system");
            }

            // SECURITY: safe — GetPlayerStartInfo uses hardcoded commands
            // (powershell, afplay, mpv, ffplay, aplay) with ArgumentList.
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<bool, string>.Failure("Failed to start audio player");
            }

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return Result<bool, string>.Success(true);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<bool, string>.Failure($"Playback failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Plays audio data and waits for playback to complete.
    /// </summary>
    /// <param name="speechResult">The speech result to play.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static Task<Result<bool, string>> PlayAsync(
        SpeechResult speechResult,
        CancellationToken ct = default)
    {
        return PlayAsync(speechResult.AudioData, speechResult.Format, ct);
    }

    private static ProcessStartInfo? GetPlayerStartInfo(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            // Use PowerShell with System.Media.SoundPlayer for WAV (fast, synchronous)
            // For other formats, use Windows Media Player
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".wav")
            {
                // SoundPlayer is much faster for WAV files
                var wavPsi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                wavPsi.ArgumentList.Add("-NoProfile");
                wavPsi.ArgumentList.Add("-Command");
                wavPsi.ArgumentList.Add($"Add-Type -AssemblyName System.Windows.Forms; $p = New-Object System.Media.SoundPlayer('{filePath.Replace("'", "''")}'); $p.PlaySync(); $p.Dispose()");
                return wavPsi;
            }
            else
            {
                // For MP3 and other formats, use a simpler approach
                var mp3Psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                mp3Psi.ArgumentList.Add("-NoProfile");
                mp3Psi.ArgumentList.Add("-Command");
                mp3Psi.ArgumentList.Add($"Add-Type -AssemblyName presentationCore; $p = New-Object System.Windows.Media.MediaPlayer; $p.Open([Uri]::new('{filePath.Replace("'", "''")}')); $p.Play(); while($p.NaturalDuration.HasTimeSpan -eq $false){{Start-Sleep -Milliseconds 100}}; Start-Sleep -Milliseconds ([int]$p.NaturalDuration.TimeSpan.TotalMilliseconds + 100); $p.Close()");
                return mp3Psi;
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Use afplay on macOS
            var afplayPsi = new ProcessStartInfo
            {
                FileName = "afplay",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            afplayPsi.ArgumentList.Add(filePath);
            return afplayPsi;
        }
        else if (OperatingSystem.IsLinux())
        {
            // Try mpv, then ffplay, then aplay for wav
            string[] players = ["mpv", "ffplay", "paplay", "aplay"];

            var availablePlayer = players.FirstOrDefault(IsCommandAvailable);
            if (availablePlayer == null) return null;

            var linuxPsi = new ProcessStartInfo
            {
                FileName = availablePlayer,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            switch (availablePlayer)
            {
                case "mpv":
                    linuxPsi.ArgumentList.Add("--no-video");
                    linuxPsi.ArgumentList.Add(filePath);
                    break;
                case "ffplay":
                    linuxPsi.ArgumentList.Add("-nodisp");
                    linuxPsi.ArgumentList.Add("-autoexit");
                    linuxPsi.ArgumentList.Add(filePath);
                    break;
                default:
                    linuxPsi.ArgumentList.Add(filePath);
                    break;
            }

            return linuxPsi;
        }

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "which",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(command);

            // SECURITY: safe — hardcoded "which" with internally-sourced command names
            using Process? process = Process.Start(startInfo);
            process?.WaitForExit(1000);
            return process?.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }
}