using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class CsvTableTests
{
    [Fact]
    public void Constructor_SetsHeaderAndRows()
    {
        // Arrange
        var header = new[] { "Name", "Age", "City" };
        var rows = new List<string[]>
        {
            new[] { "Alice", "30", "NYC" },
            new[] { "Bob", "25", "LA" }
        };

        // Act
        var table = new CsvTable(header, rows);

        // Assert
        table.Header.Should().BeEquivalentTo(new[] { "Name", "Age", "City" });
        table.Rows.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_EmptyHeaderAndRows_CreatesValidTable()
    {
        // Arrange & Act
        var table = new CsvTable(Array.Empty<string>(), new List<string[]>());

        // Assert
        table.Header.Should().BeEmpty();
        table.Rows.Should().BeEmpty();
    }

    [Fact]
    public void Rows_AreMutable_CanAddRows()
    {
        // Arrange
        var rows = new List<string[]>();
        var table = new CsvTable(new[] { "Col1" }, rows);

        // Act
        rows.Add(new[] { "Value1" });

        // Assert - List<T> is mutable so changes are reflected
        table.Rows.Should().HaveCount(1);
    }

    [Fact]
    public void Equality_SameHeaderAndRows_AreEqual()
    {
        // Arrange
        var header = new[] { "A", "B" };
        var rows = new List<string[]> { new[] { "1", "2" } };
        var t1 = new CsvTable(header, rows);
        var t2 = new CsvTable(header, rows);

        // Assert - records compare by reference for mutable members
        t1.Should().Be(t2); // same List reference
    }

    [Fact]
    public void Header_CanContainSpecialCharacters()
    {
        // Arrange & Act
        var table = new CsvTable(new[] { "First Name", "Age (years)", "City/Town" }, new List<string[]>());

        // Assert
        table.Header.Should().HaveCount(3);
        table.Header[0].Should().Be("First Name");
    }
}
