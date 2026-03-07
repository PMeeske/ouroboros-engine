namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class QueryStepTypeTests
{
    [Theory]
    [InlineData(QueryStepType.VectorSearch, 0)]
    [InlineData(QueryStepType.GraphTraversal, 1)]
    [InlineData(QueryStepType.SymbolicMatch, 2)]
    [InlineData(QueryStepType.TypeFilter, 3)]
    [InlineData(QueryStepType.PropertyFilter, 4)]
    [InlineData(QueryStepType.Aggregate, 5)]
    [InlineData(QueryStepType.Rank, 6)]
    [InlineData(QueryStepType.Inference, 7)]
    public void EnumValues_AreDefinedCorrectly(QueryStepType value, int expectedInt)
    {
        ((int)value).Should().Be(expectedInt);
    }

    [Fact]
    public void EnumHasExpectedCount()
    {
        Enum.GetValues<QueryStepType>().Should().HaveCount(8);
    }
}
