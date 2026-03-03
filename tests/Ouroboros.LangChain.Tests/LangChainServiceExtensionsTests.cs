using FluentAssertions;
using LangChain.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ouroboros.LangChainBridge;
using Xunit;

namespace Ouroboros.LangChain.Tests;

[Trait("Category", "Unit")]
public class LangChainServiceExtensionsTests
{
    [Fact]
    public void AddLangChainIntegration_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;

        var act = () => services.AddLangChainIntegration();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLangChainIntegration_RegistersIChatModelAsSingleton()
    {
        var services = new ServiceCollection();
        var mockClient = new Mock<IChatClient>();
        services.AddSingleton(mockClient.Object);

        services.AddLangChainIntegration();

        var provider = services.BuildServiceProvider();
        var chatModel = provider.GetService<IChatModel>();
        chatModel.Should().NotBeNull();
        chatModel.Should().BeOfType<ChatClientChatModel>();
    }

    [Fact]
    public void AddLangChainIntegration_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddLangChainIntegration();

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddLangChainIntegration_CalledTwice_DoesNotDuplicate()
    {
        var services = new ServiceCollection();
        var mockClient = new Mock<IChatClient>();
        services.AddSingleton(mockClient.Object);

        services.AddLangChainIntegration();
        services.AddLangChainIntegration();

        // TryAddSingleton should prevent duplicate registrations
        var provider = services.BuildServiceProvider();
        var chatModel = provider.GetService<IChatModel>();
        chatModel.Should().NotBeNull();
    }

    [Fact]
    public void AddLangChainIntegration_MissingIChatClient_ThrowsOnResolve()
    {
        var services = new ServiceCollection();
        services.AddLangChainIntegration();

        var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<IChatModel>();

        act.Should().Throw<InvalidOperationException>();
    }
}
