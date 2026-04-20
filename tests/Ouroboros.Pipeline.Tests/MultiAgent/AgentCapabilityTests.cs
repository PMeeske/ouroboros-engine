using FluentAssertions;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.MultiAgent;

[Trait("Category", "Unit")]
public sealed class AgentCapabilityTests
{
    [Fact]
    public void Create_WithValidParameters_ReturnsCapability()
    {
        // Act
        var capability = AgentCapability.Create("coding", "Can write code", 0.9, "ide", "compiler");

        // Assert
        capability.Name.Should().Be("coding");
        capability.Description.Should().Be("Can write code");
        capability.Proficiency.Should().Be(0.9);
        capability.RequiredTools.Should().BeEquivalentTo(new[] { "ide", "compiler" });
    }

    [Fact]
    public void Create_WithDefaultProficiency_ReturnsProficiencyOne()
    {
        // Act
        var capability = AgentCapability.Create("coding", "Can write code");

        // Assert
        capability.Proficiency.Should().Be(1.0);
        capability.RequiredTools.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithNullName_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => AgentCapability.Create(null!, "description");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Create_WithNullDescription_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => AgentCapability.Create("name", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("description");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void Create_WithInvalidProficiency_ThrowsArgumentOutOfRangeException(double proficiency)
    {
        // Act
        Action act = () => AgentCapability.Create("name", "desc", proficiency);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("proficiency");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Create_WithBoundaryProficiency_Succeeds(double proficiency)
    {
        // Act
        var capability = AgentCapability.Create("name", "desc", proficiency);

        // Assert
        capability.Proficiency.Should().Be(proficiency);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var tools = ImmutableList.Create("tool1");
        var cap1 = new AgentCapability("name", "desc", 0.5, tools);
        var cap2 = new AgentCapability("name", "desc", 0.5, tools);

        // Assert
        cap1.Should().Be(cap2);
    }
}
