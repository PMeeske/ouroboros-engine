// <copyright file="GpuTensorNodeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;

namespace Ouroboros.Tests.Orchestration;

// ═══════════════════════════════════════════════════════════════════════
// Test doubles
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// CPU-only test node that scales every element by a factor.
/// No GPU required — validates reactive wiring and scheduler integration.
/// </summary>
internal sealed class ScaleNode : GpuTensorNode
{
    private readonly float _factor;

    public ScaleNode(string id, GpuScheduler scheduler, float factor = 2f)
        : base(id, scheduler, CpuTensorBackend.Instance)
    {
        _factor = factor;
    }

    protected override Result<ITensor<float>, string> Execute(ITensor<float> input)
    {
        var data = input.AsSpan().ToArray();
        for (int i = 0; i < data.Length; i++)
            data[i] *= _factor;
        return Result<ITensor<float>, string>.Success(
            CpuTensorBackend.Instance.Create(input.Shape, data.AsSpan()));
    }
}

/// <summary>Node that always returns a failure result.</summary>
internal sealed class FailingNode : GpuTensorNode
{
    public FailingNode(string id, GpuScheduler scheduler)
        : base(id, scheduler, CpuTensorBackend.Instance) { }

    protected override Result<ITensor<float>, string> Execute(ITensor<float> input)
        => Result<ITensor<float>, string>.Failure("Intentional failure for testing.");
}

// ═══════════════════════════════════════════════════════════════════════
// GpuTensorNode Tests
// ═══════════════════════════════════════════════════════════════════════

[Trait("Category", "Unit")]
public sealed class GpuTensorNodeTests : IDisposable
{
    private readonly GpuScheduler _scheduler = new(totalVramBytes: 1024 * 1024 * 1024);

    private static ITensor<float> CreateTestTensor(params float[] data)
        => TensorMemoryPool.RentAndFill<float>(TensorShape.Of(data.Length), data.AsSpan());

    [Fact]
    public async Task ProcessAsync_ProducesOutput_OnSuccess()
    {
        // Arrange
        using var sut = new ScaleNode("test-scale", _scheduler, factor: 3f);
        ITensor<float>? received = null;
        sut.Output.Subscribe(t => received = t);

        // Act
        await sut.ProcessAsync(CreateTestTensor(1, 2, 3));
        await Task.Delay(100);

        // Assert
        received.Should().NotBeNull();
        received!.AsSpan().ToArray().Should().Equal(3f, 6f, 9f);
        received.Dispose();
    }

    [Fact]
    public async Task ProcessAsync_EmitsError_OnFailure()
    {
        // Arrange
        using var sut = new FailingNode("test-fail", _scheduler);
        Exception? receivedError = null;
        sut.Output.Subscribe(onNext: _ => { }, onError: ex => receivedError = ex);

        // Act
        await sut.ProcessAsync(CreateTestTensor(1, 2));
        await Task.Delay(100);

        // Assert
        receivedError.Should().NotBeNull();
        receivedError!.Message.Should().Contain("Intentional failure");
    }

    [Fact]
    public async Task StateChanges_EmitsCorrectLifecycle()
    {
        // Arrange
        using var sut = new ScaleNode("test-states", _scheduler);
        var states = new List<GpuNodeState>();
        sut.StateChanges.Subscribe(s => states.Add(s));

        // Act
        await sut.ProcessAsync(CreateTestTensor(1, 2));
        await Task.Delay(100);

        // Assert
        states.Should().Contain(GpuNodeState.Queued);
        states.Should().Contain(GpuNodeState.Executing);
        states.Should().Contain(GpuNodeState.Idle);
    }

    [Fact]
    public async Task ConnectTo_WiresUpstreamToDownstream()
    {
        // Arrange: upstream ×2, downstream ×3 → input 5 → 5×2×3 = 30
        using var upstream = new ScaleNode("up", _scheduler, factor: 2f);
        using var downstream = new ScaleNode("down", _scheduler, factor: 3f);
        upstream.ConnectTo(downstream);

        ITensor<float>? result = null;
        downstream.Output.Subscribe(t => result = t);

        // Act
        await upstream.ProcessAsync(CreateTestTensor(5f));
        await Task.Delay(200);

        // Assert
        result.Should().NotBeNull();
        result!.AsSpan().ToArray().Should().Equal(30f);
        result.Dispose();
    }

