using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using Ouroboros.Providers.Meai;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class ChatClientCompletionModelAdapterTests
{
    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var act = () => new ChatClientCompletionModelAdapter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Client_ReturnsInnerClient()
    {
        var clientMock = new Mock<IChatClient>();
        var adapter = new ChatClientCompletionModelAdapter(clientMock.Object);

        adapter.Client.Should().BeSameAs(clientMock.Object);
    }

    [Fact]
    public async Task GenerateTextAsync_CallsClientAndReturnsText()
    {
        var clientMock = new Mock<IChatClient>();
        clientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([new ChatMessage(ChatRole.Assistant, "hello world")]));

        var adapter = new ChatClientCompletionModelAdapter(clientMock.Object);

        var result = await adapter.GenerateTextAsync("test prompt");

        result.Should().Be("hello world");
    }

    [Fact]
    public async Task GenerateTextAsync_NullResponseText_ReturnsEmpty()
    {
        var clientMock = new Mock<IChatClient>();
        clientMock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse([]));

        var adapter = new ChatClientCompletionModelAdapter(clientMock.Object);
        var result = await adapter.GenerateTextAsync("test");

        result.Should().BeEmpty();
    }
}
