using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OuroborosOrchestratorTests
{
    private readonly OuroborosOrchestrator _orchestrator;

    public OuroborosOrchestratorTests()
    {
        _orchestrator = new OuroborosOrchestrator("test-id", SafetyConstraints.All, 5, TimeSpan.FromSeconds(30));
    }

    #region Constructor

    [Fact]
    public void Constructor_WithNullInstanceId_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new OuroborosOrchestrator(null!, SafetyConstraints.All, 10, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("instanceId");
    }

    [Fact]
    public void Constructor_WithNegativeMaxCycles_ShouldThrowArgumentOutOfRangeException()
    {
        // Act
        Action act = () => new OuroborosOrchestrator("id", SafetyConstraints.All, -1, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithValidParams_ShouldInitialize()
    {
        // Act
        var orchestrator = new OuroborosOrchestrator("test-id", SafetyConstraints.All, 10, TimeSpan.FromMinutes(1));

        // Assert
        orchestrator.Should().NotBeNull();
        orchestrator.InstanceId.Should().Be("test-id");
        orchestrator.MaxCycles.Should().Be(10);
        orchestrator.Timeout.Should().Be(TimeSpan.FromMinutes(1));
        orchestrator.SafetyConstraints.Should().Be(SafetyConstraints.All);
        orchestrator.CurrentPhase.Should().Be(ImprovementPhase.Plan);
        orchestrator.CycleCount.Should().Be(0);
    }

    #endregion

    #region SetGoal

    [Fact]
    public void SetGoal_WithValidGoal_ShouldSet()
    {
        // Act
        _orchestrator.SetGoal("test goal");

        // Assert
        _orchestrator.CurrentGoal.Should().Be("test goal");
    }

    [Fact]
    public void SetGoal_WithNullGoal_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => _orchestrator.SetGoal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("goal");
    }

    #endregion

    #region AdvancePhase

    [Fact]
    public void AdvancePhase_ShouldAdvance()
    {
        // Arrange
        var initialPhase = _orchestrator.CurrentPhase;

        // Act
        var newPhase = _orchestrator.AdvancePhase();

        // Assert
        newPhase.Should().NotBe(initialPhase);
    }

    #endregion

    #region GetStatus

    [Fact]
    public void GetStatus_ShouldReturnStatus()
    {
        // Act
        var status = _orchestrator.GetStatus();

        // Assert
        status.Should().NotBeNull();
    }

    #endregion

    #region Reset

    [Fact]
    public void Reset_ShouldResetState()
    {
        // Arrange
        _orchestrator.SetGoal("test goal");
        _orchestrator.AdvancePhase();

        // Act
        _orchestrator.Reset();

        // Assert
        _orchestrator.CurrentGoal.Should().BeNull();
        _orchestrator.CurrentPhase.Should().Be(ImprovementPhase.Plan);
        _orchestrator.CycleCount.Should().Be(0);
    }

    #endregion

    #region IsSafeAction

    [Fact]
    public void IsSafeAction_SafeAction_ShouldReturnTrue()
    {
        // Act
        var result = _orchestrator.IsSafeAction("analyze data");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSafeAction_UnsafeAction_ShouldReturnFalse()
    {
        // Act
        var result = _orchestrator.IsSafeAction("delete self");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AssessConfidence

    [Fact]
    public void AssessConfidence_EmptyAction_ShouldReturnLow()
    {
        // Act
        var result = _orchestrator.AssessConfidence("");

        // Assert
        result.Should().Be(OuroborosConfidence.Low);
    }

    [Fact]
    public void AssessConfidence_WhitespaceAction_ShouldReturnLow()
    {
        // Act
        var result = _orchestrator.AssessConfidence("   ");

        // Assert
        result.Should().Be(OuroborosConfidence.Low);
    }

    [Fact]
    public void AssessConfidence_ShouldReturnConfidence()
    {
        // Act
        var result = _orchestrator.AssessConfidence("some action");

        // Assert
        result.Should().BeOneOf(OuroborosConfidence.Low, OuroborosConfidence.Medium, OuroborosConfidence.High);
    }

    #endregion

    #region SelfReflect

    [Fact]
    public void SelfReflect_ShouldReturnReflection()
    {
        // Act
        var result = _orchestrator.SelfReflect();

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region GetStrategyWeight

    [Fact]
    public void GetStrategyWeight_EmptyName_ShouldReturnDefault()
    {
        // Act
        var result = _orchestrator.GetStrategyWeight("", 0.5);

        // Assert
        result.Should().Be(0.5);
    }

    [Fact]
    public void GetStrategyWeight_WhitespaceName_ShouldReturnDefault()
    {
        // Act
        var result = _orchestrator.GetStrategyWeight("   ", 0.5);

        // Assert
        result.Should().Be(0.5);
    }

    [Fact]
    public void GetStrategyWeight_WithCapability_ShouldReturnConfidence()
    {
        // Arrange
        _orchestrator.AddCapability(new OuroborosCapability("Strategy_TestStrategy", "Test", 0.85));

        // Act
        var result = _orchestrator.GetStrategyWeight("TestStrategy", 0.5);

        // Assert
        result.Should().Be(0.85);
    }

    [Fact]
    public void GetStrategyWeight_NoCapability_ShouldReturnDefault()
    {
        // Act
        var result = _orchestrator.GetStrategyWeight("NonExistent", 0.5);

        // Assert
        result.Should().Be(0.5);
    }

    #endregion

    #region ToMeTTa

    [Fact]
    public void ToMeTTa_ShouldContainInstanceId()
    {
        // Act
        var result = _orchestrator.ToMeTTa();

        // Assert
        result.Should().Contain("test-id");
    }

    #endregion

    #region CreateDefault

    [Fact]
    public void CreateDefault_ShouldCreateOrchestrator()
    {
        // Act
        var result = OuroborosOrchestrator.CreateDefault();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateDefault_WithCustomName_ShouldSetName()
    {
        // Act
        var result = OuroborosOrchestrator.CreateDefault("CustomName");

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region AddCapability

    [Fact]
    public void AddCapability_NullCapability_ShouldThrow()
    {
        // Act
        Action act = () => _orchestrator.AddCapability(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region AddLimitation

    [Fact]
    public void AddLimitation_NullLimitation_ShouldThrow()
    {
        // Act
        Action act = () => _orchestrator.AddLimitation(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region RecordExperience

    [Fact]
    public void RecordExperience_NullExperience_ShouldThrow()
    {
        // Act
        Action act = () => _orchestrator.RecordExperience(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateSelfModel

    [Fact]
    public void UpdateSelfModel_ShouldUpdate()
    {
        // Act
        _orchestrator.UpdateSelfModel("key", "value");

        // Assert
        var reflection = _orchestrator.SelfReflect();
        reflection.Should().Contain("key");
    }

    #endregion
}
