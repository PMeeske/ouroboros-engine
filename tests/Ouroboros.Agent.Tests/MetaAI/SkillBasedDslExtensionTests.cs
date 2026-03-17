using FluentAssertions;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class SkillBasedDslExtensionTests
{
    private readonly Mock<ISkillRegistry> _mockSkillRegistry = new();
    private readonly Mock<Ouroboros.Abstractions.Core.IChatCompletionModel> _mockModel = new();

    private SkillBasedDslExtension CreateSut()
    {
        return new SkillBasedDslExtension(_mockSkillRegistry.Object, _mockModel.Object);
    }

    // === Constructor Tests ===

    [Fact]
    public void Constructor_NullSkillRegistry_ThrowsArgumentNullException()
    {
        var act = () => new SkillBasedDslExtension(null!, _mockModel.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("skillRegistry");
    }

    [Fact]
    public void Constructor_NullModel_ThrowsArgumentNullException()
    {
        var act = () => new SkillBasedDslExtension(_mockSkillRegistry.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    [Fact]
    public void Constructor_ValidArgs_DoesNotThrow()
    {
        var act = () => CreateSut();
        act.Should().NotThrow();
    }

    // === SkillTokens Tests ===

    [Fact]
    public void SkillTokens_Initially_IsEmpty()
    {
        var sut = CreateSut();
        sut.SkillTokens.Should().BeEmpty();
    }

    // === RefreshSkillTokens Tests ===

    [Fact]
    public void RefreshSkillTokens_NoSkills_KeepsEmpty()
    {
        _mockSkillRegistry.Setup(r => r.GetAllSkills())
            .Returns(new List<AgentSkill>());

        var sut = CreateSut();
        sut.RefreshSkillTokens();

        sut.SkillTokens.Should().BeEmpty();
    }

    // === TryResolveSkillToken Tests ===

    [Fact]
    public void TryResolveSkillToken_NonSkillToken_ReturnsFalse()
    {
        var sut = CreateSut();

        bool resolved = sut.TryResolveSkillToken("NotASkillToken", null, out var executor);

        resolved.Should().BeFalse();
        executor.Should().BeNull();
    }

    [Fact]
    public void TryResolveSkillToken_UnknownSkill_ReturnsFalse()
    {
        _mockSkillRegistry.Setup(r => r.GetSkill(It.IsAny<string>())).Returns((AgentSkill?)null);

        var sut = CreateSut();

        bool resolved = sut.TryResolveSkillToken("UseSkill_Unknown", null, out var executor);

        resolved.Should().BeFalse();
        executor.Should().BeNull();
    }

    // === GenerateSkillTokenHelp Tests ===

    [Fact]
    public void GenerateSkillTokenHelp_NoSkills_ReturnsDefaultMessage()
    {
        var sut = CreateSut();

        var help = sut.GenerateSkillTokenHelp();

        help.Should().Contain("No learned skills available");
    }

    // === GenerateSkillPipeline Tests ===

    [Fact]
    public void GenerateSkillPipeline_NoMatchingSkills_ReturnsEmptyPipeline()
    {
        var sut = CreateSut();

        var pipeline = sut.GenerateSkillPipeline(new[] { "Nonexistent" });

        pipeline.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSkillPipeline_EmptySkillNames_ReturnsEmpty()
    {
        var sut = CreateSut();

        var pipeline = sut.GenerateSkillPipeline(Enumerable.Empty<string>());

        pipeline.Should().BeEmpty();
    }

    // === SuggestSkillsForGoalAsync Tests ===

    [Fact]
    public async Task SuggestSkillsForGoalAsync_NoMatchingSkills_ReturnsEmpty()
    {
        _mockSkillRegistry.Setup(r => r.FindMatchingSkillsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(new List<Skill>());

        var sut = CreateSut();

        var suggestions = await sut.SuggestSkillsForGoalAsync("some goal");

        suggestions.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestSkillsForGoalAsync_WithMaxSuggestions_RespectsLimit()
    {
        var skills = Enumerable.Range(1, 10).Select(i =>
            new Skill($"skill-{i}", $"Description {i}", new List<string>(),
                new List<PlanStep>(), 0.9, i, DateTime.UtcNow.AddDays(-i), DateTime.UtcNow))
            .ToList();

        _mockSkillRegistry.Setup(r => r.FindMatchingSkillsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .ReturnsAsync(skills);

        var sut = CreateSut();

        var suggestions = await sut.SuggestSkillsForGoalAsync("goal", maxSuggestions: 3);

        suggestions.Should().HaveCount(3);
    }
}
