using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class QueryTypeTests
{
    [Fact]
    public void SingleHop_HasExpectedValue()
    {
        QueryType.SingleHop.Should().Be((QueryType)0);
    }

    [Fact]
    public void MultiHop_HasExpectedValue()
    {
        QueryType.MultiHop.Should().Be((QueryType)1);
    }

    [Fact]
    public void Aggregation_HasExpectedValue()
    {
        QueryType.Aggregation.Should().Be((QueryType)2);
    }

    [Fact]
    public void Comparison_HasExpectedValue()
    {
        QueryType.Comparison.Should().Be((QueryType)3);
    }

    [Fact]
    public void Enum_ContainsExactlyFourValues()
    {
        Enum.GetValues<QueryType>().Should().HaveCount(4);
    }

    [Theory]
    [InlineData(QueryType.SingleHop, "SingleHop")]
    [InlineData(QueryType.MultiHop, "MultiHop")]
    [InlineData(QueryType.Aggregation, "Aggregation")]
    [InlineData(QueryType.Comparison, "Comparison")]
    public void ToString_ReturnsExpectedName(QueryType queryType, string expectedName)
    {
        queryType.ToString().Should().Be(expectedName);
    }
}
