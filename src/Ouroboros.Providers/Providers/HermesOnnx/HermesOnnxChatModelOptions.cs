// <copyright file="HermesOnnxChatModelOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.HermesOnnx;

/// <summary>
/// Sampling and runtime options for <see cref="HermesOnnxChatModel"/>.
/// </summary>
/// <param name="ModelPath">Absolute path to the local ONNX-GenAI model directory containing
/// <c>model.onnx</c> + <c>model.onnx.data</c> + <c>tokenizer.json</c> + <c>genai_config.json</c>.</param>
/// <param name="MaxLength">Override for <c>genai_config.json</c>'s <c>search.max_length</c>. The shipped
/// value is 524288 (512K KV cache) which OOMs the GPU instantly. Default 4096.</param>
/// <param name="Temperature">Sampling temperature.</param>
/// <param name="TopP">Nucleus sampling cumulative probability.</param>
/// <param name="TopK">Top-K candidate cutoff.</param>
/// <param name="ExecutionProvider">Target execution provider for the retargeter:
/// <c>dml</c> (default; uses DirectML on Windows) or <c>cpu</c> (slow but works on
/// graphs that fail under DML — see <c>docs/hermes-onnx-mode.md</c> "DML EP residual").</param>
public sealed record HermesOnnxChatModelOptions(
    string? ModelPath = null,
    int MaxLength = 4096,
    float Temperature = 0.6f,
    float TopP = 0.95f,
    int TopK = 20,
    string ExecutionProvider = "dml");
