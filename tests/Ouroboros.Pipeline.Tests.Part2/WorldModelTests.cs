namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class CapabilityTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var cap = new Capability("coding", "Writes code", 0.9);
        cap.Name.Should().Be("coding");
        cap.Description.Should().Be("Writes code");
        cap.Proficiency.Should().Be(0.9);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_InvalidProficiency_ShouldThrowArgumentOutOfRangeException(double proficiency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Capability("name", "desc", proficiency));
    }
}

[Trait("Category", "Unit")]
public class CausalEdgeTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        var edge = new CausalEdge(from, to, "causes", 0.8);
        edge.FromNodeId.Should().Be(from);
        edge.ToNodeId.Should().Be(to);
        edge.Relationship.Should().Be("causes");
        edge.Strength.Should().Be(0.8);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_InvalidStrength_ShouldThrowArgumentOutOfRangeException(double strength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CausalEdge(Guid.NewGuid(), Guid.NewGuid(), "rel", strength));
    }
}

[Trait("Category", "Unit")]
public class CausalGraphTests
{
    #region Empty

    [Fact]
    public void Empty_ShouldCreateEmptyGraph()
    {
        var graph = CausalGraph.Empty();
        graph.NodeCount.Should().Be(0);
        graph.EdgeCount.Should().Be(0);
        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
    }

    #endregion

    #region AddNode

    [Fact]
    public void AddNode_Null_ShouldThrowArgumentNullException()
    {
        var graph = CausalGraph.Empty();
        Assert.Throws<ArgumentNullException>(() => graph.AddNode(null!));
    }

    [Fact]
    public void AddNode_Valid_ShouldAddNode()
    {
        var graph = CausalGraph.Empty();
        var node = CausalNode.CreateState("State1", "description");
        var updated = graph.AddNode(node);
        updated.NodeCount.Should().Be(1);
        updated.Nodes.Should().Contain(node);
    }

    [Fact]
    public void AddNode_Duplicate_ShouldReplaceNode()
    {
        var graph = CausalGraph.Empty();
        var node = CausalNode.CreateState("State1", "description");
        var updated = graph.AddNode(node).AddNode(node);
        updated.NodeCount.Should().Be(1);
    }

    #endregion

    #region AddEdge

