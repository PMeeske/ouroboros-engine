using FluentAssertions;
using LangChain.Providers;
using Microsoft.Extensions.AI;
using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.LangChainBridge;
using Xunit;
using LcMessage = LangChain.Providers.Message;
using LcMessageRole = LangChain.Providers.MessageRole;
using LcChatRequest = LangChain.Providers.ChatRequest;

namespace Ouroboros.LangChain.Tests;

[Trait("Category", "Unit")]
public class ChatClientChatModelTests
{
    private static Mock<IChatClient> CreateMockChatClient(string responseText = "response")
    {
        var mock = new Mock<IChatClient>();
        var chatResponse = new ChatResponse(new ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, responseText));
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);
        return mock;
    }

    // --- Constructor: IChatClient ---

    [Fact]
    public void Constructor_NullIChatClient_ThrowsArgumentNullException()
    {
        var act = () => new ChatClientChatModel((IChatClient)null!);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("client");
    }

    [Fact]
    public void Constructor_ValidIChatClient_DoesNotThrow()
    {
        var mock = new Mock<IChatClient>();

        var model = new ChatClientChatModel(mock.Object);

        model.Should().NotBeNull();
    }

    // --- Constructor: IOuroborosChatClient ---

    [Fact]
    public void Constructor_NullIOuroborosChatClient_ThrowsArgumentNullException()
    {
        var act = () => new ChatClientChatModel((IOuroborosChatClient)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidIOuroborosChatClient_DoesNotThrow()
    {
        var mock = new Mock<IOuroborosChatClient>();

        var model = new ChatClientChatModel(mock.Object);

        model.Should().NotBeNull();
    }

    // --- Constructor: IChatClient + modelId ---

    [Fact]
    public void Constructor_WithModelId_NullClient_ThrowsArgumentNullException()
    {
        var act = () => new ChatClientChatModel((IChatClient)null!, "custom-model");

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("client");
    }

    [Fact]
    public void Constructor_WithModelId_SetsModelId()
    {
        var mock = new Mock<IChatClient>();

        var model = new ChatClientChatModel(mock.Object, "gpt-4");

        model.Id.Should().Be("gpt-4");
    }

    // --- ContextLength ---

    [Fact]
    public void ContextLength_ReturnsDefault128K()
    {
        var mock = new Mock<IChatClient>();

        var model = new ChatClientChatModel(mock.Object);

        model.ContextLength.Should().Be(128_000);
    }

    // --- GenerateAsync ---

    [Fact]
    public async Task GenerateAsync_NullRequest_ThrowsArgumentNullException()
    {
        var mock = CreateMockChatClient();
        var model = new ChatClientChatModel(mock.Object);

        var act = async () =>
        {
            await foreach (var _ in model.GenerateAsync(null!))
            {
            }
        };

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GenerateAsync_SimpleRequest_ReturnsSingleResponse()
    {
        var mock = CreateMockChatClient("Hello!");
        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("Hi", LcMessageRole.Human) },
        };

        var responses = new List<LangChain.Providers.ChatResponse>();
        await foreach (var response in model.GenerateAsync(request))
        {
            responses.Add(response);
        }

        responses.Should().HaveCount(1);
        responses[0].Messages.Should().HaveCount(1);
        responses[0].Messages[0].Content.Should().Be("Hello!");
        responses[0].Messages[0].Role.Should().Be(LcMessageRole.Ai);
    }

    [Fact]
    public async Task GenerateAsync_WithSettings_PassesSettingsThrough()
    {
        var mock = CreateMockChatClient("ok");
        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("test", LcMessageRole.Human) },
        };
        var settings = new ChatSettings { };

        var responses = new List<LangChain.Providers.ChatResponse>();
        await foreach (var response in model.GenerateAsync(request, settings))
        {
            responses.Add(response);
        }

        responses[0].UsedSettings.Should().BeSameAs(settings);
    }

    [Fact]
    public async Task GenerateAsync_WithNullSettings_UsesDefaultSettings()
    {
        var mock = CreateMockChatClient("ok");
        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("test", LcMessageRole.Human) },
        };

        var responses = new List<LangChain.Providers.ChatResponse>();
        await foreach (var response in model.GenerateAsync(request))
        {
            responses.Add(response);
        }

        responses[0].UsedSettings.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAsync_EmptyMessages_CallsClientWithEmptyList()
    {
        var mock = CreateMockChatClient("empty");
        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage>(),
        };

        var responses = new List<LangChain.Providers.ChatResponse>();
        await foreach (var response in model.GenerateAsync(request))
        {
            responses.Add(response);
        }

        responses.Should().HaveCount(1);
        mock.Verify(c => c.GetResponseAsync(
            It.Is<IEnumerable<ChatMessage>>(msgs => !msgs.Any()),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_NullMessages_CallsClientWithEmptyList()
    {
        var mock = CreateMockChatClient("null msgs");
        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest { Messages = null };

        var responses = new List<LangChain.Providers.ChatResponse>();
        await foreach (var response in model.GenerateAsync(request))
        {
            responses.Add(response);
        }

        responses.Should().HaveCount(1);
        mock.Verify(c => c.GetResponseAsync(
            It.Is<IEnumerable<ChatMessage>>(msgs => !msgs.Any()),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Role mapping ---

    [Theory]
    [InlineData(LcMessageRole.System)]
    [InlineData(LcMessageRole.Human)]
    [InlineData(LcMessageRole.Ai)]
    [InlineData(LcMessageRole.Chat)]
    public async Task GenerateAsync_MapsLangChainRolesToMeaiRoles(LcMessageRole lcRole)
    {
        IEnumerable<ChatMessage>? capturedMessages = null;
        var mock = new Mock<IChatClient>();
        var chatResponse = new ChatResponse(new ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, "ok"));
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(chatResponse);

        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("test", lcRole) },
        };

        await foreach (var _ in model.GenerateAsync(request))
        {
        }

        capturedMessages.Should().NotBeNull();
        var msg = capturedMessages!.First();
        switch (lcRole)
        {
            case LcMessageRole.System:
                msg.Role.Should().Be(Microsoft.Extensions.AI.ChatRole.System);
                break;
            case LcMessageRole.Ai:
                msg.Role.Should().Be(Microsoft.Extensions.AI.ChatRole.Assistant);
                break;
            case LcMessageRole.Human:
            case LcMessageRole.Chat:
            default:
                msg.Role.Should().Be(Microsoft.Extensions.AI.ChatRole.User);
                break;
        }
    }

    [Fact]
    public async Task GenerateAsync_ToolCallRole_MapsToToolRole()
    {
        IEnumerable<ChatMessage>? capturedMessages = null;
        var mock = new Mock<IChatClient>();
        var chatResponse = new ChatResponse(new ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, "ok"));
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(chatResponse);

        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("tool data", LcMessageRole.ToolCall) },
        };

        await foreach (var _ in model.GenerateAsync(request))
        {
        }

        capturedMessages!.First().Role.Should().Be(Microsoft.Extensions.AI.ChatRole.Tool);
    }

    [Fact]
    public async Task GenerateAsync_ToolResultRole_MapsToToolRole()
    {
        IEnumerable<ChatMessage>? capturedMessages = null;
        var mock = new Mock<IChatClient>();
        var chatResponse = new ChatResponse(new ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, "ok"));
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((msgs, _, _) =>
                capturedMessages = msgs.ToList())
            .ReturnsAsync(chatResponse);

        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("tool result", LcMessageRole.ToolResult) },
        };

        await foreach (var _ in model.GenerateAsync(request))
        {
        }

        capturedMessages!.First().Role.Should().Be(Microsoft.Extensions.AI.ChatRole.Tool);
    }

    // --- Cancellation ---

    [Fact]
    public async Task GenerateAsync_CancelledToken_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var mock = new Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("test", LcMessageRole.Human) },
        };

        var act = async () =>
        {
            await foreach (var _ in model.GenerateAsync(request, cancellationToken: cts.Token))
            {
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- Null response text ---

    [Fact]
    public async Task GenerateAsync_NullResponseText_ReturnsEmptyContent()
    {
        var mock = new Mock<IChatClient>();
        var chatResponse = new ChatResponse(new ChatMessage(
            Microsoft.Extensions.AI.ChatRole.Assistant, (string?)null));
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var model = new ChatClientChatModel(mock.Object);
        var request = new LcChatRequest
        {
            Messages = new List<LcMessage> { new("hi", LcMessageRole.Human) },
        };

        var responses = new List<LangChain.Providers.ChatResponse>();
        await foreach (var response in model.GenerateAsync(request))
        {
            responses.Add(response);
        }

        responses[0].Messages[0].Content.Should().BeEmpty();
    }
}
