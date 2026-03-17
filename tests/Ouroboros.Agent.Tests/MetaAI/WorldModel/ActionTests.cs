// <copyright file="ActionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Action = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Agent.Tests.MetaAI.WorldModel;

/// <summary>
/// Unit tests for the WorldModel Action record.
/// </summary>
[Trait("Category", "Unit")]
public class ActionTests
{
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // Arrange & Act
        var parameters = new Dictionary<string, object> { ["target"] = "A", ["speed"] = 1.5 };
        var sut = new Action("move", parameters);

        // Assert
        sut.Name.Should().Be("move");
        sut.Parameters.Should().HaveCount(2);
        sut.Parameters["target"].Should().Be("A");
    }

    [Fact]
    public void Equality_TwoIdenticalActions_AreEqual()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["key"] = "value" };
        var a = new Action("act", parameters);
        var b = new Action("act", parameters);

        // Act & Assert
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentNames_AreNotEqual()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();
        var a = new Action("move", parameters);
        var b = new Action("jump", parameters);

        // Act & Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Constructor_EmptyParameters_Succeeds()
    {
        // Arrange & Act
        var sut = new Action("noop", new Dictionary<string, object>());

        // Assert
        sut.Name.Should().Be("noop");
        sut.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void With_ModifiedName_CreatesNewRecord()
    {
        // Arrange
        var original = new Action("move", new Dictionary<string, object>());

        // Act
        var modified = original with { Name = "jump" };

        // Assert
        modified.Name.Should().Be("jump");
        original.Name.Should().Be("move");
    }
}
