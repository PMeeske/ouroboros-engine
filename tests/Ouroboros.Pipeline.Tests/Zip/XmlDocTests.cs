using System.Xml.Linq;
using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class XmlDocTests
{
    [Fact]
    public void Constructor_SetsDocument()
    {
        // Arrange
        var doc = new XDocument(new XElement("root"));

        // Act
        var xmlDoc = new XmlDoc(doc);

        // Assert
        xmlDoc.Document.Should().BeSameAs(doc);
    }

    [Fact]
    public void Constructor_WithComplexDocument_PreservesStructure()
    {
        // Arrange
        var doc = new XDocument(
            new XElement("root",
                new XElement("child1", "value1"),
                new XElement("child2",
                    new XAttribute("attr", "val"),
                    "value2")));

        // Act
        var xmlDoc = new XmlDoc(doc);

        // Assert
        xmlDoc.Document.Root!.Name.LocalName.Should().Be("root");
        xmlDoc.Document.Root.Elements().Should().HaveCount(2);
    }

    [Fact]
    public void Equality_SameDocument_AreEqual()
    {
        // Arrange
        var doc = new XDocument(new XElement("root"));
        var x1 = new XmlDoc(doc);
        var x2 = new XmlDoc(doc);

        // Assert
        x1.Should().Be(x2);
    }

    [Fact]
    public void Equality_DifferentDocuments_AreNotEqual()
    {
        // Arrange
        var x1 = new XmlDoc(new XDocument(new XElement("a")));
        var x2 = new XmlDoc(new XDocument(new XElement("b")));

        // Assert
        x1.Should().NotBe(x2);
    }
}
