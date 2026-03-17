// <copyright file="GoalConflictTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI.SelfImprovement;

[Trait("Category", "Unit")]
public class GoalConflictTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var goal1 = new Goal("Goal A", GoalType.Primary, 1.0);
        var goal2 = new Goal("Goal B", GoalType.Safety, 0.9);
        var conflictType = "ResourceContention";
        var description = "Both goals require exclusive access to the same resource";
        var resolutions = new List<string> { "Prioritize safety goal", "Time-share resource" };

        // Act
        var conflict = new GoalConflict(goal1, goal2, conflictType, description, resolutions);

        // Assert
        conflict.Goal1.Should().Be(goal1);
        conflict.Goal2.Should().Be(goal2);
        conflict.ConflictType.Should().Be(conflictType);
        conflict.Description.Should().Be(description);
        conflict.SuggestedResolutions.Should().BeEquivalentTo(resolutions);
    }

    [Fact]
    public void Constructor_WithEmptyResolutions_Succeeds()
    {
        var goal1 = new Goal("A", GoalType.Primary, 1.0);
        var goal2 = new Goal("B", GoalType.Primary, 1.0);

        var conflict = new GoalConflict(goal1, goal2, "Type", "Desc", new List<string>());

        conflict.SuggestedResolutions.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var goal1 = new Goal("A", GoalType.Primary, 1.0);
        var goal2 = new Goal("B", GoalType.Primary, 1.0);
        var resolutions = new List<string> { "resolve" };

        var a = new GoalConflict(goal1, goal2, "type", "desc", resolutions);
        var b = new GoalConflict(goal1, goal2, "type", "desc", resolutions);

        a.Should().Be(b);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var goal1 = new Goal("A", GoalType.Primary, 1.0);
        var goal2 = new Goal("B", GoalType.Primary, 1.0);
        var original = new GoalConflict(goal1, goal2, "type", "desc", new List<string>());

        var modified = original with { ConflictType = "ValueMisalignment" };

        modified.ConflictType.Should().Be("ValueMisalignment");
        modified.Goal1.Should().Be(goal1);
    }
}
