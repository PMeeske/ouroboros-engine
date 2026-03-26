// <copyright file="CachingTensorBackendTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Decorators;

[Trait("Category", "Unit")]
public sealed class CachingTensorBackendTests
{
    [Fact]
    public void Create_SameShapeAndData_ReturnsCachedTensor()
    {
        // Arrange — use a counting wrapper because ReadOnlySpan<T> is not mockable
        var inner = new CountingBackend();
        var sut = new CachingTensorBackend(inner);
        var data = new float[] { 1f, 2f, 3f };
        var shape = TensorShape.Of(3);

        // Act — call twice with same args
        var t1 = sut.Create(shape, data.AsSpan());
        var t2 = sut.Create(shape, data.AsSpan());

        // Assert — same tensor reference returned on second call; inner called only once
        t1.Should().BeSameAs(t2);
        inner.CreateCallCount.Should().Be(1);
    }

    [Fact]
    public void Create_DifferentData_CallsInnerTwice()
    {
        var inner = new CountingBackend();
        var sut = new CachingTensorBackend(inner);

        sut.Create(TensorShape.Of(3), new float[] { 1f, 2f, 3f }.AsSpan());
        sut.Create(TensorShape.Of(3), new float[] { 4f, 5f, 6f }.AsSpan());

        inner.CreateCallCount.Should().Be(2);
    }

    [Fact]
    public void Create_DifferentShape_CallsInnerTwice()
    {
        var inner = new CountingBackend();
        var sut = new CachingTensorBackend(inner);

        sut.Create(TensorShape.Of(2), new float[] { 1f, 2f }.AsSpan());
        sut.Create(TensorShape.Of(3), new float[] { 1f, 2f, 3f }.AsSpan());

        inner.CreateCallCount.Should().Be(2);
    }

    [Fact]
    public void MatMul_AlwaysDelegatesToInner()
    {
        // MatMul results depend on operand state — must not be cached
        var inner = new CountingBackend();
        var sut = new CachingTensorBackend(inner);

        using var a = CpuTensorBackend.Instance.Create(TensorShape.Of(2, 2), new float[4]);
        using var b = CpuTensorBackend.Instance.Create(TensorShape.Of(2, 2), new float[4]);

        sut.MatMul(a, b);
        sut.MatMul(a, b);

        inner.MatMulCallCount.Should().Be(2);
    }

    [Fact]
    public void Add_AlwaysDelegatesToInner()
    {
        var inner = new CountingBackend();
        var sut = new CachingTensorBackend(inner);

        using var a = CpuTensorBackend.Instance.Create(TensorShape.Of(3), new float[] { 1f, 2f, 3f });
        using var b = CpuTensorBackend.Instance.Create(TensorShape.Of(3), new float[] { 4f, 5f, 6f });

        sut.Add(a, b);
        sut.Add(a, b);

        inner.AddCallCount.Should().Be(2);
    }

    [Fact]
    public void Device_DelegatesToInner()
    {
        var inner = new CountingBackend();
        new CachingTensorBackend(inner).Device.Should().Be(DeviceType.Cpu);
    }

    // ── Counting backend ─────────────────────────────────────────────────────

    /// <summary>
    /// Test double that tracks invocation counts without using mocking frameworks
    /// (avoids the ReadOnlySpan&lt;T&gt; mock limitation).
    /// </summary>
    private sealed class CountingBackend : ITensorBackend
    {
        public int CreateCallCount { get; private set; }
        public int MatMulCallCount { get; private set; }
        public int AddCallCount { get; private set; }

        public DeviceType Device => DeviceType.Cpu;

        public ITensor<float> Create(TensorShape shape, ReadOnlySpan<float> data)
        {
            CreateCallCount++;
            return TensorMemoryPool.RentAndFill(shape, data);
        }

        public ITensor<float> CreateUninitialized(TensorShape shape)
            => TensorMemoryPool.Rent<float>(shape);

        public ITensor<float> FromMemory(ReadOnlyMemory<float> memory, TensorShape shape)
            => CpuTensorBackend.Instance.FromMemory(memory, shape);

        public Result<ITensor<float>, string> MatMul(ITensor<float> a, ITensor<float> b)
        {
            MatMulCallCount++;
            return CpuTensorBackend.Instance.MatMul(a, b);
        }

        public Result<ITensor<float>, string> Add(ITensor<float> a, ITensor<float> b)
        {
            AddCallCount++;
            return CpuTensorBackend.Instance.Add(a, b);
        }
    }
}
