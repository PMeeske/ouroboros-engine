using Ouroboros.Agent.MetaAI;
using System.Reflection;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OptionExtensionsTests
{
    private static Option<T> InvokeToOption<T>(T? value) where T : class
    {
        var method = typeof(OptionExtensions).GetMethod("ToOption", BindingFlags.Public | BindingFlags.Static);
        var genericMethod = method!.MakeGenericMethod(typeof(T));
        return (Option<T>)genericMethod.Invoke(null, new object?[] { value })!;
    }

    #region ToOption

    [Fact]
    public void ToOption_WithNonNullValue_ShouldReturnSome()
    {
        var result = InvokeToOption("test");
        result.IsSome.Should().BeTrue();
        result.Value.Should().Be("test");
    }

    [Fact]
    public void ToOption_WithNullValue_ShouldReturnNone()
    {
        var result = InvokeToOption<string>(null);
        result.IsSome.Should().BeFalse();
    }

    [Fact]
    public void ToOption_WithObject_ShouldReturnSome()
    {
        var obj = new object();
        var result = InvokeToOption(obj);
        result.IsSome.Should().BeTrue();
        result.Value.Should().BeSameAs(obj);
    }

    #endregion
}
