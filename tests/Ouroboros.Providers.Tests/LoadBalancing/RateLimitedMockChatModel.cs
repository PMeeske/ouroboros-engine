using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Mock chat model that simulates rate limiting (429 error).
/// </summary>
internal sealed class RateLimitedMockChatModel : IChatCompletionModel
{
    private readonly string _name;

    public RateLimitedMockChatModel(string name)
    {
        _name = name;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        // Simulate 429 Too Many Requests error
        throw new HttpRequestException("429 (Too Many Requests)", null, System.Net.HttpStatusCode.TooManyRequests);
    }
}