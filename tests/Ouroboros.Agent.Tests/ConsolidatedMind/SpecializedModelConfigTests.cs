using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class SpecializedModelConfigTests
{
    [Fact]
    public void Constructor_RequiredProperties_Set()
    {
        var config = new SpecializedModelConfig(SpecializedRole.CodeExpert, "llama3.1:70b");

        config.Role.Should().Be(SpecializedRole.CodeExpert);
        config.OllamaModel.Should().Be("llama3.1:70b");
    }

    [Fact]
    public void Constructor_DefaultValues_Correct()
    {
        var config = new SpecializedModelConfig(SpecializedRole.DeepReasoning, "model");

        config.Endpoint.Should().BeNull();
        config.Capabilities.Should().BeNull();
        config.Priority.Should().Be(1.0);
        config.MaxTokens.Should().Be(4096);
        config.Temperature.Should().Be(0.7);
    }

    [Fact]
    public void Constructor_CustomValues_Override()
    {
        var caps = new[] { "reasoning", "logic" };
        var config = new SpecializedModelConfig(
            SpecializedRole.DeepReasoning,
            "qwen2.5:72b",
            Endpoint: "http://custom:11434",
            Capabilities: caps,
            Priority: 2.0,
            MaxTokens: 8192,
            Temperature: 0.3);

        config.Endpoint.Should().Be("http://custom:11434");
        config.Capabilities.Should().HaveCount(2);
        config.Priority.Should().Be(2.0);
        config.MaxTokens.Should().Be(8192);
        config.Temperature.Should().Be(0.3);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new SpecializedModelConfig(SpecializedRole.Creative, "model1");
        var b = new SpecializedModelConfig(SpecializedRole.Creative, "model1");
        a.Should().Be(b);
    }

    [Fact]
    public void RecordWith_CreatesModifiedCopy()
    {
        var original = new SpecializedModelConfig(SpecializedRole.Creative, "model1");
        var modified = original with { Temperature = 0.9 };

        modified.Temperature.Should().Be(0.9);
        modified.OllamaModel.Should().Be("model1");
        original.Temperature.Should().Be(0.7);
    }
}
