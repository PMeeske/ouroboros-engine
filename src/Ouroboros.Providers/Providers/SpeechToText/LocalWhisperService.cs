// <copyright file="LocalWhisperService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Local Whisper-based speech-to-text service using whisper.cpp or faster-whisper.
/// Falls back to using the system's installed whisper command-line tool.
/// </summary>
[Obsolete("Use OpenClawSttService via OpenClaw gateway. Kept as fallback.")]
public sealed partial class LocalWhisperService : ISpeechToTextService
{
    private readonly string _whisperPath;
    private readonly string _modelPath;
    private readonly string _modelSize;

    /// <summary>
    /// Supported audio formats for local Whisper.
    /// </summary>
    private static readonly string[] SupportedAudioFormats =
    [
        ".wav", ".mp3", ".m4a", ".flac", ".ogg", ".opus",
    ];

    /// <inheritdoc/>
    public string ProviderName => "Local Whisper";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => SupportedAudioFormats;

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 500 * 1024 * 1024; // 500 MB for local processing

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWhisperService"/> class.
    /// </summary>
    /// <param name="whisperPath">Path to whisper executable (e.g., whisper.cpp main or faster-whisper-cli).</param>
    /// <param name="modelPath">Path to the Whisper model file.</param>
    /// <param name="modelSize">Model size: tiny, base, small, medium, large.</param>
    public LocalWhisperService(
        string? whisperPath = null,
        string? modelPath = null,
        string modelSize = "small")
    {
        _whisperPath = whisperPath ?? FindWhisperExecutable();
        _modelPath = modelPath ?? string.Empty;
        _modelSize = modelSize;
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<TranscriptionResult, string>.Failure($"File not found: {filePath}");
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedAudioFormats.Contains(extension))
        {
            return Result<TranscriptionResult, string>.Failure(
                $"Unsupported audio format: {extension}. Supported: {string.Join(", ", SupportedAudioFormats)}");
        }

        try
        {
            return await RunWhisperAsync(filePath, options, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        // Save stream to temp file
        string tempPath = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        try
        {
            using FileStream fileStream = File.Create(tempPath);
            await audioStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
            await fileStream.FlushAsync(ct).ConfigureAwait(false);
            fileStream.Close();

            return await TranscribeFileAsync(tempPath, options, ct).ConfigureAwait(false);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // ignore cleanup errors
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        using MemoryStream stream = new MemoryStream(audioData);
        return await TranscribeStreamAsync(stream, fileName, options, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new TranscriptionOptions();

        // Add translation flag via prompt or special handling
        TranscriptionOptions translationOptions = options with { Prompt = "--translate " + (options.Prompt ?? string.Empty) };
        return await TranscribeFileAsync(filePath, translationOptions, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // First check for Python whisper (most common)
            if (IsPythonWhisperAvailable())
            {
                return Task.FromResult(true);
            }

            // Check if whisper executable exists
            if (File.Exists(_whisperPath) || CanFindInPath(_whisperPath))
            {
                // Optionally check if model exists
                if (!string.IsNullOrEmpty(_modelPath) && !File.Exists(_modelPath))
                {
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(false);
        }
    }

    private static string FindWhisperExecutable()
    {
        // First check for Python whisper via our wrapper script
        string? scriptPath = FindWhisperPythonScript();
        if (!string.IsNullOrEmpty(scriptPath) && IsPythonWhisperAvailable())
        {
            return "python"; // Use python to run script
        }

        // Common whisper executable names
        string[] candidates =
        [
            "whisper",
            "whisper.exe",
            "whisper-cpp",
            "main", // whisper.cpp default
            "faster-whisper",
            "faster-whisper.exe",
        ];

        string? foundCandidate = candidates.FirstOrDefault(candidate => CanFindInPath(candidate));
        if (foundCandidate != null)
        {
            return foundCandidate;
        }

        // Check if Python whisper is available
        if (IsPythonWhisperAvailable())
        {
            return "python";
        }

        // Return default, will fail gracefully if not found
        return "whisper";
    }

    private static string? FindWhisperPythonScript()
    {
        // Look for the whisper_transcribe.py script in common locations
        string[] scriptLocations =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "whisper_transcribe.py"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "scripts", "whisper_transcribe.py"),
            Path.Combine(Environment.CurrentDirectory, "scripts", "whisper_transcribe.py"),
        ];

        foreach (string path in scriptLocations)
        {
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static bool IsPythonWhisperAvailable()
    {
        try
        {
            // Check if Python is available and whisper is installed
            string python = FindPythonExecutable();
            if (string.IsNullOrEmpty(python))
            {
                return false;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = python,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("import whisper; print('ok')");

            // SECURITY: safe — hardcoded python with ArgumentList for import check
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return process.ExitCode == 0 && output.Contains("ok");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static string FindPythonExecutable()
    {
        // Check common Python locations
        string[] pythonCandidates = ["python", "python3", "py"];

        string? foundPython = pythonCandidates.FirstOrDefault(candidate => CanFindInPath(candidate));
        if (foundPython != null)
        {
            return foundPython;
        }

        // Check Windows-specific Python installation paths
        if (OperatingSystem.IsWindows())
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] windowsPaths =
            [
                Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python311", "python.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python310", "python.exe"),
            ];

            string? foundPath = windowsPaths.FirstOrDefault(path => File.Exists(path));
            if (foundPath != null)
            {
                return foundPath;
            }
        }

        return string.Empty;
    }

    private static bool CanFindInPath(string executable)
    {
        try
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] paths = pathEnv.Split(Path.PathSeparator);

            foreach (string path in paths)
            {
                string fullPath = Path.Combine(path, executable);
                if (File.Exists(fullPath))
                {
                    return true;
                }

                // On Windows, also check with .exe extension
                if (OperatingSystem.IsWindows() && File.Exists(fullPath + ".exe"))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return false;
        }
    }

    private static Result<TranscriptionResult, string> ParseWhisperOutput(string output, string format)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Result<TranscriptionResult, string>.Failure("Whisper produced no output");
        }

        if (format == "json" || format == "verbose_json")
        {
            try
            {
                // Try to parse JSON output
                int jsonStart = output.IndexOf('{');
                int jsonEnd = output.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    string json = output[jsonStart..(jsonEnd + 1)];
                    WhisperJsonOutput? result = JsonSerializer.Deserialize<WhisperJsonOutput>(json);
                    if (result != null)
                    {
                        List<TranscriptionSegment>? segments = result.Segments?
                            .Select(s => new TranscriptionSegment(
                                s.Text ?? string.Empty,
                                s.Start,
                                s.End,
                                s.Confidence))
                            .ToList();

                        return Result<TranscriptionResult, string>.Success(
                            new TranscriptionResult(
                                result.Text ?? string.Empty,
                                result.Language,
                                null,
                                segments));
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fall through to plain text parsing
            }
        }

        // Plain text output
        return Result<TranscriptionResult, string>.Success(
            new TranscriptionResult(output.Trim()));
    }
}
