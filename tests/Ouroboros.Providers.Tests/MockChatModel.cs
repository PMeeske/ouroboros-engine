using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests.Providers;

/// <summary>
/// Mock chat model for testing.
/// </summary>
internal class MockChatModel : IChatCompletionModel
{
    private readonly string _response;

    public MockChatModel(string response)
    {
        _response = response;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        return Task.FromResult(_response);
    }
}