    [Fact]
    public void AddEdge_MissingFromNode_ShouldReturnFailure()
    {
        var graph = CausalGraph.Empty();
        var toNode = CausalNode.CreateState("To", "desc");
        graph = graph.AddNode(toNode);
        var edge = new CausalEdge(Guid.NewGuid(), toNode.Id, "causes", 0.5);
        var result = graph.AddEdge(edge);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddEdge_MissingToNode_ShouldReturnFailure()
    {
        var graph = CausalGraph.Empty();
        var fromNode = CausalNode.CreateState("From", "desc");
        graph = graph.AddNode(fromNode);
        var edge = new CausalEdge(fromNode.Id, Guid.NewGuid(), "causes", 0.5);
        var result = graph.AddEdge(edge);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AddEdge_Valid_ShouldReturnSuccess()
    {
        var graph = CausalGraph.Empty();
        var fromNode = CausalNode.CreateState("From", "desc");
        var toNode = CausalNode.CreateState("To", "desc");
        graph = graph.AddNode(fromNode).AddNode(toNode);
        var edge = new CausalEdge(fromNode.Id, toNode.Id, "causes", 0.5);
        var result = graph.AddEdge(edge);
        result.IsSuccess.Should().BeTrue();
        result.Value.EdgeCount.Should().Be(1);
    }

    [Fact]
    public void AddEdge_Duplicate_ShouldReturnFailure()
    {
        var graph = CausalGraph.Empty();
        var fromNode = CausalNode.CreateState("From", "desc");
        var toNode = CausalNode.CreateState("To", "desc");
        graph = graph.AddNode(fromNode).AddNode(toNode);
        var edge = new CausalEdge(fromNode.Id, toNode.Id, "causes", 0.5);
        graph = graph.AddEdge(edge).Value;
        var result = graph.AddEdge(edge);
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region GetNode

    [Fact]
    public void GetNode_Existing_ShouldReturnSome()
    {
        var node = CausalNode.CreateState("Test", "desc");
        var graph = CausalGraph.Empty().AddNode(node);
        var result = graph.GetNode(node.Id);
        result.HasValue.Should().BeTrue();
    }

    [Fact]
    public void GetNode_Missing_ShouldReturnNone()
    {
        var graph = CausalGraph.Empty();
        var result = graph.GetNode(Guid.NewGuid());
        result.HasValue.Should().BeFalse();
    }

    #endregion

    #region GetOutgoingEdges

    [Fact]
    public void GetOutgoingEdges_ShouldReturnCorrectEdges()
    {
        var fromNode = CausalNode.CreateState("From", "desc");
        var toNode = CausalNode.CreateState("To", "desc");
        var graph = CausalGraph.Empty().AddNode(fromNode).AddNode(toNode);
        var edge = new CausalEdge(fromNode.Id, toNode.Id, "causes", 0.5);
        graph = graph.AddEdge(edge).Value;
        var outgoing = graph.GetOutgoingEdges(fromNode.Id);
        outgoing.Should().ContainSingle();
    }

    #endregion

    #region GetIncomingEdges

    [Fact]
    public void GetIncomingEdges_ShouldReturnCorrectEdges()
    {
        var fromNode = CausalNode.CreateState("From", "desc");
        var toNode = CausalNode.CreateState("To", "desc");
        var graph = CausalGraph.Empty().AddNode(fromNode).AddNode(toNode);
        var edge = new CausalEdge(fromNode.Id, toNode.Id, "causes", 0.5);
        graph = graph.AddEdge(edge).Value;
        var incoming = graph.GetIncomingEdges(toNode.Id);
        incoming.Should().ContainSingle();
    }

    #endregion

    #region PredictEffects

    [Fact]
    public void PredictEffects_MissingNode_ShouldReturnFailure()
    {
        var graph = CausalGraph.Empty();
        var result = graph.PredictEffects(Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void PredictEffects_NoOutgoingEdges_ShouldReturnEmpty()
    {
        var node = CausalNode.CreateState("Isolated", "desc");
        var graph = CausalGraph.Empty().AddNode(node);
        var result = graph.PredictEffects(node.Id);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void PredictEffects_WithOutgoingEdges_ShouldReturnEffects()
    {
        var fromNode = CausalNode.CreateState("From", "desc");
        var toNode = CausalNode.CreateState("To", "desc");
        var graph = CausalGraph.Empty().AddNode(fromNode).AddNode(toNode);
        var edge = new CausalEdge(fromNode.Id, toNode.Id, "causes", 0.8);
        graph = graph.AddEdge(edge).Value;
        var result = graph.PredictEffects(fromNode.Id);
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
    }

    #endregion

    #region FindPath

    [Fact]
    public void FindPath_SameNode_ShouldReturnEmpty()
    {
        var node = CausalNode.CreateState("Same", "desc");
        var graph = CausalGraph.Empty().AddNode(node);
        var result = graph.FindPath(node.Id, node.Id);
        result.IsSuccess.Should().BeTrue();
        result.Value.Nodes.Should().BeEmpty();
    }

    [Fact]
    public void FindPath_MissingFrom_ShouldReturnFailure()
    {
        var toNode = CausalNode.CreateState("To", "desc");
        var graph = CausalGraph.Empty().AddNode(toNode);
        var result = graph.FindPath(Guid.NewGuid(), toNode.Id);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FindPath_MissingTo_ShouldReturnFailure()
    {
        var fromNode = CausalNode.CreateState("From", "desc");
        var graph = CausalGraph.Empty().AddNode(fromNode);
        var result = graph.FindPath(fromNode.Id, Guid.NewGuid());
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FindPath_DirectPath_ShouldReturnPath()
    {
        var fromNode = CausalNode.CreateState("From", "desc");
        var toNode = CausalNode.CreateState("To", "desc");
        var graph = CausalGraph.Empty().AddNode(fromNode).AddNode(toNode);
        var edge = new CausalEdge(fromNode.Id, toNode.Id, "causes", 0.8);
        graph = graph.AddEdge(edge).Value;
        var result = graph.FindPath(fromNode.Id, toNode.Id);
        result.IsSuccess.Should().BeTrue();
        result.Value.Nodes.Should().HaveCount(2);
    }

    #endregion

    #region RemoveNode

    [Fact]
    public void RemoveNode_Missing_ShouldReturnSameGraph()
    {
        var graph = CausalGraph.Empty();
        var updated = graph.RemoveNode(Guid.NewGuid());
        updated.NodeCount.Should().Be(0);
    }

    [Fact]
    public void RemoveNode_Existing_ShouldRemoveNodeAndEdges()
    {
        var fromNode = CausalNode.CreateState("From", "desc");
        var toNode = CausalNode.CreateState("To", "desc");
        var graph = CausalGraph.Empty().AddNode(fromNode).AddNode(toNode);
        var edge = new CausalEdge(fromNode.Id, toNode.Id, "causes", 0.8);
        graph = graph.AddEdge(edge).Value;
        var updated = graph.RemoveNode(fromNode.Id);
        updated.NodeCount.Should().Be(1);
        updated.EdgeCount.Should().Be(0);
    }

    #endregion

    #region RemoveEdge

    [Fact]
    public void RemoveEdge_Missing_ShouldReturnSameGraph()
    {
        var graph = CausalGraph.Empty();
        var updated = graph.RemoveEdge(Guid.NewGuid());
        updated.EdgeCount.Should().Be(0);
    }

    #endregion
}

[Trait("Category", "Unit")]
public class CausalNodeTests
{
    #region Create

    [Fact]
    public void Create_NullName_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CausalNode.Create(null!, "desc", CausalNodeType.State));
    }

    [Fact]
    public void Create_NullDescription_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CausalNode.Create("name", null!, CausalNodeType.State));
    }

    [Fact]
    public void Create_Valid_ShouldInitialize()
    {
        var node = CausalNode.Create("Test", "description", CausalNodeType.State);
        node.Name.Should().Be("Test");
        node.NodeType.Should().Be(CausalNodeType.State);
        node.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void CreateState_ShouldCreateStateNode()
    {
        var node = CausalNode.CreateState("State1", "desc");
        node.NodeType.Should().Be(CausalNodeType.State);
    }

    [Fact]
    public void CreateAction_ShouldCreateActionNode()
    {
        var node = CausalNode.CreateAction("Action1", "desc");
        node.NodeType.Should().Be(CausalNodeType.Action);
    }

    [Fact]
    public void CreateEvent_ShouldCreateEventNode()
    {
        var node = CausalNode.CreateEvent("Event1", "desc");
        node.NodeType.Should().Be(CausalNodeType.Event);
    }

    #endregion

    #region WithDescription

    [Fact]
    public void WithDescription_Null_ShouldThrowArgumentNullException()
    {
        var node = CausalNode.CreateState("Test", "desc");
        Assert.Throws<ArgumentNullException>(() => node.WithDescription(null!));
    }

    [Fact]
    public void WithDescription_ShouldUpdateDescription()
    {
        var node = CausalNode.CreateState("Test", "old");
        var updated = node.WithDescription("new");
        updated.Description.Should().Be("new");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class CausalNodeTypeTests
{
    [Theory]
    [InlineData(CausalNodeType.State)]
    [InlineData(CausalNodeType.Action)]
    [InlineData(CausalNodeType.Event)]
    public void AllEnumValues_ShouldBeDefined(CausalNodeType value)
    {
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveThreeValues()
    {
        var values = Enum.GetValues<CausalNodeType>();
        values.Should().HaveCount(3);
    }
}

[Trait("Category", "Unit")]
public class CausalPathTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var node1 = CausalNode.CreateState("A", "desc");
        var node2 = CausalNode.CreateState("B", "desc");
        var edge = new CausalEdge(node1.Id, node2.Id, "causes", 0.8);
        var path = new CausalPath(new[] { node1, node2 }, new[] { edge });
        path.Nodes.Should().HaveCount(2);
        path.Edges.Should().HaveCount(1);
        path.PathStrength.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void PathStrength_EmptyPath_ShouldBeZero()
    {
        var path = new CausalPath(Array.Empty<CausalNode>(), Array.Empty<CausalEdge>());
        path.PathStrength.Should().Be(0.0);
    }
}

[Trait("Category", "Unit")]
public class ConstraintTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var constraint = new Constraint("name", "description", c => true);
        constraint.Name.Should().Be("name");
        constraint.Description.Should().Be("description");
    }

    [Fact]
    public void Constructor_NullName_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Constraint(null!, "desc", c => true));
    }

    [Fact]
    public void Constructor_NullDescription_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Constraint("name", null!, c => true));
    }

    [Fact]
    public void Constructor_NullPredicate_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Constraint("name", "desc", null!));
    }
}

