using FluentAssertions;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Tests.Planning;

[Trait("Category", "Unit")]
public sealed class MeTTaTypeTests
{
    [Fact]
    public void Constructor_WithName_SetsName()
    {
        // Arrange & Act
        var type = new MeTTaType("Custom");

        // Assert
        type.Name.Should().Be("Custom");
    }

    [Fact]
    public void Text_StaticField_HasCorrectName()
    {
        MeTTaType.Text.Name.Should().Be("Text");
    }

    [Fact]
    public void Summary_StaticField_HasCorrectName()
    {
        MeTTaType.Summary.Name.Should().Be("Summary");
    }

    [Fact]
    public void Code_StaticField_HasCorrectName()
    {
        MeTTaType.Code.Name.Should().Be("Code");
    }

    [Fact]
    public void TestResult_StaticField_HasCorrectName()
    {
        MeTTaType.TestResult.Name.Should().Be("TestResult");
    }

    [Fact]
    public void Query_StaticField_HasCorrectName()
    {
        MeTTaType.Query.Name.Should().Be("Query");
    }

    [Fact]
    public void Answer_StaticField_HasCorrectName()
    {
        MeTTaType.Answer.Name.Should().Be("Answer");
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        // Arrange
        var type = new MeTTaType("MyType");

        // Act
        var result = type.ToString();

        // Assert
        result.Should().Be("MyType");
    }

    [Fact]
    public void Equality_SameNames_AreEqual()
    {
        // Arrange
        var type1 = new MeTTaType("Text");
        var type2 = new MeTTaType("Text");

        // Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void Equality_DifferentNames_AreNotEqual()
    {
        // Arrange
        var type1 = new MeTTaType("Text");
        var type2 = new MeTTaType("Code");

        // Assert
        type1.Should().NotBe(type2);
    }

    [Fact]
    public void StaticField_Text_IsEqualToNewInstanceWithSameName()
    {
        // Arrange & Act
        var custom = new MeTTaType("Text");

        // Assert
        MeTTaType.Text.Should().Be(custom);
    }
}
