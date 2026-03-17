// <copyright file="PluginFactoryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;

namespace Ouroboros.SemanticKernel.Tests;

public sealed class PluginFactoryTests
{
    // ── CreateWebSearchPlugin ────────────────────────────────────────────

    [Fact]
    public void CreateWebSearchPlugin_NullApiKey_ReturnsNull()
    {
        var result = PluginFactory.CreateWebSearchPlugin(null);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateWebSearchPlugin_EmptyApiKey_ReturnsNull()
    {
        var result = PluginFactory.CreateWebSearchPlugin(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void CreateWebSearchPlugin_WhitespaceApiKey_ReturnsNull()
    {
        var result = PluginFactory.CreateWebSearchPlugin("   ");

        result.Should().BeNull();
    }

    [Fact]
    public void CreateWebSearchPlugin_ValidApiKey_ReturnsPlugin()
    {
        var result = PluginFactory.CreateWebSearchPlugin("test-bing-api-key");

        result.Should().NotBeNull();
        result!.Name.Should().Be("WebSearch");
    }

    // ── CreateMemoryPlugin ───────────────────────────────────────────────

    [Fact]
    public void CreateMemoryPlugin_NullMemory_ThrowsArgumentNullException()
    {
        var act = () => PluginFactory.CreateMemoryPlugin(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("memory");
    }

    [Fact]
    public void CreateMemoryPlugin_ValidMemory_ReturnsPlugin()
    {
        var mockMemory = new Mock<ISemanticTextMemory>();

        var result = PluginFactory.CreateMemoryPlugin(mockMemory.Object);

        result.Should().NotBeNull();
        result.Name.Should().Be("Memory");
    }
}
