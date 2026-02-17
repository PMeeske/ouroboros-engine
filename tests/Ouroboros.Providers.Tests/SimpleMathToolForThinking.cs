namespace Ouroboros.Tests.Providers;

/// <summary>
/// Simple math tool for testing thinking mode.
/// Note: This is intentionally a minimal mock that only handles specific expressions.
/// </summary>
internal class SimpleMathToolForThinking : ITool
{
    public string Name => "math";
    public string Description => "Performs basic math operations";
    public string? JsonSchema => null;

    public Task<Ouroboros.Abstractions.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // Simple expression evaluation for testing
            var result = input.Trim() switch
            {
                "10+5" => "15",
                "2+2" => "4",
                "1+1" => "2",
                _ => "unknown"
            };
            return Task.FromResult(Ouroboros.Abstractions.Monads.Result<string, string>.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Ouroboros.Abstractions.Monads.Result<string, string>.Failure(ex.Message));
        }
    }
}