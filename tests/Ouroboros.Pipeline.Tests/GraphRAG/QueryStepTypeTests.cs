using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class QueryStepTypeTests
{
    [Fact]
    public void VectorSearch_HasExpectedValue()
    {
        QueryStepType.VectorSearch.Should().Be((QueryStepType)0);
    }

    [Fact]
    public void GraphTraversal_HasExpectedValue()
    {
        QueryStepType.GraphTraversal.Should().Be((QueryStepType)1);
    }

    [Fact]
    public void SymbolicMatch_HasExpectedValue()
    {
        QueryStepType.SymbolicMatch.Should().Be((QueryStepType)2);
    }

    [Fact]
    public void TypeFilter_HasExpectedValue()
    {
        QueryStepType.TypeFilter.Should().Be((QueryStepType)3);
    }

    [Fact]
    public void PropertyFilter_HasExpectedValue()
    {
        QueryStepType.PropertyFilter.Should().Be((QueryStepType)4);
    }

    [Fact]
    public void Aggregate_HasExpectedValue()
    {
        QueryStepType.Aggregate.Should().Be((QueryStepType)5);
    }

    [Fact]
    public void Rank_HasExpectedValue()
    {
        QueryStepType.Rank.Should().Be((QueryStepType)6);
    }

    [Fact]
    public void Inference_HasExpectedValue()
    {
        QueryStepType.Inference.Should().Be((QueryStepType)7);
    }

    [Fact]
    public void Enum_ContainsExactlyEightValues()
    {
        Enum.GetValues<QueryStepType>().Should().HaveCount(8);
    }

    [Theory]
    [InlineData(QueryStepType.VectorSearch, "VectorSearch")]
    [InlineData(QueryStepType.GraphTraversal, "GraphTraversal")]
    [InlineData(QueryStepType.SymbolicMatch, "SymbolicMatch")]
    [InlineData(QueryStepType.TypeFilter, "TypeFilter")]
    [InlineData(QueryStepType.PropertyFilter, "PropertyFilter")]
    [InlineData(QueryStepType.Aggregate, "Aggregate")]
    [InlineData(QueryStepType.Rank, "Rank")]
    [InlineData(QueryStepType.Inference, "Inference")]
    public void ToString_ReturnsExpectedName(QueryStepType stepType, string expectedName)
    {
        stepType.ToString().Should().Be(expectedName);
    }
}
