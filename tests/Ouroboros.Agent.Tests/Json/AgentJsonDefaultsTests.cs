using System.Text.Json;
using FluentAssertions;
using Ouroboros.Agent.Json;
using Xunit;

namespace Ouroboros.Tests.Json;

[Trait("Category", "Unit")]
public class AgentJsonDefaultsTests
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
    public void AllInstances_ShouldBeSingletons()
    {
        var indented1 = JsonDefaults.Indented;
        var indented2 = JsonDefaults.Indented;

        var camelCase1 = JsonDefaults.CamelCase;
        var camelCase2 = JsonDefaults.CamelCase;

        var default1 = JsonDefaults.Default;
        var default2 = JsonDefaults.Default;

        indented1.Should().BeSameAs(indented2);
        camelCase1.Should().BeSameAs(camelCase2);
        default1.Should().BeSameAs(default2);
    }
}
