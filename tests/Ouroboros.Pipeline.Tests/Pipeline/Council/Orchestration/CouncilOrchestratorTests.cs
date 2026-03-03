using FluentAssertions;
using NSubstitute;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests.Pipeline.Council.Orchestration;

[Trait("Category", "Unit")]
public sealed class CouncilOrchestratorTests
{
    private readonly ToolAwareChatModel _llm;

    public CouncilOrchestratorTests()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient.GenerateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("test response"));
        _llm = new ToolAwareChatModel(chatClient);
    }

    [Fact]
    public void Constructor_NullLlm_Throws()
    {
        var act = () => new CouncilOrchestrator(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Agents_InitiallyEmpty()
    {
        var sut = new CouncilOrchestrator(_llm);

        sut.Agents.Should().BeEmpty();
    }

    [Fact]
    public void AddAgent_IncreasesCount()
    {
        var sut = new CouncilOrchestrator(_llm);

        sut.AddAgent(new OptimistAgent());

        sut.Agents.Should().HaveCount(1);
    }

    [Fact]
    public void AddAgent_NullAgent_Throws()
    {
        var sut = new CouncilOrchestrator(_llm);

        var act = () => sut.AddAgent(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddAgent_DuplicateName_Throws()
    {
        var sut = new CouncilOrchestrator(_llm);
        sut.AddAgent(new OptimistAgent());

        var act = () => sut.AddAgent(new OptimistAgent());

        act.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
    }

    [Fact]
    public void RemoveAgent_ExistingAgent_ReturnsTrue()
    {
        var sut = new CouncilOrchestrator(_llm);
        var agent = new OptimistAgent();
        sut.AddAgent(agent);

        var removed = sut.RemoveAgent(agent.Name);

        removed.Should().BeTrue();
        sut.Agents.Should().BeEmpty();
    }

    [Fact]
    public void RemoveAgent_NonExistentAgent_ReturnsFalse()
    {
        var sut = new CouncilOrchestrator(_llm);

        var removed = sut.RemoveAgent("NonExistent");

        removed.Should().BeFalse();
    }

    [Fact]
    public void CreateWithDefaultAgents_HasFiveAgents()
    {
        var sut = CouncilOrchestrator.CreateWithDefaultAgents(_llm);

        sut.Agents.Should().HaveCount(5);
    }

    [Fact]
    public void CreateWithDefaultAgents_ContainsAllPersonas()
    {
        var sut = CouncilOrchestrator.CreateWithDefaultAgents(_llm);
        var names = sut.Agents.Select(a => a.Name).ToList();

        names.Should().Contain("Optimist");
        names.Should().Contain("Security Cynic");
        names.Should().Contain("Pragmatist");
        names.Should().Contain("Theorist");
        names.Should().Contain("User Advocate");
    }

    [Fact]
    public async Task ConveneCouncilAsync_NoAgents_ReturnsFailure()
    {
        var sut = new CouncilOrchestrator(_llm);
        var topic = new CouncilTopic("Test question", "context");

        var result = await sut.ConveneCouncilAsync(topic);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No agents");
    }

    [Fact]
    public void Agents_ReturnsSnapshot()
    {
        var sut = new CouncilOrchestrator(_llm);
        sut.AddAgent(new PragmatistAgent());
        sut.AddAgent(new TheoristAgent());

        var agents = sut.Agents;

        agents.Should().HaveCount(2);
    }

    [Fact]
    public void AddAgent_AfterRemoval_CanReAddSameName()
    {
        var sut = new CouncilOrchestrator(_llm);
        sut.AddAgent(new OptimistAgent());
        sut.RemoveAgent("Optimist");

        // Should not throw
        sut.AddAgent(new OptimistAgent());
        sut.Agents.Should().HaveCount(1);
    }
}
