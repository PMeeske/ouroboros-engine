namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Reasoning;

[Trait("Category", "Unit")]
public class CommonsenseKnowledgeBaseTests
{
    [Fact]
    public void Lookup_NullFact_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CommonsenseKnowledgeBase.Lookup(null!));
    }

    [Fact]
    public void Lookup_ExistingFact_ShouldReturnFact()
    {
        var result = CommonsenseKnowledgeBase.Lookup("gravity");
        result.Should().NotBeNull();
    }

    [Fact]
    public void Lookup_MissingFact_ShouldReturnNull()
    {
        var result = CommonsenseKnowledgeBase.Lookup("nonexistent_xyz_123");
        result.Should().BeNull();
    }
}

[Trait("Category", "Unit")]
public class HyperonReasoningExtensionsTests
{
    [Fact]
    public void ToHyperonQuery_NullReasoningState_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((ReasoningState)null!).ToHyperonQuery());
    }
}

[Trait("Category", "Unit")]
public class HyperonReasoningStepTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var step = new HyperonReasoningStep("step1", "description", ImmutableList<string>.Empty);
        step.Name.Should().Be("step1");
        step.Description.Should().Be("description");
    }
}

[Trait("Category", "Unit")]
public class NoOpEmbeddingModelTests
{
    [Fact]
    public async Task GenerateEmbeddingsAsync_ShouldReturnZeroEmbeddings()
    {
        var model = new NoOpEmbeddingModel();
        var result = await model.GenerateEmbeddingsAsync(new[] { "text1", "text2" });
        result.Should().HaveCount(2);
        result[0].Should().AllBeEquivalentTo(0.0f);
    }

    [Fact]
    public void GetEmbeddingDimension_ShouldReturn1536()
    {
        var model = new NoOpEmbeddingModel();
        model.GetEmbeddingDimension().Should().Be(1536);
    }

    [Fact]
    public void GetModelName_ShouldReturnNoOp()
    {
        var model = new NoOpEmbeddingModel();
        model.GetModelName().Should().Be("NoOp");
    }
}

[Trait("Category", "Unit")]
public class OperatingCostAuditArrowsTests
{
    [Fact]
    public void CostAuditArrow_ShouldReturnArrow()
    {
        var arrow = OperatingCostAuditArrows.CostAuditArrow;
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void SafeCostAuditArrow_ShouldReturnArrow()
    {
        var arrow = OperatingCostAuditArrows.SafeCostAuditArrow;
        arrow.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class OperatingCostAuditPromptsTests
{
    [Fact]
    public void AuditPrompt_ShouldNotBeNullOrEmpty()
    {
        OperatingCostAuditPrompts.AuditPrompt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void SafeAuditPrompt_ShouldNotBeNullOrEmpty()
    {
        OperatingCostAuditPrompts.SafeAuditPrompt.Should().NotBeNullOrEmpty();
    }
}

[Trait("Category", "Unit")]
public class PromptTemplateTests
{
    [Fact]
    public void Constructor_NullTemplate_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PromptTemplate(null!));
    }

    [Fact]
    public void Format_ShouldReplaceVariables()
    {
        var template = new PromptTemplate("Hello {name}!");
        var result = template.Format(new Dictionary<string, string> { ["name"] = "World" });
        result.Should().Be("Hello World!");
    }

    [Fact]
    public void Format_MissingVariable_ShouldKeepPlaceholder()
    {
        var template = new PromptTemplate("Hello {name}!");
        var result = template.Format(new Dictionary<string, string>());
        result.Should().Be("Hello {name}!");
    }

    [Fact]
    public void Format_NullValues_ShouldThrowArgumentNullException()
    {
        var template = new PromptTemplate("test");
        Assert.Throws<ArgumentNullException>(() => template.Format(null!));
    }
}

[Trait("Category", "Unit")]
public class PromptsTests
{
    [Fact]
    public void Thinking_ShouldNotBeNull()
    {
        Prompts.Thinking.Should().NotBeNull();
    }

    [Fact]
    public void Draft_ShouldNotBeNull()
    {
        Prompts.Draft.Should().NotBeNull();
    }

    [Fact]
    public void Critique_ShouldNotBeNull()
    {
        Prompts.Critique.Should().NotBeNull();
    }

    [Fact]
    public void Final_ShouldNotBeNull()
    {
        Prompts.Final.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class ReasoningArrowsTests
{
    [Fact]
    public void ThinkingArrow_ShouldCreateArrow()
    {
        // Can't easily test without real LLM, just verify creation
        var llm = new Mock<ToolAwareChatModel>();
        var tools = new ToolRegistry();
        var embed = new Mock<IEmbeddingModel>().Object;
        var arrow = ReasoningArrows.ThinkingArrow(llm.Object, tools, embed, "topic", "query");
        arrow.Should().NotBeNull();
    }

    [Fact]
    public void SafeThinkingArrow_ShouldCreateArrow()
    {
        var llm = new Mock<ToolAwareChatModel>();
        var tools = new ToolRegistry();
        var embed = new Mock<IEmbeddingModel>().Object;
        var arrow = ReasoningArrows.SafeThinkingArrow(llm.Object, tools, embed, "topic", "query");
        arrow.Should().NotBeNull();
    }
}

[Trait("Category", "Unit")]
public class ReasoningTraceEntryTests
{
    [Fact]
    public void Properties_ShouldGetAndSet()
    {
        var entry = new ReasoningTraceEntry
        {
            Event = "test-event",
            StepName = "step1",
            Details = "details",
            Timestamp = DateTime.UtcNow
        };
        entry.Event.Should().Be("test-event");
        entry.StepName.Should().Be("step1");
        entry.Details.Should().Be("details");
    }
}
