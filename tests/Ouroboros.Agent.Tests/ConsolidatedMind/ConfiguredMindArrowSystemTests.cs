using FluentAssertions;
using Moq;
using Ouroboros.Agent.ConsolidatedMind;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class ConfiguredMindArrowSystemTests
{
    [Fact]
    public void Configuration_ReturnsProvidedConfig()
    {
        var config = new MindConfig { EnableThinking = true };
        var system = CreateSystem(config);

        system.Configuration.Should().BeSameAs(config);
    }

    [Fact]
    public void Specialists_ReturnsProvidedSpecialists()
    {
        var specialists = new[]
        {
            CreateSpecialist(SpecializedRole.CodeExpert),
            CreateSpecialist(SpecializedRole.Creative),
        };
        var system = new ConfiguredMindArrowSystem(specialists, new MindConfig());

        system.Specialists.Should().HaveCount(2);
    }

    [Fact]
    public void CreateReasoningArrow_ReturnsNonNull()
    {
        var system = CreateSystem(new MindConfig());
        var embedMock = new Mock<IEmbeddingModel>();

        var arrow = system.CreateReasoningArrow(embedMock.Object, "topic", "query");

        arrow.Should().NotBeNull();
    }

    [Fact]
    public void CreateComplexTaskArrow_ReturnsNonNull()
    {
        var system = CreateSystem(new MindConfig());
        var embedMock = new Mock<IEmbeddingModel>();

        var arrow = system.CreateComplexTaskArrow(embedMock.Object, "complex task");

        arrow.Should().NotBeNull();
    }

    [Fact]
    public void CreateProcessingFactory_ReturnsNonNull()
    {
        var system = CreateSystem(new MindConfig());
        var factory = system.CreateProcessingFactory();

        factory.Should().NotBeNull();
    }

    [Fact]
    public void CreateProcessingFactory_WithTools_ReturnsNonNull()
    {
        var system = CreateSystem(new MindConfig());
        var tools = new Ouroboros.Abstractions.Core.ToolRegistry();
        var factory = system.CreateProcessingFactory(tools);

        factory.Should().NotBeNull();
    }

    private static ConfiguredMindArrowSystem CreateSystem(MindConfig config)
    {
        var specialists = new[]
        {
            CreateSpecialist(SpecializedRole.QuickResponse),
        };
        return new ConfiguredMindArrowSystem(specialists, config);
    }

    private static SpecializedModel CreateSpecialist(SpecializedRole role)
    {
        var mock = new Mock<IChatCompletionModel>();
        return new SpecializedModel(role, mock.Object, role.ToString(), new[] { "general" });
    }
}
