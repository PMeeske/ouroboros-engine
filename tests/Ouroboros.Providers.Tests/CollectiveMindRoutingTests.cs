#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for CollectiveMind.Routing — AddPathway, ConfigurePathway, tier inference, specialization inference.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CollectiveMindRoutingTests : IDisposable
{
    private readonly CollectiveMind _sut;

    public CollectiveMindRoutingTests()
    {
        _sut = new CollectiveMind();
    }

    [Fact]
    public void AddPathway_WithOllamaLocal_AddsPathway()
    {
        _sut.AddPathway("Local", ChatEndpointType.OllamaLocal, "llama3");
        _sut.Pathways.Should().HaveCount(1);
        _sut.Pathways[0].Name.Should().Be("Local");
    }

    [Fact]
    public void AddPathway_ReturnsSameInstance_ForChaining()
    {
        var result = _sut.AddPathway("Local", ChatEndpointType.OllamaLocal, "llama3");
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void AddPathway_WithOllamaLocal_InfersLocalTier()
    {
        _sut.AddPathway("Local", ChatEndpointType.OllamaLocal, "llama3");
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.Local);
    }

    [Fact]
    public void AddPathway_WithAnthropicOpus_InfersCloudPremiumTier()
    {
        _sut.AddPathway("Claude", ChatEndpointType.Anthropic, "claude-opus-4-5-20251101");
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.CloudPremium);
    }

    [Fact]
    public void AddPathway_WithGpt4o_InfersCloudPremiumTier()
    {
        _sut.AddPathway("OpenAI", ChatEndpointType.OpenAI, "gpt-4o");
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.CloudPremium);
    }

    [Fact]
    public void AddPathway_WithHaiku_InfersCloudLightTier()
    {
        _sut.AddPathway("Haiku", ChatEndpointType.Anthropic, "claude-3-haiku");
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.CloudLight);
    }

    [Fact]
    public void AddPathway_WithCodeModel_InfersSpecializedTier()
    {
        _sut.AddPathway("Codex", ChatEndpointType.OpenAI, "codex-model");
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.Specialized);
    }

    [Fact]
    public void AddPathway_WithCoderModel_InfersCodingSpecialization()
    {
        _sut.AddPathway("Coder", ChatEndpointType.OllamaLocal, "deepseek-coder");
        _sut.Pathways[0].Specializations.Should().Contain(SubGoalType.Coding);
    }

    [Fact]
    public void AddPathway_WithMathModel_InfersMathSpecialization()
    {
        _sut.AddPathway("Math", ChatEndpointType.OllamaLocal, "mathwizard");
        _sut.Pathways[0].Specializations.Should().Contain(SubGoalType.Math);
    }

    [Fact]
    public void AddPathway_WithPlainModel_HasNoSpecializations()
    {
        _sut.AddPathway("Plain", ChatEndpointType.OllamaLocal, "llama3");
        _sut.Pathways[0].Specializations.Should().BeEmpty();
    }

    [Fact]
    public void AddPathway_MultiplePathways_AllAdded()
    {
        _sut.AddPathway("A", ChatEndpointType.OllamaLocal, "llama3");
        _sut.AddPathway("B", ChatEndpointType.OllamaLocal, "llama3");
        _sut.AddPathway("C", ChatEndpointType.OllamaLocal, "llama3");
        _sut.Pathways.Should().HaveCount(3);
    }

    [Fact]
    public void ConfigurePathway_SetsReturnsChainedInstance()
    {
        _sut.AddPathway("Test", ChatEndpointType.OllamaLocal, "llama3");
        var result = _sut.ConfigurePathway("Test", PathwayTier.CloudPremium, SubGoalType.Reasoning);
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void ConfigurePathway_UpdatesTier()
    {
        _sut.AddPathway("Test", ChatEndpointType.OllamaLocal, "llama3");
        _sut.ConfigurePathway("Test", PathwayTier.CloudPremium);
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.CloudPremium);
    }

    [Fact]
    public void ConfigurePathway_AddsSpecializations()
    {
        _sut.AddPathway("Test", ChatEndpointType.OllamaLocal, "llama3");
        _sut.ConfigurePathway("Test", PathwayTier.Specialized, SubGoalType.Coding, SubGoalType.Math);
        _sut.Pathways[0].Specializations.Should().Contain(SubGoalType.Coding);
        _sut.Pathways[0].Specializations.Should().Contain(SubGoalType.Math);
    }

    [Fact]
    public void SetMaster_SetsExistingPathway()
    {
        _sut.AddPathway("Primary", ChatEndpointType.OllamaLocal, "llama3");
        _sut.SetMaster("Primary");
        // Verify through GetConsciousnessStatus
        var status = _sut.GetConsciousnessStatus();
        status.Should().Contain("Primary");
    }

    [Fact]
    public void SetFirstAsMaster_WithPathways_SetsFirst()
    {
        _sut.AddPathway("First", ChatEndpointType.OllamaLocal, "llama3");
        _sut.AddPathway("Second", ChatEndpointType.OllamaLocal, "llama3");
        _sut.SetFirstAsMaster();
        // Should not throw
    }

    [Fact]
    public void HealthyPathwayCount_WithPathways_ReturnsCorrectCount()
    {
        _sut.AddPathway("A", ChatEndpointType.OllamaLocal, "llama3");
        _sut.AddPathway("B", ChatEndpointType.OllamaLocal, "llama3");
        // New pathways start healthy
        _sut.HealthyPathwayCount.Should().Be(2);
    }

    [Fact]
    public void AddPathway_EmitsThoughtStreamEvent()
    {
        string? receivedMessage = null;
        _sut.ThoughtStream.Subscribe(msg => receivedMessage = msg);

        _sut.AddPathway("Test", ChatEndpointType.OllamaLocal, "llama3");

        receivedMessage.Should().NotBeNull();
        receivedMessage.Should().Contain("Test");
    }

    [Fact]
    public void AddPathway_WithFlashModel_InfersCloudLightTier()
    {
        _sut.AddPathway("Flash", ChatEndpointType.Google, "gemini-2.0-flash");
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.CloudLight);
    }

    [Fact]
    public void AddPathway_WithTurboModel_InfersCloudLightTier()
    {
        _sut.AddPathway("Turbo", ChatEndpointType.OpenAI, "gpt-3.5-turbo");
        _sut.Pathways[0].Tier.Should().Be(PathwayTier.CloudLight);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
