// <copyright file="CpuGaussianRasterizerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Rasterizers;

namespace Ouroboros.Tensor.Tests.Rasterizers;

public class CpuGaussianRasterizerTests
{
    [Fact]
    public async Task RasterizeAsync_SingleGaussianAtCenter_ProducesNonZeroCenterPixel()
    {
        GaussianSet set = new(
            count: 1,
            positions: [32f, 32f, 0f],
            scales: [5f, 5f, 5f],
            rotations: [1f, 0f, 0f, 0f],
            opacities: [1.0f],
            colors: [1.0f, 0.0f, 0.0f],
            triangleIndices: [0L],
            barycentricWeights: [1f, 0f, 0f]);

        CameraParams camera = CameraParams.CreateOrthographicIdentity(64, 64);
        var rasterizer = new CpuGaussianRasterizer();

        FrameBuffer frame = await rasterizer.RasterizeAsync(
            set, camera, RasterizerRequirements.Cpu);

        frame.Width.Should().Be(64);
        frame.Height.Should().Be(64);
        frame.Rgba.Should().HaveCount(64 * 64 * 4);

        // Center pixel (32, 32): red channel should be near 255, alpha 255.
        int centerIdx = (32 * 64 + 32) * 4;
        frame.Rgba[centerIdx].Should().BeGreaterThan(200, "gaussian at center should light the red channel");
        frame.Rgba[centerIdx + 3].Should().Be(255, "center pixel covered → full alpha");
    }

    [Fact]
    public async Task RasterizeAsync_EmptyGaussianSet_Rejected()
    {
        CameraParams camera = CameraParams.CreateOrthographicIdentity(32, 32);
        var rasterizer = new CpuGaussianRasterizer();

        // GaussianSet's constructor rejects count == 0; we verify the contract
        // at that layer instead of rendering an empty set here.
        Action construct = () => new GaussianSet(
            count: 0,
            positions: [],
            scales: [],
            rotations: [],
            opacities: [],
            colors: [],
            triangleIndices: [],
            barycentricWeights: []);

        construct.Should().Throw<ArgumentException>();
        await Task.Yield();
    }

    [Fact]
    public async Task RasterizeAsync_OffscreenGaussian_AlphaZero()
    {
        GaussianSet set = new(
            count: 1,
            positions: [999f, 999f, 0f],
            scales: [1f, 1f, 1f],
            rotations: [1f, 0f, 0f, 0f],
            opacities: [1.0f],
            colors: [1.0f, 1.0f, 1.0f],
            triangleIndices: [0L],
            barycentricWeights: [1f, 0f, 0f]);

        CameraParams camera = CameraParams.CreateOrthographicIdentity(16, 16);
        var rasterizer = new CpuGaussianRasterizer();

        FrameBuffer frame = await rasterizer.RasterizeAsync(
            set, camera, RasterizerRequirements.Cpu);

        // No pixel should be touched.
        for (int p = 0; p < 16 * 16; p++)
        {
            frame.Rgba[p * 4 + 3].Should().Be(0, "off-screen gaussian must not contribute");
        }
    }

    [Fact]
    public async Task RasterizeAsync_RecordsLatencyTicks()
    {
        GaussianSet set = new(
            count: 1,
            positions: [8f, 8f, 0f],
            scales: [2f, 2f, 2f],
            rotations: [1f, 0f, 0f, 0f],
            opacities: [0.8f],
            colors: [0.5f, 0.5f, 0.5f],
            triangleIndices: [0L],
            barycentricWeights: [1f, 0f, 0f]);

        CameraParams camera = CameraParams.CreateOrthographicIdentity(16, 16);
        var rasterizer = new CpuGaussianRasterizer();

        FrameBuffer frame = await rasterizer.RasterizeAsync(
            set, camera, RasterizerRequirements.Cpu);

        frame.RasterLatencyTicks.Should().BeGreaterThan(0, "stopwatch ticks populated for plan-06 probe");
    }
}
