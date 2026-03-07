namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class QueryPlanTests
{
    [Fact]
    public void SingleHop_CreatesSimplePlan()
    {
        var plan = QueryPlan.SingleHop("Find documents about AI");

        plan.OriginalQuery.Should().Be("Find documents about AI");
        plan.QueryType.Should().Be(QueryType.SingleHop);
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].StepType.Should().Be(QueryStepType.VectorSearch);
    }

    [Fact]
    public void EstimatedComplexity_EqualsStepCountForNonMultiHop()
    {
        var plan = QueryPlan.SingleHop("query");
        plan.EstimatedComplexity.Should().Be(1);
    }

    [Fact]
    public void EstimatedComplexity_AddsExtraForMultiHop()
    {
        var steps = new List<QueryStep>
        {
            new(1, QueryStepType.VectorSearch, "q", new List<int>()),
            new(2, QueryStepType.GraphTraversal, "t", new List<int> { 1 }),
        };
        var plan = new QueryPlan("query", QueryType.MultiHop, steps);

        plan.EstimatedComplexity.Should().Be(4); // 2 steps + 2 for MultiHop
    }
}
