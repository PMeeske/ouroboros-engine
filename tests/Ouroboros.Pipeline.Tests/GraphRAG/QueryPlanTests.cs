using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

[Trait("Category", "Unit")]
public sealed class QueryPlanTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        // Arrange
        var steps = new List<QueryStep>
        {
            new(1, QueryStepType.VectorSearch, "search", new List<int>()),
            new(2, QueryStepType.Rank, "rank results", new List<int> { 1 })
        };

        // Act
        var plan = new QueryPlan("What is AI?", QueryType.SingleHop, steps);

        // Assert
        plan.OriginalQuery.Should().Be("What is AI?");
        plan.QueryType.Should().Be(QueryType.SingleHop);
        plan.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void SingleHop_CreatesValidPlan()
    {
        // Arrange & Act
        var plan = QueryPlan.SingleHop("find all people");

        // Assert
        plan.OriginalQuery.Should().Be("find all people");
        plan.QueryType.Should().Be(QueryType.SingleHop);
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].Order.Should().Be(1);
        plan.Steps[0].StepType.Should().Be(QueryStepType.VectorSearch);
        plan.Steps[0].Query.Should().Be("find all people");
        plan.Steps[0].Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void EstimatedComplexity_ForSingleHop_EqualsStepCount()
    {
        // Arrange
        var plan = QueryPlan.SingleHop("simple query");

        // Act
        var complexity = plan.EstimatedComplexity;

        // Assert
        complexity.Should().Be(1);
    }

    [Fact]
    public void EstimatedComplexity_ForMultiHop_AddsTwo()
    {
        // Arrange
        var steps = new List<QueryStep>
        {
            new(1, QueryStepType.VectorSearch, "search", new List<int>()),
            new(2, QueryStepType.GraphTraversal, "traverse", new List<int> { 1 }),
            new(3, QueryStepType.Rank, "rank", new List<int> { 2 })
        };
        var plan = new QueryPlan("complex query", QueryType.MultiHop, steps);

        // Act
        var complexity = plan.EstimatedComplexity;

        // Assert
        complexity.Should().Be(5); // 3 steps + 2 for MultiHop
    }

    [Fact]
    public void EstimatedComplexity_ForAggregation_EqualsStepCount()
    {
        // Arrange
        var steps = new List<QueryStep>
        {
            new(1, QueryStepType.VectorSearch, "search", new List<int>()),
            new(2, QueryStepType.Aggregate, "aggregate", new List<int> { 1 })
        };
        var plan = new QueryPlan("aggregate query", QueryType.Aggregation, steps);

        // Act
        var complexity = plan.EstimatedComplexity;

        // Assert
        complexity.Should().Be(2); // no extra for Aggregation
    }

    [Fact]
    public void EstimatedComplexity_ForComparison_EqualsStepCount()
    {
        // Arrange
        var steps = new List<QueryStep>
        {
            new(1, QueryStepType.VectorSearch, "search", new List<int>())
        };
        var plan = new QueryPlan("compare query", QueryType.Comparison, steps);

        // Act
        var complexity = plan.EstimatedComplexity;

        // Assert
        complexity.Should().Be(1);
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Arrange
        var steps = new List<QueryStep>();
        var plan1 = new QueryPlan("query", QueryType.SingleHop, steps);
        var plan2 = new QueryPlan("query", QueryType.SingleHop, steps);

        // Act & Assert
        plan1.Should().Be(plan2);
    }
}
