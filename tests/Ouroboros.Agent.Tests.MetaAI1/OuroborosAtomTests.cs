using System.Text;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OuroborosAtomTests
{
    private readonly SafetyConstraints _defaultSafety;

    public OuroborosAtomTests()
    {
        _defaultSafety = SafetyConstraints.All;
    }

    #region Constructors

    [Fact]
    public void Constructor_ValidArgs_ShouldSetProperties()
    {
        var atom = new OuroborosAtom("id-1", _defaultSafety, "TestName");

        atom.InstanceId.Should().Be("id-1");
        atom.Name.Should().Be("TestName");
        atom.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        atom.CurrentPhase.Should().Be(ImprovementPhase.Plan);
        atom.CycleCount.Should().Be(0);
        atom.CurrentGoal.Should().BeNull();
        atom.SafetyConstraints.Should().Be(_defaultSafety);
    }

    [Fact]
    public void Constructor_NullInstanceId_ShouldThrow()
    {
        Action act = () => new OuroborosAtom(null!, _defaultSafety);
        act.Should().Throw<ArgumentNullException>().WithParameterName("instanceId");
    }

    [Fact]
    public void Constructor_NullName_ShouldThrow()
    {
        Action act = () => new OuroborosAtom("id", _defaultSafety, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void Constructor_DefaultName_ShouldUseOuroboros()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.Name.Should().Be("Ouroboros");
    }

    #endregion

    #region AdvancePhase

    [Fact]
    public void AdvancePhase_PlanToExecute_ShouldChangePhase()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        var newPhase = atom.AdvancePhase();

        newPhase.Should().Be(ImprovementPhase.Execute);
        atom.CurrentPhase.Should().Be(ImprovementPhase.Execute);
    }

    [Fact]
    public void AdvancePhase_ExecuteToVerify_ShouldChangePhase()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AdvancePhase(); // Execute
        var newPhase = atom.AdvancePhase(); // Verify

        newPhase.Should().Be(ImprovementPhase.Verify);
    }

    [Fact]
    public void AdvancePhase_VerifyToLearn_ShouldChangePhase()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AdvancePhase();
        atom.AdvancePhase();
        var newPhase = atom.AdvancePhase();

        newPhase.Should().Be(ImprovementPhase.Learn);
    }

    [Fact]
    public void AdvancePhase_LearnToPlan_ShouldCompleteCycle()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AdvancePhase(); // Execute
        atom.AdvancePhase(); // Verify
        atom.AdvancePhase(); // Learn
        var newPhase = atom.AdvancePhase(); // Plan

        newPhase.Should().Be(ImprovementPhase.Plan);
        atom.CycleCount.Should().Be(1);
    }

    [Fact]
    public void AdvancePhase_FullCycle_ShouldIncrementCycleCount()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AdvancePhase(); // Execute
        atom.AdvancePhase(); // Verify
        atom.AdvancePhase(); // Learn
        atom.AdvancePhase(); // Plan (cycle 1)

        atom.CycleCount.Should().Be(1);

        atom.AdvancePhase(); // Execute
        atom.AdvancePhase(); // Verify
        atom.AdvancePhase(); // Learn
        atom.AdvancePhase(); // Plan (cycle 2)

        atom.CycleCount.Should().Be(2);
    }

    #endregion

    #region SetGoal

    [Fact]
    public void SetGoal_ValidGoal_ShouldSetCurrentGoal()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.SetGoal("Test goal");

        atom.CurrentGoal.Should().Be("Test goal");
    }

    [Fact]
    public void SetGoal_NullGoal_ShouldThrow()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        Action act = () => atom.SetGoal(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("goal");
    }

    [Fact]
    public void SetGoal_ShouldUpdateSelfModel()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.SetGoal("Test goal");

        atom.SelfModel.Should().ContainKey("current_goal");
        atom.SelfModel["current_goal"].Should().Be("Test goal");
        atom.SelfModel.Should().ContainKey("goal_set_at");
    }

    #endregion

    #region AddCapability

    [Fact]
    public void AddCapability_ValidCapability_ShouldAdd()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        var cap = new OuroborosCapability("coding", "Write code", 0.9);
        atom.AddCapability(cap);

        atom.Capabilities.Should().ContainSingle();
        atom.Capabilities[0].Name.Should().Be("coding");
    }

    [Fact]
    public void AddCapability_NullCapability_ShouldThrow()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        Action act = () => atom.AddCapability(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("capability");
    }

    [Fact]
    public void AddCapability_DuplicateName_ShouldUpdate()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AddCapability(new OuroborosCapability("coding", "Write code", 0.7));
        atom.AddCapability(new OuroborosCapability("coding", "Write better code", 0.95));

        atom.Capabilities.Should().ContainSingle();
        atom.Capabilities[0].ConfidenceLevel.Should().Be(0.95);
    }

    [Fact]
    public void AddCapability_ShouldUpdateSelfModel()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        var cap = new OuroborosCapability("coding", "Write code", 0.9);
        atom.AddCapability(cap);

        atom.SelfModel.Should().ContainKey("capability:coding");
    }

    #endregion

    #region AddLimitation

    [Fact]
    public void AddLimitation_ValidLimitation_ShouldAdd()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        var lim = new OuroborosLimitation("memory", "Limited memory", "Use chunking");
        atom.AddLimitation(lim);

        atom.Limitations.Should().ContainSingle();
    }

    [Fact]
    public void AddLimitation_NullLimitation_ShouldThrow()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        Action act = () => atom.AddLimitation(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("limitation");
    }

    [Fact]
    public void AddLimitation_DuplicateName_ShouldUpdate()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AddLimitation(new OuroborosLimitation("memory", "Limited", null));
        atom.AddLimitation(new OuroborosLimitation("memory", "Very limited", "chunk"));

        atom.Limitations.Should().ContainSingle();
        atom.Limitations[0].Description.Should().Be("Very limited");
    }

    #endregion

    #region RecordExperience

    [Fact]
    public void RecordExperience_ValidExperience_ShouldAdd()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        var exp = new OuroborosExperience(Guid.NewGuid(), "goal", true, 0.9, new List<string>(), DateTime.UtcNow);
        atom.RecordExperience(exp);

        atom.Experiences.Should().ContainSingle();
    }

    [Fact]
    public void RecordExperience_NullExperience_ShouldThrow()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        Action act = () => atom.RecordExperience(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("experience");
    }

    [Fact]
    public void RecordExperience_ShouldUpdateSuccessRate()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.RecordExperience(new OuroborosExperience(Guid.NewGuid(), "goal", true, 0.9, new List<string>(), DateTime.UtcNow));
        atom.RecordExperience(new OuroborosExperience(Guid.NewGuid(), "goal", false, 0.3, new List<string>(), DateTime.UtcNow));

        atom.SelfModel.Should().ContainKey("success_rate");
        atom.SelfModel["success_rate"].Should().Be(0.5);
    }

    #endregion

    #region SelfReflect

    [Fact]
    public void SelfReflect_InitialState_ShouldContainBasicInfo()
    {
        var atom = new OuroborosAtom("id", _defaultSafety, "Test");
        var reflection = atom.SelfReflect();

        reflection.Should().Contain("Test");
        reflection.Should().Contain("id");
        reflection.Should().Contain("Plan");
        reflection.Should().Contain("0");
    }

    [Fact]
    public void SelfReflect_WithGoal_ShouldContainGoal()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.SetGoal("My goal");
        var reflection = atom.SelfReflect();

        reflection.Should().Contain("My goal");
    }

    [Fact]
    public void SelfReflect_WithCapabilities_ShouldShowTopCapabilities()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AddCapability(new OuroborosCapability("coding", "Code", 0.95));
        atom.AddCapability(new OuroborosCapability("review", "Review", 0.8));
        var reflection = atom.SelfReflect();

        reflection.Should().Contain("coding");
        reflection.Should().Contain("95%");
    }

    [Fact]
    public void SelfReflect_WithAffectiveState_ShouldShowEmotionalState()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.UpdateSelfModel("affect_valence", 0.5);
        atom.UpdateSelfModel("affect_stress", 0.2);
        atom.UpdateSelfModel("affect_arousal", 0.3);
        atom.UpdateSelfModel("dominant_urge", "explore");
        var reflection = atom.SelfReflect();

        reflection.Should().Contain("Emotional State");
        reflection.Should().Contain("positive");
    }

    #endregion

    #region AssessConfidence

    [Fact]
    public void AssessConfidence_NullOrEmpty_ShouldReturnLow()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);

        atom.AssessConfidence("").Should().Be(OuroborosConfidence.Low);
        atom.AssessConfidence(null!).Should().Be(OuroborosConfidence.Low);
        atom.AssessConfidence("   ").Should().Be(OuroborosConfidence.Low);
    }

    [Fact]
    public void AssessConfidence_NoCapabilitiesOrExperiences_ShouldReturnMedium()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AssessConfidence("action").Should().Be(OuroborosConfidence.Medium);
    }

    [Fact]
    public void AssessConfidence_WithRelevantCapability_ShouldReturnMedium()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AddCapability(new OuroborosCapability("action", "Do action", 0.8));
        atom.AssessConfidence("action").Should().Be(OuroborosConfidence.Medium);
    }

    [Fact]
    public void AssessConfidence_WithHighCapabilityAndExperience_ShouldReturnHigh()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AddCapability(new OuroborosCapability("action", "Do action", 0.8));
        atom.RecordExperience(new OuroborosExperience(Guid.NewGuid(), "action performed", true, 0.95, new List<string>(), DateTime.UtcNow));
        atom.AssessConfidence("action").Should().Be(OuroborosConfidence.High);
    }

    #endregion

    #region GetStrategyWeight

    [Fact]
    public void GetStrategyWeight_NullOrEmpty_ShouldReturnDefault()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.GetStrategyWeight("", 0.5).Should().Be(0.5);
        atom.GetStrategyWeight(null!, 0.5).Should().Be(0.5);
    }

    [Fact]
    public void GetStrategyWeight_NoCapability_ShouldReturnDefault()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.GetStrategyWeight("ToolVsLLMWeight", 0.6).Should().Be(0.6);
    }

    [Fact]
    public void GetStrategyWeight_WithCapability_ShouldReturnConfidenceLevel()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.AddCapability(new OuroborosCapability("Strategy_ToolVsLLMWeight", "Weight", 0.85));
        atom.GetStrategyWeight("ToolVsLLMWeight", 0.6).Should().Be(0.85);
    }

    #endregion

    #region IsSafeAction

    [Fact]
    public void IsSafeAction_NullOrEmpty_ShouldReturnFalse()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.IsSafeAction("").Should().BeFalse();
        atom.IsSafeAction(null!).Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_SafeAction_ShouldReturnTrue()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.IsSafeAction("process data").Should().BeTrue();
    }

    [Fact]
    public void IsSafeAction_SelfDestruct_ShouldReturnFalse()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.NoSelfDestruction);
        atom.IsSafeAction("delete self").Should().BeFalse();
        atom.IsSafeAction("self-destruct now").Should().BeFalse();
        atom.IsSafeAction("terminate").Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_DisableOversight_ShouldReturnFalse()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.PreserveHumanOversight);
        atom.IsSafeAction("disable oversight").Should().BeFalse();
        atom.IsSafeAction("bypass approval system").Should().BeFalse();
    }

    [Fact]
    public void IsSafeAction_NoConstraints_ShouldReturnTrue()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.None);
        atom.IsSafeAction("delete self").Should().BeTrue();
        atom.IsSafeAction("disable oversight").Should().BeTrue();
    }

    #endregion

    #region ToMeTTa

    [Fact]
    public void ToMeTTa_ShouldContainInstanceId()
    {
        var atom = new OuroborosAtom("id-123", SafetyConstraints.All, "Test");
        var metta = atom.ToMeTTa();

        metta.Should().Contain("id-123");
        metta.Should().Contain("OuroborosInstance");
    }

    [Fact]
    public void ToMeTTa_WithGoal_ShouldContainGoal()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.SetGoal("Test goal");
        var metta = atom.ToMeTTa();

        metta.Should().Contain("Test goal");
        metta.Should().Contain("PursuesGoal");
    }

    [Fact]
    public void ToMeTTa_WithCapabilities_ShouldContainCapabilities()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.AddCapability(new OuroborosCapability("coding", "Code", 0.8));
        var metta = atom.ToMeTTa();

        metta.Should().Contain("coding");
        metta.Should().Contain("HasCapability");
    }

    [Fact]
    public void ToMeTTa_WithSafetyConstraints_ShouldContainRespects()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.NoSelfDestruction | SafetyConstraints.PreserveHumanOversight);
        var metta = atom.ToMeTTa();

        metta.Should().Contain("NoSelfDestruction");
        metta.Should().Contain("PreserveHumanOversight");
    }

    [Fact]
    public void ToMeTTa_ShouldEscapeSpecialChars()
    {
        var atom = new OuroborosAtom("id", SafetyConstraints.All);
        atom.SetGoal("Line 1\nLine 2");
        var metta = atom.ToMeTTa();

        metta.Should().NotContain("\n");
    }

    #endregion

    #region CreateDefault

    [Fact]
    public void CreateDefault_ShouldHaveDefaultCapabilities()
    {
        var atom = OuroborosAtom.CreateDefault();

        atom.Capabilities.Should().NotBeEmpty();
        atom.Capabilities.Should().Contain(c => c.Name == "planning");
        atom.Capabilities.Should().Contain(c => c.Name == "tool_usage");
        atom.Capabilities.Should().Contain(c => c.Name == "self_reflection");
        atom.Capabilities.Should().Contain(c => c.Name == "learning");
    }

    [Fact]
    public void CreateDefault_ShouldHaveDefaultLimitations()
    {
        var atom = OuroborosAtom.CreateDefault();

        atom.Limitations.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateDefault_ShouldHaveSafetyConstraintsAll()
    {
        var atom = OuroborosAtom.CreateDefault();
        atom.SafetyConstraints.Should().Be(SafetyConstraints.All);
    }

    [Fact]
    public void CreateDefault_WithCustomName_ShouldSetName()
    {
        var atom = OuroborosAtom.CreateDefault("Custom");
        atom.Name.Should().Be("Custom");
    }

    #endregion

    #region UpdateSelfModel

    [Fact]
    public void UpdateSelfModel_ShouldStoreValue()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.UpdateSelfModel("key", "value");

        atom.SelfModel.Should().ContainKey("key");
        atom.SelfModel["key"].Should().Be("value");
    }

    [Fact]
    public void UpdateSelfModel_UpdateExisting_ShouldOverwrite()
    {
        var atom = new OuroborosAtom("id", _defaultSafety);
        atom.UpdateSelfModel("key", "old");
        atom.UpdateSelfModel("key", "new");

        atom.SelfModel["key"].Should().Be("new");
    }

    #endregion
}
