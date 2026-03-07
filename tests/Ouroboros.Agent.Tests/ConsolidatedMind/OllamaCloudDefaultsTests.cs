// <copyright file="OllamaCloudDefaultsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public class OllamaCloudDefaultsTests
{
    [Fact]
    public void LocalEndpoint_IsLocalhost()
    {
        OllamaCloudDefaults.LocalEndpoint.Should().Contain("localhost");
    }

    [Fact]
    public void CloudEndpoint_IsOllamaApi()
    {
        OllamaCloudDefaults.CloudEndpoint.Should().Contain("ollama");
    }

    [Theory]
    [InlineData(SpecializedRole.QuickResponse)]
    [InlineData(SpecializedRole.DeepReasoning)]
    [InlineData(SpecializedRole.CodeExpert)]
    [InlineData(SpecializedRole.Creative)]
    [InlineData(SpecializedRole.Mathematical)]
    [InlineData(SpecializedRole.Analyst)]
    [InlineData(SpecializedRole.Synthesizer)]
    [InlineData(SpecializedRole.Planner)]
    [InlineData(SpecializedRole.Verifier)]
    [InlineData(SpecializedRole.MetaCognitive)]
    public void GetDefaultConfig_ReturnsConfigForRole(SpecializedRole role)
    {
        var config = OllamaCloudDefaults.GetDefaultConfig(role);

        config.Should().NotBeNull();
        config.Role.Should().Be(role);
        config.OllamaModel.Should().NotBeNullOrWhiteSpace();
        config.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAllDefaultConfigs_ReturnsAllRoles()
    {
        var configs = OllamaCloudDefaults.GetAllDefaultConfigs().ToList();
        var roleCount = Enum.GetValues<SpecializedRole>().Length;

        configs.Should().HaveCount(roleCount);
    }

    [Fact]
    public void GetMinimalConfigs_ReturnsSubset()
    {
        var configs = OllamaCloudDefaults.GetMinimalConfigs().ToList();

        configs.Should().NotBeEmpty();
        configs.Count.Should().BeLessThan(Enum.GetValues<SpecializedRole>().Length);
    }

    [Fact]
    public void GetHighQualityConfigs_ReturnsConfigs()
    {
        var configs = OllamaCloudDefaults.GetHighQualityConfigs().ToList();

        configs.Should().NotBeEmpty();
    }

    [Fact]
    public void CloudModels_Constants_AreNotEmpty()
    {
        OllamaCloudDefaults.CloudModels.GptOss_20B.Should().NotBeNullOrWhiteSpace();
        OllamaCloudDefaults.CloudModels.DeepSeekV3_1.Should().NotBeNullOrWhiteSpace();
        OllamaCloudDefaults.CloudModels.DevstralSmall2_24B.Should().NotBeNullOrWhiteSpace();
    }
}