[Trait("Category", "Unit")]
public class ObservationTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var obs = new Observation("key", "value", DateTime.UtcNow);
        obs.Key.Should().Be("key");
        obs.Value.Should().Be("value");
    }
}

[Trait("Category", "Unit")]
public class OptimizationStrategyTests
{
    [Theory]
    [InlineData(OptimizationStrategy.MinimizeCost)]
    [InlineData(OptimizationStrategy.MaximizeQuality)]
    [InlineData(OptimizationStrategy.MinimizeLatency)]
    [InlineData(OptimizationStrategy.Balanced)]
    public void AllEnumValues_ShouldBeDefined(OptimizationStrategy value)
    {
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }
}

[Trait("Category", "Unit")]
public class PredictedEffectTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var node = CausalNode.CreateState("Effect", "desc");
        var effect = new PredictedEffect(node, 0.8, "reasoning");
        effect.Node.Should().Be(node);
        effect.Probability.Should().Be(0.8);
        effect.Reasoning.Should().Be("reasoning");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Constructor_InvalidProbability_ShouldThrowArgumentOutOfRangeException(double prob)
    {
        var node = CausalNode.CreateState("Effect", "desc");
        Assert.Throws<ArgumentOutOfRangeException>(() => new PredictedEffect(node, prob, "reason"));
    }
}

