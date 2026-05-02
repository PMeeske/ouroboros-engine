// Copyright (c) Ouroboros. All rights reserved.

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Avatar;
using Xunit;

namespace Ouroboros.Tensor.Tests.HermesOnnx;

public sealed class HermesOnnxTenantRegistrationTests
{
    [Fact]
    public void Profile_MatchesSpec()
    {
        HermesOnnxTenantRegistration tenant = new();

        tenant.Profile.TenantName.Should().Be("HermesOnnx");
        tenant.Profile.BasePriority.Should().Be(Ouroboros.Application.Avatar.GpuTaskPriority.Normal);
        tenant.Profile.VramBytes.Should().Be(22_000_000_000L);
        tenant.Profile.MaxDispatchTime.Should().Be(TimeSpan.FromSeconds(60));
        tenant.Profile.Preemptible.Should().BeFalse();
        tenant.Profile.Eviction.Should().Be(Ouroboros.Application.Avatar.EvictionPolicy.FullUnload);
    }

    [Fact]
    public void EffectivePriority_AlwaysNormal()
    {
        HermesOnnxTenantRegistration tenant = new();
        tenant.EffectivePriority.Should().Be(Ouroboros.Application.Avatar.GpuTaskPriority.Normal);

        using (tenant.BeginSynthesisScope())
        {
            // LLMs don't elevate during a synthesis scope (no-op).
            tenant.EffectivePriority.Should().Be(Ouroboros.Application.Avatar.GpuTaskPriority.Normal);
        }
    }

    [Fact]
    public void SelfRegisters_When_RegistrarPresent()
    {
        TestRegistrar registrar = new();
        HermesOnnxTenantRegistration tenant = new(registrar);

        registrar.RegisteredTenant.Should().BeSameAs(tenant);
    }

    [Fact]
    public void AddHermesOnnxTenant_RegistersAdditively()
    {
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLogging();

        // Directly register both Kokoro and Hermes tenants additively.
        // (Avoids pulling in the full AddOuroborosTtsStrategies dependency graph,
        // which requires KokoroSharp and other heavy services not needed here.)
        services.AddSingleton<IGpuTenantSource>(sp =>
        {
            ILogger<KokoroTenantRegistration>? logger = sp.GetService<ILogger<KokoroTenantRegistration>>();
            return new KokoroTenantRegistration(registrar: null, logger: logger);
        });
        services.AddHermesOnnxTenant();

        using ServiceProvider provider = services.BuildServiceProvider();
        IEnumerable<IGpuTenantSource> tenants = provider.GetServices<IGpuTenantSource>();

        tenants.Should().HaveCount(2);
        tenants.Should().Contain(t => t is KokoroTenantRegistration);
        tenants.Should().Contain(t => t is HermesOnnxTenantRegistration);
    }

    private sealed class TestRegistrar : IGpuSchedulerRegistrar
    {
        public IGpuTenantSource? RegisteredTenant { get; private set; }

        public void Register(IGpuTenantSource tenant) => RegisteredTenant = tenant;
    }
}
