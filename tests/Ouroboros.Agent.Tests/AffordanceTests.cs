namespace Ouroboros.Tests.EmbodiedInteraction;

/// <summary>
/// Tests for Affordance.
/// </summary>
[Trait("Category", "Unit")]
public class AffordanceTests
{
    [Fact]
    public void Affordance_Traversable_HasCorrectType()
    {
        // Arrange & Act
        var affordance = Affordance.Traversable("floor", 0.95);

        // Assert
        Assert.Equal(AffordanceType.Traversable, affordance.Type);
        Assert.Equal("floor", affordance.TargetObjectId);
        Assert.Equal("walk", affordance.ActionVerb);
    }

    [Fact]
    public void Affordance_Graspable_HasCorrectProperties()
    {
        // Arrange & Act
        var affordance = Affordance.Graspable("cup", 0.92);

        // Assert
        Assert.Equal(AffordanceType.Graspable, affordance.Type);
        Assert.Equal("cup", affordance.TargetObjectId);
        Assert.Equal("grasp", affordance.ActionVerb);
        Assert.Contains("manipulator", affordance.RequiredCapabilities);
    }

    [Fact]
    public void Affordance_Create_WithDefaults()
    {
        // Arrange & Act
        var affordance = Affordance.Create(
            AffordanceType.Activatable,
            "button1",
            "press");

        // Assert
        Assert.Equal(AffordanceType.Activatable, affordance.Type);
        Assert.Equal("button1", affordance.TargetObjectId);
        Assert.Equal("press", affordance.ActionVerb);
        Assert.NotEqual(Guid.Empty, affordance.Id);
    }

    [Fact]
    public void AffordanceMap_AddAndGetByType()
    {
        // Arrange
        var map = new AffordanceMap();
        map.Add(Affordance.Traversable("floor"));
        map.Add(Affordance.Graspable("cup"));
        map.Add(Affordance.Traversable("ramp"));

        // Act
        var traversable = map.GetByType(AffordanceType.Traversable);

        // Assert
        Assert.Equal(2, traversable.Count);
        Assert.All(traversable, a => Assert.Equal(AffordanceType.Traversable, a.Type));
    }

    [Fact]
    public void AffordanceMap_GetForObject_ReturnsOption()
    {
        // Arrange
        var map = new AffordanceMap();
        map.Add(Affordance.Graspable("cup"));
        map.Add(AffordanceTestHelpers.Pushable("cup"));

        // Act
        var cupAffordances = map.GetForObject("cup");
        var notFound = map.GetForObject("nonexistent");

        // Assert
        Assert.True(cupAffordances.HasValue);
        Assert.Equal(2, cupAffordances.Value.Count);
        Assert.False(notFound.HasValue);
    }

    [Fact]
    public void Affordance_CanBeUsedBy_ChecksCapabilities()
    {
        // Arrange
        var affordance = Affordance.Graspable("cup");
        var agentWithCapabilities = new HashSet<string> { "manipulator", "gripper" };
        var agentWithoutCapabilities = new HashSet<string> { "vision" };

        // Act & Assert
        Assert.True(affordance.CanBeUsedBy(agentWithCapabilities));
        Assert.False(affordance.CanBeUsedBy(agentWithoutCapabilities));
    }

    [Fact]
    public void Affordance_RiskAdjustedConfidence_CalculatesCorrectly()
    {
        // Arrange
        var affordance = Affordance.Create(
            AffordanceType.Traversable,
            "ledge",
            "walk",
            confidence: 0.8,
            riskLevel: 0.3);

        // Act
        var riskAdjusted = affordance.RiskAdjustedConfidence;

        // Assert
        Assert.Equal(0.8 * 0.7, riskAdjusted, 3); // 0.8 * (1 - 0.3) = 0.56
    }
}