[Trait("Category", "Unit")]
public class SelectionConfigTests
{
    [Fact]
    public void Default_ShouldInitializeWithDefaults()
    {
        var config = SelectionConfig.Default;
        config.MaxCandidates.Should().BeGreaterThan(0);
        config.MinConfidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void WithMaxCandidates_ShouldUpdateValue()
    {
        var config = SelectionConfig.Default.WithMaxCandidates(5);
        config.MaxCandidates.Should().Be(5);
    }

    [Fact]
    public void WithMinConfidence_ShouldUpdateValue()
    {
        var config = SelectionConfig.Default.WithMinConfidence(0.5);
        config.MinConfidence.Should().Be(0.5);
    }
}

[Trait("Category", "Unit")]
public class SmartToolSelectorTests
{
    #region Construction

    [Fact]
    public void Constructor_NullWorldState_ShouldThrowArgumentNullException()
    {
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher();
        Assert.Throws<ArgumentNullException>(() => new SmartToolSelector(null!, registry, matcher));
    }

    [Fact]
    public void Constructor_NullRegistry_ShouldThrowArgumentNullException()
    {
        var worldState = WorldState.Empty();
        var matcher = new ToolCapabilityMatcher();
        Assert.Throws<ArgumentNullException>(() => new SmartToolSelector(worldState, null!, matcher));
    }

