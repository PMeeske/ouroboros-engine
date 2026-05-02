// Copyright (c) Ouroboros. All rights reserved.

using FluentAssertions;
using Xunit;
using App = Ouroboros.Application.Avatar;
using Engine = Ouroboros.Tensor.Orchestration;

namespace Ouroboros.Tensor.Tests.HermesOnnx;

/// <summary>
/// Verifies the app-layer EvictionPolicy maps correctly to the engine-layer
/// canonical enum. Specifically guards against the regression in
/// .planning/phases/263-hermes-onnx-extra-mode/263-RESEARCH.md Section 5
/// where FullUnload silently fell through to Cooperative.
/// </summary>
public sealed class EvictionPolicyMapperTests
{
    [Theory]
    [InlineData(App.EvictionPolicy.Cooperative, Engine.EvictionPolicy.Cooperative)]
    [InlineData(App.EvictionPolicy.HardHeap, Engine.EvictionPolicy.HardHeap)]
    [InlineData(App.EvictionPolicy.None, Engine.EvictionPolicy.None)]
    [InlineData(App.EvictionPolicy.FullUnload, Engine.EvictionPolicy.FullUnload)]
    public void MapEviction_AllValues(App.EvictionPolicy app, Engine.EvictionPolicy expected)
    {
        // GpuSchedulerRegistrar.MapEviction is internal — exposed via InternalsVisibleTo Ouroboros.Tensor.Tests.
        Engine.EvictionPolicy actual = App.GpuSchedulerRegistrar.MapEviction(app);
        actual.Should().Be(expected);
    }
}
