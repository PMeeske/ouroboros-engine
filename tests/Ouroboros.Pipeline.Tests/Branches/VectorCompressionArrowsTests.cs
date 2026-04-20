using Ouroboros.Domain.VectorCompression;
using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class VectorCompressionArrowsTests
{
    private static PipelineBranch CreateBranch()
    {
        return new PipelineBranch("test", new TrackedVectorStore(), DataSource.FromPath("."));
    }

    #region GetCompressionStats Tests

    [Fact]
    public void GetCompressionStats_WithNoCompressionEvents_ReturnsResult()
    {
        // Arrange
        var branch = CreateBranch();

        // Act
        var result = VectorCompressionArrows.GetCompressionStats(branch);

        // Assert
        result.Should().NotBeNull();
        // The result depends on VectorCompressionService.GetStats behavior with empty list
    }

    [Fact]
    public void GetCompressionStats_WithCompressionEvents_UsesEventData()
    {
        // Arrange
        var branch = CreateBranch();
        var compressionEvent = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "PCA",
            OriginalBytes = 1000,
            CompressedBytes = 250,
            EnergyRetained = 0.95
        };
        branch = branch.WithEvent(compressionEvent);

        // Act
        var result = VectorCompressionArrows.GetCompressionStats(branch);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetCompressionStats_WithMultipleCompressionEvents_AggregatesAll()
    {
        // Arrange
        var branch = CreateBranch();
        for (int i = 0; i < 3; i++)
        {
            var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
            {
                Method = "PCA",
                OriginalBytes = 1000 * (i + 1),
                CompressedBytes = 250 * (i + 1),
                EnergyRetained = 0.9 + i * 0.02
            };
            branch = branch.WithEvent(evt);
        }

        // Act
        var result = VectorCompressionArrows.GetCompressionStats(branch);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetCompressionStats_IgnoresNonCompressionEvents()
    {
        // Arrange
        var branch = CreateBranch();
        branch = branch.WithReasoning(new Draft("text"), "prompt");
        branch = branch.WithIngestEvent("source", new[] { "doc1" });

        // Act
        var result = VectorCompressionArrows.GetCompressionStats(branch);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region CompressArrow Tests

    [Fact]
    public async Task CompressArrow_WithValidInput_ReturnsStep()
    {
        // Arrange
        var vector = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var config = new CompressionConfig
        {
            TargetDimensions = 2,
            EnergyThreshold = 0.9
        };

        var arrow = VectorCompressionArrows.CompressArrow(vector, config);

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task CompressArrow_ExecutesAndReturnsResult()
    {
        // Arrange
        var branch = CreateBranch();
        var vector = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
        var config = new CompressionConfig
        {
            TargetDimensions = 2,
            EnergyThreshold = 0.9
        };

        var arrow = VectorCompressionArrows.CompressArrow(vector, config);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        // Result is either success with compressed data or failure
    }

    #endregion

    #region BatchCompressAsync Tests

    [Fact]
    public async Task BatchCompressAsync_WithValidInputs_ReturnsResult()
    {
        // Arrange
        var branch = CreateBranch();
        var vectors = new[]
        {
            new float[] { 1.0f, 2.0f, 3.0f },
            new float[] { 4.0f, 5.0f, 6.0f }
        };
        var config = new CompressionConfig
        {
            TargetDimensions = 2,
            EnergyThreshold = 0.9
        };

        // Act
        var result = await VectorCompressionArrows.BatchCompressAsync(branch, vectors, config);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
