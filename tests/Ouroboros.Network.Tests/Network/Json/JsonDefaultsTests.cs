using System.Text.Json;
using FluentAssertions;
using Ouroboros.Network.Json;

namespace Ouroboros.Tests.Network.Json;

[Trait("Category", "Unit")]
public sealed class JsonDefaultsTests
{
    [Fact]
    public void Indented_HasWriteIndented()
    {
        JsonDefaults.Indented.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void Indented_UsesCamelCase()
    {
        JsonDefaults.Indented.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CamelCase_UsesCamelCasePolicy()
    {
        JsonDefaults.CamelCase.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void Default_IsCaseInsensitive()
    {
        JsonDefaults.Default.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Indented_IsSameInstanceOnMultipleAccesses()
    {
        var first = JsonDefaults.Indented;
        var second = JsonDefaults.Indented;

        first.Should().BeSameAs(second);
    }
}
