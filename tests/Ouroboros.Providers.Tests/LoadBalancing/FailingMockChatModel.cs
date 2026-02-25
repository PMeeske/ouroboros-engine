using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Mock chat model that always fails with a generic error.
/// </summary>
internal sealed class FailingMockChatModel : IChatCompletionModel
{
    private readonly string _name;

    public FailingMockChatModel(string name)
    {
        _name = name;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        throw new InvalidOperationException("Provider is unavailable");
    }
}