namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class AgentCapabilityTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var cap = AgentCapability.Create("coding", "Writes code", 0.9, "editor", "terminal");

        cap.Name.Should().Be("coding");
        cap.Description.Should().Be("Writes code");
        cap.Proficiency.Should().Be(0.9);
        cap.RequiredTools.Should().HaveCount(2);
    }

    [Fact]
    public void Create_DefaultProficiencyIsOne()
    {
        var cap = AgentCapability.Create("coding", "Writes code");
        cap.Proficiency.Should().Be(1.0);
    }

    [Fact]
    public void Create_ThrowsOnNullName()
    {
        var act = () => AgentCapability.Create(null!, "desc");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ThrowsOnNullDescription()
    {
        var act = () => AgentCapability.Create("name", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ThrowsOnInvalidProficiency()
    {
        var act = () => AgentCapability.Create("name", "desc", 1.5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
