// <copyright file="CpuGaussianRasterizer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Rasterizers;

/// <summary>
/// Pure-C# CPU rasterizer implementing <see cref="IGaussianRasterizer"/>.
/// Ports the tile-based orthographic alpha-blend algorithm from the App-layer
/// <c>GaussianSplatRasterizer.Render</c> and re-homes it inside
/// <c>Ouroboros.Tensor</c> so every Application-layer renderer calls the
/// adapter contract instead of a static utility. CPU baseline + correctness
/// reference for plan 03's HLSL compute-shader implementation.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm is an orthographic 3-sigma tile-based raster at 16×16 tiles.
/// Scales / opacities are assumed pre-activated (exp / sigmoid applied by the
/// caller — the existing training pipeline's <c>exports</c> store already-
/// activated values on disk). Colors are direct RGB in <c>[0, 1]</c> (no SH
/// lifting — the repo checkpoints are degree-0).
/// </para>
/// <para>
/// Even on CPU the dispatch flows through <see cref="GpuScheduler"/> so the
/// telemetry surface (queue depth, latency histogram) covers both the CPU
/// baseline and the HLSL path uniformly. <see cref="RasterizerRequirements.Cpu"/>
/// declares zero estimated VRAM so the scheduler's overcommit guard passes
/// unconditionally.
/// </para>
/// </remarks>
public sealed class CpuGaussianRasterizer : IGaussianRasterizer
{
    private const int TileSize = 16;

    private readonly GpuScheduler? _scheduler;

    /// <summary>Creates a scheduler-less rasterizer (tests / no-orchestration paths).</summary>
    public CpuGaussianRasterizer()
        : this(scheduler: null)
    {
    }

    /// <summary>Creates a rasterizer that routes dispatches through <paramref name="scheduler"/>.</summary>
    /// <param name="scheduler">GPU scheduler providing priority + telemetry. May be null for direct execution.</param>
    public CpuGaussianRasterizer(GpuScheduler? scheduler)
    {
        _scheduler = scheduler;
    }

    /// <inheritdoc />
    public Task<FrameBuffer> RasterizeAsync(
        GaussianSet gaussians,
        CameraParams camera,
        GpuResourceRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gaussians);
        ArgumentNullException.ThrowIfNull(camera);

        if (_scheduler is null)
        {
            return Task.FromResult(RenderDirect(gaussians, camera));
        }

        return _scheduler.ScheduleAsync(
            GpuTaskPriority.Realtime,
            requirements,
            () => Task.FromResult(RenderDirect(gaussians, camera)),
            cancellationToken);
    }

    private static FrameBuffer RenderDirect(GaussianSet gaussians, CameraParams camera)
    {
        var sw = Stopwatch.StartNew();
        int width = camera.Width;
        int height = camera.Height;
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Camera width/height must be positive.", nameof(camera));

        // Extract orthographic translation (column-major view matrix). Maps
        // world → screen with only xy translation; matches CreateOrthographicIdentity
        // path used by the legacy renderer.
        float tx = camera.ViewMatrix.Length >= 16 ? camera.ViewMatrix[12] : 0f;
        float ty = camera.ViewMatrix.Length >= 16 ? camera.ViewMatrix[13] : 0f;

        int n = gaussians.Count;
        float[] canvas = new float[width * height * 3];
        float[] weightSum = new float[width * height];

        if (n > 0)
        {
            RasterizeTiled(
                gaussians.Positions, gaussians.Scales, gaussians.Opacities, gaussians.Colors,
                n, tx, ty, width, height, canvas, weightSum);
        }

        byte[] rgba = new byte[width * height * 4];
        for (int p = 0; p < width * height; p++)
        {
            float w = MathF.Max(weightSum[p], 1e-6f);
            int srcIdx = p * 3;
            int dstIdx = p * 4;
            float r = canvas[srcIdx] / w;
            float g = canvas[srcIdx + 1] / w;
            float b = canvas[srcIdx + 2] / w;
            rgba[dstIdx] = ClampToByte(r);
            rgba[dstIdx + 1] = ClampToByte(g);
            rgba[dstIdx + 2] = ClampToByte(b);
            rgba[dstIdx + 3] = weightSum[p] > 1e-6f ? (byte)255 : (byte)0;
        }

        sw.Stop();
        return new FrameBuffer(width, height, rgba, sw.ElapsedTicks);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void RasterizeTiled(
        ReadOnlySpan<float> positions,
        ReadOnlySpan<float> scales,
        ReadOnlySpan<float> opacities,
        ReadOnlySpan<float> colors,
        int n,
        float tx,
        float ty,
        int width,
        int height,
        Span<float> canvas,
        Span<float> weightSum)
    {
        int tilesX = (width + TileSize - 1) / TileSize;
        int tilesY = (height + TileSize - 1) / TileSize;

        for (int tileY = 0; tileY < tilesY; tileY++)
        {
            int y0 = tileY * TileSize;
            int y1 = Math.Min(y0 + TileSize, height);

            for (int tileX = 0; tileX < tilesX; tileX++)
            {
                int x0 = tileX * TileSize;
                int x1 = Math.Min(x0 + TileSize, width);

                float tileCx = (x0 + x1) * 0.5f;
                float tileCy = (y0 + y1) * 0.5f;
                const float TileRadius = TileSize * 0.707f;

                for (int g = 0; g < n; g++)
                {
                    float gx = positions[g * 3] + tx;
                    float gy = positions[g * 3 + 1] + ty;

                    float s = (scales[g * 3] + scales[g * 3 + 1] + scales[g * 3 + 2]) / 3.0f;
                    float sigma = MathF.Max(0.5f, MathF.Min(s, 30.0f) / 2.0f);
                    float cutoff = sigma * 3.0f;

                    float dxCenter = MathF.Abs(gx - tileCx);
                    float dyCenter = MathF.Abs(gy - tileCy);
                    if (dxCenter > cutoff + TileRadius || dyCenter > cutoff + TileRadius)
                        continue;

                    float opacity = opacities[g];
                    float cr = colors[g * 3];
                    float cg = colors[g * 3 + 1];
                    float cb = colors[g * 3 + 2];
                    float invSigma2 = 1.0f / (2.0f * sigma * sigma);

                    for (int py = y0; py < y1; py++)
                    {
                        float dy = py - gy;
                        float dy2 = dy * dy;

                        for (int px = x0; px < x1; px++)
                        {
                            float dx = px - gx;
                            float dist2 = dx * dx + dy2;
                            float gauss = MathF.Exp(-dist2 * invSigma2) * opacity;
                            if (gauss < 1e-6f) continue;

                            int pixelIdx = py * width + px;
                            weightSum[pixelIdx] += gauss;
                            canvas[pixelIdx * 3] += gauss * cr;
                            canvas[pixelIdx * 3 + 1] += gauss * cg;
                            canvas[pixelIdx * 3 + 2] += gauss * cb;
                        }
                    }
                }
            }
        }
    }

    private static byte ClampToByte(float v)
    {
        int scaled = (int)MathF.Round(MathF.Max(0f, MathF.Min(1f, v)) * 255.0f);
        return (byte)scaled;
    }
}
