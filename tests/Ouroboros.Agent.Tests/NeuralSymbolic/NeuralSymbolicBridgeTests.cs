// <copyright file="NeuralSymbolicBridgeTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI.Affect;
using Hypothesis = Ouroboros.Agent.MetaAI.Hypothesis;
using PlanStep = Ouroboros.Agent.PlanStep;
using Skill = Ouroboros.Agent.MetaAI.Skill;

namespace Ouroboros.Agent.Tests.NeuralSymbolic;

/// <summary>
/// Unit tests for the NeuralSymbolicBridge hybrid reasoning component.
/// </summary>
[Trait("Category", "Unit")]
public class NeuralSymbolicBridgeTests
{
    private readonly Mock<IChatCompletionModel> _mockLlm;
    private readonly Mock<ISymbolicKnowledgeBase> _mockKnowledgeBase;
    private readonly NeuralSymbolicBridge _bridge;

    public NeuralSymbolicBridgeTests()
    {
        _mockLlm = new Mock<IChatCompletionModel>();
        _mockKnowledgeBase = new Mock<ISymbolicKnowledgeBase>();
        _bridge = new NeuralSymbolicBridge(_mockLlm.Object, _mockKnowledgeBase.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullLlm_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new NeuralSymbolicBridge(null!, _mockKnowledgeBase.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("llm");
    }

    [Fact]
    public void Constructor_NullKnowledgeBase_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new NeuralSymbolicBridge(_mockLlm.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .And.ParamName.Should().Be("knowledgeBase");
    }

    #endregion

    #region ExtractRulesFromSkillAsync Tests

    [Fact]
    public async Task ExtractRulesFromSkillAsync_NullSkill_ReturnsFailure()
    {
        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Skill cannot be null");
    }

    [Fact]
    public async Task ExtractRulesFromSkillAsync_ValidSkill_ReturnsExtractedRules()
    {
        // Arrange
        var skill = new Skill(
            "TestSkill",
            "A test skill for unit testing",
            new List<string> { "prereq1" },
            new List<PlanStep>
            {
                new PlanStep("Step1", new Dictionary<string, object>(), "outcome1", 0.9),
                new PlanStep("Step2", new Dictionary<string, object>(), "outcome2", 0.8)
            },
            0.85,
            5,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);

        var llmResponse =
            "RULE: test_rule\nMETTA: (implies (prereq) (effect))\nDESCRIPTION: A test rule\nPRECONDITIONS: prereq1\nEFFECTS: effect1\n---";

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].Name.Should().Be("test_rule");
        result.Value[0].MeTTaRepresentation.Should().Be("(implies (prereq) (effect))");
        result.Value[0].NaturalLanguageDescription.Should().Be("A test rule");
        result.Value[0].Source.Should().Be(RuleSource.ExtractedFromSkill);
    }

    [Fact]
    public async Task ExtractRulesFromSkillAsync_MultipleRulesInResponse_ParsesAll()
    {
        // Arrange
        var skill = new Skill(
            "MultiRuleSkill",
            "Skill with multiple rules",
            new List<string>(),
            new List<PlanStep>
            {
                new PlanStep("Action1", new Dictionary<string, object>(), "out1", 0.9)
            },
            0.9,
            3,
            DateTime.UtcNow,
            DateTime.UtcNow);

        var llmResponse =
            "RULE: rule_one\nMETTA: (rule1 x)\nDESCRIPTION: First rule\nPRECONDITIONS: none\nEFFECTS: none\n---\n" +
            "RULE: rule_two\nMETTA: (rule2 y)\nDESCRIPTION: Second rule\nPRECONDITIONS: cond_a\nEFFECTS: eff_b\n---";

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);

        // Act
        var result = await _bridge.ExtractRulesFromSkillAsync(skill);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Name.Should().Be("rule_one");
        result.Value[1].Name.Should().Be("rule_two");
    }

    #endregion

    #region NaturalLanguageToMeTTaAsync Tests

    [Fact]
    public async Task NaturalLanguageToMeTTaAsync_EmptyInput_ReturnsFailure()
    {
        // Act
        var result = await _bridge.NaturalLanguageToMeTTaAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Natural language cannot be empty");
    }

    [Fact]
    public async Task NaturalLanguageToMeTTaAsync_ValidInput_ReturnsParsedExpression()
    {
        // Arrange
        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("(isa Cat Animal)");

        // Act
        var result = await _bridge.NaturalLanguageToMeTTaAsync("A cat is an animal");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RawExpression.Should().Be("(isa Cat Animal)");
        result.Value.Symbols.Should().Contain("isa");
        result.Value.Type.Should().Be(ExpressionType.Expression);
    }

    #endregion

    #region MeTTaToNaturalLanguageAsync Tests

    [Fact]
    public async Task MeTTaToNaturalLanguageAsync_NullExpression_ReturnsFailure()
    {
        // Act
        var result = await _bridge.MeTTaToNaturalLanguageAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expression cannot be null");
    }

