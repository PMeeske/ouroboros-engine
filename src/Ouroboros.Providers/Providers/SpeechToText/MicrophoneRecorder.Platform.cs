// <copyright file="MicrophoneRecorder.Platform.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Partial class containing platform-specific ProcessStartInfo builders,
/// device detection, and command availability helpers.
/// </summary>
public static partial class MicrophoneRecorder
{
    private static ProcessStartInfo? GetRecorderStartInfo(string outputPath, int durationSeconds, string format)
    {
        if (OperatingSystem.IsWindows())
        {
            // Use ffmpeg with DirectShow on Windows
            if (IsCommandAvailable("ffmpeg"))
            {
                // Get the default/active audio input device
                string? audioDevice = GetDefaultWindowsAudioDevice();
                if (string.IsNullOrEmpty(audioDevice))
                {
                    audioDevice = "Microphone"; // Fallback
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                psi.ArgumentList.Add("-f");
                psi.ArgumentList.Add("dshow");
                psi.ArgumentList.Add("-i");
                psi.ArgumentList.Add($"audio={audioDevice}");
                psi.ArgumentList.Add("-t");
                psi.ArgumentList.Add(durationSeconds.ToString());
                psi.ArgumentList.Add("-y");
                psi.ArgumentList.Add(outputPath);
                return psi;
            }

            // Fallback to PowerShell with Windows.Media.Capture
            var psPsi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            psPsi.ArgumentList.Add("-NoProfile");
            psPsi.ArgumentList.Add("-Command");
            psPsi.ArgumentList.Add(
                "Add-Type -AssemblyName System.Speech; " +
                "$recognizer = New-Object System.Speech.Recognition.SpeechRecognitionEngine; " +
                "$recognizer.SetInputToDefaultAudioDevice(); " +
                $"Start-Sleep -Seconds {durationSeconds}");
            return psPsi;
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Use ffmpeg with AVFoundation on macOS
            if (IsCommandAvailable("ffmpeg"))
            {
                var macPsi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                macPsi.ArgumentList.Add("-f");
                macPsi.ArgumentList.Add("avfoundation");
                macPsi.ArgumentList.Add("-i");
                macPsi.ArgumentList.Add(":0");
                macPsi.ArgumentList.Add("-t");
                macPsi.ArgumentList.Add(durationSeconds.ToString());
                macPsi.ArgumentList.Add("-y");
                macPsi.ArgumentList.Add(outputPath);
                return macPsi;
            }

            // Fallback to sox/rec
            if (IsCommandAvailable("rec"))
            {
                var recPsi = new ProcessStartInfo
                {
                    FileName = "rec",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                recPsi.ArgumentList.Add(outputPath);
                recPsi.ArgumentList.Add("trim");
                recPsi.ArgumentList.Add("0");
                recPsi.ArgumentList.Add(durationSeconds.ToString());
                return recPsi;
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // Use ffmpeg with ALSA/PulseAudio on Linux
            if (IsCommandAvailable("ffmpeg"))
            {
                // Try PulseAudio first, fall back to ALSA
                string inputFormat = IsCommandAvailable("pactl") ? "pulse" : "alsa";
                var linuxFfmpegPsi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                linuxFfmpegPsi.ArgumentList.Add("-f");
                linuxFfmpegPsi.ArgumentList.Add(inputFormat);
                linuxFfmpegPsi.ArgumentList.Add("-i");
                linuxFfmpegPsi.ArgumentList.Add("default");
                linuxFfmpegPsi.ArgumentList.Add("-t");
                linuxFfmpegPsi.ArgumentList.Add(durationSeconds.ToString());
                linuxFfmpegPsi.ArgumentList.Add("-y");
                linuxFfmpegPsi.ArgumentList.Add(outputPath);
                return linuxFfmpegPsi;
            }

            // Fallback to arecord
            if (IsCommandAvailable("arecord"))
            {
                var arecordPsi = new ProcessStartInfo
                {
                    FileName = "arecord",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                arecordPsi.ArgumentList.Add("-d");
                arecordPsi.ArgumentList.Add(durationSeconds.ToString());
                arecordPsi.ArgumentList.Add("-f");
                arecordPsi.ArgumentList.Add("cd");
                arecordPsi.ArgumentList.Add(outputPath);
                return arecordPsi;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the default/active Windows audio input device.
    /// Prioritizes devices with "Microphone" in name, falls back to first audio device.
    /// </summary>
    private static string? GetDefaultWindowsAudioDevice()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-list_devices");
            startInfo.ArgumentList.Add("true");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("dshow");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add("dummy");

            // SECURITY: safe — hardcoded "ffmpeg" with ArgumentList for device listing
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            // Parse output to find audio devices
            // Format: [dshow @ ...] "Device Name" (audio)
            List<string> audioDevices = new List<string>();
            foreach (string line in error.Split('\n').Where(line => line.Contains("(audio)")))
            {
                int start = line.IndexOf('"');
                int end = line.LastIndexOf('"');
                if (start >= 0 && end > start)
                {
                    audioDevices.Add(line.Substring(start + 1, end - start - 1));
                }
            }

            if (audioDevices.Count == 0)
            {
                return null;
            }

            // Prioritize device with "Microphone" or "Mikrofon" in name (active/primary mic)
            string? preferredDevice = audioDevices.FirstOrDefault(d =>
                d.Contains("Microphone", StringComparison.OrdinalIgnoreCase) ||
                d.Contains("Mikrofon", StringComparison.OrdinalIgnoreCase));

            return preferredDevice ?? audioDevices[0];
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            string whichCommand = OperatingSystem.IsWindows() ? "where" : "which";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = whichCommand,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(command);

            // SECURITY: safe — hardcoded "where"/"which" with internally-sourced command names
            using Process? process = Process.Start(startInfo);
            process?.WaitForExit(1000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunCommandAsync(string command, string args)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // SECURITY: safe — called only with hardcoded command/arg pairs from GetDeviceInfoAsync
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return "Failed to start process";
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrEmpty(output) ? error : output;
        }
        catch (InvalidOperationException ex)
        {
            return $"Error: {ex.Message}";
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
