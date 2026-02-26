// <copyright file="SkillRegistryTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using MetaAIPlanStep = Ouroboros.Agent.MetaAI.PlanStep;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class SkillRegistryTests
{
    [Fact]
    public void Constructor_NoParams_DoesNotThrow()
    {
        var act = () => new SkillRegistry();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task RegisterSkillAsync_ValidSkill_Succeeds()
    {
        var registry = new SkillRegistry();
        var skill = CreateSkill("test-skill");

        var result = await registry.RegisterSkillAsync(skill);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterSkillAsync_NullSkill_ReturnsFailure()
    {
        var registry = new SkillRegistry();

        var result = await registry.RegisterSkillAsync(null!);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkillAsync_ExistingSkill_ReturnsSkill()
    {
        var registry = new SkillRegistry();
        var skill = CreateSkill("existing");
        await registry.RegisterSkillAsync(skill);

        var result = await registry.GetSkillAsync("existing");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be("existing");
    }

    [Fact]
    public async Task GetSkillAsync_NonexistentSkill_ReturnsFailure()
    {
        var registry = new SkillRegistry();

        var result = await registry.GetSkillAsync("nonexistent");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetSkillAsync_EmptyId_ReturnsFailure()
    {
        var registry = new SkillRegistry();

        var result = await registry.GetSkillAsync("");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task GetSkillAsync_WhitespaceId_ReturnsFailure()
    {
        var registry = new SkillRegistry();

        var result = await registry.GetSkillAsync("   ");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task FindSkillsAsync_NoFilter_ReturnsAll()
    {
        var registry = new SkillRegistry();
        await registry.RegisterSkillAsync(CreateSkill("s1"));
        await registry.RegisterSkillAsync(CreateSkill("s2"));

        var result = await registry.FindSkillsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task RegisterSkillAsync_DuplicateId_Overwrites()
    {
        var registry = new SkillRegistry();
        var skill1 = CreateSkill("dup");
        var skill2 = CreateSkill("dup");

        await registry.RegisterSkillAsync(skill1);
        await registry.RegisterSkillAsync(skill2);

        var result = await registry.GetSkillAsync("dup");
        result.IsSuccess.Should().BeTrue();
    }

    private static AgentSkill CreateSkill(string id)
    {
        return new AgentSkill(
            id,
            "Test Skill",
            "A test skill",
            "general",
            new List<string>(),
            new List<string>(),
            0.9,
            1,
            100L,
            new List<string>());
    }
}
