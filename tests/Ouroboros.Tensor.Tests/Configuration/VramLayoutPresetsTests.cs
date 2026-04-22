// <copyright file="VramLayoutPresetsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Configuration;

namespace Ouroboros.Tests.Configuration;

/// <summary>
/// Byte-identity regression + structural invariants for the three built-in
/// <see cref="VramLayoutPresets"/> entries. These guard the contract that
/// <c>VramBudgetMonitor</c> consumers keep seeing the exact legacy 2/4/6/4
/// RX 9060 XT split after the Phase 188.1-01 refactor.
/// </summary>
[Trait("Category", "Unit")]
public sealed class VramLayoutPresetsTests
{
    private const long OneGib = 1L * 1024 * 1024 * 1024;
    private const long OneMib = 1L * 1024 * 1024;

    [Fact]
    public void RX9060XT_16GB_TtsLlamaBudget_IsLegacyTwoGiB()
    {
        VramLayoutPresets.RX9060XT_16GB.Buckets[VramBucket.TtsLlama].Budget
            .Should().Be(2L * OneGib);
    }

    [Fact]
    public void RX9060XT_16GB_AvatarMinimum_IsLegacyFourGiB()
    {
        VramLayoutPresets.RX9060XT_16GB.Buckets[VramBucket.Avatar].Minimum
            .Should().Be(4L * OneGib);
    }

    [Fact]
    public void RX9060XT_16GB_TrainingBudget_IsLegacySixGiB()
    {
        VramLayoutPresets.RX9060XT_16GB.Buckets[VramBucket.Training].Budget
            .Should().Be(6L * OneGib);
    }

    [Fact]
    public void RX9060XT_16GB_TotalDeviceBytes_Is16GiB()
    {
        VramLayoutPresets.RX9060XT_16GB.TotalDeviceBytes.Should().Be(16L * OneGib);
    }

    [Fact]
    public void RX9060XT_16GB_RasterizerBudget_Is128MiB_WithMinimum64MiB()
    {
        VramBucketBudget raster = VramLayoutPresets.RX9060XT_16GB.Buckets[VramBucket.Rasterizer];
        raster.Budget.Should().Be(128L * OneMib);
        raster.Minimum.Should().Be(64L * OneMib);
    }

    [Fact]
    public void EveryPreset_ContainsRasterizerBucket_WithPositiveBudget()
    {
        foreach (VramLayout preset in VramLayoutPresets.All)
        {
            preset.Buckets.Should().ContainKey(VramBucket.Rasterizer,
                because: $"preset '{preset.Id}' must ship a Rasterizer bucket");
            preset.Buckets[VramBucket.Rasterizer].Budget.Should().BeGreaterThan(0L,
                because: $"preset '{preset.Id}' Rasterizer.Budget must be > 0");
        }
    }

    [Fact]
    public void EveryPreset_ContainsExternalTenantBuckets_WithPositiveBudget()
    {
        foreach (VramLayout preset in VramLayoutPresets.All)
        {
            preset.Buckets.Should().ContainKey(VramBucket.LlmInference,
                because: $"preset '{preset.Id}' must ship an LlmInference bucket");
            preset.Buckets.Should().ContainKey(VramBucket.Embed,
                because: $"preset '{preset.Id}' must ship an Embed bucket");
            preset.Buckets.Should().ContainKey(VramBucket.Stt,
                because: $"preset '{preset.Id}' must ship an Stt bucket");

            preset.Buckets[VramBucket.LlmInference].Budget.Should().BeGreaterThan(0L,
                because: $"preset '{preset.Id}' LlmInference.Budget must be > 0");
            preset.Buckets[VramBucket.Embed].Budget.Should().BeGreaterThan(0L,
                because: $"preset '{preset.Id}' Embed.Budget must be > 0");
            preset.Buckets[VramBucket.Stt].Budget.Should().BeGreaterThan(0L,
                because: $"preset '{preset.Id}' Stt.Budget must be > 0");
        }
    }

