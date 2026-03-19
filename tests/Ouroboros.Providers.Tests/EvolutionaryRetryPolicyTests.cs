using FluentAssertions;
using Ouroboros.Providers.Resilience;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class EvolutionaryRetryPolicyTests
{
    // ── ExecuteWithEvolutionAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithEvolution_SucceedsOnFirstAttempt_ReturnsResult()
    {
        var policy = new EvolutionaryRetryPolicy<ToolCallContext>(
            [], maxGenerations: 3);

        var context = CreateContext();
        var result = await policy.ExecuteWithEvolutionAsync(
            context,
            (ctx, ct) => Task.FromResult("success"),
            CancellationToken.None);

        result.Should().Be("success");
    }

    [Fact]
    public async Task ExecuteWithEvolution_FailsThenSucceeds_AppliesMutation()
    {
        var strategy = new CountingMutationStrategy();
        var policy = new EvolutionaryRetryPolicy<ToolCallContext>(
            [strategy], maxGenerations: 3);

        var context = CreateContext();
        int attempts = 0;

        var result = await policy.ExecuteWithEvolutionAsync(
            context,
            (ctx, ct) =>
            {
                attempts++;
                if (attempts < 2)
                    throw new InvalidOperationException("first attempt fails");
                return Task.FromResult("success");
            },
            CancellationToken.None);

        result.Should().Be("success");
        strategy.MutateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithEvolution_ExhaustsAllGenerations_ThrowsExhaustedException()
    {
        var strategy = new AlwaysCanMutateStrategy();
        var policy = new EvolutionaryRetryPolicy<ToolCallContext>(
            [strategy], maxGenerations: 2);

        var context = CreateContext();

        Func<Task> act = () => policy.ExecuteWithEvolutionAsync(
            context,
            (ctx, ct) => throw new InvalidOperationException("always fails"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<EvolutionaryRetryExhaustedException>();
        ex.Which.GenerationsAttempted.Should().Be(2);
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteWithEvolution_NoStrategyAvailable_StopsEarly()
    {
        // Strategy that never matches
        var strategy = new NeverCanMutateStrategy();
        var policy = new EvolutionaryRetryPolicy<ToolCallContext>(
            [strategy], maxGenerations: 5);

        var context = CreateContext();

        Func<Task> act = () => policy.ExecuteWithEvolutionAsync(
            context,
            (ctx, ct) => throw new InvalidOperationException("fails"),
            CancellationToken.None);

        await act.Should().ThrowAsync<EvolutionaryRetryExhaustedException>();
        // Should stop after first failure since no strategy matches
    }

    [Fact]
    public async Task ExecuteWithEvolution_RespectsCancellation()
    {
        var policy = new EvolutionaryRetryPolicy<ToolCallContext>([], maxGenerations: 3);
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var context = CreateContext();

        Func<Task> act = () => policy.ExecuteWithEvolutionAsync(
            context,
            (ctx, ct) => Task.FromResult("result"),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteWithEvolution_FiresMutationAppliedEvent()
    {
        var strategy = new AlwaysCanMutateStrategy();
        var policy = new EvolutionaryRetryPolicy<ToolCallContext>(
            [strategy], maxGenerations: 3);

        var events = new List<MutationAppliedEventArgs>();
        policy.OnMutationApplied += (_, e) => events.Add(e);

        var context = CreateContext();
        int attempts = 0;

        await policy.ExecuteWithEvolutionAsync(
            context,
            (ctx, ct) =>
            {
                attempts++;
                if (attempts < 3)
                    throw new InvalidOperationException($"fail {attempts}");
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        events.Should().HaveCount(2);
        events[0].Generation.Should().Be(1);
        events[1].Generation.Should().Be(2);
        events[0].StrategyName.Should().Be("always");
    }

    // ── Strategy selection ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteWithEvolution_SelectsHighestPriorityStrategy()
    {
        var lowPriority = new CountingMutationStrategy { PriorityValue = 100 };
        var highPriority = new CountingMutationStrategy { PriorityValue = 1 };
        var policy = new EvolutionaryRetryPolicy<ToolCallContext>(
            [lowPriority, highPriority], maxGenerations: 3);

        var context = CreateContext();
        int attempts = 0;

        await policy.ExecuteWithEvolutionAsync(
            context,
            (ctx, ct) =>
            {
                attempts++;
                if (attempts < 2)
                    throw new InvalidOperationException("fail");
                return Task.FromResult("ok");
            },
            CancellationToken.None);

        highPriority.MutateCallCount.Should().Be(1);
        lowPriority.MutateCallCount.Should().Be(0);
    }

    // ── Builder ──────────────────────────────────────────────────────────────

    [Fact]
    public void Builder_ForToolCallsWithDefaults_CreatesPolicy()
    {
        var policy = EvolutionaryRetryPolicyBuilder.ForToolCallsWithDefaults().Build();

        policy.Should().NotBeNull();
    }

    [Fact]
    public void Builder_CustomConfiguration_CreatesPolicy()
    {
        var policy = EvolutionaryRetryPolicyBuilder.ForToolCalls()
            .WithStrategy(new FormatHintMutation())
            .WithStrategy(new TemperatureMutation())
            .WithMaxGenerations(10)
            .Build();

        policy.Should().NotBeNull();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ToolCallContext CreateContext() => new()
    {
        Prompt = "What is the weather?",
        Tools =
        [
            new ToolDefinitionSlim("weather", "Get weather info", null),
            new ToolDefinitionSlim("search", "Search the web", null)
        ]
    };

    private sealed class CountingMutationStrategy : IMutationStrategy<ToolCallContext>
    {
        public string Name => "counting";
        public int PriorityValue { get; set; } = 10;
        public int Priority => PriorityValue;
        public int MutateCallCount { get; private set; }

        public bool CanMutate(ToolCallContext context, Exception lastError) => true;

        public ToolCallContext Mutate(ToolCallContext context, int generation)
        {
            MutateCallCount++;
            context.Generation = generation;
            return context;
        }
    }

    private sealed class AlwaysCanMutateStrategy : IMutationStrategy<ToolCallContext>
    {
        public string Name => "always";
        public int Priority => 10;
        public bool CanMutate(ToolCallContext context, Exception lastError) => true;
        public ToolCallContext Mutate(ToolCallContext context, int generation)
        {
            context.Generation = generation;
            return context;
        }
    }

    private sealed class NeverCanMutateStrategy : IMutationStrategy<ToolCallContext>
    {
        public string Name => "never";
        public int Priority => 10;
        public bool CanMutate(ToolCallContext context, Exception lastError) => false;
        public ToolCallContext Mutate(ToolCallContext context, int generation) => context;
    }
}
