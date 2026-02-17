using Ouroboros.Abstractions.Core;

namespace Ouroboros.Tests.Providers.Routing;

/// <summary>
/// Mock chat model that always fails.
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
        throw new InvalidOperationException($"Mock model {_name} failed");
    }
}