    [Fact]
    public void Constructor_NullMatcher_ShouldThrowArgumentNullException()
    {
        var worldState = WorldState.Empty();
        var registry = new ToolRegistry();
        Assert.Throws<ArgumentNullException>(() => new SmartToolSelector(worldState, registry, null!));
    }

    [Fact]
    public void Constructor_NullConfig_ShouldThrowArgumentNullException()
    {
        var worldState = WorldState.Empty();
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher();
        Assert.Throws<ArgumentNullException>(() => new SmartToolSelector(worldState, registry, matcher, null!));
    }

    [Fact]
    public void Constructor_Valid_ShouldInitialize()
    {
        var worldState = WorldState.Empty();
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher();
        var selector = new SmartToolSelector(worldState, registry, matcher);
        selector.Should().NotBeNull();
    }

    #endregion

    #region SelectTools

    [Fact]
    public void SelectTools_NullGoal_ShouldThrowArgumentNullException()
    {
        var worldState = WorldState.Empty();
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher();
        var selector = new SmartToolSelector(worldState, registry, matcher);
        Assert.Throws<ArgumentNullException>(() => selector.SelectTools(null!));
    }

    [Fact]
    public void SelectTools_NoTools_ShouldReturnEmpty()
    {
        var worldState = WorldState.Empty();
        var registry = new ToolRegistry();
        var matcher = new ToolCapabilityMatcher();
        var selector = new SmartToolSelector(worldState, registry, matcher);
        var result = selector.SelectTools(Goal.Atomic("test"));
        result.Should().BeEmpty();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class ToolCandidateTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var candidate = new ToolCandidate("tool1", "Tool One", 0.9, new[] { "cap1" });
        candidate.Name.Should().Be("tool1");
        candidate.DisplayName.Should().Be("Tool One");
        candidate.Confidence.Should().Be(0.9);
        candidate.Capabilities.Should().Contain("cap1");
    }
}

[Trait("Category", "Unit")]
public class ToolCapabilityMatcherTests
{
    [Fact]
    public void Match_NullGoal_ShouldThrowArgumentNullException()
    {
        var matcher = new ToolCapabilityMatcher();
        Assert.Throws<ArgumentNullException>(() => matcher.Match(null!, Array.Empty<ToolCandidate>()));
    }

    [Fact]
    public void Match_NullCandidates_ShouldThrowArgumentNullException()
    {
        var matcher = new ToolCapabilityMatcher();
        Assert.Throws<ArgumentNullException>(() => matcher.Match(Goal.Atomic("test"), null!));
    }

    [Fact]
    public void Match_EmptyCandidates_ShouldReturnEmpty()
    {
        var matcher = new ToolCapabilityMatcher();
        var result = matcher.Match(Goal.Atomic("test"), Array.Empty<ToolCandidate>());
        result.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class ToolMatchTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var candidate = new ToolCandidate("tool1", "Tool One", 0.9, new[] { "cap1" });
        var match = new ToolMatch(candidate, 0.8, "reasoning");
        match.Candidate.Should().Be(candidate);
        match.Score.Should().Be(0.8);
        match.Reasoning.Should().Be("reasoning");
    }
}

[Trait("Category", "Unit")]
public class ToolSelectionTests
{
    [Fact]
    public void Empty_ShouldCreateEmptySelection()
    {
        var selection = ToolSelection.Empty;
        selection.SelectedTools.Should().BeEmpty();
    }

    [Fact]
    public void FromMatches_Null_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ToolSelection.FromMatches(null!));
    }
}

[Trait("Category", "Unit")]
public class ToolSelectionStateTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var state = new ToolSelectionState(new[] { "tool1" }, DateTime.UtcNow);
        state.SelectedTools.Should().ContainSingle();
    }
}

[Trait("Category", "Unit")]
public class WorldStateTests
{
    #region Empty

    [Fact]
    public void Empty_ShouldCreateEmptyState()
    {
        var state = WorldState.Empty();
        state.Observations.Should().BeEmpty();
        state.HasObservation("key").Should().BeFalse();
    }

    #endregion

    #region FromObservations

