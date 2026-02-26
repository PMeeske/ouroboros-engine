namespace Ouroboros.Tests.Pipeline.MultiAgent;

using Ouroboros.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public class AgentIdentityTests
{
    [Fact]
    public void Create_SetsNameAndRole()
    {
        var identity = AgentIdentity.Create("TestAgent", AgentRole.Coder);

        identity.Name.Should().Be("TestAgent");
        identity.Role.Should().Be(AgentRole.Coder);
        identity.Capabilities.Should().BeEmpty();
        identity.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Create_ThrowsOnNullName()
    {
        var act = () => AgentIdentity.Create(null!, AgentRole.Coder);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithCapability_AddsCapability()
    {
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder);
        var cap = AgentCapability.Create("coding", "Writes code");
        var updated = identity.WithCapability(cap);

        updated.Capabilities.Should().HaveCount(1);
        identity.Capabilities.Should().BeEmpty();
    }

    [Fact]
    public void WithMetadata_AddsEntry()
    {
        var identity = AgentIdentity.Create("Agent", AgentRole.Analyst);
        var updated = identity.WithMetadata("version", "1.0");

        updated.Metadata.Should().ContainKey("version");
    }

    [Fact]
    public void HasCapability_ReturnsTrueWhenExists()
    {
        var cap = AgentCapability.Create("coding", "Writes code");
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder)
            .WithCapability(cap);

        identity.HasCapability("coding").Should().BeTrue();
        identity.HasCapability("unknown").Should().BeFalse();
    }

    [Fact]
    public void GetProficiencyFor_ReturnsValueOrZero()
    {
        var cap = AgentCapability.Create("coding", "Writes code", 0.8);
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder)
            .WithCapability(cap);

        identity.GetProficiencyFor("coding").Should().Be(0.8);
        identity.GetProficiencyFor("unknown").Should().Be(0.0);
    }

    [Fact]
    public void GetCapabilitiesAbove_FiltersCorrectly()
    {
        var c1 = AgentCapability.Create("coding", "desc", 0.9);
        var c2 = AgentCapability.Create("review", "desc", 0.3);
        var identity = AgentIdentity.Create("Agent", AgentRole.Coder)
            .WithCapability(c1).WithCapability(c2);

        identity.GetCapabilitiesAbove(0.5).Should().HaveCount(1);
    }
}
