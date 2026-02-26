namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class CausalPathTests
{
    [Fact]
    public void FromNode_CreatesSingleNodePath()
    {
        var node = CausalNode.CreateState("start", "desc");
        var path = CausalPath.FromNode(node);

        path.Nodes.Should().HaveCount(1);
        path.Edges.Should().BeEmpty();
        path.Length.Should().Be(0);
        path.TotalStrength.Should().Be(1.0);
    }

    [Fact]
    public void Extend_AddsNodeAndEdge()
    {
        var n1 = CausalNode.CreateState("start", "desc");
        var n2 = CausalNode.CreateEvent("end", "desc");
        var edge = CausalEdge.Create(n1.Id, n2.Id, 0.8);

        var path = CausalPath.FromNode(n1).Extend(n2, edge);

        path.Nodes.Should().HaveCount(2);
        path.Edges.Should().HaveCount(1);
        path.Length.Should().Be(1);
        path.TotalStrength.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void Extend_MultipliesStrength()
    {
        var n1 = CausalNode.CreateState("a", "d");
        var n2 = CausalNode.CreateState("b", "d");
        var n3 = CausalNode.CreateState("c", "d");
        var e1 = CausalEdge.Create(n1.Id, n2.Id, 0.5);
        var e2 = CausalEdge.Create(n2.Id, n3.Id, 0.6);

        var path = CausalPath.FromNode(n1).Extend(n2, e1).Extend(n3, e2);

        path.TotalStrength.Should().BeApproximately(0.3, 0.001);
    }
}