    [Fact]
    public void FromObservations_Null_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => WorldState.FromObservations(null!));
    }

    [Fact]
    public void FromObservations_Valid_ShouldCreateState()
    {
        var state = WorldState.FromObservations(new[] { new KeyValuePair<string, object>("key", "value") });
        state.Observations.Should().ContainKey("key");
    }

    #endregion

    #region Observe

    [Fact]
    public void Observe_NullKey_ShouldThrowArgumentNullException()
    {
        var state = WorldState.Empty();
        Assert.Throws<ArgumentNullException>(() => state.Observe(null!, "value"));
    }

    [Fact]
    public void Observe_NullValue_ShouldThrowArgumentNullException()
    {
        var state = WorldState.Empty();
        Assert.Throws<ArgumentNullException>(() => state.Observe("key", null!));
    }

    [Fact]
    public void Observe_Valid_ShouldAddObservation()
    {
        var state = WorldState.Empty().Observe("key", "value");
        state.HasObservation("key").Should().BeTrue();
        state.GetObservation("key").HasValue.Should().BeTrue();
    }

    #endregion

    #region Update

    [Fact]
    public void Update_NullKey_ShouldThrowArgumentNullException()
    {
        var state = WorldState.Empty();
        Assert.Throws<ArgumentNullException>(() => state.Update(null!, "value"));
    }

    [Fact]
    public void Update_NullValue_ShouldThrowArgumentNullException()
    {
        var state = WorldState.Empty();
        Assert.Throws<ArgumentNullException>(() => state.Update("key", null!));
    }

    [Fact]
    public void Update_Existing_ShouldReplace()
    {
        var state = WorldState.Empty().Observe("key", "old").Update("key", "new");
        state.GetObservation("key").Value.Should().Be("new");
    }

    [Fact]
    public void Update_Missing_ShouldAdd()
    {
        var state = WorldState.Empty().Update("key", "value");
        state.HasObservation("key").Should().BeTrue();
    }

    #endregion

    #region RemoveObservation

    [Fact]
    public void RemoveObservation_Null_ShouldThrowArgumentNullException()
    {
        var state = WorldState.Empty();
        Assert.Throws<ArgumentNullException>(() => state.RemoveObservation(null!));
    }

    [Fact]
    public void RemoveObservation_Existing_ShouldRemove()
    {
        var state = WorldState.Empty().Observe("key", "value").RemoveObservation("key");
        state.HasObservation("key").Should().BeFalse();
    }

    #endregion

    #region WithCausalGraph

    [Fact]
    public void WithCausalGraph_Null_ShouldThrowArgumentNullException()
    {
        var state = WorldState.Empty();
        Assert.Throws<ArgumentNullException>(() => state.WithCausalGraph(null!));
    }

    [Fact]
    public void WithCausalGraph_Valid_ShouldSetGraph()
    {
        var graph = CausalGraph.Empty();
        var state = WorldState.Empty().WithCausalGraph(graph);
        state.CausalGraph.Should().NotBeNull();
    }

    #endregion

    #region Combine

    [Fact]
    public void Combine_Null_ShouldThrowArgumentNullException()
    {
        var state = WorldState.Empty();
        Assert.Throws<ArgumentNullException>(() => state.Combine(null!));
    }

    [Fact]
    public void Combine_ShouldMergeObservations()
    {
        var state1 = WorldState.Empty().Observe("key1", "value1");
        var state2 = WorldState.Empty().Observe("key2", "value2");
        var combined = state1.Combine(state2);
        combined.HasObservation("key1").Should().BeTrue();
        combined.HasObservation("key2").Should().BeTrue();
    }

    [Fact]
    public void Combine_DuplicateKeys_ShouldPreferOther()
    {
        var state1 = WorldState.Empty().Observe("key", "value1");
        var state2 = WorldState.Empty().Observe("key", "value2");
        var combined = state1.Combine(state2);
        combined.GetObservation("key").Value.Should().Be("value2");
    }

    #endregion
}
