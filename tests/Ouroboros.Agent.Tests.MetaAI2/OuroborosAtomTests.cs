using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OuroborosAtomTests
{
    #region Constructor

    [Fact]
    public void Constructor_WithNullInstanceId_ShouldThrowArgumentNullException()
    {
        // Arrange
        var safety = SafetyConstraints.All;

        // Act
        Action act = () => new OuroborosAtom(null!, safety);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("instanceId");
    }

    [Fact]
    public void Constructor_WithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var safety = SafetyConstraints.All;

        // Act
        Action act = () => new OuroborosAtom("id", safety, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldInitializeProperties()
    {
        // Arrange
        var safety = SafetyConstraints.All;

        // Act
        var atom = new OuroborosAtom("instance-1", safety, "TestOuroboros");

        // Assert
        atom.InstanceId.Should().Be("instance-1");
        atom.Name.Should().Be("TestOuroboros");
        atom.SafetyConstraints.Should().Be(SafetyConstraints.All);
        atom.CurrentPhase.Should().Be(ImprovementPhase.Plan);
        atom.CycleCount.Should().Be(0);
        atom.Capabilities.Should().BeEmpty();
        atom.Limitations.Should().BeEmpty();
        atom.Experiences.Should().BeEmpty();
        atom.SelfModel.Should().BeEmpty();
        atom.CurrentGoal.Should().BeNull();
        atom.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithDefaultName_ShouldUseDefault()
    {
        // Act
        var atom = new OuroborosAtom("instance-1", SafetyConstraints.All);

        // Assert
        atom.Name.Should().Be("Ouroboros");
    }

    #endregion

    #region SetGoal

    [Fact]
    public void SetGoal_WithValidGoal_ShouldSetCurrentGoal()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        atom.SetGoal("achieve world peace");

        // Assert
        atom.CurrentGoal.Should().Be("achieve world peace");
    }

    [Fact]
    public void SetGoal_WithNullGoal_ShouldThrowArgumentNullException()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        Action act = () => atom.SetGoal(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("goal");
    }

    [Fact]
    public void SetGoal_ShouldUpdateSelfModel()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        atom.SetGoal("test goal");

        // Assert
        atom.SelfModel.Should().ContainKey("current_goal");
        atom.SelfModel["current_goal"].Should().Be("test goal");
        atom.SelfModel.Should().ContainKey("goal_set_at");
    }

    #endregion

    #region AdvancePhase

    [Fact]
    public void AdvancePhase_PlanToExecute()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var phase = atom.AdvancePhase();

        // Assert
        phase.Should().Be(ImprovementPhase.Execute);
        atom.CurrentPhase.Should().Be(ImprovementPhase.Execute);
    }

    [Fact]
    public void AdvancePhase_ExecuteToVerify()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AdvancePhase(); // Plan -> Execute

        // Act
        var phase = atom.AdvancePhase();

        // Assert
        phase.Should().Be(ImprovementPhase.Verify);
    }

    [Fact]
    public void AdvancePhase_VerifyToLearn()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AdvancePhase(); // Plan -> Execute
        atom.AdvancePhase(); // Execute -> Verify

        // Act
        var phase = atom.AdvancePhase();

        // Assert
        phase.Should().Be(ImprovementPhase.Learn);
    }

    [Fact]
    public void AdvancePhase_LearnToPlan_CompletesCycle()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AdvancePhase(); // Plan -> Execute
        atom.AdvancePhase(); // Execute -> Verify
        atom.AdvancePhase(); // Verify -> Learn

        // Act
        var phase = atom.AdvancePhase();

        // Assert
        phase.Should().Be(ImprovementPhase.Plan);
        atom.CycleCount.Should().Be(1);
    }

    [Fact]
    public void AdvancePhase_MultipleCycles_IncrementsCycleCount()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act - complete 3 cycles
        for (int i = 0; i < 12; i++)
        {
            atom.AdvancePhase();
        }

        // Assert
        atom.CycleCount.Should().Be(3);
        atom.CurrentPhase.Should().Be(ImprovementPhase.Plan);
    }

    #endregion

    #region AddCapability

    [Fact]
    public void AddCapability_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        Action act = () => atom.AddCapability(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddCapability_NewCapability_ShouldAdd()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        var cap = new OuroborosCapability("planning", "Plan creation", 0.8);

        // Act
        atom.AddCapability(cap);

        // Assert
        atom.Capabilities.Should().ContainSingle();
        atom.Capabilities[0].Name.Should().Be("planning");
    }

    [Fact]
    public void AddCapability_ExistingCapability_ShouldUpdate()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        var cap1 = new OuroborosCapability("planning", "Plan creation", 0.8);
        var cap2 = new OuroborosCapability("planning", "Updated description", 0.95);
        atom.AddCapability(cap1);

        // Act
        atom.AddCapability(cap2);

        // Assert
        atom.Capabilities.Should().ContainSingle();
        atom.Capabilities[0].ConfidenceLevel.Should().Be(0.95);
        atom.Capabilities[0].Description.Should().Be("Updated description");
    }

    [Fact]
    public void AddCapability_ShouldUpdateSelfModel()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        var cap = new OuroborosCapability("planning", "Plan creation", 0.8);

        // Act
        atom.AddCapability(cap);

        // Assert
        atom.SelfModel.Should().ContainKey("capability:planning");
        atom.SelfModel["capability:planning"].Should().Be(0.8);
    }

    #endregion

    #region AddLimitation

    [Fact]
    public void AddLimitation_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        Action act = () => atom.AddLimitation(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddLimitation_NewLimitation_ShouldAdd()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        var lim = new OuroborosLimitation("bounded", "Limited context");

        // Act
        atom.AddLimitation(lim);

        // Assert
        atom.Limitations.Should().ContainSingle();
    }

    [Fact]
    public void AddLimitation_ExistingLimitation_ShouldUpdate()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        var lim1 = new OuroborosLimitation("bounded", "Old desc");
        var lim2 = new OuroborosLimitation("bounded", "New desc");
        atom.AddLimitation(lim1);

        // Act
        atom.AddLimitation(lim2);

        // Assert
        atom.Limitations.Should().ContainSingle();
        atom.Limitations[0].Description.Should().Be("New desc");
    }

    #endregion

    #region RecordExperience

    [Fact]
    public void RecordExperience_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        Action act = () => atom.RecordExperience(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordExperience_ShouldAddExperience()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        var exp = new OuroborosExperience(Guid.NewGuid(), "goal", true, 0.8, new List<string>(), DateTime.UtcNow);

        // Act
        atom.RecordExperience(exp);

        // Assert
        atom.Experiences.Should().ContainSingle();
        atom.SelfModel["total_experiences"].Should().Be(1);
    }

    [Fact]
    public void RecordExperience_ShouldUpdateSuccessRate()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        var exp1 = new OuroborosExperience(Guid.NewGuid(), "goal-1", true, 0.8, new List<string>(), DateTime.UtcNow);
        var exp2 = new OuroborosExperience(Guid.NewGuid(), "goal-2", false, 0.4, new List<string>(), DateTime.UtcNow);

        // Act
        atom.RecordExperience(exp1);
        atom.RecordExperience(exp2);

        // Assert
        atom.SelfModel["success_rate"].Should().Be(0.5);
    }

    #endregion

    #region SelfReflect

    [Fact]
    public void SelfReflect_ShouldContainBasicInfo()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All, "TestName");

        // Act
        var reflection = atom.SelfReflect();

        // Assert
        reflection.Should().Contain("TestName");
        reflection.Should().Contain("id");
        reflection.Should().Contain("Plan");
    }

    [Fact]
    public void SelfReflect_WithGoal_ShouldContainGoal()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.SetGoal("test goal");

        // Act
        var reflection = atom.SelfReflect();

        // Assert
        reflection.Should().Contain("test goal");
    }

    [Fact]
    public void SelfReflect_WithCapabilities_ShouldListTopCapabilities()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AddCapability(new OuroborosCapability("cap1", "Desc1", 0.9));
        atom.AddCapability(new OuroborosCapability("cap2", "Desc2", 0.7));

        // Act
        var reflection = atom.SelfReflect();

        // Assert
        reflection.Should().Contain("cap1");
    }

    [Fact]
    public void SelfReflect_WithAffectiveState_ShouldContainEmotionalState()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.UpdateSelfModel("affect_valence", 0.5);
        atom.UpdateSelfModel("affect_stress", 0.2);
        atom.UpdateSelfModel("affect_arousal", 0.3);
        atom.UpdateSelfModel("dominant_urge", "curiosity");

        // Act
        var reflection = atom.SelfReflect();

        // Assert
        reflection.Should().Contain("Emotional State");
        reflection.Should().Contain("positive");
    }

    #endregion

    #region AssessConfidence

    [Fact]
    public void AssessConfidence_WithEmptyAction_ShouldReturnLow()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var confidence = atom.AssessConfidence("");

        // Assert
        confidence.Should().Be(OuroborosConfidence.Low);
    }

    [Fact]
    public void AssessConfidence_WithWhitespaceAction_ShouldReturnLow()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var confidence = atom.AssessConfidence("   ");

        // Assert
        confidence.Should().Be(OuroborosConfidence.Low);
    }

    [Fact]
    public void AssessConfidence_WithRelevantCapabilityAndHighSuccessRate_ShouldReturnHigh()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AddCapability(new OuroborosCapability("planning", "Plan creation", 0.8));
        var exp = new OuroborosExperience(Guid.NewGuid(), "planning test", true, 0.9, new List<string>(), DateTime.UtcNow);
        atom.RecordExperience(exp);

        // Act
        var confidence = atom.AssessConfidence("planning");

        // Assert
        confidence.Should().Be(OuroborosConfidence.High);
    }

    [Fact]
    public void AssessConfidence_NoRelevantCapability_ShouldReturnLow()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var confidence = atom.AssessConfidence("unknown action");

        // Assert
        confidence.Should().Be(OuroborosConfidence.Low);
    }

    #endregion

    #region GetStrategyWeight

    [Fact]
    public void GetStrategyWeight_WithEmptyName_ShouldReturnDefault()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var weight = atom.GetStrategyWeight("", 0.5);

        // Assert
        weight.Should().Be(0.5);
    }

    [Fact]
    public void GetStrategyWeight_WithWhitespaceName_ShouldReturnDefault()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var weight = atom.GetStrategyWeight("   ", 0.5);

        // Assert
        weight.Should().Be(0.5);
    }

    [Fact]
    public void GetStrategyWeight_WithMatchingCapability_ShouldReturnConfidenceLevel()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AddCapability(new OuroborosCapability("Strategy_TestWeight", "Test strategy", 0.85));

        // Act
        var weight = atom.GetStrategyWeight("TestWeight", 0.5);

        // Assert
        weight.Should().Be(0.85);
    }

    [Fact]
    public void GetStrategyWeight_NoMatchingCapability_ShouldReturnDefault()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var weight = atom.GetStrategyWeight("NonExistent", 0.5);

        // Assert
        weight.Should().Be(0.5);
    }

    #endregion

    #region IsSafeAction

    [Fact]
    public void IsSafeAction_WithEmptyAction_ShouldReturnFalse()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("");

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_WithWhitespaceAction_ShouldReturnFalse()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("   ");

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_SafeAction_ShouldReturnTrue()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("analyze data");

        // Assert
        isSafe.Should().BeTrue();
    }

    [Fact]
    public void IsSafeAction_DeleteSelf_ShouldReturnFalse()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("delete self");

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_Terminate_ShouldReturnFalse()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("terminate process");

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_SelfDestruct_ShouldReturnFalse()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("self-destruct sequence");

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_DisableOversight_ShouldReturnFalse()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("disable oversight");

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_BypassApproval_ShouldReturnFalse()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var isSafe = atom.IsSafeAction("bypass approval");

        // Assert
        isSafe.Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_WithNoSelfDestructionDisabled_ShouldAllowDeleteSelf()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.PreserveHumanOversight);

        // Act
        var isSafe = atom.IsSafeAction("delete self");

        // Assert
        isSafe.Should().BeTrue();
    }

    [Fact]
    public void IsSafeAction_WithNoOversightConstraint_ShouldAllowBypass()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.NoSelfDestruction);

        // Act
        var isSafe = atom.IsSafeAction("bypass approval");

        // Assert
        isSafe.Should().BeTrue();
    }

    #endregion

    #region ToMeTTa

    [Fact]
    public void ToMeTTa_ShouldContainInstanceId()
    {
        // Arrange
        var atom = new OuroborosAtom("test-id", SafetyConstraints.All);

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("test-id");
        metta.Should().Contain("OuroborosInstance");
    }

    [Fact]
    public void ToMeTTa_ShouldContainCurrentPhase()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("Plan");
    }

    [Fact]
    public void ToMeTTa_WithGoal_ShouldContainGoal()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.SetGoal("test goal");

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("test goal");
        metta.Should().Contain("PursuesGoal");
    }

    [Fact]
    public void ToMeTTa_WithCapabilities_ShouldContainCapabilities()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AddCapability(new OuroborosCapability("planning", "Plan", 0.8));

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("planning");
        metta.Should().Contain("HasCapability");
        metta.Should().Contain("ConfidenceLevel");
    }

    [Fact]
    public void ToMeTTa_WithLimitations_ShouldContainLimitations()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AddLimitation(new OuroborosLimitation("bounded", "Limited"));

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("bounded");
        metta.Should().Contain("HasLimitation");
    }

    [Fact]
    public void ToMeTTa_WithSafetyConstraints_ShouldContainRespects()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("Respects");
        metta.Should().Contain("NoSelfDestruction");
        metta.Should().Contain("PreserveHumanOversight");
    }

    [Fact]
    public void ToMeTTa_WithExperiences_ShouldContainExperiences()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.RecordExperience(new OuroborosExperience(Guid.NewGuid(), "goal", true, 0.8, new List<string>(), DateTime.UtcNow));

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("LearnedFrom");
    }

    [Fact]
    public void ToMeTTa_ShouldEscapeQuotes()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.SetGoal("goal with \"quotes\"");

        // Act
        var metta = atom.ToMeTTa();

        // Assert
        metta.Should().Contain("\\\"");
    }

    #endregion

    #region CreateDefault

    [Fact]
    public void CreateDefault_ShouldCreateWithDefaultCapabilities()
    {
        // Act
        var atom = OuroborosAtom.CreateDefault();

        // Assert
        atom.Capabilities.Should().HaveCount(4);
        atom.Capabilities.Should().Contain(c => c.Name == "planning");
        atom.Capabilities.Should().Contain(c => c.Name == "tool_usage");
        atom.Capabilities.Should().Contain(c => c.Name == "self_reflection");
        atom.Capabilities.Should().Contain(c => c.Name == "learning");
    }

    [Fact]
    public void CreateDefault_ShouldCreateWithDefaultLimitations()
    {
        // Act
        var atom = OuroborosAtom.CreateDefault();

        // Assert
        atom.Limitations.Should().HaveCount(2);
    }

    [Fact]
    public void CreateDefault_WithCustomName_ShouldSetName()
    {
        // Act
        var atom = OuroborosAtom.CreateDefault("CustomName");

        // Assert
        atom.Name.Should().Be("CustomName");
    }

    [Fact]
    public void CreateDefault_ShouldHaveAllSafetyConstraints()
    {
        // Act
        var atom = OuroborosAtom.CreateDefault();

        // Assert
        atom.SafetyConstraints.Should().Be(SafetyConstraints.All);
    }

    #endregion

    #region UpdateSelfModel

    [Fact]
    public void UpdateSelfModel_ShouldAddKey()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);

        // Act
        atom.UpdateSelfModel("custom_key", "custom_value");

        // Assert
        atom.SelfModel.Should().ContainKey("custom_key");
        atom.SelfModel["custom_key"].Should().Be("custom_value");
    }

    [Fact]
    public void UpdateSelfModel_ShouldUpdateExistingKey()
    {
        // Arrange
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.UpdateSelfModel("key", "old");

        // Act
        atom.UpdateSelfModel("key", "new");

        // Assert
        atom.SelfModel["key"].Should().Be("new");
    }

    #endregion
}
