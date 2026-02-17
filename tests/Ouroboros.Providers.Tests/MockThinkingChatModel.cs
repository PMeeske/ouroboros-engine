namespace Ouroboros.Tests.Providers;

/// <summary>
/// Mock chat model that supports thinking mode for testing.
/// </summary>
internal class MockThinkingChatModel : IThinkingChatModel
{
    private readonly string _thinking;
    private readonly string _content;

    public MockThinkingChatModel(string thinking, string content)
    {
        _thinking = thinking;
        _content = content;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = new ThinkingResponse(_thinking, _content);
        return Task.FromResult(response.ToFormattedString());
    }

    public Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Task.FromResult(new ThinkingResponse(_thinking, _content));
    }
}