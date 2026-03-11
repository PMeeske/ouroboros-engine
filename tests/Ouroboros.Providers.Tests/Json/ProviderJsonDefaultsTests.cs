using System.Text.Json;
using FluentAssertions;
using Ouroboros.Providers.Json;
using Xunit;

namespace Ouroboros.Tests.Providers.Json;

[Trait("Category", "Unit")]
public class ProviderJsonDefaultsTests
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
    public void CamelCase_ShouldBeCaseInsensitive()
    {
        JsonDefaults.CamelCase.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void Default_ShouldBeCaseInsensitive()
    {
        JsonDefaults.Default.PropertyNameCaseInsensitive.Should().BeTrue();
    }

    [Fact]
    public void AllInstances_ShouldBeSingletons()
    {
        JsonDefaults.Indented.Should().BeSameAs(JsonDefaults.Indented);
        JsonDefaults.CamelCase.Should().BeSameAs(JsonDefaults.CamelCase);
        JsonDefaults.Default.Should().BeSameAs(JsonDefaults.Default);
    }
}
