using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests.SelfImprovement;

[Trait("Category", "Unit")]
public class CapabilityRegistryTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidArgs_ShouldInitialize()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();

        var registry = new CapabilityRegistry(mockLlm.Object, tools);
        registry.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullLLM_ShouldThrow()
    {
        var tools = ToolRegistry.CreateDefault();

        Action act = () => new CapabilityRegistry(null!, tools);
        act.Should().Throw<ArgumentNullException>().WithParameterName("llm");
    }

    [Fact]
    public void Constructor_NullTools_ShouldThrow()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();

        Action act = () => new CapabilityRegistry(mockLlm.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tools");
    }

    [Fact]
    public void Constructor_NullConfig_ShouldUseDefault()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();

        var registry = new CapabilityRegistry(mockLlm.Object, tools, null);
        registry.Should().NotBeNull();
    }

    #endregion

    #region GetCapabilitiesAsync

    [Fact]
    public async Task GetCapabilitiesAsync_ShouldReturnEmptyInitially()
    {
        var mockLlm = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        var tools = ToolRegistry.CreateDefault();
        var registry = new CapabilityRegistry(mockLlm.Object, tools);

        var caps = await registry.GetCapabilitiesAsync();
        caps.Should().BeEmpty();
    }

    #endregion
}
