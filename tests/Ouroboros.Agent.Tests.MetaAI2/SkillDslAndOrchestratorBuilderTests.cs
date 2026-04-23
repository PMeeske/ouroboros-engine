using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class SkillBasedDslExtensionTests
{
    #region TryParseSkillDSL

    [Fact]
    public void TryParseSkillDSL_NullOrEmpty_ShouldReturnFalse()
    {
        // Act
        var result1 = SkillBasedDslExtension.TryParseSkillDSL(null!, out var _);
        var result2 = SkillBasedDslExtension.TryParseSkillDSL("", out var _);
        var result3 = SkillBasedDslExtension.TryParseSkillDSL("   ", out var _);

        // Assert
        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }

    [Fact]
    public void TryParseSkillDSL_ValidSkillCommand_ShouldReturnTrue()
    {
        // Act
        var result = SkillBasedDslExtension.TryParseSkillDSL("UseSkill_TestSkill(arg1=1)", out var invocation);

        // Assert
        result.Should().BeTrue();
        invocation.Should().NotBeNull();
        invocation!.SkillName.Should().Be("TestSkill");
        invocation.Parameters.Should().ContainKey("arg1");
    }

    [Fact]
    public void TryParseSkillDSL_NoSkillCommand_ShouldReturnFalse()
    {
        // Act
        var result = SkillBasedDslExtension.TryParseSkillDSL("some random text", out var invocation);

        // Assert
        result.Should().BeFalse();
        invocation.Should().BeNull();
    }

    [Fact]
    public void TryParseSkillDSL_MultipleSkills_ShouldParseAll()
    {
        // Act
        var result = SkillBasedDslExtension.TryParseSkillDSL("UseSkill_A(arg=1) UseSkill_B(arg=2)", out var invocation);

        // Assert
        result.Should().BeTrue();
        invocation!.SkillName.Should().Be("A");
    }

    #endregion

    #region CreateSkillInvocation

    [Fact]
    public void CreateSkillInvocation_WithNullName_ShouldThrow()
    {
        // Act
        Action act = () => SkillBasedDslExtension.CreateSkillInvocation(null!, new Dictionary<string, object>());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateSkillInvocation_WithValidName_ShouldCreate()
    {
        // Act
        var invocation = SkillBasedDslExtension.CreateSkillInvocation("TestSkill", new Dictionary<string, object> { ["arg1"] = "value1" });

        // Assert
        invocation.SkillName.Should().Be("TestSkill");
        invocation.Parameters.Should().ContainKey("arg1");
    }

    #endregion

    #region ToSkillDSL

    [Fact]
    public void ToSkillDSL_WithNullInvocation_ShouldReturnEmpty()
    {
        // Act
        var result = SkillBasedDslExtension.ToSkillDSL(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToSkillDSL_WithValidInvocation_ShouldReturnString()
    {
        // Arrange
        var invocation = new SkillInvocation("TestSkill", new Dictionary<string, object> { ["arg1"] = "value1" });

        // Act
        var result = SkillBasedDslExtension.ToSkillDSL(invocation);

        // Assert
        result.Should().Contain("UseSkill_TestSkill");
        result.Should().Contain("arg1");
    }

    #endregion

    #region SkillInvocationToAction

    [Fact]
    public void SkillInvocationToAction_WithNull_ShouldReturnEmpty()
    {
        // Act
        var result = SkillBasedDslExtension.SkillInvocationToAction(null!);

        // Assert
        result.Should().Be("UseSkill_NullSkill()");
    }

    [Fact]
    public void SkillInvocationToAction_WithValidInvocation_ShouldReturnAction()
    {
        // Arrange
        var invocation = new SkillInvocation("TestSkill", new Dictionary<string, object> { ["arg1"] = "value1" });

        // Act
        var result = SkillBasedDslExtension.SkillInvocationToAction(invocation);

        // Assert
        result.Should().Contain("UseSkill_TestSkill");
    }

    #endregion

    #region WrapInSkillDSL

    [Fact]
    public void WrapInSkillDSL_ShouldWrapAction()
    {
        // Arrange
        var step = new PlanStep("action", new Dictionary<string, object>(), "outcome", 0.8);

        // Act
        var result = step.WrapInSkillDSL("TestSkill");

        // Assert
        result.Should().Contain("UseSkill_TestSkill");
    }

    #endregion
}

[Trait("Category", "Unit")]
public class OuroborosOrchestratorBuilderTests
{
    #region Create

    [Fact]
    public void Create_ShouldReturnBuilder()
    {
        // Act
        var builder = OuroborosOrchestratorBuilder.Create("test-id");

        // Assert
        builder.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithEmptyId_ShouldThrow()
    {
        // Act
        Action act = () => OuroborosOrchestratorBuilder.Create("");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region FluentConfiguration

    [Fact]
    public void WithSafetyConstraints_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");

        // Act
        var result = builder.WithSafetyConstraints(SafetyConstraints.All);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithCapability_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");
        var cap = new OuroborosCapability("planning", "Plan creation", 0.8);

        // Act
        var result = builder.WithCapability(cap);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithLimitation_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");
        var lim = new OuroborosLimitation("bounded", "Limited");

        // Act
        var result = builder.WithLimitation(lim);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithMaxCycles_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");

        // Act
        var result = builder.WithMaxCycles(5);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithTimeout_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");

        // Act
        var result = builder.WithTimeout(TimeSpan.FromMinutes(5));

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithToolSelector_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");
        var selector = new ToolSelector();

        // Act
        var result = builder.WithToolSelector(selector);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithSkillRegistry_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");
        var registry = new SkillRegistry();

        // Act
        var result = builder.WithSkillRegistry(registry);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithContext_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");
        var mockContext = new Mock<IActiveContext>();

        // Act
        var result = builder.WithContext(mockContext.Object);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void WithConfig_ShouldReturnBuilder()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");

        // Act
        var result = builder.WithConfig(new OrchestrationObservabilityConfig());

        // Assert
        result.Should().Be(builder);
    }

    #endregion

    #region Build

    [Fact]
    public void Build_ShouldReturnOrchestrator()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id");

        // Act
        var orchestrator = builder.Build();

        // Assert
        orchestrator.Should().NotBeNull();
        orchestrator.InstanceId.Should().Be("test-id");
    }

    [Fact]
    public void Build_WithSafetyConstraints_ShouldSetConstraints()
    {
        // Arrange
        var builder = OuroborosOrchestratorBuilder.Create("test-id").WithSafetyConstraints(SafetyConstraints.NoSelfDestruction);

        // Act
        var orchestrator = builder.Build();

        // Assert
        orchestrator.SafetyConstraints.Should().Be(SafetyConstraints.NoSelfDestruction);
    }

    #endregion
}
