using Ouroboros.Pipeline.Branches;
using DomainCompressionEvent = Ouroboros.Domain.VectorCompression.VectorCompressionEvent;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class VectorCompressionEventTests
{
    [Fact]
    public void Constructor_WithRequiredProperties_SetsValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        // Act
        var evt = new VectorCompressionEvent(id, timestamp)
        {
            Method = "PCA",
            OriginalBytes = 1000,
            CompressedBytes = 250,
            EnergyRetained = 0.95
        };

        // Assert
        evt.Id.Should().Be(id);
        evt.Timestamp.Should().Be(timestamp);
        evt.Method.Should().Be("PCA");
        evt.OriginalBytes.Should().Be(1000);
        evt.CompressedBytes.Should().Be(250);
        evt.EnergyRetained.Should().BeApproximately(0.95, 0.001);
    }

    [Fact]
    public void CompressionRatio_WithValidValues_ReturnsCorrectRatio()
    {
        // Arrange
        var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "PCA",
            OriginalBytes = 1000,
            CompressedBytes = 250,
            EnergyRetained = 0.95
        };

        // Act & Assert
        evt.CompressionRatio.Should().BeApproximately(4.0, 0.001);
    }

    [Fact]
    public void CompressionRatio_WithZeroOriginalBytes_ReturnsOne()
    {
        // Arrange
        var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "PCA",
            OriginalBytes = 0,
            CompressedBytes = 250,
            EnergyRetained = 0.95
        };

        // Act & Assert
        evt.CompressionRatio.Should().Be(1.0);
    }

    [Fact]
    public void CompressionRatio_WithEqualSizes_ReturnsOne()
    {
        // Arrange
        var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "None",
            OriginalBytes = 500,
            CompressedBytes = 500,
            EnergyRetained = 1.0
        };

        // Act & Assert
        evt.CompressionRatio.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Metadata_DefaultsToEmptyDictionary()
    {
        // Arrange
        var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "PCA",
            OriginalBytes = 100,
            CompressedBytes = 50,
            EnergyRetained = 0.9
        };

        // Assert
        evt.Metadata.Should().NotBeNull();
        evt.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Metadata_CanBeSetToCustomDictionary()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["components"] = 64,
            ["algorithm"] = "PCA"
        };

        // Act
        var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "PCA",
            OriginalBytes = 100,
            CompressedBytes = 50,
            EnergyRetained = 0.9,
            Metadata = metadata
        };

        // Assert
        evt.Metadata.Should().HaveCount(2);
    }

    [Fact]
    public void FromDomainEvent_CreatesCorrectPipelineEvent()
    {
        // Arrange
        var domainEvent = new DomainCompressionEvent
        {
            Method = "Quantization",
            OriginalBytes = 2000,
            CompressedBytes = 500,
            EnergyRetained = 0.85,
            Timestamp = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            Metadata = new Dictionary<string, object> { ["bits"] = 8 }
        };

        // Act
        var pipelineEvent = VectorCompressionEvent.FromDomainEvent(domainEvent);

        // Assert
        pipelineEvent.Id.Should().NotBe(Guid.Empty);
        pipelineEvent.Method.Should().Be("Quantization");
        pipelineEvent.OriginalBytes.Should().Be(2000);
        pipelineEvent.CompressedBytes.Should().Be(500);
        pipelineEvent.EnergyRetained.Should().BeApproximately(0.85, 0.001);
        pipelineEvent.Timestamp.Should().Be(domainEvent.Timestamp);
        pipelineEvent.Metadata.Should().ContainKey("bits");
    }

    [Fact]
    public void ToDomainEvent_CreatesCorrectDomainEvent()
    {
        // Arrange
        var timestamp = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var pipelineEvent = new VectorCompressionEvent(Guid.NewGuid(), timestamp)
        {
            Method = "PCA",
            OriginalBytes = 1500,
            CompressedBytes = 300,
            EnergyRetained = 0.92,
            Metadata = new Dictionary<string, object> { ["info"] = "test" }
        };

        // Act
        var domainEvent = pipelineEvent.ToDomainEvent();

        // Assert
        domainEvent.Method.Should().Be("PCA");
        domainEvent.OriginalBytes.Should().Be(1500);
        domainEvent.CompressedBytes.Should().Be(300);
        domainEvent.EnergyRetained.Should().BeApproximately(0.92, 0.001);
        domainEvent.Timestamp.Should().Be(timestamp);
        domainEvent.Metadata.Should().ContainKey("info");
    }

    [Fact]
    public void RoundTrip_FromDomainAndBack_PreservesData()
    {
        // Arrange
        var originalDomain = new DomainCompressionEvent
        {
            Method = "Hybrid",
            OriginalBytes = 3000,
            CompressedBytes = 750,
            EnergyRetained = 0.88,
            Timestamp = DateTime.UtcNow,
            Metadata = new Dictionary<string, object> { ["test"] = true }
        };

        // Act
        var pipeline = VectorCompressionEvent.FromDomainEvent(originalDomain);
        var roundTripped = pipeline.ToDomainEvent();

        // Assert
        roundTripped.Method.Should().Be(originalDomain.Method);
        roundTripped.OriginalBytes.Should().Be(originalDomain.OriginalBytes);
        roundTripped.CompressedBytes.Should().Be(originalDomain.CompressedBytes);
        roundTripped.EnergyRetained.Should().BeApproximately(originalDomain.EnergyRetained, 0.001);
        roundTripped.Timestamp.Should().Be(originalDomain.Timestamp);
    }
}
