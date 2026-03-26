// <copyright file="RemoteTensorBackendTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using Ouroboros.Tensor.Configuration;

namespace Ouroboros.Tests.Backends;

[Trait("Category", "Unit")]
public sealed class RemoteTensorBackendTests : IDisposable
{
    private readonly TensorServiceOptions _options;
    private readonly TensorServiceClient _client;
    private readonly RemoteTensorBackend _sut;

    public RemoteTensorBackendTests()
    {
        _options = new TensorServiceOptions
        {
            BaseUrl = new Uri("http://localhost:8768"),
            TimeoutSeconds = 30,
            MaxRetryAttempts = 3
        };

        // Create HttpClient without actual network calls
        var httpClient = new HttpClient
        {
            BaseAddress = _options.BaseUrl,
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };
        _client = Substitute.For<TensorServiceClient>(httpClient, _options);
        _sut = new RemoteTensorBackend(_client, _options);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Device Detection ─────────────────────────────────────────────────────

    [Fact]
    public void Device_ReturnsCuda_WhenServiceReportsCuda()
    {
        // Arrange
        _client.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HealthResponse("healthy", "cuda")));

        // Act
        var backend = new RemoteTensorBackend(_client, _options);

        // Assert
        backend.Device.Should().Be(DeviceType.Cuda);
    }

    [Fact]
    public void Device_ReturnsCpu_WhenServiceReportsCpu()
    {
        // Arrange
        _client.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new HealthResponse("healthy", "cpu")));

        // Act
        var backend = new RemoteTensorBackend(_client, _options);

        // Assert
        backend.Device.Should().Be(DeviceType.Cpu);
    }

    [Fact]
    public void Device_ReturnsCpu_WhenHealthCheckFails()
    {
        // Arrange
        _client.GetHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<HealthResponse>(new HttpRequestException("Connection refused")));

        // Act
        var backend = new RemoteTensorBackend(_client, _options);

        // Assert - Falls back to CPU when health check fails
        backend.Device.Should().Be(DeviceType.Cpu);
    }

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ReturnsTensorWithCorrectShapeAndData()
    {
        // Arrange
        var data = new float[] { 1f, 2f, 3f, 4f };
        var shape = TensorShape.Of(2, 2);

        // Act
        using var tensor = _sut.Create(shape, data.AsSpan());

        // Assert
        tensor.Shape.Should().Be(shape);
        tensor.AsSpan().ToArray().Should().Equal(data);
    }

    [Fact]
    public void Create_ThrowsWhenDataLengthMismatch()
    {
        // Arrange
        var data = new float[] { 1f, 2f, 3f };
        var shape = TensorShape.Of(2, 2); // Expects 4 elements

        // Act & Assert
        var act = () => _sut.Create(shape, data.AsSpan());
        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not match*");
    }

    // ── CreateUninitialized ─────────────────────────────────────────────────

    [Fact]
    public void CreateUninitialized_ReturnsTensorWithCorrectShape()
    {
        // Arrange
        var shape = TensorShape.Of(3, 4);

        // Act
        using var tensor = _sut.CreateUninitialized(shape);

        // Assert
        tensor.Shape.Should().Be(shape);
    }

    // ── FromMemory ─────────────────────────────────────────────────────────

    [Fact]
    public void FromMemory_WrapsExistingMemoryWithoutCopy()
    {
        // Arrange
        var data = new float[] { 10f, 20f, 30f };
        var memory = data.AsMemory();
        var shape = TensorShape.Of(3);

        // Act
        using var tensor = _sut.FromMemory(memory, shape);

        // Assert
        tensor.Shape.Should().Be(shape);
        tensor.AsSpan().ToArray().Should().Equal(data);

        // Verify zero-copy: modifying original should affect tensor
        data[0] = 100f;
        tensor.AsSpan()[0].Should().Be(100f);
    }

    [Fact]
    public void FromMemory_ThrowsWhenMemoryLengthMismatch()
    {
        // Arrange
        var data = new float[] { 1f, 2f, 3f };
        var shape = TensorShape.Of(4); // Expects 4 elements

        // Act & Assert
        var act = () => _sut.FromMemory(data.AsMemory(), shape);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*does not match*");
    }

    // ── MatMul ──────────────────────────────────────────────────────────────

    [Fact]
    public void MatMul_ReturnsSuccess_ForValidShapes()
    {
        // Arrange
        var aData = new float[] { 1f, 2f, 3f, 4f };
        var bData = new float[] { 5f, 6f, 7f, 8f };
        var resultData = new float[] { 19f, 22f, 43f, 50f };
        var resultShape = new List<int> { 2, 2 };

        _client.MatMulAsync(Arg.Any<TensorData>(), Arg.Any<TensorData>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TensorDataResponse(resultShape, resultData.ToList())));

        using var a = _sut.Create(TensorShape.Of(2, 2), aData.AsSpan());
        using var b = _sut.Create(TensorShape.Of(2, 2), bData.AsSpan());

        // Act
        var result = _sut.MatMul(a, b);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var tensor = result.Value;
        tensor.Shape.Should().Be(TensorShape.Of(2, 2));
        tensor.AsSpan().ToArray().Should().Equal(resultData);
    }

    [Fact]
    public void MatMul_ReturnsFailure_ForMismatchedInnerDimensions()
    {
        // Arrange
        using var a = _sut.Create(TensorShape.Of(2, 3), new float[6]);
        using var b = _sut.Create(TensorShape.Of(4, 2), new float[8]);

        // Act
        var result = _sut.MatMul(a, b);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("mismatch");
    }

    [Fact]
    public void MatMul_ReturnsFailure_ForRank1Tensors()
    {
        // Arrange
        using var a = _sut.Create(TensorShape.Of(3), new float[] { 1f, 2f, 3f });
        using var b = _sut.Create(TensorShape.Of(3), new float[] { 4f, 5f, 6f });

        // Act
        var result = _sut.MatMul(a, b);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("rank-2");
    }

    // ── FFT ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FFT_ReturnsSuccess_WithCorrectOutputShape()
    {
        // Arrange
        var inputData = new float[] { 1f, 0f, 0f, 0f };
        var resultData = new float[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f }; // Complex as float pairs
        var resultShape = new List<int> { 4 };

        _client.FftAsync(Arg.Any<TensorData>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TensorDataResponse(resultShape, resultData.ToList())));

        using var input = _sut.Create(TensorShape.Of(4), inputData.AsSpan());

        // Act
        var result = _sut.FFT(input, dimensions: 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var tensor = result.Value;
        tensor.Shape.Should().Be(TensorShape.Of(4));
    }

    [Fact]
    public void FFT_CallsClient_WithCorrectParameters()
    {
        // Arrange
        var inputData = new float[] { 1f, 0f, 0f, 0f };
        using var input = _sut.Create(TensorShape.Of(4), inputData.AsSpan());

        _client.FftAsync(Arg.Any<TensorData>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TensorDataResponse(new List<int> { 4 }, new List<float> { 1, 0, 0, 0 })));

        // Act
        _sut.FFT(input, dimensions: 2);

        // Assert
        _client.Received(1).FftAsync(
            Arg.Is<TensorData>(t => t.Shape.SequenceEqual(new[] { 4 })),
            Arg.Is(2),
            Arg.Any<CancellationToken>());
    }

    // ── Add ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Add_DelegatesToCpuTensorBackend()
    {
        // Arrange
        var aData = new float[] { 1f, 2f, 3f };
        var bData = new float[] { 4f, 5f, 6f };
        using var a = _sut.Create(TensorShape.Of(3), aData.AsSpan());
        using var b = _sut.Create(TensorShape.Of(3), bData.AsSpan());

        // Act - Add uses CpuTensorBackend, no remote call
        var result = _sut.Add(a, b);

        // Assert
        result.IsSuccess.Should().BeTrue();
        using var tensor = result.Value;
        tensor.AsSpan().ToArray().Should().Equal(5f, 7f, 9f);
    }

    // ── Error Handling ──────────────────────────────────────────────────────

    [Fact]
    public void MatMul_ReturnsFailure_WhenRemoteCallFails()
    {
        // Arrange
        _client.MatMulAsync(Arg.Any<TensorData>(), Arg.Any<TensorData>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TensorDataResponse>(new HttpRequestException("Connection refused")));

        using var a = _sut.Create(TensorShape.Of(2, 2), new float[4]);
        using var b = _sut.Create(TensorShape.Of(2, 2), new float[4]);

        // Act
        var result = _sut.MatMul(a, b);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("failed");
    }

    [Fact]
    public void FFT_ReturnsFailure_WhenRemoteCallFails()
    {
        // Arrange
        _client.FftAsync(Arg.Any<TensorData>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TensorDataResponse>(new HttpRequestException("Connection refused")));

        using var input = _sut.Create(TensorShape.Of(4), new float[4]);

        // Act
        var result = _sut.FFT(input);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("failed");
    }
}