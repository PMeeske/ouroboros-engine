// <copyright file="JsonDefaultsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Agent.Json;

namespace Ouroboros.Tests.Json;

/// <summary>
/// Unit tests for <see cref="JsonDefaults"/>.
/// </summary>
[Trait("Category", "Unit")]
public class JsonDefaultsTests
{
    [Fact]
    public void Indented_IsNotNull()
    {
        JsonDefaults.Indented.Should().NotBeNull();
    }

    [Fact]
    public void Indented_HasWriteIndentedEnabled()
    {
        JsonDefaults.Indented.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void Indented_UsesCamelCaseNaming()
    {
        JsonDefaults.Indented.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CamelCase_IsNotNull()
    {
        JsonDefaults.CamelCase.Should().NotBeNull();
    }

    [Fact]
    public void CamelCase_UsesCamelCaseNaming()
    {
        JsonDefaults.CamelCase.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CamelCase_DoesNotWriteIndented()
    {
        JsonDefaults.CamelCase.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void Default_IsNotNull()
    {
        JsonDefaults.Default.Should().NotBeNull();
    }

    [Fact]
    public void Default_HasCaseInsensitivePropertyNames()
    {
        JsonDefaults.Default.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Indented_SerializesWithCamelCase()
    {
        // Arrange
        var obj = new { PropertyName = "value" };

        // Act
        string json = JsonSerializer.Serialize(obj, JsonDefaults.Indented);

        // Assert
        json.Should().Contain("propertyName");
        json.Should().NotContain("PropertyName");
    }

    [Fact]
    public void Default_DeserializesCaseInsensitively()
    {
        // Arrange
        const string json = """{"NAME": "test"}""";

        // Act
        var result = JsonSerializer.Deserialize<TestDto>(json, JsonDefaults.Default);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("test");
    }

    [Fact]
    public void Instances_AreSameAcrossAccesses()
    {
        // Verify instances are reused (static readonly)
        var first = JsonDefaults.Indented;
        var second = JsonDefaults.Indented;

        first.Should().BeSameAs(second);
    }

    private sealed record TestDto(string Name);
}