    [Fact]
    public void EveryPreset_SumOfBucketBudgets_DoesNotExceedTotalDeviceBytes()
    {
        foreach (VramLayout preset in VramLayoutPresets.All)
        {
            long sum = 0L;
            foreach (VramBucketBudget bucket in preset.Buckets.Values)
            {
                sum += bucket.Budget;
            }

            sum.Should().Be(preset.TotalDeviceBytes,
                because: $"preset '{preset.Id}' bucket budgets must sum exactly to TotalDeviceBytes");
        }
    }

    [Fact]
    public void PresetIds_AreDistinct()
    {
        var ids = VramLayoutPresets.All.Select(p => p.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().HaveCount(3);
    }

    [Fact]
    public void InCodePresets_HaveZeroAdapterLuid_AsSentinel()
    {
        foreach (VramLayout preset in VramLayoutPresets.All)
        {
            preset.AdapterLuid.Should().Be(0UL,
                because: $"preset '{preset.Id}' is in-code and must leave AdapterLuid=0 as the sentinel — real LUIDs come from DxgiVramLayoutProvider only");
        }
    }

    [Theory]
    [InlineData("RX9060XT_16GB", 512L * OneMib, 128L * OneMib, 128L * OneMib)]
    [InlineData("Generic_8GB", 256L * OneMib, 64L * OneMib, 64L * OneMib)]
    [InlineData("Generic_24GB_Plus", 1L * OneGib, 256L * OneMib, 256L * OneMib)]
    public void ExternalTenantBudgets_AreConservativeAndPreserveExistingBuckets(
        string presetId, long expectedLlm, long expectedEmbed, long expectedStt)
    {
        VramLayout preset = VramLayoutPresets.TryGet(presetId)!;

        preset.Buckets[VramBucket.LlmInference].Budget.Should().Be(expectedLlm);
        preset.Buckets[VramBucket.Embed].Budget.Should().Be(expectedEmbed);
        preset.Buckets[VramBucket.Stt].Budget.Should().Be(expectedStt);

        // Legacy buckets must be untouched
        preset.Buckets[VramBucket.TtsLlama].Budget.Should().Be(
            presetId switch { "RX9060XT_16GB" => 2L * OneGib, "Generic_8GB" => 1L * OneGib, "Generic_24GB_Plus" => 3L * OneGib, _ => 0L });
        preset.Buckets[VramBucket.Avatar].Budget.Should().Be(
            presetId switch { "RX9060XT_16GB" => 4L * OneGib, "Generic_8GB" => 2L * OneGib, "Generic_24GB_Plus" => 6L * OneGib, _ => 0L });
        preset.Buckets[VramBucket.Training].Budget.Should().Be(
            presetId switch { "RX9060XT_16GB" => 6L * OneGib, "Generic_8GB" => 2L * OneGib, "Generic_24GB_Plus" => 10L * OneGib, _ => 0L });
    }

    [Fact]
    public void TryGet_MatchesByIdCaseInsensitively()
    {
        VramLayoutPresets.TryGet("RX9060XT_16GB").Should().BeSameAs(VramLayoutPresets.RX9060XT_16GB);
        VramLayoutPresets.TryGet("rx9060xt_16gb").Should().BeSameAs(VramLayoutPresets.RX9060XT_16GB);
        VramLayoutPresets.TryGet("Generic_8GB").Should().BeSameAs(VramLayoutPresets.Generic_8GB);
        VramLayoutPresets.TryGet("Generic_24GB_Plus").Should().BeSameAs(VramLayoutPresets.Generic_24GB_Plus);
        VramLayoutPresets.TryGet("unknown-preset").Should().BeNull();
        VramLayoutPresets.TryGet(null).Should().BeNull();
        VramLayoutPresets.TryGet("").Should().BeNull();
    }
}
