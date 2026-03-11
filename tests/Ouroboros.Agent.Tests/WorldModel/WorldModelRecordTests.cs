using FluentAssertions;
using Ouroboros.Agent.MetaAI.WorldModel;
using Xunit;
using WmAction = Ouroboros.Agent.MetaAI.WorldModel.Action;

namespace Ouroboros.Tests.WorldModel;

[Trait("Category", "Unit")]
public class StateTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var features = new Dictionary<string, object> { ["position"] = (object)5 };
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        var state = new State(features, embedding);

        state.Features.Should().ContainKey("position");
        state.Embedding.Should().HaveCount(3);
    }

    [Fact]
    public void Create_WithEmptyFeatures_ShouldBeEmpty()
    {
        var state = new State(new Dictionary<string, object>(), Array.Empty<float>());

        state.Features.Should().BeEmpty();
        state.Embedding.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class WmActionTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var parameters = new Dictionary<string, object> { ["target"] = (object)"north" };
        var action = new WmAction("move", parameters);

        action.Name.Should().Be("move");
        action.Parameters.Should().ContainKey("target");
    }

    [Fact]
    public void Equality_SameNameAndParams_ShouldBeEqual()
    {
        var parameters = new Dictionary<string, object>();
        var a = new WmAction("act", parameters);
        var b = new WmAction("act", parameters);

        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class TransitionTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var prevState = new State(new Dictionary<string, object>(), new float[] { 0.1f });
        var nextState = new State(new Dictionary<string, object>(), new float[] { 0.2f });
        var action = new WmAction("move", new Dictionary<string, object>());
        var transition = new Transition(prevState, action, nextState, 1.0, false);

        transition.PreviousState.Should().Be(prevState);
        transition.ActionTaken.Should().Be(action);
        transition.NextState.Should().Be(nextState);
        transition.Reward.Should().Be(1.0);
        transition.Terminal.Should().BeFalse();
    }

    [Fact]
    public void Create_TerminalTransition_ShouldSetTerminal()
    {
        var state = new State(new Dictionary<string, object>(), Array.Empty<float>());
        var action = new WmAction("end", new Dictionary<string, object>());
        var transition = new Transition(state, action, state, 10.0, true);

        transition.Terminal.Should().BeTrue();
        transition.Reward.Should().Be(10.0);
    }
}
