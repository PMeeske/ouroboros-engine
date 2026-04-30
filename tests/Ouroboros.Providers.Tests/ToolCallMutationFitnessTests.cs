using Ouroboros.Providers.Resilience;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ToolCallMutationFitnessTests
{
    [Fact]
    public void Evaluate_SuccessOnFirstTry_HighFitness()
    {
        var fitness = new ToolCallMutationFitness();
        var chromosome = ToolCallMutationChromosome.CreateDefault();

        double score = fitness.Evaluate(
            chromosome,
            history: [],
            totalGenerations: 0,
            succeeded: true,
            totalLatency: TimeSpan.FromSeconds(1),
            totalCost: 0m);

        score.Should().BeGreaterThan(0.5, "success on first try should yield high fitness");
    }

    [Fact]
    public void Evaluate_SuccessAfterManyRetries_LowerFitness()
    {
        var fitness = new ToolCallMutationFitness();
        var chromosome = ToolCallMutationChromosome.CreateDefault();
        var history = new List<MutationHistoryEntry>
        {
            new("format-hint", 1, new Exception(), DateTime.UtcNow),
            new("format-switch", 2, new Exception(), DateTime.UtcNow),
            new("temperature", 3, new Exception(), DateTime.UtcNow),
        };

        double score = fitness.Evaluate(
            chromosome,
            history,
            totalGenerations: 4,
            succeeded: true,
            totalLatency: TimeSpan.FromSeconds(20),
            totalCost: 0.01m);

        score.Should().BeLessThan(0.8, "success after many retries should have lower fitness");
        score.Should().BeGreaterThan(0.0, "but still positive for success");
    }

    [Fact]
    public void Evaluate_Failure_ZeroSuccessComponent()
    {
        var fitness = new ToolCallMutationFitness();
        var chromosome = ToolCallMutationChromosome.CreateDefault();

        double score = fitness.Evaluate(
            chromosome,
            history: [],
            totalGenerations: 5,
            succeeded: false,
            totalLatency: TimeSpan.FromSeconds(30),
            totalCost: 0.05m);

        score.Should().BeLessThan(0.5, "failure should have low fitness");
    }

    [Fact]
    public void Evaluate_HighCost_LowerFitness()
    {
        var fitness = new ToolCallMutationFitness();
        var chromosome = ToolCallMutationChromosome.CreateDefault();

        double cheapScore = fitness.Evaluate(
            chromosome, [], 0, true, TimeSpan.FromSeconds(1), 0m);

        double expensiveScore = fitness.Evaluate(
            chromosome, [], 0, true, TimeSpan.FromSeconds(1), 100m);

        cheapScore.Should().BeGreaterThan(expensiveScore,
            "cheaper execution should have higher fitness");
    }

    [Fact]
    public void Evaluate_ResultIsClamped()
    {
        var fitness = new ToolCallMutationFitness();
        var chromosome = ToolCallMutationChromosome.CreateDefault();

        double score = fitness.Evaluate(
            chromosome, [], 0, true, TimeSpan.Zero, 0m);

        score.Should().BeGreaterThanOrEqualTo(0.0);
        score.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public async Task EvaluateAsync_RespectsCancellation()
    {
        var fitness = new ToolCallMutationFitness();
        var chromosome = ToolCallMutationChromosome.CreateDefault();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        Func<Task> act = () => fitness.EvaluateAsync(
            chromosome, [], 0, true, TimeSpan.Zero, 0m, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
