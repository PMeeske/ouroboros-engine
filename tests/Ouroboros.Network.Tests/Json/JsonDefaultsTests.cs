// <copyright file="JsonDefaultsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Network.Json;
using Xunit;

namespace Ouroboros.Tests.Network.Json;

[Trait("Category", "Unit")]
public sealed class JsonDefaultsTests
{
    [Fact]
    public void Indented_WriteIndented_IsTrue()
    {
        // Assert
        JsonDefaults.Indented.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void Indented_PropertyNamingPolicy_IsCamelCase()
    {
        // Assert
        JsonDefaults.Indented.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void Indented_SerializesWithCamelCaseAndIndentation()
    {
        // Arrange
        var obj = new { MyProperty = "value" };

        // Act
        var json = JsonSerializer.Serialize(obj, JsonDefaults.Indented);

        // Assert
        json.Should().Contain("myProperty");
        json.Should().Contain("\n");
    }

    [Fact]
    public void IndentedPascalCase_WriteIndented_IsTrue()
    {
        // Assert
        JsonDefaults.IndentedPascalCase.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void IndentedPascalCase_PropertyNamingPolicy_IsNull()
    {
        // Assert
        JsonDefaults.IndentedPascalCase.PropertyNamingPolicy.Should().BeNull();
    }

    [Fact]
    public void IndentedPascalCase_SerializesWithPascalCaseAndIndentation()
    {
        // Arrange
        var obj = new { MyProperty = "value" };

        // Act
        var json = JsonSerializer.Serialize(obj, JsonDefaults.IndentedPascalCase);

        // Assert
        json.Should().Contain("MyProperty");
        json.Should().Contain("\n");
    }

    [Fact]
    public void CamelCase_WriteIndented_IsFalse()
    {
        // Assert
        JsonDefaults.CamelCase.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void CamelCase_PropertyNamingPolicy_IsCamelCase()
    {
        // Assert
        JsonDefaults.CamelCase.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CamelCase_SerializesCompactWithCamelCase()
    {
        // Arrange
        var obj = new { MyProperty = "value" };

        // Act
        var json = JsonSerializer.Serialize(obj, JsonDefaults.CamelCase);

        // Assert
        json.Should().Contain("myProperty");
        json.Should().NotContain("\n");
    }

    [Fact]
    public void Default_PropertyNameCaseInsensitive_IsTrue()
    {
        // Assert
        JsonDefaults.Default.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Default_WriteIndented_IsFalse()
    {
        // Assert
        JsonDefaults.Default.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void Default_CanDeserializeCaseInsensitively()
    {
        // Arrange
        var json = "{\"MYPROPERTY\":\"test\"}";

        // Act
        var result = JsonSerializer.Deserialize<TestDto>(json, JsonDefaults.Default);

        // Assert
        result.Should().NotBeNull();
        result!.MyProperty.Should().Be("test");
    }

    [Fact]
    public void Indented_IsSingleton_SameReferenceOnMultipleAccesses()
    {
        // Act
        var first = JsonDefaults.Indented;
        var second = JsonDefaults.Indented;

        // Assert
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void IndentedPascalCase_IsSingleton_SameReferenceOnMultipleAccesses()
    {
        // Act
        var first = JsonDefaults.IndentedPascalCase;
        var second = JsonDefaults.IndentedPascalCase;

        // Assert
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void CamelCase_IsSingleton_SameReferenceOnMultipleAccesses()
    {
        // Act
        var first = JsonDefaults.CamelCase;
        var second = JsonDefaults.CamelCase;

        // Assert
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void Default_IsSingleton_SameReferenceOnMultipleAccesses()
    {
        // Act
        var first = JsonDefaults.Default;
        var second = JsonDefaults.Default;

        // Assert
        first.Should().BeSameAs(second);
    }

    private sealed class TestDto
    {
        public string? MyProperty { get; set; }
    }
}
