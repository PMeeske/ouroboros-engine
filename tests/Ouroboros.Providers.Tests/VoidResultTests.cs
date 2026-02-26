namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class VoidResultTests
{
    [Fact]
    public void Value_IsDefaultInstance()
    {
        var value = VoidResult.Value;

        value.Should().Be(default(VoidResult));
    }

    [Fact]
    public void Equality_TwoDefaults_AreEqual()
    {
        var a = VoidResult.Value;
        var b = default(VoidResult);

        a.Should().Be(b);
    }

    [Fact]
    public void Struct_IsValueType()
    {
        typeof(VoidResult).IsValueType.Should().BeTrue();
    }
}
