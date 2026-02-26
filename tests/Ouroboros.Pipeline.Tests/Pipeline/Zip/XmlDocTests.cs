namespace Ouroboros.Tests.Pipeline.Zip;

using System.Xml.Linq;
using Ouroboros.Pipeline.Ingestion.Zip;

[Trait("Category", "Unit")]
public class XmlDocTests
{
    [Fact]
    public void Constructor_SetsDocument()
    {
        var doc = new XDocument(new XElement("root"));
        var xmlDoc = new XmlDoc(doc);

        xmlDoc.Document.Should().NotBeNull();
        xmlDoc.Document.Root!.Name.LocalName.Should().Be("root");
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var doc = new XDocument(new XElement("root"));
        var x1 = new XmlDoc(doc);
        var x2 = new XmlDoc(doc);

        x1.Should().Be(x2);
    }
}
