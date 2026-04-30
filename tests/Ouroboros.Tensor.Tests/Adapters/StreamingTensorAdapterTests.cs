// <copyright file="StreamingTensorAdapterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;

namespace Ouroboros.Tests.Adapters;

[Trait("Category", "Unit")]
public sealed class StreamingTensorAdapterTests
{
    private readonly CpuTensorBackend _backend = CpuTensorBackend.Instance;

    [Fact]
    public async Task AdaptAsync_FullBatch_EmitsSingleTensor()
    {
        // Arrange
        var vectors = new[]
        {
            new float[] { 1f, 2f },
            new float[] { 3f, 4f },
        };

        // Act
        var tensors = new List<ITensor<float>>();
        await foreach (var t in StreamingTensorAdapter.AdaptAsync(ToAsync(vectors), _backend, batchSize: 2))
            tensors.Add(t);

        // Assert
        tensors.Should().HaveCount(1);
        tensors[0].Shape.Should().Be(TensorShape.Of(2, 2));
        tensors[0].AsSpan().ToArray().Should().Equal(1f, 2f, 3f, 4f);

        foreach (var t in tensors) t.Dispose();
    }

    [Fact]
    public async Task AdaptAsync_PartialFinalBatch_EmitsPartialTensor()
    {
        // Arrange: 3 vectors with batch size 2 → full batch [2,2] + partial [1,2]
        var vectors = new[]
        {
            new float[] { 1f, 2f },
            new float[] { 3f, 4f },
            new float[] { 5f, 6f },
        };

        // Act
        var tensors = new List<ITensor<float>>();
        await foreach (var t in StreamingTensorAdapter.AdaptAsync(ToAsync(vectors), _backend, batchSize: 2))
            tensors.Add(t);

        // Assert
        tensors.Should().HaveCount(2);
        tensors[0].Shape.Should().Be(TensorShape.Of(2, 2));
        tensors[1].Shape.Should().Be(TensorShape.Of(1, 2));

        foreach (var t in tensors) t.Dispose();
    }

    [Fact]
    public async Task AdaptAsync_EmptySource_EmitsNoTensors()
    {
        var tensors = new List<ITensor<float>>();
        await foreach (var t in StreamingTensorAdapter.AdaptAsync(ToAsync(Array.Empty<float[]>()), _backend, batchSize: 4))
            tensors.Add(t);

        tensors.Should().BeEmpty();
    }

    [Fact]
    public async Task AdaptAsync_InconsistentDimensions_ThrowsArgumentException()
    {
        var vectors = new[]
        {
            new float[] { 1f, 2f },
            new float[] { 3f },    // wrong dimension
        };

        var act = async () =>
        {
            await foreach (var _ in StreamingTensorAdapter.AdaptAsync(ToAsync(vectors), _backend, batchSize: 4))
            { /* consume */ }
        };

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*dimension*");
    }

    [Fact]
    public void AdaptAsync_ZeroBatchSize_ThrowsArgumentOutOfRangeException()
    {
        var act = () => StreamingTensorAdapter.AdaptAsync(ToAsync(Array.Empty<float[]>()), _backend, batchSize: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task AdaptAsync_CancellationToken_FlowsThroughToSource()
    {
        // Arrange: source respects cancellation token
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // pre-cancel

        var act = async () =>
        {
            await foreach (var t in StreamingTensorAdapter.AdaptAsync(
                CancellableSource(cts.Token), _backend, batchSize: 1, cts.Token))
            {
                t.Dispose();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<float[]> ToAsync(
        float[][] vectors,
        [EnumeratorCancellation] CancellationToken _ = default)
    {
        foreach (var v in vectors)
            yield return await Task.FromResult(v);
    }

#pragma warning disable S2190 // False positive: async iterator with yield is not recursion
    private static async IAsyncEnumerable<float[]> CancellableSource(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            yield return await Task.FromResult(new float[] { 1f, 2f });
        }
    }
#pragma warning restore S2190
}
