namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Default console-based human feedback provider for testing.
/// </summary>
public sealed class ConsoleFeedbackProvider : IHumanFeedbackProvider
{
    /// <inheritdoc/>
    public async Task<HumanFeedbackResponse> RequestFeedbackAsync(
        HumanFeedbackRequest request,
        CancellationToken ct = default)
    {
        Console.WriteLine($"\n=== Human Feedback Required ===");
        Console.WriteLine($"Context: {request.Context}");
        Console.WriteLine($"Question: {request.Question}");

        if (request.Options != null && request.Options.Any())
        {
            Console.WriteLine("Options:");
            for (int i = 0; i < request.Options.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {request.Options[i]}");
            }
        }

        Console.Write("Your response: ");
        string response = await Task.Run(() => Console.ReadLine() ?? "", ct);

        return new HumanFeedbackResponse(
            request.RequestId,
            response,
            null,
            DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken ct = default)
    {
        Console.WriteLine($"\n=== Approval Required ===");
        Console.WriteLine($"Action: {request.Action}");
        Console.WriteLine($"Parameters: {System.Text.Json.JsonSerializer.Serialize(request.Parameters)}");
        Console.WriteLine($"Rationale: {request.Rationale}");
        Console.Write("Approve? (y/n): ");

        string response = await Task.Run(() => Console.ReadLine() ?? "n", ct);
        bool approved = response.ToLowerInvariant() == "y";

        return new ApprovalResponse(
            request.RequestId,
            approved,
            approved ? null : "User rejected",
            null,
            DateTime.UtcNow);
    }
}