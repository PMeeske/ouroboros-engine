namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class QueryTypeTests
{
    [Theory]
    [InlineData(QueryType.SingleHop, 0)]
    [InlineData(QueryType.MultiHop, 1)]
    [InlineData(QueryType.Aggregation, 2)]
    [InlineData(QueryType.Comparison, 3)]
    public void EnumValues_AreDefinedCorrectly(QueryType value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }

    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<QueryType>().Should().HaveCount(4);
    }
}
