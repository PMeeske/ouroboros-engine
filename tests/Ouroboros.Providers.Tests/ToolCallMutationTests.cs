using Ouroboros.Providers.Resilience;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class ToolCallMutationTests
{
    // ── FormatHintMutation ───────────────────────────────────────────────────

    [Fact]
    public void FormatHint_CanMutate_WhenNoFormatHintPresent()
    {
        var sut = new FormatHintMutation();
        var ctx = CreateContext("What's the weather?");

        sut.CanMutate(ctx, new InvalidOperationException("no tools")).Should().BeTrue();
    }

    [Fact]
    public void FormatHint_CannotMutate_WhenFormatHintAlreadyPresent()
    {
        var sut = new FormatHintMutation();
        var ctx = CreateContext("Use <tool_call> format please");

        sut.CanMutate(ctx, new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void FormatHint_Mutate_AppendsXmlFormatInstruction()
    {
        var sut = new FormatHintMutation();
        var ctx = CreateContext("What's the weather?");
        ctx.PreferredFormat = ToolCallFormat.XmlTag;

        var result = sut.Mutate(ctx, 1);

        result.Prompt.Should().Contain("<tool_call>");
        result.Prompt.Should().Contain("</tool_call>");
        result.Generation.Should().Be(1);
        result.History.Should().ContainSingle(h => h.StrategyName == "format-hint");
    }

    [Fact]
    public void FormatHint_Mutate_AppendsJsonFormatInstruction()
    {
        var sut = new FormatHintMutation();
        var ctx = CreateContext("What's the weather?");
        ctx.PreferredFormat = ToolCallFormat.JsonToolCalls;

        var result = sut.Mutate(ctx, 1);

        result.Prompt.Should().Contain("tool_calls");
    }

    [Fact]
    public void FormatHint_Mutate_AppendsBracketFormatInstruction()
    {
        var sut = new FormatHintMutation();
        var ctx = CreateContext("What's the weather?");
        ctx.PreferredFormat = ToolCallFormat.BracketLegacy;

        var result = sut.Mutate(ctx, 1);

        result.Prompt.Should().Contain("[TOOL:");
    }

    // ── FormatSwitchMutation ─────────────────────────────────────────────────

    [Fact]
    public void FormatSwitch_CanMutate_WhenNotAllFormatsTried()
    {
        var sut = new FormatSwitchMutation();
        var ctx = CreateContext("prompt");

        sut.CanMutate(ctx, new InvalidOperationException()).Should().BeTrue();
    }

    [Fact]
    public void FormatSwitch_CannotMutate_WhenAllFormatsTried()
    {
        var sut = new FormatSwitchMutation();
        var ctx = CreateContext("prompt");
        // Simulate 2 previous format switches (3 formats total - 1 = 2 switches)
        ctx.History.Add(new MutationHistoryEntry("format-switch", 1, new Exception(), DateTime.UtcNow));
        ctx.History.Add(new MutationHistoryEntry("format-switch", 2, new Exception(), DateTime.UtcNow));

        sut.CanMutate(ctx, new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void FormatSwitch_Mutate_ChangesFormat()
    {
        var sut = new FormatSwitchMutation();
        var ctx = CreateContext("prompt");
        ctx.PreferredFormat = ToolCallFormat.XmlTag;

        var result = sut.Mutate(ctx, 1);

        result.PreferredFormat.Should().NotBe(ToolCallFormat.XmlTag);
    }

    // ── ToolSimplificationMutation ───────────────────────────────────────────

    [Fact]
    public void ToolSimplification_CanMutate_WhenMoreThanThreeTools()
    {
        var sut = new ToolSimplificationMutation();
        var ctx = CreateContext("prompt");
        ctx.Tools =
        [
            new("t1", "d1", null), new("t2", "d2", null),
            new("t3", "d3", null), new("t4", "d4", null)
        ];

        sut.CanMutate(ctx, new InvalidOperationException()).Should().BeTrue();
    }

    [Fact]
    public void ToolSimplification_CannotMutate_WhenThreeOrFewerTools()
    {
        var sut = new ToolSimplificationMutation();
        var ctx = CreateContext("prompt");
        ctx.Tools = [new("t1", "d1", null), new("t2", "d2", null)];

        sut.CanMutate(ctx, new InvalidOperationException()).Should().BeFalse();
    }

    [Fact]
    public void ToolSimplification_Mutate_ReducesToolCount()
    {
        var sut = new ToolSimplificationMutation();
        var ctx = CreateContext("prompt");
        ctx.Tools =
        [
            new("t1", "d1", null), new("t2", "d2", null),
            new("t3", "d3", null), new("t4", "d4", null),
            new("t5", "d5", null), new("t6", "d6", null)
        ];

        var result = sut.Mutate(ctx, 1);

        result.Tools.Count.Should().BeLessThan(6);
        result.Tools.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    // ── TemperatureMutation ──────────────────────────────────────────────────

    [Fact]
    public void Temperature_CanMutate_Always()
    {
        var sut = new TemperatureMutation();
        var ctx = CreateContext("prompt");

        sut.CanMutate(ctx, new InvalidOperationException()).Should().BeTrue();
    }

    [Fact]
    public void Temperature_Mutate_EvenGeneration_LowersTemperature()
    {
        var sut = new TemperatureMutation();
        var ctx = CreateContext("prompt");
        ctx.Temperature = 0.7f;

        var result = sut.Mutate(ctx, 2); // even generation

        result.Temperature.Should().BeLessThan(0.7f);
    }

    [Fact]
    public void Temperature_Mutate_OddGeneration_RaisesTemperature()
    {
        var sut = new TemperatureMutation();
        var ctx = CreateContext("prompt");
        ctx.Temperature = 0.7f;

        var result = sut.Mutate(ctx, 1); // odd generation

        result.Temperature.Should().BeGreaterThan(0.7f);
    }

    [Fact]
    public void Temperature_Mutate_RespectsLowerBound()
    {
        var sut = new TemperatureMutation();
        var ctx = CreateContext("prompt");
        ctx.Temperature = 0.05f; // very low

        var result = sut.Mutate(ctx, 2); // even → lower

        result.Temperature.Should().BeGreaterThanOrEqualTo(0.1f);
    }

    [Fact]
    public void Temperature_Mutate_RespectsUpperBound()
    {
        var sut = new TemperatureMutation();
        var ctx = CreateContext("prompt");
        ctx.Temperature = 1.4f; // very high

        var result = sut.Mutate(ctx, 1); // odd → higher

        result.Temperature.Should().BeLessThanOrEqualTo(1.5f);
    }

    // ── Priority ordering ────────────────────────────────────────────────────

    [Fact]
    public void Strategies_HaveCorrectPriorityOrder()
    {
        var strategies = new IMutationStrategy<ToolCallContext>[]
        {
            new TemperatureMutation(),
            new FormatHintMutation(),
            new ToolSimplificationMutation(),
            new FormatSwitchMutation()
        };

        var ordered = strategies.OrderBy(s => s.Priority).ToList();

        ordered[0].Should().BeOfType<FormatHintMutation>();     // 10
        ordered[1].Should().BeOfType<FormatSwitchMutation>();    // 20
        ordered[2].Should().BeOfType<ToolSimplificationMutation>(); // 30
        ordered[3].Should().BeOfType<TemperatureMutation>();     // 40
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ToolCallContext CreateContext(string prompt) => new()
    {
        Prompt = prompt,
        Tools = [new ToolDefinitionSlim("weather", "Get weather", null)]
    };
}