    [Fact]
    public async Task MeTTaToNaturalLanguageAsync_ValidExpression_ReturnsExplanation()
    {
        // Arrange
        var expression = new MeTTaExpression(
            "(isa Cat Animal)",
            ExpressionType.Expression,
            new List<string> { "isa", "Cat", "Animal" },
            new List<string>(),
            new Dictionary<string, object>());

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("A Cat is a type of Animal.");

        // Act
        var result = await _bridge.MeTTaToNaturalLanguageAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("A Cat is a type of Animal.");
    }

    #endregion

    #region HybridReasonAsync Tests

    [Fact]
    public async Task HybridReasonAsync_EmptyQuery_ReturnsFailure()
    {
        // Act
        var result = await _bridge.HybridReasonAsync("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Query cannot be empty");
    }

    [Fact]
    public async Task HybridReasonAsync_SymbolicFirst_SymbolicSucceeds_ReturnsSymbolicResult()
    {
        // Arrange
        _mockKnowledgeBase
            .Setup(kb => kb.ExecuteMeTTaQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("symbolic_answer"));

        // Act
        var result = await _bridge.HybridReasonAsync("What is X?", ReasoningMode.SymbolicFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SymbolicSucceeded.Should().BeTrue();
        result.Value.Answer.Should().Be("symbolic_answer");
        result.Value.ModeUsed.Should().Be(ReasoningMode.SymbolicFirst);
        result.Value.Confidence.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public async Task HybridReasonAsync_SymbolicFirst_SymbolicFails_FallsBackToNeural()
    {
        // Arrange
        _mockKnowledgeBase
            .Setup(kb => kb.ExecuteMeTTaQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Failure("no symbolic match"));

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("neural_answer");

        // Act
        var result = await _bridge.HybridReasonAsync("What is X?", ReasoningMode.SymbolicFirst);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SymbolicSucceeded.Should().BeFalse();
        result.Value.NeuralSucceeded.Should().BeTrue();
        result.Value.Answer.Should().Be("neural_answer");
    }

    [Fact]
    public async Task HybridReasonAsync_NeuralOnly_UsesOnlyLlm()
    {
        // Arrange
        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("pure_neural_answer");

        // Act
        var result = await _bridge.HybridReasonAsync("Explain gravity", ReasoningMode.NeuralOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NeuralSucceeded.Should().BeTrue();
        result.Value.Answer.Should().Be("pure_neural_answer");
        result.Value.Confidence.Should().BeApproximately(0.7, 0.01);

        _mockKnowledgeBase.Verify(
            kb => kb.ExecuteMeTTaQueryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CheckConsistencyAsync Tests

    [Fact]
    public async Task CheckConsistencyAsync_NullHypothesis_ReturnsFailure()
    {
        // Act
        var result = await _bridge.CheckConsistencyAsync(null!, new List<SymbolicRule>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Hypothesis cannot be null");
    }

    [Fact]
    public async Task CheckConsistencyAsync_ConsistentHypothesis_ReturnsConsistentReport()
    {
        // Arrange
        var hypothesis = new Hypothesis(
            Guid.NewGuid(),
            "All cats are animals",
            "Biology",
            0.9,
            new List<string> { "evidence1" },
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);

        var rules = new List<SymbolicRule>
        {
            new SymbolicRule(
                "cat_rule",
                "(isa Cat Animal)",
                "Cats are animals",
                new List<string>(),
                new List<string>(),
                0.95,
                RuleSource.UserProvided)
        };

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("CONSISTENT: Yes\nCONFLICTS: None\nMISSING: None");

        // Act
        var result = await _bridge.CheckConsistencyAsync(hypothesis, rules);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsConsistent.Should().BeTrue();
        result.Value.Conflicts.Should().BeEmpty();
        result.Value.ConsistencyScore.Should().Be(1.0);
    }

    #endregion

    #region GroundConceptAsync Tests

    [Fact]
    public async Task GroundConceptAsync_EmptyDescription_ReturnsFailure()
    {
        // Act
        var result = await _bridge.GroundConceptAsync("", new float[] { 0.1f, 0.2f });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Concept description cannot be empty");
    }

    [Fact]
    public async Task GroundConceptAsync_NullEmbedding_ReturnsFailure()
    {
        // Act
        var result = await _bridge.GroundConceptAsync("test concept", null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Embedding cannot be null or empty");
    }

    [Fact]
    public async Task GroundConceptAsync_ValidInput_ReturnsGroundedConcept()
    {
        // Arrange
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        _mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Type: Entity\nProperties: color, size, shape\nRelations: part_of, instance_of");

        // Act
        var result = await _bridge.GroundConceptAsync("A red ball", embedding);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("A red ball");
        result.Value.MeTTaType.Should().Be("Entity");
        result.Value.Properties.Should().Contain("color");
        result.Value.Properties.Should().Contain("size");
        result.Value.Relations.Should().Contain("part_of");
        result.Value.Embedding.Should().BeEquivalentTo(embedding);
        result.Value.GroundingConfidence.Should().Be(0.8);
    }

    #endregion
}
