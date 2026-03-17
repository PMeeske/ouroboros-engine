// <copyright file="OllamaCloudDefaultsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class OllamaCloudDefaultsTests
{
    // ── Constants ───────────────────────────────────────────────────────

    [Fact]
    public void CloudEndpoint_IsOllamaApi()
    {
        OllamaCloudDefaults.CloudEndpoint.Should().Be("https://api.ollama.ai");
    }

    [Fact]
    public void ApiKeyEnvVar_IsExpected()
    {
        OllamaCloudDefaults.ApiKeyEnvVar.Should().Be("OLLAMA_CLOUD_API_KEY");
    }

    [Fact]
    public void EndpointEnvVar_IsExpected()
    {
        OllamaCloudDefaults.EndpointEnvVar.Should().Be("OLLAMA_ENDPOINT");
    }

    [Fact]
    public void LocalModeEnvVar_IsExpected()
    {
        OllamaCloudDefaults.LocalModeEnvVar.Should().Be("OLLAMA_LOCAL");
    }

    // ── GetDefaultConfig ────────────────────────────────────────────────

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
    public void GetDefaultConfig_ReturnsConfigForAllKnownRoles(SpecializedRole role)
    {
        // Act
        var config = OllamaCloudDefaults.GetDefaultConfig(role);

        // Assert
        config.Role.Should().Be(role);
        config.OllamaModel.Should().NotBeNullOrEmpty();
        config.Capabilities.Should().NotBeNull();
        config.Capabilities.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDefaultConfig_UnknownRole_ReturnsFallback()
    {
        // Act
        var config = OllamaCloudDefaults.GetDefaultConfig((SpecializedRole)999);

        // Assert
        config.Priority.Should().Be(0.5);
        config.Capabilities.Should().Contain("general");
    }

    [Fact]
    public void GetDefaultConfig_CodeExpert_HasLowTemperature()
    {
        // Act
        var config = OllamaCloudDefaults.GetDefaultConfig(SpecializedRole.CodeExpert);

        // Assert
        config.Temperature.Should().BeLessThanOrEqualTo(0.3);
    }

    [Fact]
    public void GetDefaultConfig_Creative_HasHighTemperature()
    {
        // Act
        var config = OllamaCloudDefaults.GetDefaultConfig(SpecializedRole.Creative);

        // Assert
        config.Temperature.Should().BeGreaterThanOrEqualTo(0.8);
    }

    // ── GetAllDefaultConfigs ────────────────────────────────────────────

    [Fact]
    public void GetAllDefaultConfigs_ReturnsConfigForEveryRole()
    {
        // Act
        var configs = OllamaCloudDefaults.GetAllDefaultConfigs().ToList();

        // Assert
        var allRoles = Enum.GetValues<SpecializedRole>();
        configs.Should().HaveCount(allRoles.Length);

        foreach (var role in allRoles)
        {
            configs.Should().Contain(c => c.Role == role);
        }
    }

    // ── GetMinimalConfigs ───────────────────────────────────────────────

    [Fact]
    public void GetMinimalConfigs_ReturnsSubset()
    {
        // Act
        var configs = OllamaCloudDefaults.GetMinimalConfigs().ToList();

        // Assert
        configs.Should().HaveCountGreaterThan(0);
        configs.Should().HaveCountLessThan(Enum.GetValues<SpecializedRole>().Length);
        configs.Should().Contain(c => c.Role == SpecializedRole.QuickResponse);
        configs.Should().Contain(c => c.Role == SpecializedRole.DeepReasoning);
        configs.Should().Contain(c => c.Role == SpecializedRole.CodeExpert);
    }

    // ── GetHighQualityConfigs ───────────────────────────────────────────

    [Fact]
    public void GetHighQualityConfigs_ReturnsFullSet()
    {
        // Act
        var configs = OllamaCloudDefaults.GetHighQualityConfigs().ToList();

        // Assert
        configs.Should().HaveCountGreaterThanOrEqualTo(10);
        configs.Should().Contain(c => c.Role == SpecializedRole.QuickResponse);
        configs.Should().Contain(c => c.Role == SpecializedRole.DeepReasoning);
        configs.Should().Contain(c => c.Role == SpecializedRole.CodeExpert);
        configs.Should().Contain(c => c.Role == SpecializedRole.Creative);
        configs.Should().Contain(c => c.Role == SpecializedRole.Mathematical);
    }

    [Fact]
    public void GetHighQualityConfigs_HasLargerModels()
    {
        // Act
        var highQuality = OllamaCloudDefaults.GetHighQualityConfigs().ToList();
        var minimal = OllamaCloudDefaults.GetMinimalConfigs().ToList();

        // Assert — high quality code expert should use larger model than minimal
        var hqCode = highQuality.First(c => c.Role == SpecializedRole.CodeExpert);
        var minCode = minimal.First(c => c.Role == SpecializedRole.CodeExpert);
        hqCode.OllamaModel.Should().NotBe(minCode.OllamaModel);
    }

    // ── Cloud model constants ───────────────────────────────────────────

    [Fact]
    public void CloudModels_HaveCloudSuffix()
    {
        // Assert — all cloud model constants should contain "cloud"
        OllamaCloudDefaults.CloudModels.GptOss_20B.Should().Contain("cloud");
        OllamaCloudDefaults.CloudModels.DeepSeekV3_1.Should().Contain("cloud");
        OllamaCloudDefaults.CloudModels.DevstralSmall2_24B.Should().Contain("cloud");
    }

    // ── Role-specific defaults ──────────────────────────────────────────

    [Fact]
    public void QuickResponse_CloudDefault_IsSet()
    {
        OllamaCloudDefaults.QuickResponse.CloudDefault.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DeepReasoning_CloudDefault_IsSet()
    {
        OllamaCloudDefaults.DeepReasoning.CloudDefault.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CodeExpert_CloudDefault_IsSet()
    {
        OllamaCloudDefaults.CodeExpert.CloudDefault.Should().NotBeNullOrEmpty();
    }
}
