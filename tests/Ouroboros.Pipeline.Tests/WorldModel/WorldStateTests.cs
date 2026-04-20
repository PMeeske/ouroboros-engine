using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class WorldStateTests
{
    #region Factory Methods

    [Fact]
    public void Empty_CreatesEmptyState()
    {
        // Act
        var state = WorldState.Empty();

        // Assert
        state.Observations.Should().BeEmpty();
        state.Capabilities.Should().BeEmpty();
        state.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void FromObservations_CreatesStateWithObservations()
    {
        // Arrange
        var observations = new[]
        {
            new KeyValuePair<string, object>("key1", "value1"),
            new KeyValuePair<string, object>("key2", 42)
        };

        // Act
        var state = WorldState.FromObservations(observations);

        // Assert
        state.Observations.Should().HaveCount(2);
        state.Observations["key1"].Value.Should().Be("value1");
        state.Observations["key1"].Confidence.Should().Be(1.0);
    }

    [Fact]
    public void FromObservations_NullInput_ThrowsArgumentNullException()
    {
        var act = () => WorldState.FromObservations(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithObservation

    [Fact]
    public void WithObservation_AddsNewObservation()
    {
        // Arrange
        var state = WorldState.Empty();

        // Act
        var updated = state.WithObservation("temp", 25.5, 0.9);

        // Assert
        updated.Observations.Should().HaveCount(1);
        updated.Observations["temp"].Confidence.Should().Be(0.9);
        state.Observations.Should().BeEmpty(); // original unchanged
    }

    [Fact]
    public void WithObservation_UpdatesExisting()
    {
        // Arrange
        var state = WorldState.Empty().WithObservation("key", "old", 0.5);

        // Act
        var updated = state.WithObservation("key", "new", 0.9);

        // Assert
        updated.Observations["key"].Value.Should().Be("new");
        updated.Observations["key"].Confidence.Should().Be(0.9);
    }

    [Fact]
    public void WithObservation_FullConfidenceOverload_SetsConfidenceToOne()
    {
        // Act
        var state = WorldState.Empty().WithObservation("key", "value");

        // Assert
        state.Observations["key"].Confidence.Should().Be(1.0);
    }

    [Fact]
    public void WithObservation_NullKey_ThrowsArgumentNullException()
    {
        var act = () => WorldState.Empty().WithObservation(null!, "value", 0.5);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithObservation_NullValue_ThrowsArgumentNullException()
    {
        var act = () => WorldState.Empty().WithObservation("key", null!, 0.5);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithCapability

    [Fact]
    public void WithCapability_AddsNewCapability()
    {
        // Arrange
        var state = WorldState.Empty();
        var cap = Capability.Create("search", "Search the web");

        // Act
        var updated = state.WithCapability(cap);

        // Assert
        updated.Capabilities.Should().HaveCount(1);
        updated.Capabilities[0].Name.Should().Be("search");
    }

    [Fact]
    public void WithCapability_ReplacesSameNameCapability()
    {
        // Arrange
        var cap1 = Capability.Create("search", "Old search");
        var cap2 = Capability.Create("search", "New search", "tool1");
        var state = WorldState.Empty().WithCapability(cap1);

        // Act
        var updated = state.WithCapability(cap2);

        // Assert
        updated.Capabilities.Should().HaveCount(1);
        updated.Capabilities[0].Description.Should().Be("New search");
    }

    [Fact]
    public void WithCapability_NullCapability_ThrowsArgumentNullException()
    {
        var act = () => WorldState.Empty().WithCapability(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region WithConstraint

    [Fact]
    public void WithConstraint_AddsNewConstraint()
    {
        // Arrange
        var state = WorldState.Empty();
        var constraint = Constraint.Create("no-write", "exclude:write_tool");

        // Act
        var updated = state.WithConstraint(constraint);

        // Assert
        updated.Constraints.Should().HaveCount(1);
    }

    [Fact]
    public void WithConstraint_ReplacesSameNameConstraint()
    {
        // Arrange
        var c1 = Constraint.Create("safety", "rule1", 10);
        var c2 = Constraint.Create("safety", "rule2", 100);
        var state = WorldState.Empty().WithConstraint(c1);

        // Act
        var updated = state.WithConstraint(c2);

        // Assert
        updated.Constraints.Should().HaveCount(1);
        updated.Constraints[0].Rule.Should().Be("rule2");
        updated.Constraints[0].Priority.Should().Be(100);
    }

    #endregion

    #region Without methods

    [Fact]
    public void WithoutObservation_RemovesExistingObservation()
    {
        // Arrange
        var state = WorldState.Empty().WithObservation("key", "value");

        // Act
        var updated = state.WithoutObservation("key");

        // Assert
        updated.Observations.Should().BeEmpty();
    }

    [Fact]
    public void WithoutObservation_NonexistentKey_NoError()
    {
        // Arrange
        var state = WorldState.Empty();

        // Act
        var updated = state.WithoutObservation("nope");

        // Assert
        updated.Observations.Should().BeEmpty();
    }

    [Fact]
    public void WithoutCapability_RemovesCapability()
    {
        // Arrange
        var state = WorldState.Empty()
            .WithCapability(Capability.Create("cap1", "desc"))
            .WithCapability(Capability.Create("cap2", "desc"));

        // Act
        var updated = state.WithoutCapability("cap1");

        // Assert
        updated.Capabilities.Should().HaveCount(1);
        updated.Capabilities[0].Name.Should().Be("cap2");
    }

    [Fact]
    public void WithoutConstraint_RemovesConstraint()
    {
        // Arrange
        var state = WorldState.Empty()
            .WithConstraint(Constraint.Create("c1", "r1"))
            .WithConstraint(Constraint.Create("c2", "r2"));

        // Act
        var updated = state.WithoutConstraint("c1");

        // Assert
        updated.Constraints.Should().HaveCount(1);
        updated.Constraints[0].Name.Should().Be("c2");
    }

    #endregion

    #region Get methods

    [Fact]
    public void GetObservation_ExistingKey_ReturnsSome()
    {
        // Arrange
        var state = WorldState.Empty().WithObservation("temp", 25.0);

        // Act
        var result = state.GetObservation("temp");

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetObservation_NonexistentKey_ReturnsNone()
    {
        // Act
        var result = WorldState.Empty().GetObservation("nope");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetObservationValue_CorrectType_ReturnsSome()
    {
        // Arrange
        var state = WorldState.Empty().WithObservation("count", 42);

        // Act
        var result = state.GetObservationValue<int>("count");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void GetObservationValue_WrongType_ReturnsNone()
    {
        // Arrange
        var state = WorldState.Empty().WithObservation("count", "not a number");

        // Act
        var result = state.GetObservationValue<int>("count");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void HasCapability_Exists_ReturnsTrue()
    {
        // Arrange
        var state = WorldState.Empty().WithCapability(Capability.Create("search", "desc"));

        // Assert
        state.HasCapability("search").Should().BeTrue();
    }

    [Fact]
    public void HasCapability_NotExists_ReturnsFalse()
    {
        // Assert
        WorldState.Empty().HasCapability("nope").Should().BeFalse();
    }

    [Fact]
    public void GetCapability_Exists_ReturnsSome()
    {
        // Arrange
        var state = WorldState.Empty().WithCapability(Capability.Create("cap", "desc"));

        // Act
        var result = state.GetCapability("cap");

        // Assert
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetCapability_NotExists_ReturnsNone()
    {
        // Act
        var result = WorldState.Empty().GetCapability("nope");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void HasConstraint_Exists_ReturnsTrue()
    {
        var state = WorldState.Empty().WithConstraint(Constraint.Create("c", "r"));
        state.HasConstraint("c").Should().BeTrue();
    }

    [Fact]
    public void HasConstraint_NotExists_ReturnsFalse()
    {
        WorldState.Empty().HasConstraint("nope").Should().BeFalse();
    }

    [Fact]
    public void GetConstraint_Exists_ReturnsSome()
    {
        var state = WorldState.Empty().WithConstraint(Constraint.Create("c", "r"));
        state.GetConstraint("c").HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetConstraint_NotExists_ReturnsNone()
    {
        WorldState.Empty().GetConstraint("nope").HasValue.Should().BeFalse();
    }

    #endregion

    #region Query methods

    [Fact]
    public void GetConstraintsByPriority_ReturnsHighestFirst()
    {
        // Arrange
        var state = WorldState.Empty()
            .WithConstraint(Constraint.Create("low", "r", 1))
            .WithConstraint(Constraint.Create("high", "r", 100))
            .WithConstraint(Constraint.Create("mid", "r", 50));

        // Act
        var ordered = state.GetConstraintsByPriority().ToList();

        // Assert
        ordered[0].Name.Should().Be("high");
        ordered[1].Name.Should().Be("mid");
        ordered[2].Name.Should().Be("low");
    }

    [Fact]
    public void GetHighConfidenceObservations_FiltersCorrectly()
    {
        // Arrange
        var state = WorldState.Empty()
            .WithObservation("sure", "yes", 0.9)
            .WithObservation("maybe", "idk", 0.3)
            .WithObservation("certain", "yes!", 1.0);

        // Act
        var highConf = state.GetHighConfidenceObservations(0.8).ToList();

        // Assert
        highConf.Should().HaveCount(2);
        highConf.Select(kv => kv.Key).Should().Contain("sure");
        highConf.Select(kv => kv.Key).Should().Contain("certain");
    }

    [Fact]
    public void GetAverageConfidence_WithObservations_ReturnsAverage()
    {
        // Arrange
        var state = WorldState.Empty()
            .WithObservation("a", "val", 0.6)
            .WithObservation("b", "val", 0.8);

        // Act
        var avg = state.GetAverageConfidence();

        // Assert
        avg.HasValue.Should().BeTrue();
        avg.Value.Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public void GetAverageConfidence_NoObservations_ReturnsNone()
    {
        // Act
        var avg = WorldState.Empty().GetAverageConfidence();

        // Assert
        avg.HasValue.Should().BeFalse();
    }

    #endregion

    #region ToOption

    [Fact]
    public void ToOption_EmptyState_ReturnsNone()
    {
        WorldState.Empty().ToOption().HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToOption_StateWithObservation_ReturnsSome()
    {
        var state = WorldState.Empty().WithObservation("key", "val");
        state.ToOption().HasValue.Should().BeTrue();
    }

    [Fact]
    public void ToOption_StateWithCapability_ReturnsSome()
    {
        var state = WorldState.Empty().WithCapability(Capability.Create("cap", "desc"));
        state.ToOption().HasValue.Should().BeTrue();
    }

    [Fact]
    public void ToOption_StateWithConstraint_ReturnsSome()
    {
        var state = WorldState.Empty().WithConstraint(Constraint.Create("c", "r"));
        state.ToOption().HasValue.Should().BeTrue();
    }

    #endregion

    #region Merge

    [Fact]
    public void Merge_OtherObservationsOverwrite()
    {
        // Arrange
        var state1 = WorldState.Empty().WithObservation("key", "old");
        var state2 = WorldState.Empty().WithObservation("key", "new");

        // Act
        var merged = state1.Merge(state2);

        // Assert
        merged.Observations["key"].Value.Should().Be("new");
    }

    [Fact]
    public void Merge_CombinesCapabilities()
    {
        // Arrange
        var state1 = WorldState.Empty().WithCapability(Capability.Create("cap1", "desc1"));
        var state2 = WorldState.Empty().WithCapability(Capability.Create("cap2", "desc2"));

        // Act
        var merged = state1.Merge(state2);

        // Assert
        merged.Capabilities.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_OverlappingCapabilities_OtherTakesPrecedence()
    {
        // Arrange
        var state1 = WorldState.Empty().WithCapability(Capability.Create("cap", "old desc"));
        var state2 = WorldState.Empty().WithCapability(Capability.Create("cap", "new desc"));

        // Act
        var merged = state1.Merge(state2);

        // Assert
        merged.Capabilities.Should().HaveCount(1);
        merged.Capabilities[0].Description.Should().Be("new desc");
    }

    [Fact]
    public void Merge_CombinesConstraints()
    {
        // Arrange
        var state1 = WorldState.Empty().WithConstraint(Constraint.Create("c1", "r1"));
        var state2 = WorldState.Empty().WithConstraint(Constraint.Create("c2", "r2"));

        // Act
        var merged = state1.Merge(state2);

        // Assert
        merged.Constraints.Should().HaveCount(2);
    }

    [Fact]
    public void Merge_NullOther_ThrowsArgumentNullException()
    {
        var act = () => WorldState.Empty().Merge(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Snapshot

    [Fact]
    public void Snapshot_CreatesNewStateWithCurrentTimestamp()
    {
        // Arrange
        var state = WorldState.Empty().WithObservation("key", "val");
        var oldTimestamp = state.LastUpdated;

        // Act
        var snapshot = state.Snapshot();

        // Assert
        snapshot.Observations.Should().HaveCount(1);
        snapshot.LastUpdated.Should().BeOnOrAfter(oldTimestamp);
    }

    #endregion
}
