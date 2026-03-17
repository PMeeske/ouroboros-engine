using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInterchangeableLlm_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        FluentActions.Invoking(() => services.AddInterchangeableLlm())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddInterchangeableLlm_RegistersChatModel()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInterchangeableLlm();

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Ouroboros.Abstractions.Core.IChatCompletionModel));
    }

    [Fact]
    public void AddInterchangeableLlm_RegistersEmbeddingModel()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInterchangeableLlm();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(Ouroboros.Domain.IEmbeddingModel));
    }

    [Fact]
    public void AddInterchangeableLlm_RegistersToolRegistry()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInterchangeableLlm();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(Ouroboros.Tools.ToolRegistry));
    }

    [Fact]
    public void AddInterchangeableLlm_RegistersToolAwareChatModel()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInterchangeableLlm();

        // Assert
        services.Should().Contain(sd => sd.ServiceType == typeof(ToolAwareChatModel));
    }

    [Fact]
    public void AddInterchangeableLlm_IsIdempotent()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddInterchangeableLlm();
        services.AddInterchangeableLlm();

        // Assert - TryAddSingleton prevents duplicates
        var chatModelRegistrations = services.Count(sd =>
            sd.ServiceType == typeof(Ouroboros.Abstractions.Core.IChatCompletionModel));
        chatModelRegistrations.Should().Be(1);
    }

    [Fact]
    public void AddInterchangeableLlm_WithCustomModels_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        FluentActions.Invoking(() => services.AddInterchangeableLlm("llama3", "nomic-embed-text"))
            .Should().NotThrow();
    }

    [Fact]
    public void AddMeaiChatClient_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        FluentActions.Invoking(() => services.AddMeaiChatClient())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMeaiChatClient_RegistersChatClient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMeaiChatClient();

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.AI.IChatClient));
    }

    [Fact]
    public void AddMeaiChatClient_RegistersEmbeddingGenerator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMeaiChatClient();

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>));
    }

    [Fact]
    public void AddVectorStore_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        FluentActions.Invoking(() => services.AddVectorStore())
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddVectorStore_RegistersVectorStoreFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddVectorStore();

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Ouroboros.Domain.Vectors.VectorStoreFactory));
    }

    [Fact]
    public void AddVectorStore_WithTypeAndConnection_RegistersCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddVectorStore("InMemory", collectionName: "test_collection");

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Ouroboros.Domain.Vectors.VectorStoreFactory));
    }

    [Fact]
    public void AddSpeechToText_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var mockService = new Mock<Ouroboros.Providers.SpeechToText.ISpeechToTextService>();
        FluentActions.Invoking(() => services.AddSpeechToText(mockService.Object))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSpeechToText_WithNullService_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        FluentActions.Invoking(() => services.AddSpeechToText(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddSpeechToText_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockService = new Mock<Ouroboros.Providers.SpeechToText.ISpeechToTextService>();

        // Act
        services.AddSpeechToText(mockService.Object);

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Ouroboros.Providers.SpeechToText.ISpeechToTextService));
    }

    [Fact]
    public void AddTextToSpeech_WithNullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var mockService = new Mock<Ouroboros.Providers.TextToSpeech.ITextToSpeechService>();
        FluentActions.Invoking(() => services.AddTextToSpeech(mockService.Object))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTextToSpeech_WithNullService_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        FluentActions.Invoking(() => services.AddTextToSpeech(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddTextToSpeech_RegistersService()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockService = new Mock<Ouroboros.Providers.TextToSpeech.ITextToSpeechService>();

        // Act
        services.AddTextToSpeech(mockService.Object);

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Ouroboros.Providers.TextToSpeech.ITextToSpeechService));
    }

    [Fact]
    public void AddBidirectionalSpeech_RegistersBothServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBidirectionalSpeech("test-key");

        // Assert
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Ouroboros.Providers.SpeechToText.ISpeechToTextService));
        services.Should().Contain(sd =>
            sd.ServiceType == typeof(Ouroboros.Providers.TextToSpeech.ITextToSpeechService));
    }
}
