// <copyright file="CameraParams.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tensor.Abstractions;

/// <summary>
/// Immutable camera description passed to <see cref="IGaussianRasterizer"/>.
/// Holds explicit 4x4 column-major <see cref="ViewMatrix"/> and
/// <see cref="ProjectionMatrix"/> so the adapter can support both the
/// orthographic-identity CPU baseline and the perspective HLSL rasterizer
/// shipped in plan 03.
/// </summary>
/// <param name="ViewMatrix">4x4 column-major view matrix (16 floats).</param>
/// <param name="ProjectionMatrix">4x4 column-major projection matrix (16 floats).</param>
/// <param name="Width">Output frame width in pixels.</param>
/// <param name="Height">Output frame height in pixels.</param>
/// <param name="NearPlane">Near clipping plane (default 0.01).</param>
/// <param name="FarPlane">Far clipping plane (default 100.0).</param>
public sealed record CameraParams(
    float[] ViewMatrix,
    float[] ProjectionMatrix,
    int Width,
    int Height,
    float NearPlane = 0.01f,
    float FarPlane = 100.0f)
{
    /// <summary>
    /// Returns <see langword="true"/> when <see cref="ViewMatrix"/> is the
    /// 4x4 identity. The CPU rasterizer uses this check to fast-path the
    /// orthographic pipeline ported from the legacy App-layer algorithm.
    /// </summary>
    /// <returns>True when every diagonal element is 1 and every off-diagonal element is 0.</returns>
    public bool IsIdentityView()
    {
        if (ViewMatrix.Length != 16) return false;
        for (int i = 0; i < 16; i++)
        {
            int row = i / 4;
            int col = i % 4;
            float expected = row == col ? 1.0f : 0.0f;
            if (MathF.Abs(ViewMatrix[i] - expected) > 1e-6f) return false;
        }
        return true;
    }

    /// <summary>
    /// Builds an orthographic-identity camera mirroring the legacy App-layer
    /// <c>GaussianSplatRasterizer.Render</c> setup (identity view, no
    /// projection math — positions are already in pixel space).
    /// </summary>
    /// <param name="width">Output frame width in pixels.</param>
    /// <param name="height">Output frame height in pixels.</param>
    /// <returns>A <see cref="CameraParams"/> instance with identity view + projection matrices.</returns>
    public static CameraParams CreateOrthographicIdentity(int width, int height)
    {
        float[] identity = new float[16];
        identity[0] = 1.0f;
        identity[5] = 1.0f;
        identity[10] = 1.0f;
        identity[15] = 1.0f;

        float[] projection = new float[16];
        projection[0] = 1.0f;
        projection[5] = 1.0f;
        projection[10] = 1.0f;
        projection[15] = 1.0f;

        return new CameraParams(identity, projection, width, height);
    }
}
