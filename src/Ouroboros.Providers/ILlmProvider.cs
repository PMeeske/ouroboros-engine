namespace Ouroboros.Providers;

/// <summary>
/// Interface for LLM providers.
/// </summary>
public interface ILlmProvider
{
    Task<string> GenerateAsync(string prompt);
}