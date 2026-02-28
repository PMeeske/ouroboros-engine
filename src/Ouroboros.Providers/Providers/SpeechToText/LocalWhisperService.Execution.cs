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
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = _whisperPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        BuildWhisperArgumentList(startInfo, filePath, options);

        // SECURITY: validated — ArgumentList prevents injection from file paths
        // and model paths. UseShellExecute = false prevents shell interpretation.
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

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = python,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (!string.IsNullOrEmpty(scriptPath))
        {
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(_modelSize);
            startInfo.ArgumentList.Add("--output");
            startInfo.ArgumentList.Add("json");

            if (!string.IsNullOrEmpty(options.Language))
            {
                startInfo.ArgumentList.Add("--language");
                startInfo.ArgumentList.Add(options.Language);
            }

            if (options.Prompt?.Contains("--translate") == true)
            {
                startInfo.ArgumentList.Add("--task");
                startInfo.ArgumentList.Add("translate");
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
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(pythonCode);
        }

        // SECURITY: validated — ArgumentList prevents injection from file paths
        // and model names. Python -c code uses hardcoded template with escaped file path.
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

    private void BuildWhisperArgumentList(ProcessStartInfo psi, string filePath, TranscriptionOptions options)
    {
        psi.ArgumentList.Add(filePath);

        if (!string.IsNullOrEmpty(_modelPath))
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(_modelPath);
        }
        else
        {
            psi.ArgumentList.Add("--model");
            psi.ArgumentList.Add(_modelSize);
        }

        if (!string.IsNullOrEmpty(options.Language))
        {
            psi.ArgumentList.Add("--language");
            psi.ArgumentList.Add(options.Language);
        }

        if (options.ResponseFormat == "json" || options.ResponseFormat == "verbose_json")
        {
            psi.ArgumentList.Add("--output_format");
            psi.ArgumentList.Add("json");
        }

        if (options.Prompt?.Contains("--translate") == true)
        {
            psi.ArgumentList.Add("--task");
            psi.ArgumentList.Add("translate");
        }

        string? cleanPrompt = options.Prompt?.Replace("--translate", string.Empty).Trim();
        if (!string.IsNullOrEmpty(cleanPrompt))
        {
            psi.ArgumentList.Add("--initial_prompt");
            psi.ArgumentList.Add(cleanPrompt);
        }
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
