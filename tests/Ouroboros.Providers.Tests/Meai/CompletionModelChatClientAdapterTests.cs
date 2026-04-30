#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using Microsoft.Extensions.AI;
using Ouroboros.Providers.Meai;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class CompletionModelChatClientAdapterTests
{
    private readonly Mock<IChatCompletionModel> _modelMock = new();

    [Fact]
    public void Constructor_NullModel_Throws()
    {
        var act = () => new CompletionModelChatClientAdapter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Metadata_ReturnsAdapterName()
    {
        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        adapter.Metadata.Should().NotBeNull();
    }

    [Fact]
    public async Task GetResponseAsync_CallsModelAndReturnsResponse()
    {
        _modelMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("model response");

        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var messages = new[] { new ChatMessage(ChatRole.User, "hello") };

        var response = await adapter.GetResponseAsync(messages);

        response.Messages.Should().HaveCount(1);
        response.Messages[0].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public async Task GetResponseAsync_ConcatenatesMultipleMessages()
    {
        string? capturedPrompt = null;
        _modelMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((p, _) => capturedPrompt = p)
            .ReturnsAsync("response");

        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, "system instruction"),
            new ChatMessage(ChatRole.User, "user question"),
        };

        await adapter.GetResponseAsync(messages);

        capturedPrompt.Should().Contain("system instruction");
        capturedPrompt.Should().Contain("user question");
    }

    [Fact]
    public async Task GetResponseAsync_EmptyMessages_ReturnsEmptyPrompt()
    {
        string? capturedPrompt = null;
        _modelMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((p, _) => capturedPrompt = p)
            .ReturnsAsync("response");

        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        await adapter.GetResponseAsync(Array.Empty<ChatMessage>());

        capturedPrompt.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_NonStreamingFallback_YieldsSingleUpdate()
    {
        _modelMock.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("streamed text");

        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var messages = new[] { new ChatMessage(ChatRole.User, "hello") };

        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in adapter.GetStreamingResponseAsync(messages))
        {
            updates.Add(update);
        }

        updates.Should().HaveCount(1);
        updates[0].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public void GetService_IChatCompletionModel_ReturnsInnerModel()
    {
        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var service = adapter.GetService(typeof(IChatCompletionModel));

        service.Should().BeSameAs(_modelMock.Object);
    }

    [Fact]
    public void GetService_Self_ReturnsSelf()
    {
        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var service = adapter.GetService(typeof(CompletionModelChatClientAdapter));

        service.Should().BeSameAs(adapter);
    }

    [Fact]
    public void GetService_WithKey_ReturnsNull()
    {
        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var service = adapter.GetService(typeof(IChatCompletionModel), "someKey");

        service.Should().BeNull();
    }

    [Fact]
    public void GetService_UnknownType_ReturnsNull()
    {
        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var service = adapter.GetService(typeof(string));

        service.Should().BeNull();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var adapter = new CompletionModelChatClientAdapter(_modelMock.Object);
        var act = () => adapter.Dispose();
        act.Should().NotThrow();
    }
}
