// <copyright file="FrameBuffer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Immutable raster output from <see cref="IGaussianRasterizer.RasterizeAsync"/>.
/// Carries a raw RGBA byte buffer so plan 05 can swap it directly into the
/// App-layer <c>NativeAvatarPipeline._latestFrame</c> slot without touching
/// the current JPEG-encode path.
/// </summary>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="Rgba">Raw <c>Width * Height * 4</c> RGBA byte buffer (row-major).</param>
/// <param name="RasterLatencyTicks">
/// Stopwatch ticks between raster start and raster completion. Convert via
/// <c>TimeSpan.FromSeconds(ticks / (double)Stopwatch.Frequency)</c>.
/// </param>
public sealed record FrameBuffer(
    int Width,
    int Height,
    byte[] Rgba,
    long RasterLatencyTicks)
{
    /// <summary>
    /// Convenience helper that returns an all-transparent <see cref="FrameBuffer"/>
    /// for cases where the rasterizer has nothing to draw (empty or entirely
    /// off-screen <c>GaussianSet</c>). Latency ticks default to zero.
    /// </summary>
    /// <param name="width">Frame width in pixels.</param>
    /// <param name="height">Frame height in pixels.</param>
    /// <returns>A <see cref="FrameBuffer"/> filled with transparent black pixels.</returns>
    public static FrameBuffer Transparent(int width, int height)
        => new(width, height, new byte[width * height * 4], 0);
}
