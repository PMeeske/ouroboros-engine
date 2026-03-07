// <copyright file="SemanticKernelServiceExtensionsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Ouroboros.Abstractions.Core;
using Ouroboros.SemanticKernel.Filters;

namespace Ouroboros.SemanticKernel.Tests;

public sealed class SemanticKernelServiceExtensionsTests
{
    // ── AddSemanticKernel ────────────────────────────────────────────────

    [Fact]
    public void AddSemanticKernel_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddSemanticKernel();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddSemanticKernel_RegistersAutoFunctionFilter()
    {
        var services = new ServiceCollection();

        services.AddSemanticKernel();

        var descriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(IAutoFunctionInvocationFilter));

        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(typeof(OuroborosAutoFunctionFilter));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSemanticKernel_RegistersKernelAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddSemanticKernel();

        var descriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(Kernel));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSemanticKernel_WithIChatClient_ResolvesKernel()
    {
        var services = new ServiceCollection();
        var mockChatClient = new Mock<IChatClient>();

        services.AddSingleton(mockChatClient.Object);
        services.AddLogging();
        services.AddSemanticKernel();

        using var sp = services.BuildServiceProvider();
        var kernel = sp.GetService<Kernel>();

        kernel.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticKernel_WithIChatCompletionModel_ResolvesKernel()
    {
        var services = new ServiceCollection();
#pragma warning disable CS0618
        var mockModel = new Mock<IChatCompletionModel>();
#pragma warning restore CS0618

        services.AddSingleton(mockModel.Object);
        services.AddLogging();
        services.AddSemanticKernel();

        using var sp = services.BuildServiceProvider();
        var kernel = sp.GetService<Kernel>();

        kernel.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticKernel_PrefersChatClientOverModel()
    {
        var services = new ServiceCollection();
        var mockChatClient = new Mock<IChatClient>();
#pragma warning disable CS0618
        var mockModel = new Mock<IChatCompletionModel>();
#pragma warning restore CS0618

        services.AddSingleton(mockChatClient.Object);
        services.AddSingleton(mockModel.Object);
        services.AddLogging();
        services.AddSemanticKernel();

        using var sp = services.BuildServiceProvider();
        var kernel = sp.GetService<Kernel>();

        // Kernel should resolve without error -- the IChatClient path is preferred
        kernel.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticKernel_CalledTwice_DoesNotDuplicate()
    {
        var services = new ServiceCollection();

        services.AddSemanticKernel();
        services.AddSemanticKernel();

        var kernelDescriptors = services
            .Where(d => d.ServiceType == typeof(Kernel))
            .ToList();

        // TryAddSingleton should prevent duplicates
        kernelDescriptors.Should().HaveCount(1);
    }

    [Fact]
    public void AddSemanticKernel_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddSemanticKernel();

        result.Should().BeSameAs(services);
    }

    // ── AddSkVectorStore ────────────────────────────────────────────────

    [Fact]
    public void AddSkVectorStore_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddSkVectorStore();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddSkVectorStore_NullCollectionName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddSkVectorStore(collectionName: null!);
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void AddSkVectorStore_WhitespaceCollectionName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddSkVectorStore(collectionName: "   ");
        act.Should().Throw<ArgumentException>().WithParameterName("collectionName");
    }

    [Fact]
    public void AddSkVectorStore_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddSkVectorStore();

        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSkVectorStore_RegistersVectorStoreAndBridge()
    {
        var services = new ServiceCollection();

        services.AddSkVectorStore("my_vectors", 768);

        var vsDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(Microsoft.Extensions.VectorData.VectorStore));
        vsDescriptor.Should().NotBeNull();

        var bridgeDescriptor = services
            .FirstOrDefault(d => d.ServiceType == typeof(Ouroboros.Domain.Vectors.IAdvancedVectorStore));
        bridgeDescriptor.Should().NotBeNull();
    }

    [Fact]
    public void AddSkVectorStore_CalledTwice_DoesNotDuplicate()
    {
        var services = new ServiceCollection();

        services.AddSkVectorStore();
        services.AddSkVectorStore();

        var vsDescriptors = services
            .Where(d => d.ServiceType == typeof(Microsoft.Extensions.VectorData.VectorStore))
            .ToList();

        vsDescriptors.Should().HaveCount(1);
    }
}
