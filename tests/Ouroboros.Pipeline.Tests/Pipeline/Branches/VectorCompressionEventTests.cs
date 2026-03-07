namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;
using DomainCompressionEvent = Ouroboros.Domain.VectorCompression.VectorCompressionEvent;

[Trait("Category", "Unit")]
public class VectorCompressionEventTests
{
    [Fact]
    public void FromDomainEvent_MapsPropertiesCorrectly()
    {
        var domainEvent = new DomainCompressionEvent
        {
            Method = "PCA",
            OriginalBytes = 1000,
            CompressedBytes = 250,
            EnergyRetained = 0.95,
            Timestamp = DateTime.UtcNow
        };

        var pipelineEvent = VectorCompressionEvent.FromDomainEvent(domainEvent);

        pipelineEvent.Method.Should().Be("PCA");
        pipelineEvent.OriginalBytes.Should().Be(1000);
        pipelineEvent.CompressedBytes.Should().Be(250);
        pipelineEvent.EnergyRetained.Should().Be(0.95);
        pipelineEvent.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ToDomainEvent_RoundTripsCorrectly()
    {
        var domainEvent = new DomainCompressionEvent
        {
            Method = "Quantization",
            OriginalBytes = 2000,
            CompressedBytes = 500,
            EnergyRetained = 0.90,
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var pipelineEvent = VectorCompressionEvent.FromDomainEvent(domainEvent);
        var roundTripped = pipelineEvent.ToDomainEvent();

        roundTripped.Method.Should().Be("Quantization");
        roundTripped.OriginalBytes.Should().Be(2000);
        roundTripped.CompressedBytes.Should().Be(500);
        roundTripped.EnergyRetained.Should().Be(0.90);
    }

    [Fact]
    public void CompressionRatio_ComputesCorrectly()
    {
        var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "PCA",
            OriginalBytes = 1000,
            CompressedBytes = 250,
            EnergyRetained = 0.95
        };

        evt.CompressionRatio.Should().Be(4.0);
    }

    [Fact]
    public void CompressionRatio_WithZeroOriginal_ReturnsOne()
    {
        var evt = new VectorCompressionEvent(Guid.NewGuid(), DateTime.UtcNow)
        {
            Method = "PCA",
            OriginalBytes = 0,
            CompressedBytes = 100,
            EnergyRetained = 0.95
        };

        evt.CompressionRatio.Should().Be(1.0);
    }
}
