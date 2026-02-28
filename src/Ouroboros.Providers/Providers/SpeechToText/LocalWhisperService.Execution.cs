// <copyright file="LocalWhisperService.Execution.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Providers.SpeechToText;

public sealed partial class LocalWhisperService
{
    private async Task<Result<TranscriptionResult, string>> RunWhisperAsync(
        string filePath,
        TranscriptionOptions? options,
        CancellationToken ct)
    {
        options ??= new TranscriptionOptions();

        // Prefer Python whisper if available
        if (IsPythonWhisperAvailable())
        {
            return await RunPythonWhisperAsync(filePath, options, ct);
        }

        // Fall back to native whisper CLI
        string args = BuildWhisperArguments(filePath, options);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = _whisperPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                return Result<TranscriptionResult, string>.Failure(
                    $"Whisper exited with code {process.ExitCode}: {error}");
            }

            return ParseWhisperOutput(output, options.ResponseFormat);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to run Whisper: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to run Whisper: {ex.Message}");
        }
    }

    private async Task<Result<TranscriptionResult, string>> RunPythonWhisperAsync(
        string filePath,
        TranscriptionOptions options,
        CancellationToken ct)
    {
        string python = FindPythonExecutable();
        string? scriptPath = FindWhisperPythonScript();

        List<string> args = new List<string>();

        if (!string.IsNullOrEmpty(scriptPath))
        {
            args.Add($"\"{scriptPath}\"");
            args.Add($"\"{filePath}\"");
            args.Add($"--model {_modelSize}");
            args.Add("--output json");

            if (!string.IsNullOrEmpty(options.Language))
            {
                args.Add($"--language {options.Language}");
            }

            if (options.Prompt?.Contains("--translate") == true)
            {
                args.Add("--task translate");
            }
        }
        else
        {
            string task = options.Prompt?.Contains("--translate") == true ? "translate" : "transcribe";
            string langArg = string.IsNullOrEmpty(options.Language) ? "" : $", language='{options.Language}'";

            string pythonCode = $@"
import json
import whisper
model = whisper.load_model('{_modelSize}')
result = model.transcribe(r'{filePath.Replace("'", @"\'")}''{langArg}, task='{task}', verbose=False)
output = {{
    'text': result['text'].strip(),
    'language': result.get('language', 'unknown'),
    'segments': [
        {{'start': s['start'], 'end': s['end'], 'text': s['text'].strip()}}
        for s in result.get('segments', [])
    ]
}}
print(json.dumps(output, ensure_ascii=False))
";
            args.Add("-c");
            args.Add($"\"{pythonCode.Replace("\"", "\\\"").Replace("\n", " ")}\"");
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = python,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            string output = await outputTask;
            string error = await errorTask;

            if (string.IsNullOrWhiteSpace(output) && process.ExitCode != 0)
            {
                return Result<TranscriptionResult, string>.Failure(
                    $"Python Whisper failed: {error}");
            }

            return ParseWhisperOutput(output, "json");
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to run Python Whisper: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to run Python Whisper: {ex.Message}");
        }
    }

    private string BuildWhisperArguments(string filePath, TranscriptionOptions options)
    {
        List<string> args = new List<string>();

        args.Add($"\"{filePath}\"");

        if (!string.IsNullOrEmpty(_modelPath))
        {
            args.Add($"--model \"{_modelPath}\"");
        }
        else
        {
            args.Add($"--model {_modelSize}");
        }

        if (!string.IsNullOrEmpty(options.Language))
        {
            args.Add($"--language {options.Language}");
        }

        if (options.ResponseFormat == "json" || options.ResponseFormat == "verbose_json")
        {
            args.Add("--output_format json");
        }

        if (options.Prompt?.Contains("--translate") == true)
        {
            args.Add("--task translate");
        }

        string? cleanPrompt = options.Prompt?.Replace("--translate", string.Empty).Trim();
        if (!string.IsNullOrEmpty(cleanPrompt))
        {
            args.Add($"--initial_prompt \"{cleanPrompt}\"");
        }

        return string.Join(" ", args);
    }

    private sealed class WhisperJsonOutput
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("segments")]
        public List<WhisperSegmentOutput>? Segments { get; set; }
    }

    private sealed class WhisperSegmentOutput
    {
        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }
    }
}
