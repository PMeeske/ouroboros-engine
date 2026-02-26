namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class PathwayTierTests
{
    [Fact]
    public void EnumValues_ContainAllExpected()
    {
        var values = Enum.GetValues<PathwayTier>();

        values.Should().HaveCount(4);
        values.Should().Contain(PathwayTier.Local);
        values.Should().Contain(PathwayTier.CloudLight);
        values.Should().Contain(PathwayTier.CloudPremium);
        values.Should().Contain(PathwayTier.Specialized);
    }
}
