using FluentAssertions;
using Ouroboros.Providers;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class SimulatedLLMTests
{
    [Fact]
    public async Task GenerateAsync_WithSuggestions_ReturnsJsonSuggestions()
    {
        var llm = new SimulatedLLM();
        var result = await llm.GenerateAsync("Give me suggestions");

        result.Should().Contain("UseDraft");
        result.Should().Contain("confidence");
    }

    [Fact]
    public async Task GenerateAsync_OtherPrompt_ReturnsSimulatedResponse()
    {
        var llm = new SimulatedLLM();
        var result = await llm.GenerateAsync("Hello world");

        result.Should().Be("Simulated response");
    }

    [Fact]
    public async Task GenerateAsync_IsAsync_CompletesWithoutBlocking()
    {
        var llm = new SimulatedLLM();
        var result = await llm.GenerateAsync("test");

        result.Should().NotBeNull();
    }
}
