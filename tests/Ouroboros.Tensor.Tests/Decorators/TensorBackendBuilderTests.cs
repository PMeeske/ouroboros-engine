// <copyright file="TensorBackendBuilderTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace Ouroboros.Tests.Decorators;

[Trait("Category", "Unit")]
public sealed class TensorBackendBuilderTests
{
    [Fact]
    public void Build_NoDependencies_ReturnsCoreBackend()
    {
        var backend = new TensorBackendBuilder(CpuTensorBackend.Instance).Build();
        backend.Should().BeSameAs(CpuTensorBackend.Instance);
    }

    [Fact]
    public void WithValidation_WrapsBackend()
    {
        var backend = new TensorBackendBuilder(CpuTensorBackend.Instance)
            .WithValidation()
            .Build();

        backend.Should().BeOfType<ValidatingTensorBackend>();
    }

    [Fact]
    public void WithLogging_WrapsBackend()
    {
        var logger = NullLogger.Instance;
        var backend = new TensorBackendBuilder(CpuTensorBackend.Instance)
            .WithLogging(logger)
            .Build();

        backend.Should().BeOfType<LoggingTensorBackend>();
    }

    [Fact]
    public void WithMetrics_WrapsBackend()
    {
        var backend = new TensorBackendBuilder(CpuTensorBackend.Instance)
            .WithMetrics()
            .Build();

        backend.Should().BeOfType<MetricsTensorBackend>();
    }

    [Fact]
    public void WithCaching_WrapsBackend()
    {
        var backend = new TensorBackendBuilder(CpuTensorBackend.Instance)
            .WithCaching()
            .Build();

        backend.Should().BeOfType<CachingTensorBackend>();
    }

    [Fact]
    public void FullChain_ComposesAllDecorators_WorksEndToEnd()
    {
        // Arrange
        var messages = new List<string>();
        var logger = NullLogger.Instance;

        var backend = new TensorBackendBuilder(CpuTensorBackend.Instance)
            .WithValidation()
            .WithCaching()
            .WithLogging(logger)
            .WithMetrics()
            .Build();

        var data = new float[] { 1f, 2f, 3f };

        // Act
        using var tensor = backend.Create(TensorShape.Of(3), data.AsSpan());
        using var a = backend.Create(TensorShape.Of(3), data.AsSpan());
        using var b = backend.Create(TensorShape.Of(3), data.AsSpan());
        var addResult = backend.Add(a, b);

        // Assert
        addResult.IsSuccess.Should().BeTrue();
        addResult.Value.AsSpan().ToArray().Should().Equal(2f, 4f, 6f);
        addResult.Value.Dispose();
    }
}
