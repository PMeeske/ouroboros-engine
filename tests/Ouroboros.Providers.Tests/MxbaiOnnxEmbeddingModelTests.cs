// <copyright file="MxbaiOnnxEmbeddingModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Moq;
using Ouroboros.Providers;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Orchestration;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for the Phase 272 ONNX embedding model. Most tests exercise the
/// internal helpers (<c>ApplyMaskedMeanPool</c>, <c>ApplyL2Normalize</c>) and
/// the construction-time guards — they do not require a real ONNX file. The
/// integration-shaped batch test is marked Skip until a CI fixture provisions
/// the FP16 model.
/// </summary>
public sealed class MxbaiOnnxEmbeddingModelTests
{
    [Fact]
    public void Constructor_WithMissingModelPath_ThrowsFileNotFoundException()
    {
        // Arrange
        string missingModel = Path.Combine(Path.GetTempPath(), $"definitely-not-here-{Guid.NewGuid():N}.onnx");
        string vocabPath = WriteTempVocab();

        try
        {
            // Act
            Action act = () => _ = new MxbaiOnnxEmbeddingModel(
                missingModel,
                vocabPath,
                sessionFactory: null,
                scheduler: null,
                logger: null);

            // Assert
            act.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            File.Delete(vocabPath);
        }
    }

    [Fact]
    public void Constructor_WithMissingVocabPath_ThrowsFileNotFoundException()
    {
        // Arrange
        string modelPath = WriteTempPlaceholder(".onnx");
        string missingVocab = Path.Combine(Path.GetTempPath(), $"no-vocab-{Guid.NewGuid():N}.txt");

        try
        {
            // Act
            Action act = () => _ = new MxbaiOnnxEmbeddingModel(
                modelPath,
                missingVocab,
                sessionFactory: null,
                scheduler: null,
                logger: null);

            // Assert
            act.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            File.Delete(modelPath);
        }
    }

    [Fact]
    public void Constructor_WithFactoryThatThrows_PropagatesInvalidOperationException()
    {
        // Arrange — the DI strategy chain handles the fallback at the layer above; this
        // class itself must surface the failure so the chain can move to the next strategy.
        var factory = new Mock<ISharedOrtDmlSessionFactory>();
        factory.Setup(f => f.CreateSessionOptions())
            .Throws(new InvalidOperationException("Shared D3D12 device unavailable"));

        string modelPath = WriteTempPlaceholder(".onnx");
        string vocabPath = WriteTempVocab();

        try
        {
            // Act
            Action act = () => _ = new MxbaiOnnxEmbeddingModel(
                modelPath,
                vocabPath,
                factory.Object,
                scheduler: null,
                logger: null);

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("*Shared D3D12 device unavailable*");
        }
        finally
        {
            File.Delete(modelPath);
            File.Delete(vocabPath);
        }
    }

    [Fact]
    public void ApplyMaskedMeanPool_AveragesOnlyMaskedTokens_DivisorIsMaskSum()
    {
        // Arrange — sequence of length 4, hidden dim 3. Token vectors:
        //   t0 = [1,1,1]   mask=1
        //   t1 = [3,3,3]   mask=1
        //   t2 = [5,5,5]   mask=1
        //   t3 = [99,99,99] mask=0  (padded — must NOT contribute)
        // Expected: (1+3+5)/3 = 3 per channel (NOT divided by 4).
        const int hiddenDim = 3;
        float[] hidden = [1, 1, 1, 3, 3, 3, 5, 5, 5, 99, 99, 99];
        long[] mask = [1, 1, 1, 0];

        // Act
        float[] pooled = MxbaiOnnxEmbeddingModel.ApplyMaskedMeanPool(hidden, mask, hiddenDim);

        // Assert
        pooled.Should().HaveCount(hiddenDim);
        pooled[0].Should().BeApproximately(3.0f, 1e-5f);
        pooled[1].Should().BeApproximately(3.0f, 1e-5f);
        pooled[2].Should().BeApproximately(3.0f, 1e-5f);
    }

