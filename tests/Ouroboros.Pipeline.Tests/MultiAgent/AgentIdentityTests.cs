using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentIdentityTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsIdentity()
    {
        // Act
        var identity = AgentIdentity.Create("TestAgent", AgentRole.Coder);

        // Assert
        identity.Id.Should().NotBeEmpty();
        identity.Name.Should().Be("TestAgent");
        identity.Role.Should().Be(AgentRole.Coder);
        identity.Capabilities.Should().BeEmpty();
        identity.Metadata.Should().BeEmpty();
        identity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithNullName_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => AgentIdentity.Create(null!, AgentRole.Analyst);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void WithCapability_AddsCapabilityToNewInstance()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);
        var capability = AgentCapability.Create("coding", "Write code", 0.9);

        // Act
        var updated = identity.WithCapability(capability);

        // Assert
        updated.Capabilities.Should().HaveCount(1);
        updated.Capabilities[0].Name.Should().Be("coding");
        identity.Capabilities.Should().BeEmpty(); // original unchanged
    }

    [Fact]
    public void WithCapability_WithNullCapability_ThrowsArgumentNullException()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);

        // Act
        Action act = () => identity.WithCapability(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("capability");
    }

    [Fact]
    public void WithMetadata_AddsMetadataToNewInstance()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Analyst);

        // Act
        var updated = identity.WithMetadata("version", "1.0");

        // Assert
        updated.Metadata.Should().ContainKey("version");
        updated.Metadata["version"].Should().Be("1.0");
        identity.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void WithMetadata_WithNullKey_ThrowsArgumentNullException()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Analyst);

        // Act
        Action act = () => identity.WithMetadata(null!, "value");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("key");
    }

    [Fact]
    public void WithMetadata_WithNullValue_ThrowsArgumentNullException()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Analyst);

        // Act
        Action act = () => identity.WithMetadata("key", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }

    [Fact]
    public void GetCapability_WhenExists_ReturnsSome()
    {
        // Arrange
        var capability = AgentCapability.Create("coding", "Write code", 0.9);
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder).WithCapability(capability);

        // Act
        var result = identity.GetCapability("coding");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Name.Should().Be("coding");
    }

    [Fact]
    public void GetCapability_WhenNotExists_ReturnsNone()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);

        // Act
        var result = identity.GetCapability("nonexistent");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetCapability_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);

        // Act
        Action act = () => identity.GetCapability(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void HasCapability_WhenExists_ReturnsTrue()
    {
        // Arrange
        var capability = AgentCapability.Create("coding", "Write code");
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder).WithCapability(capability);

        // Act & Assert
        identity.HasCapability("coding").Should().BeTrue();
    }

    [Fact]
    public void HasCapability_WhenNotExists_ReturnsFalse()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);

        // Act & Assert
        identity.HasCapability("coding").Should().BeFalse();
    }

    [Fact]
    public void HasCapability_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);

        // Act
        Action act = () => identity.HasCapability(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void GetProficiencyFor_WhenCapabilityExists_ReturnsProficiency()
    {
        // Arrange
        var capability = AgentCapability.Create("coding", "Write code", 0.85);
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder).WithCapability(capability);

        // Act
        double proficiency = identity.GetProficiencyFor("coding");

        // Assert
        proficiency.Should().Be(0.85);
    }

    [Fact]
    public void GetProficiencyFor_WhenCapabilityNotExists_ReturnsZero()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);

        // Act
        double proficiency = identity.GetProficiencyFor("nonexistent");

        // Assert
        proficiency.Should().Be(0.0);
    }

    [Fact]
    public void GetProficiencyFor_WithNullName_ThrowsArgumentNullException()
    {
        // Arrange
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);

        // Act
        Action act = () => identity.GetProficiencyFor(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("capabilityName");
    }

    [Fact]
    public void GetCapabilitiesAbove_ReturnsCapabilitiesAboveThreshold()
    {
        // Arrange
        var low = AgentCapability.Create("reading", "Read docs", 0.3);
        var mid = AgentCapability.Create("coding", "Write code", 0.7);
        var high = AgentCapability.Create("design", "System design", 0.95);
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder)
            .WithCapability(low)
            .WithCapability(mid)
            .WithCapability(high);

        // Act
        var result = identity.GetCapabilitiesAbove(0.5);

        // Assert
        result.Should().HaveCount(2);
        result.Select(c => c.Name).Should().BeEquivalentTo("coding", "design");
    }

    [Fact]
    public void GetCapabilitiesAbove_WithHighThreshold_ReturnsEmpty()
    {
        // Arrange
        var cap = AgentCapability.Create("coding", "Write code", 0.5);
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder).WithCapability(cap);

        // Act
        var result = identity.GetCapabilitiesAbove(0.9);

        // Assert
        result.Should().BeEmpty();
    }
}
