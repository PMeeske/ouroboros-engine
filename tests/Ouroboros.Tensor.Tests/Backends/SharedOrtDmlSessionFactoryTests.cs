// <copyright file="SharedOrtDmlSessionFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Ouroboros.Tensor.Abstractions;
using Ouroboros.Tensor.Backends;
using Ouroboros.Tensor.Configuration;
using Ouroboros.Tensor.Extensions;
using Ouroboros.Tensor.Rasterizers;

namespace Ouroboros.Tensor.Tests.Backends;

/// <summary>
/// Phase 196.3-01 — SharedOrtDmlSessionFactory + DI wiring. The factory is the
/// ONLY construction seam allowed to call
/// <c>SessionOptions.AppendExecutionProvider_DML</c> inside
/// <c>Ouroboros.Tensor</c>; all DML consumers route through
/// <see cref="ISharedOrtDmlSessionFactory"/>.
///
/// Phase 265 (BUILD-02): live-DML tests are gated behind
/// <c>OUROBOROS_RDNA4_HOST=1</c> to prevent default-profile crashes on hosts
/// without an RDNA 4 adapter. Set the env var on the developer/CI box that
/// owns the GPU; default runs skip and pass.
/// </summary>
public class SharedOrtDmlSessionFactoryTests
{
    private static bool IsRdna4Host()
        => string.Equals(
            Environment.GetEnvironmentVariable("OUROBOROS_RDNA4_HOST"),
            "1",
            StringComparison.Ordinal);

    [Fact]
    [Trait("Category", "Unit")]
    public void CreateSessionOptions_UnavailableSharedDevice_ThrowsInvalidOperation()
    {
        // TryCreate with AdapterLuid=0UL → IsAvailable=false (documented sentinel).
        SharedD3D12Device shared = SharedD3D12Device.TryCreate(StubLayout.WithLuid(0UL));
        shared.IsAvailable.Should().BeFalse("sentinel LUID must yield unavailable device");

        var factory = new SharedOrtDmlSessionFactory(
            shared,
            new DxgiAdapterLuidResolver(new EmptyEnumerator()),
            NullLogger<SharedOrtDmlSessionFactory>.Instance);

        Action act = () => factory.CreateSessionOptions();

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("*SharedD3D12Device*unavailable*");
    }

    [Fact]
    [Trait("Category", "RDNA4")]
    [Trait("Category", "GPU")]
    public void CreateSessionOptions_LiveSharedDevice_ReturnsOptionsConfiguredForDml()
    {
        if (!IsRdna4Host())
        {
            Assert.True(true, "OUROBOROS_RDNA4_HOST not set — skipped (Phase 265 default-profile gate)");
            return;
        }
        string? env = Environment.GetEnvironmentVariable("GSD_GPU_AVAILABLE");
        if (string.Equals(env, "false", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(true, "GSD_GPU_AVAILABLE=false — skipped");
            return;
        }
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(true, "shared D3D12 device requires Windows — skipped");
            return;
        }

        DxgiAdapterEnumerator enumerator = new();
        IReadOnlyList<AdapterInfo> adapters = enumerator.EnumerateAdapters();
        AdapterInfo? primary = null;
        foreach (AdapterInfo a in adapters)
        {
            if (!a.IsSoftware && a.AdapterLuid != 0UL)
            {
                primary = a;
                break;
            }
        }
        if (primary is null)
        {
            Assert.True(true, "no non-software DXGI adapter — skipped");
            return;
        }

        SharedD3D12Device shared = SharedD3D12Device.TryCreate(StubLayout.WithLuid(primary.AdapterLuid));
        if (!shared.IsAvailable)
        {
            Assert.True(true, "D3D12CreateDevice unavailable on this host — skipped");
            return;
        }

        try
        {
            var factory = new SharedOrtDmlSessionFactory(
                shared,
                new DxgiAdapterLuidResolver(enumerator),
                NullLogger<SharedOrtDmlSessionFactory>.Instance);

            SessionOptions first = factory.CreateSessionOptions();
            SessionOptions second = factory.CreateSessionOptions();

            try
            {
                first.Should().NotBeNull();
                second.Should().NotBeNull();
                first.Should().NotBeSameAs(second, "each call produces a fresh SessionOptions the caller owns");
                first.GraphOptimizationLevel.Should().Be(GraphOptimizationLevel.ORT_ENABLE_ALL);
                first.ExecutionMode.Should().Be(ExecutionMode.ORT_SEQUENTIAL);
            }
            finally
            {
                first.Dispose();
                second.Dispose();
            }
        }
        finally
        {
            shared.Dispose();
        }
    }

    [Fact]
    [Trait("Category", "RDNA4")]
    [Trait("Category", "GPU")]
    public void AddDirectComputeGaussianRasterizer_RegistersFactoryAndResolverAsSingletons()
    {
        if (!IsRdna4Host())
        {
            Assert.True(true, "OUROBOROS_RDNA4_HOST not set — skipped (Phase 265 default-profile gate)");
            return;
        }
        string? env = Environment.GetEnvironmentVariable("GSD_GPU_AVAILABLE");
        if (string.Equals(env, "false", StringComparison.OrdinalIgnoreCase))
        {
            Assert.True(true, "GSD_GPU_AVAILABLE=false — skipped");
            return;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IVramLayout>(_ => StubLayout.WithLuid(0UL)); // IsAvailable=false ok for wiring test
        services.AddSingleton<IDxgiAdapterEnumerator, DxgiAdapterEnumerator>();
        services.AddDirectComputeGaussianRasterizer();

        using ServiceProvider sp = services.BuildServiceProvider();
        var factoryA = sp.GetRequiredService<ISharedOrtDmlSessionFactory>();
        var factoryB = sp.GetRequiredService<ISharedOrtDmlSessionFactory>();
        var resolverA = sp.GetRequiredService<DxgiAdapterLuidResolver>();
        var resolverB = sp.GetRequiredService<DxgiAdapterLuidResolver>();

        factoryA.Should().BeSameAs(factoryB, "factory must be singleton-scoped");
        resolverA.Should().BeSameAs(resolverB, "resolver must be singleton-scoped");
        factoryA.Should().BeOfType<SharedOrtDmlSessionFactory>();
    }

    private sealed class EmptyEnumerator : IDxgiAdapterEnumerator
    {
        public IReadOnlyList<AdapterInfo> EnumerateAdapters() => Array.Empty<AdapterInfo>();
    }

    private static class StubLayout
    {
        public static IVramLayout WithLuid(ulong luid)
        {
            return VramLayoutPresets.Generic_8GB.WithAdapter("test-stub", luid);
        }
    }
}
