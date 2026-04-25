// <copyright file="TensorPipelineConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Pipeline;

/// <summary>
/// Immutable configuration for the end-to-end streaming tensor pipeline (R04, R10).
/// </summary>
/// <param name="BatchSize">Number of vectors per emitted batch tensor. Must be positive.</param>
/// <param name="Decoder">Function that converts raw bytes to a float array (the Decode stage).</param>
/// <param name="NormalizationMean">
/// Mean subtracted from each element during normalisation. Default 0 (no shift).
/// </param>
/// <param name="NormalizationStd">
/// Standard deviation to divide by during normalisation. Default 1 (no scaling).
/// Must not be zero.
/// </param>
/// <param name="BackendPreference">
/// Preferred execution device. Falls back to CPU when the device is unavailable.
/// </param>
public sealed record TensorPipelineConfig(
    int BatchSize,
    Func<byte[], float[]> Decoder,
    float NormalizationMean = 0f,
    float NormalizationStd = 1f,
    DeviceType BackendPreference = DeviceType.Cpu)
{
    /// <summary>
    /// A minimal preset — batch size 32, identity decoder (interprets raw bytes as floats),
    /// no normalisation, CPU backend.
    /// </summary>
    public static readonly TensorPipelineConfig Default = new(
        BatchSize: 32,
        Decoder: BytesToFloats,
        NormalizationMean: 0f,
        NormalizationStd: 1f,
        BackendPreference: DeviceType.Cpu);

    /// <summary>Validates the config, throwing on invalid values.</summary>
    public void Validate()
    {
        if (BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(BatchSize),
                $"BatchSize must be positive. Got {BatchSize}.");
        }

        if (Decoder is null)
        {
            throw new ArgumentNullException(nameof(Decoder));
        }

        if (NormalizationStd == 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(NormalizationStd),
                "NormalizationStd must not be zero.");
        }
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        if (bytes.Length % sizeof(float) != 0)
        {
            throw new ArgumentException(
                $"Byte array length {bytes.Length} is not a multiple of sizeof(float)={sizeof(float)}.",
                nameof(bytes));
        }

        var floats = new float[bytes.Length / sizeof(float)];
        System.Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
