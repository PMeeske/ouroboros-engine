// <copyright file="SkillComposerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class SkillComposerTests
{
    private readonly Mock<ISkillRegistry> _skillsMock = new();
    private readonly Mock<IMemoryStore> _memoryMock = new();

    [Fact]
    public void Constructor_NullSkills_Throws()
    {
        var act = () => new SkillComposer(null!, _memoryMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullMemory_Throws()
    {
        var act = () => new SkillComposer(_skillsMock.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ValidParams_DoesNotThrow()
    {
        var act = () => new SkillComposer(_skillsMock.Object, _memoryMock.Object);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ComposeSkillsAsync_EmptyName_ReturnsFailure()
    {
        var composer = CreateComposer();
        var result = await composer.ComposeSkillsAsync("", "desc", new List<string> { "s1" });
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task ComposeSkillsAsync_WhitespaceName_ReturnsFailure()
    {
        var composer = CreateComposer();
        var result = await composer.ComposeSkillsAsync("  ", "desc", new List<string> { "s1" });
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ComposeSkillsAsync_EmptyComponentList_ReturnsFailure()
    {
        var composer = CreateComposer();
        var result = await composer.ComposeSkillsAsync("composite", "desc", new List<string>());
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least one");
    }

    private SkillComposer CreateComposer()
    {
        return new SkillComposer(_skillsMock.Object, _memoryMock.Object);
    }
}
