namespace Ouroboros.Tests.Pipeline.Zip;

using Ouroboros.Pipeline.Ingestion.Zip;

[Trait("Category", "Unit")]
public class ZipContentKindTests
{
    [Theory]
    [InlineData(ZipContentKind.Csv, 0)]
    [InlineData(ZipContentKind.Xml, 1)]
    [InlineData(ZipContentKind.Text, 2)]
    [InlineData(ZipContentKind.Binary, 3)]
    public void EnumValues_AreDefinedCorrectly(ZipContentKind value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }

    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<ZipContentKind>().Should().HaveCount(4);
    }
}
