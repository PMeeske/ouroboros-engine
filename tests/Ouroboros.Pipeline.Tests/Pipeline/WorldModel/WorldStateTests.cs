namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class WorldStateTests
{
    [Fact]
    public void Empty_HasNoContent()
    {
        var state = WorldState.Empty();

        state.Observations.Should().BeEmpty();
        state.Capabilities.Should().BeEmpty();
        state.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void WithObservation_AddsObservation()
    {
        var state = WorldState.Empty().WithObservation("temp", 25.0, 0.9);

        state.Observations.Should().ContainKey("temp");
    }

    [Fact]
    public void WithObservation_FullConfidence_DefaultsToOne()
    {
        var state = WorldState.Empty().WithObservation("key", "value");

        state.GetObservation("key").HasValue.Should().BeTrue();
    }

    [Fact]
    public void WithCapability_AddsCapability()
    {
        var cap = Capability.Create("search", "Searches");
        var state = WorldState.Empty().WithCapability(cap);

        state.Capabilities.Should().HaveCount(1);
        state.HasCapability("search").Should().BeTrue();
    }

    [Fact]
    public void WithCapability_ReplacesExistingByName()
    {
        var cap1 = Capability.Create("search", "Old");
        var cap2 = Capability.Create("search", "New");
        var state = WorldState.Empty().WithCapability(cap1).WithCapability(cap2);

        state.Capabilities.Should().HaveCount(1);
    }

    [Fact]
    public void WithConstraint_AddsConstraint()
    {
        var constraint = Constraint.Create("safety", "No harm");
        var state = WorldState.Empty().WithConstraint(constraint);

        state.HasConstraint("safety").Should().BeTrue();
    }

    [Fact]
    public void WithoutObservation_RemovesObservation()
    {
        var state = WorldState.Empty()
            .WithObservation("key", "value")
            .WithoutObservation("key");

        state.Observations.Should().BeEmpty();
    }

    [Fact]
    public void WithoutCapability_RemovesCapability()
    {
        var cap = Capability.Create("search", "Searches");
        var state = WorldState.Empty()
            .WithCapability(cap)
            .WithoutCapability("search");

        state.Capabilities.Should().BeEmpty();
    }

    [Fact]
    public void GetConstraintsByPriority_OrdersDescending()
    {
        var c1 = Constraint.Create("low", "rule", 1);
        var c2 = Constraint.Critical("high", "rule");
        var state = WorldState.Empty().WithConstraint(c1).WithConstraint(c2);

        var ordered = state.GetConstraintsByPriority().ToList();
        ordered[0].Name.Should().Be("high");
    }

    [Fact]
    public void ToOption_ReturnsNoneWhenEmpty()
    {
        WorldState.Empty().ToOption().HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToOption_ReturnsSomeWhenHasContent()
    {
        var state = WorldState.Empty().WithObservation("key", "value");
        state.ToOption().HasValue.Should().BeTrue();
    }

    [Fact]
    public void Merge_CombinesTwoStates()
    {
        var s1 = WorldState.Empty().WithObservation("a", "1");
        var s2 = WorldState.Empty().WithObservation("b", "2");

        var merged = s1.Merge(s2);

        merged.Observations.Should().HaveCount(2);
    }

    [Fact]
    public void GetHighConfidenceObservations_FiltersCorrectly()
    {
        var state = WorldState.Empty()
            .WithObservation("high", "val", 0.9)
            .WithObservation("low", "val", 0.1);

        state.GetHighConfidenceObservations(0.5).Should().HaveCount(1);
    }
}
