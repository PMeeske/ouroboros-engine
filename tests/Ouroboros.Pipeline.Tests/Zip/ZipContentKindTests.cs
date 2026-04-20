using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class ZipContentKindTests
{
    [Fact]
    public void Csv_HasExpectedValue()
    {
        ((int)ZipContentKind.Csv).Should().Be(0);
    }

    [Fact]
    public void Xml_HasExpectedValue()
    {
        ((int)ZipContentKind.Xml).Should().Be(1);
    }

    [Fact]
    public void Text_HasExpectedValue()
    {
        ((int)ZipContentKind.Text).Should().Be(2);
    }

    [Fact]
    public void Binary_HasExpectedValue()
    {
        ((int)ZipContentKind.Binary).Should().Be(3);
    }

    [Fact]
    public void Enum_HasExactlyFourValues()
    {
        var values = Enum.GetValues<ZipContentKind>();
        values.Should().HaveCount(4);
    }

    [Theory]
    [InlineData(ZipContentKind.Csv, "Csv")]
    [InlineData(ZipContentKind.Xml, "Xml")]
    [InlineData(ZipContentKind.Text, "Text")]
    [InlineData(ZipContentKind.Binary, "Binary")]
    public void ToString_ReturnsExpectedName(ZipContentKind kind, string expected)
    {
        kind.ToString().Should().Be(expected);
    }
}
