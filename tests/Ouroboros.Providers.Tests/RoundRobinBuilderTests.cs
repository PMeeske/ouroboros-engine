using FluentAssertions;
using Ouroboros.Providers;
using Ouroboros.Providers.Configuration;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public sealed class RoundRobinBuilderTests
{
    [Fact]
    public void AddProvider_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddProvider("Test", ChatEndpointType.OllamaLocal, "model");

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddOllama_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddOllama("llama3.2");

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithFailover_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.WithFailover(true);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void WithMaxRetries_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.WithMaxRetries(5);

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddAnthropic_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddAnthropic("claude-sonnet-4-20250514");

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddOpenAI_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddOpenAI("gpt-4o");

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddDeepSeek_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddDeepSeek("deepseek-chat");

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddGroq_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddGroq();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddGoogle_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddGoogle();

        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddMistral_FluentlyChains()
    {
        var builder = new RoundRobinBuilder();
        var result = builder.AddMistral();

        result.Should().BeSameAs(builder);
    }
}
