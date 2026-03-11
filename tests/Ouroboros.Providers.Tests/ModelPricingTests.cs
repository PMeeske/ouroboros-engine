using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests.Providers;

[Trait("Category", "Unit")]
public class ModelPricingTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var pricing = new LlmCostTracker.ModelPricing("Anthropic", 3.00m, 15.00m, "Claude Sonnet");

        pricing.Provider.Should().Be("Anthropic");
        pricing.InputCostPer1M.Should().Be(3.00m);
        pricing.OutputCostPer1M.Should().Be(15.00m);
        pricing.Notes.Should().Be("Claude Sonnet");
    }

    [Fact]
    public void Constructor_WithNullNotes_DefaultsToNull()
    {
        var pricing = new LlmCostTracker.ModelPricing("OpenAI", 5.00m, 15.00m);

        pricing.Notes.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new LlmCostTracker.ModelPricing("P", 1m, 2m);
        var b = new LlmCostTracker.ModelPricing("P", 1m, 2m);

        a.Should().Be(b);
    }
}