    [Fact]
    public async Task ForkTo_SendsOutputToMultipleDownstreams()
    {
        // Arrange
        using var source = new ScaleNode("src", _scheduler, factor: 1f);
        using var a = new ScaleNode("a", _scheduler, factor: 2f);
        using var b = new ScaleNode("b", _scheduler, factor: 10f);
        source.ForkTo(a, b);

        var resultsA = new List<float>();
        var resultsB = new List<float>();
        a.Output.Subscribe(t => { resultsA.Add(t.AsSpan()[0]); t.Dispose(); });
        b.Output.Subscribe(t => { resultsB.Add(t.AsSpan()[0]); t.Dispose(); });

        // Act
        await source.ProcessAsync(CreateTestTensor(7f));
        await Task.Delay(300);

        // Assert
        resultsA.Should().Contain(14f);
        resultsB.Should().Contain(70f);
    }

    [Fact]
    public void Dispose_SetsStateToDisposed()
    {
        var sut = new ScaleNode("test-dispose", _scheduler);
        sut.Dispose();

        sut.State.Should().Be(GpuNodeState.Disposed);
    }

    [Fact]
    public void NodeId_ReturnsConfiguredId()
    {
        using var sut = new ScaleNode("my-node", _scheduler);
        sut.NodeId.Should().Be("my-node");
    }

    [Fact]
    public void Priority_IsConfigurable()
    {
        using var sut = new ScaleNode("p-node", _scheduler);
        sut.Priority = GpuTaskPriority.Realtime;
        sut.Priority.Should().Be(GpuTaskPriority.Realtime);
    }

    public void Dispose() => _scheduler.Dispose();
}

// ═══════════════════════════════════════════════════════════════════════
// TensorReactiveExtensions Tests
// ═══════════════════════════════════════════════════════════════════════

[Trait("Category", "Unit")]
public sealed class TensorReactiveExtensionsTests
{
    [Fact]
    public async Task ToObservable_ConvertsAsyncEnumerable()
    {
        var items = new List<int>();

        await TensorReactiveExtensions.ToObservable(AsyncSequence(1, 2, 3)).ForEachAsync(i => items.Add(i));

        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ToAsyncEnumerable_ConvertsObservableBack()
    {
        var source = new[] { 10, 20, 30 }.ToObservable();
        var items = new List<int>();

        await foreach (var item in source.ToAsyncEnumerable())
            items.Add(item);

        items.Should().Equal(10, 20, 30);
    }

    [Fact]
    public async Task ToObservable_PropagatesErrors()
    {
        var act = () => TensorReactiveExtensions.ToObservable(FailingSequence<int>()).ForEachAsync(_ => { });

        await act.Should().ThrowAsync<Exception>().WithMessage("boom");
    }

    [Fact]
    public async Task ScheduleOnGpu_AppliesTransform()
    {
        // Arrange
        using var scheduler = new GpuScheduler(1024 * 1024 * 1024);
        var backend = CpuTensorBackend.Instance;
        var input = TensorMemoryPool.RentAndFill<float>(TensorShape.Of(3), new float[] { 2, 4, 6 });
        ITensor<float>? result = null;

        // Act
        await Observable.Return(input as ITensor<float>)
            .ScheduleOnGpu(
                scheduler,
                GpuTaskPriority.Normal,
                new GpuResourceRequirements(1024),
                tensor =>
                {
                    var data = tensor.AsSpan().ToArray();
                    for (int i = 0; i < data.Length; i++) data[i] /= 2;
                    return Result<ITensor<float>, string>.Success(
                        backend.Create(tensor.Shape, data.AsSpan()));
                })
            .ForEachAsync(t => result = t);

        // Assert
        result.Should().NotBeNull();
        result!.AsSpan().ToArray().Should().Equal(1f, 2f, 3f);
        result.Dispose();
    }

    [Fact]
    public async Task MergeOutputs_CombinesMultipleNodes()
    {
        // Arrange
        using var scheduler = new GpuScheduler(1024 * 1024 * 1024);
        using var node1 = new ScaleNode("n1", scheduler, factor: 1f);
        using var node2 = new ScaleNode("n2", scheduler, factor: 1f);
        var received = new List<float>();

        TensorReactiveExtensions.MergeOutputs(node1, node2)
            .Subscribe(t => { received.Add(t.AsSpan()[0]); t.Dispose(); });

        // Act
        await node1.ProcessAsync(
            TensorMemoryPool.RentAndFill<float>(TensorShape.Of(1), new float[] { 10f }));
        await node2.ProcessAsync(
            TensorMemoryPool.RentAndFill<float>(TensorShape.Of(1), new float[] { 20f }));
        await Task.Delay(200);

        // Assert
        received.Should().Contain(10f);
        received.Should().Contain(20f);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<T> AsyncSequence<T>(params T[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> FailingSequence<T>()
    {
        await Task.Yield();
        throw new Exception("boom");
        yield break;
    }
}
