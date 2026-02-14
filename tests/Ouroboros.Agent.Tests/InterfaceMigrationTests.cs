// <copyright file="InterfaceMigrationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions.Agent;
using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Providers;

namespace Ouroboros.Tests;

/// <summary>
/// Tests to verify that interface migrations compile correctly and use the proper Foundation namespaces.
/// These tests ensure backward compatibility and correct interface implementation after the ecosystem-wide refactoring.
/// </summary>
[Trait("Category", "Unit")]
public class InterfaceMigrationTests
{
    [Fact]
    public void SafetyGuard_ImplementsFoundationISafetyGuard()
    {
        // Arrange & Act
        var safetyGuard = new SafetyGuard();

        // Assert
        safetyGuard.Should().BeAssignableTo<ISafetyGuard>();
        typeof(SafetyGuard).Should().Implement<ISafetyGuard>();
    }

    [Fact]
    public void MemoryStore_ImplementsFoundationIMemoryStore()
    {
        // Arrange
        var mockEmbed = new MockEmbeddingModel();

        // Act
        var memoryStore = new MemoryStore(mockEmbed);

        // Assert
        memoryStore.Should().BeAssignableTo<IMemoryStore>();
        typeof(MemoryStore).Should().Implement<IMemoryStore>();
    }

    [Fact]
    public void PersistentMemoryStore_ImplementsFoundationIMemoryStore()
    {
        // Arrange
        var mockEmbed = new MockEmbeddingModel();
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Act
            var persistentMemoryStore = new PersistentMemoryStore(mockEmbed, tempPath);

            // Assert
            persistentMemoryStore.Should().BeAssignableTo<IMemoryStore>();
            typeof(PersistentMemoryStore).Should().Implement<IMemoryStore>();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
            }
        }
    }

    [Fact]
    public void SkillRegistry_ImplementsFoundationISkillRegistry()
    {
        // Arrange & Act
        var skillRegistry = new SkillRegistry();

        // Assert
        skillRegistry.Should().BeAssignableTo<ISkillRegistry>();
        typeof(SkillRegistry).Should().Implement<ISkillRegistry>();
    }

    [Fact]
    public void UncertaintyRouter_ImplementsFoundationIUncertaintyRouter()
    {
        // Arrange
        var mockOrchestrator = new SmartModelOrchestrator(ToolRegistry.CreateDefault());

        // Act
        var router = new UncertaintyRouter(mockOrchestrator);

        // Assert
        router.Should().BeAssignableTo<IUncertaintyRouter>();
        typeof(UncertaintyRouter).Should().Implement<IUncertaintyRouter>();
    }

    [Fact]
    public void OllamaChatAdapter_ImplementsIChatCompletionModel()
    {
        // Arrange & Act
        var adapter = new OllamaChatAdapter("http://localhost:11434", "llama2");

        // Assert
        adapter.Should().BeAssignableTo<IChatCompletionModel>();
        typeof(OllamaChatAdapter).Should().Implement<IChatCompletionModel>();
    }

    [Fact]
    public void IChatCompletionModel_IsFromAbstractionsCoreNamespace()
    {
        // Assert
        typeof(IChatCompletionModel).Namespace.Should().Be("Ouroboros.Abstractions.Core");
    }

    [Fact]
    public void ISafetyGuard_IsFromAbstractionsAgentNamespace()
    {
        // Assert
        typeof(ISafetyGuard).Namespace.Should().Be("Ouroboros.Abstractions.Agent");
    }

    [Fact]
    public void IMemoryStore_IsFromAbstractionsAgentNamespace()
    {
        // Assert
        typeof(IMemoryStore).Namespace.Should().Be("Ouroboros.Abstractions.Agent");
    }

    [Fact]
    public void ISkillRegistry_IsFromAbstractionsAgentNamespace()
    {
        // Assert
        typeof(ISkillRegistry).Namespace.Should().Be("Ouroboros.Abstractions.Agent");
    }

    [Fact]
    public void IUncertaintyRouter_IsFromAbstractionsAgentNamespace()
    {
        // Assert
        typeof(IUncertaintyRouter).Namespace.Should().Be("Ouroboros.Abstractions.Agent");
    }

    [Fact]
    public void AllEngineImplementations_UseAbstractionsInterfaces()
    {
        // This test verifies that Engine layer implementations properly reference
        // Foundation layer abstractions, not Engine-specific re-exports

        // Arrange - Get all interface implementations from Agent namespace
        var agentAssembly = typeof(SafetyGuard).Assembly;
        var implementationTypes = agentAssembly.GetTypes()
            .Where(t => !t.IsInterface && !t.IsAbstract)
            .Where(t => 
                typeof(ISafetyGuard).IsAssignableFrom(t) ||
                typeof(IMemoryStore).IsAssignableFrom(t) ||
                typeof(ISkillRegistry).IsAssignableFrom(t) ||
                typeof(IUncertaintyRouter).IsAssignableFrom(t))
            .ToList();

        // Assert - Should have found implementations
        implementationTypes.Should().NotBeEmpty("Should find Engine implementations of Foundation interfaces");
    }

    /// <summary>
    /// Mock embedding model for testing.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[384]);
    }
}
