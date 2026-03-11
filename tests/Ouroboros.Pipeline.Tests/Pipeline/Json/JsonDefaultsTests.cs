using System.Text.Json;
using FluentAssertions;
using Ouroboros.Pipeline.Json;
using Xunit;

namespace Ouroboros.Tests.Pipeline.Json;

[Trait("Category", "Unit")]
public class JsonDefaultsTests
{
    [Fact]
    public void Indented_ShouldWriteIndented()
    {
        JsonDefaults.Indented.WriteIndented.Should().BeTrue();
    }

    [Fact]
    public void Indented_ShouldUseCamelCase()
    {
        JsonDefaults.Indented.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void CamelCase_ShouldNotWriteIndented()
    {
        JsonDefaults.CamelCase.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void CamelCase_ShouldUseCamelCase()
    {
        JsonDefaults.CamelCase.PropertyNamingPolicy.Should().Be(JsonNamingPolicy.CamelCase);
    }

    [Fact]
    public void Default_ShouldBeCaseInsensitive()
    {
        JsonDefaults.Default.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Default_ShouldNotWriteIndented()
    {
        JsonDefaults.Default.WriteIndented.Should().BeFalse();
    }

    [Fact]
    public void AllInstances_ShouldBeSameAcrossCalls()
    {
        var a = JsonDefaults.Indented;
        var b = JsonDefaults.Indented;
        a.Should().BeSameAs(b);
    }
}
