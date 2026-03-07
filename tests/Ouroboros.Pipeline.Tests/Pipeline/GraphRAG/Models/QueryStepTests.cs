namespace Ouroboros.Tests.Pipeline.GraphRAG.Models;

using Ouroboros.Pipeline.GraphRAG.Models;

[Trait("Category", "Unit")]
public class QueryStepTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var deps = new List<int> { 1, 2 };
        var step = new QueryStep(3, QueryStepType.GraphTraversal, "traverse", deps);

        step.Order.Should().Be(3);
        step.StepType.Should().Be(QueryStepType.GraphTraversal);
        step.Query.Should().Be("traverse");
        step.Dependencies.Should().HaveCount(2);
    }

    [Fact]
    public void EntityTypeFilter_DefaultsToNull()
    {
        var step = new QueryStep(1, QueryStepType.VectorSearch, "q", new List<int>());
        step.EntityTypeFilter.Should().BeNull();
    }

    [Fact]
    public void RelationshipTypeFilter_DefaultsToNull()
    {
        var step = new QueryStep(1, QueryStepType.VectorSearch, "q", new List<int>());
        step.RelationshipTypeFilter.Should().BeNull();
    }

    [Fact]
    public void MaxHops_DefaultsToNull()
    {
        var step = new QueryStep(1, QueryStepType.VectorSearch, "q", new List<int>());
        step.MaxHops.Should().BeNull();
    }

    [Fact]
    public void WithExpression_CanSetOptionalProperties()
    {
        var step = new QueryStep(1, QueryStepType.GraphTraversal, "q", new List<int>())
        {
            MaxHops = 3,
            EntityTypeFilter = new List<string> { "Person" },
            RelationshipTypeFilter = new List<string> { "Knows" },
        };

        step.MaxHops.Should().Be(3);
        step.EntityTypeFilter.Should().Contain("Person");
        step.RelationshipTypeFilter.Should().Contain("Knows");
    }
}
