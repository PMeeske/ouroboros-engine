namespace Ouroboros.Tests.Pipeline.Zip;

using Ouroboros.Pipeline.Ingestion.Zip;

[Trait("Category", "Unit")]
public class CsvTableTests
{
    [Fact]
    public void Constructor_SetsHeaderAndRows()
    {
        var header = new[] { "Name", "Age" };
        var rows = new List<string[]>
        {
            new[] { "John", "30" },
            new[] { "Jane", "25" },
        };

        var table = new CsvTable(header, rows);

        table.Header.Should().HaveCount(2);
        table.Rows.Should().HaveCount(2);
    }

    [Fact]
    public void Record_SupportsEquality()
    {
        var header = new[] { "A" };
        var rows = new List<string[]>();
        var t1 = new CsvTable(header, rows);
        var t2 = new CsvTable(header, rows);

        t1.Should().Be(t2);
    }
}
