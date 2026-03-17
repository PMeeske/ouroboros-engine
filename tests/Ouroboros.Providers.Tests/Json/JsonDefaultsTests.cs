using System.Text.Json;
using FluentAssertions;
using Ouroboros.Providers.Json;
using Xunit;

namespace Ouroboros.Tests.Json;

[Trait("Category", "Unit")]
public sealed class JsonDefaultsTests
{
    [Fact]
    public void Indented_WriteIndented_IsTrue()
    {
        JsonDefaults.Indented.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void Indented_PropertyNamingPolicy_IsCamelCase()
    {
        JsonDefaults.Indented.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CamelCase_WriteIndented_IsFalse()
    {
        JsonDefaults.CamelCase.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void CamelCase_PropertyNamingPolicy_IsCamelCase()
    {
        JsonDefaults.CamelCase.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CamelCase_PropertyNameCaseInsensitive_IsTrue()
    {
        JsonDefaults.CamelCase.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Default_PropertyNameCaseInsensitive_IsTrue()
    {
        JsonDefaults.Default.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Default_WriteIndented_IsFalse()
    {
        JsonDefaults.Default.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void Default_PropertyNamingPolicy_IsNull()
    {
        JsonDefaults.Default.PropertyNamingPolicy.Should().BeNull();
    }

    [Fact]
    public void Indented_IsSameInstance_OnMultipleAccesses()
    {
        // Verify pre-allocated (singleton) behavior
        var first = JsonDefaults.Indented;
        var second = JsonDefaults.Indented;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void CamelCase_IsSameInstance_OnMultipleAccesses()
    {
        var first = JsonDefaults.CamelCase;
        var second = JsonDefaults.CamelCase;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Default_IsSameInstance_OnMultipleAccesses()
    {
        var first = JsonDefaults.Default;
        var second = JsonDefaults.Default;

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Indented_Serializes_WithCamelCaseAndIndentation()
    {
        // Arrange
        var obj = new { FirstName = "Test", LastName = "User" };

        // Act
        var json = JsonSerializer.Serialize(obj, JsonDefaults.Indented);

        // Assert
        json.Should().Contain("firstName");
        json.Should().Contain("lastName");
        json.Should().Contain("\n");
    }

    [Fact]
    public void CamelCase_Serializes_WithCamelCaseAndCompact()
    {
        // Arrange
        var obj = new { FirstName = "Test" };

        // Act
        var json = JsonSerializer.Serialize(obj, JsonDefaults.CamelCase);

        // Assert
        json.Should().Contain("firstName");
        json.Should().NotContain("\n");
    }

    [Fact]
    public void Default_Deserializes_CaseInsensitive()
    {
        // Arrange
        var json = """{"firstName":"Test"}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDto>(json, JsonDefaults.Default);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Test");
    }

    [Fact]
    public void CamelCase_Deserializes_CaseInsensitive()
    {
        // Arrange
        var json = """{"FIRSTNAME":"Test"}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDto>(json, JsonDefaults.CamelCase);

        // Assert
        result.Should().NotBeNull();
        result!.FirstName.Should().Be("Test");
    }

    private sealed class TestDto
    {
        public string? FirstName { get; set; }
    }
}
