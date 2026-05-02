// <copyright file="MetricTensorFieldTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Tensor.Riemannian;
using Xunit;

namespace Ouroboros.Tensor.Tests.Riemannian;

public sealed class MetricTensorFieldTests
{
    [Fact]
    public void DefaultField_IsIdentity()
    {
        var field = new MetricTensorField(3);
        var g = field.ComputeMetricTensor(stackalloc float[] { 0f, 0f, 0f });

        g[0, 0].Should().Be(1f);
        g[1, 1].Should().Be(1f);
        g[2, 2].Should().Be(1f);
    }

    [Fact]
    public void IsPositiveDefinite_OnIdentity_IsTrue()
    {
        var field = new MetricTensorField(4);
        var g = field.ComputeMetricTensor(stackalloc float[] { 0f, 0f, 0f, 0f });

        MetricTensorField.IsPositiveDefinite(g).Should().BeTrue();
    }

    [Fact]
    public void IsPositiveDefinite_OnZeroMatrix_IsFalse()
    {
        float[,] zero = new float[2, 2];
        MetricTensorField.IsPositiveDefinite(zero).Should().BeFalse();
    }

    [Fact]
    public void IsPositiveDefinite_OnNegativeDiagonal_IsFalse()
    {
        float[,] g = new float[2, 2];
        g[0, 0] = -1f;
        g[1, 1] = 1f;
        MetricTensorField.IsPositiveDefinite(g).Should().BeFalse();
    }

    [Fact]
    public void Update_FloorsNegativesToStability()
    {
        var field = new MetricTensorField(3);
        field.Update(stackalloc float[] { -1f, 2f, float.NaN });
        var g = field.ComputeMetricTensor(stackalloc float[] { 0f, 0f, 0f });

        MetricTensorField.IsPositiveDefinite(g).Should().BeTrue();
        g[1, 1].Should().Be(2f);
    }

    [Fact]
    public void ComputeDistance_OnIdentity_IsEuclidean()
    {
        var field = new MetricTensorField(3);
        float d = field.ComputeDistance(
            stackalloc float[] { 1f, 2f, 3f },
            stackalloc float[] { 4f, 6f, 3f });

        // sqrt(9 + 16 + 0) = 5
        d.Should().BeApproximately(5f, 1e-5f);
    }

    [Fact]
    public void ComputeDistance_IsSymmetric()
    {
        var field = new MetricTensorField(2);
        float ab = field.ComputeDistance(stackalloc float[] { 1f, 2f }, stackalloc float[] { 3f, 4f });
        float ba = field.ComputeDistance(stackalloc float[] { 3f, 4f }, stackalloc float[] { 1f, 2f });

        ab.Should().Be(ba);
    }

    [Fact]
    public void ComputeDistance_IsZeroOnIdentity()
    {
        var field = new MetricTensorField(3);
        float d = field.ComputeDistance(
            stackalloc float[] { 1f, 2f, 3f },
            stackalloc float[] { 1f, 2f, 3f });
        d.Should().Be(0f);
    }
}
