// <copyright file="SandboxedCompilationContextTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle

using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

public class SandboxedCompilationContextTests
{
    [Fact]
    public void Constructor_ShouldSetContextName()
    {
        // Arrange & Act
        using var context = new SandboxedCompilationContext("TestGrammar");

        // Assert
        context.ContextName.Should().Be("TestGrammar");
    }

    [Fact]
    public void Constructor_ShouldCreateCollectibleContext()
    {
        // Arrange & Act
        using var context = new SandboxedCompilationContext("Collectible");

        // Assert — collectible contexts can be unloaded
        context.IsCollectible.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var context = new SandboxedCompilationContext("Disposable");

        // Act & Assert
        var act = () => context.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var context = new SandboxedCompilationContext("DoubleDispose");

        // Act & Assert
        context.Dispose();
        var act = () => context.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void LoadFromMemoryStream_AfterDispose_ShouldThrow()
    {
        // Arrange
        var context = new SandboxedCompilationContext("Disposed");
        context.Dispose();

        // Act & Assert
        using var ms = new MemoryStream([0x00]);
        var act = () => context.LoadFromMemoryStream(ms);
        act.Should().Throw<ObjectDisposedException>();
    }
}
