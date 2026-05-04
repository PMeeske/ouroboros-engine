// <copyright file="Phi3VisionOnnxChatModelOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.Phi3Vision;

/// <summary>
/// Options for <see cref="Phi3VisionOnnxChatModel"/>. Mirrors the shape of
/// <see cref="HermesOnnx.HermesOnnxChatModelOptions"/> so config wiring stays
/// uniform across ORT-GenAI-backed clients.
/// </summary>
public sealed record Phi3VisionOnnxChatModelOptions
{
    /// <summary>Execution provider key passed to ORT-GenAI: <c>dml</c> or <c>cpu</c>.</summary>
    public string ExecutionProvider { get; init; } = "dml";

    /// <summary>Maximum total length (prompt + generated tokens). Phi-3.5 vision supports 128K.</summary>
    public int MaxLength { get; init; } = 7680;

    /// <summary>Sampling temperature.</summary>
    public float Temperature { get; init; } = 0.6f;

    /// <summary>Top-p nucleus sampling.</summary>
    public float TopP { get; init; } = 0.9f;

    /// <summary>Top-k sampling.</summary>
    public int TopK { get; init; } = 50;
}