    [Fact]
    public void ApplyMaskedMeanPool_AllMaskedOut_ReturnsZeros()
    {
        // Arrange — degenerate input; divisor would be 0. Implementation must skip the
        // divide and return the (zero-initialized) accumulator.
        float[] hidden = [1, 2, 3, 4, 5, 6];
        long[] mask = [0, 0];

        // Act
        float[] pooled = MxbaiOnnxEmbeddingModel.ApplyMaskedMeanPool(hidden, mask, hiddenDim: 3);

        // Assert
        pooled.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public void ApplyL2Normalize_OnAllOnes_NormalizesToUnitVector()
    {
        // Arrange
        float[] vector = new float[1024];
        Array.Fill(vector, 1f);

        // Act
        MxbaiOnnxEmbeddingModel.ApplyL2Normalize(vector);

        // Assert — each component should be 1/sqrt(1024)
        float expected = 1.0f / MathF.Sqrt(1024);
        foreach (float v in vector)
        {
            v.Should().BeApproximately(expected, 1e-5f);
        }

        // Norm of result should be 1.
        double sumSq = 0;
        foreach (float v in vector)
        {
            sumSq += v * v;
        }

        Math.Sqrt(sumSq).Should().BeApproximately(1.0, 1e-5);
    }

    [Fact]
    public void ApplyL2Normalize_OnZeroVector_LeavesItZero()
    {
        // Arrange
        float[] vector = new float[8];

        // Act
        MxbaiOnnxEmbeddingModel.ApplyL2Normalize(vector);

        // Assert — no NaNs from div-by-zero
        vector.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact(Skip = "Requires real mxbai-embed-large-v1 ONNX fixture; provisioned via scripts/Download-MxbaiEmbedLargeOnnx.ps1 in CI.")]
    public async Task EmbedBatchAsync_OnThreeInputs_ReturnsThreeUnitVectorsOf1024Dim()
    {
        // Arrange
        string modelPath = "models/embedding/mxbai-embed-large-v1.fp16.onnx";
        string vocabPath = "models/embedding/mxbai-vocab.txt";
        var logger = Mock.Of<ILogger<MxbaiOnnxEmbeddingModel>>();

        using var sut = new MxbaiOnnxEmbeddingModel(modelPath, vocabPath, sessionFactory: null, scheduler: null, logger: logger);

        // Act
        IReadOnlyList<float[]> result = await sut.EmbedBatchAsync(["hello world", "ouroboros", "iaret is awake"]);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(v =>
        {
            v.Length.Should().Be(1024);
            double sumSq = 0;
            foreach (float f in v) { sumSq += f * f; }
            Math.Sqrt(sumSq).Should().BeApproximately(1.0, 1e-3);
        });
    }

    [Fact]
    public void ResolvedLogLine_LiteralMatches_LockedFormat()
    {
        // The log line is an operator-grep-able diagnostic — must not change wording.
        MxbaiOnnxEmbeddingModel.ResolvedLogLine
            .Should().Be("Embedding.Resolved: provider=onnx dim=1024 model=mxbai-embed-large-v1");
    }

    [Fact]
    public void TenantName_LiteralMatches_LockedFormat()
    {
        MxbaiOnnxEmbeddingModel.TenantName.Should().Be("Embedding-Mxbai");
    }

    private static string WriteTempPlaceholder(string ext)
    {
        string path = Path.Combine(Path.GetTempPath(), $"mxbai-test-{Guid.NewGuid():N}{ext}");
        File.WriteAllBytes(path, new byte[] { 0x00 });
        return path;
    }

    private static string WriteTempVocab()
    {
        // Minimum BERT vocab needed for BertTokenizer.Create to succeed.
        // Order matters — id mapping derives from line order.
        string[] lines =
        [
            "[PAD]",
            "[UNK]",
            "[CLS]",
            "[SEP]",
            "[MASK]",
            "the",
            "a",
            "test",
            "hello",
            "world",
        ];

        string path = Path.Combine(Path.GetTempPath(), $"mxbai-vocab-{Guid.NewGuid():N}.txt");
        File.WriteAllLines(path, lines);
        return path;
    }
}
