using FluentAssertions;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public sealed class ConstraintTests
{
    [Fact]
    public void Create_WithDefaultPriority_SetsPriorityToZero()
    {
        // Act
        var constraint = Constraint.Create("no-writes", "No write operations");

        // Assert
        constraint.Name.Should().Be("no-writes");
        constraint.Rule.Should().Be("No write operations");
        constraint.Priority.Should().Be(0);
    }

    [Fact]
    public void Create_WithPriority_SetsPriority()
    {
        // Act
        var constraint = Constraint.Create("important", "Rule", 50);

        // Assert
        constraint.Priority.Should().Be(50);
    }

    [Fact]
    public void Create_NullName_ThrowsArgumentNullException()
    {
        var act = () => Constraint.Create(null!, "rule");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_NullRule_ThrowsArgumentNullException()
    {
        var act = () => Constraint.Create("name", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithPriority_NullName_ThrowsArgumentNullException()
    {
        var act = () => Constraint.Create(null!, "rule", 10);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithPriority_NullRule_ThrowsArgumentNullException()
    {
        var act = () => Constraint.Create("name", null!, 10);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Critical_SetsPriorityTo100()
    {
        // Act
        var constraint = Constraint.Critical("safety", "Must not harm");

        // Assert
        constraint.Priority.Should().Be(100);
        constraint.Name.Should().Be("safety");
        constraint.Rule.Should().Be("Must not harm");
    }

    [Fact]
    public void Critical_NullName_ThrowsArgumentNullException()
    {
        var act = () => Constraint.Critical(null!, "rule");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Critical_NullRule_ThrowsArgumentNullException()
    {
        var act = () => Constraint.Critical("name", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var c1 = Constraint.Create("name", "rule", 5);
        var c2 = Constraint.Create("name", "rule", 5);

        c1.Should().Be(c2);
    }

    [Fact]
    public void Equality_DifferentPriority_AreNotEqual()
    {
        var c1 = Constraint.Create("name", "rule", 5);
        var c2 = Constraint.Create("name", "rule", 10);

        c1.Should().NotBe(c2);
    }
}
