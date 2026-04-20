using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;
using Skill = Ouroboros.Agent.MetaAI.Skill;
using PlanStep = Ouroboros.Agent.PlanStep;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class TestCaseRecordTests
{
    [Fact]
    public void Create_WithAllProperties_ShouldSetThem()
    {
        var context = new Dictionary<string, object> { ["domain"] = "math" };

        var testCase = new TestCase("Addition", "Compute 2+2", context, null);

        testCase.Name.Should().Be("Addition");
        testCase.Goal.Should().Be("Compute 2+2");
        testCase.Context.Should().ContainKey("domain");
        testCase.CustomValidator.Should().BeNull();
    }

    [Fact]
    public void Create_WithNullContext_ShouldAllowNull()
    {
        var testCase = new TestCase("Simple", "Do something", null, null);

        testCase.Context.Should().BeNull();
        testCase.CustomValidator.Should().BeNull();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var a = new TestCase("t", "g", null, null);
        var b = new TestCase("t", "g", null, null);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class SkillSuggestionTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var skill = new Skill(
            "summarize",
            "Summarize text",
            new List<string>(),
            new List<PlanStep>(),
            0.9,
            5,
            DateTime.UtcNow,
            DateTime.UtcNow);

        var suggestion = new SkillSuggestion("summarize_text", skill, 0.85, "!summarize [input]");

        suggestion.TokenName.Should().Be("summarize_text");
        suggestion.Skill.Should().Be(skill);
        suggestion.RelevanceScore.Should().Be(0.85);
        suggestion.UsageExample.Should().Be("!summarize [input]");
    }

    [Fact]
    public void Equality_SameSkillRef_ShouldBeEqual()
    {
        var skill = new Skill(
            "s", "d", new List<string>(), new List<PlanStep>(), 0.5, 0, DateTime.UtcNow, DateTime.UtcNow);

        var a = new SkillSuggestion("token", skill, 0.5, "example");
        var b = new SkillSuggestion("token", skill, 0.5, "example");
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class DynamicSkillTokenTests
{
    [Fact]
    public void Constructor_WithValidArgs_ShouldSetSkillProperty()
    {
        var skill = new Skill(
            "analyze",
            "Analyze input",
            new List<string>(),
            new List<PlanStep>(),
            0.8,
            3,
            DateTime.UtcNow,
            DateTime.UtcNow);
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();

        var token = new DynamicSkillToken(skill, mockModel.Object);

        token.Skill.Should().Be(skill);
        token.Skill.Name.Should().Be("analyze");
    }

    [Fact]
    public void Constructor_NullSkill_ShouldThrowArgumentNullException()
    {
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();

        var act = () => new DynamicSkillToken(null!, mockModel.Object);

        act.Should().Throw<ArgumentNullException>().WithParameterName("skill");
    }

    [Fact]
    public void Constructor_NullModel_ShouldThrowArgumentNullException()
    {
        var skill = new Skill(
            "s", "d", new List<string>(), new List<PlanStep>(), 0.5, 0, DateTime.UtcNow, DateTime.UtcNow);

        var act = () => new DynamicSkillToken(skill, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("model");
    }

    [Fact]
    public async Task ExecuteAsync_WithSteps_ShouldCallModelForEachStep()
    {
        var steps = new List<PlanStep>
        {
            new("parse", new Dictionary<string, object>(), "parsed input", 0.9),
            new("transform", new Dictionary<string, object>(), "transformed", 0.85)
        };
        var skill = new Skill(
            "pipeline", "Pipeline skill", new List<string>(), steps, 0.9, 0, DateTime.UtcNow, DateTime.UtcNow);
        var mockModel = new Mock<Ouroboros.Abstractions.Core.IChatCompletionModel>();
        mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("step output");

        var token = new DynamicSkillToken(skill, mockModel.Object);

        var result = await token.ExecuteAsync("input text", null);

        result.Should().Be("step output");
        mockModel.Verify(
            m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }
}
