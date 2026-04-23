using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Agent.Tests.Affect;

[Trait("Category", "Unit")]
public class HomeostasisPolicyTests
{
    private readonly HomeostasisPolicy _policy;

    public HomeostasisPolicyTests()
    {
        _policy = new HomeostasisPolicy();
    }

    #region Constructor

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultRules()
    {
        var policy = new HomeostasisPolicy();
        var rules = policy.GetRules(false);
        rules.Should().NotBeEmpty();
    }

    #endregion

    #region AddRule

    [Fact]
    public void AddRule_ValidArgs_ShouldAddRule()
    {
        var rule = _policy.AddRule("TestRule", "Description", SignalType.Valence, -1.0, 1.0, 0.0, HomeostasisAction.Log, 1.0);

        rule.Should().NotBeNull();
        rule.Name.Should().Be("TestRule");
        rule.Description.Should().Be("Description");
        rule.TargetSignal.Should().Be(SignalType.Valence);
        rule.LowerBound.Should().Be(-1.0);
        rule.UpperBound.Should().Be(1.0);
        rule.TargetValue.Should().Be(0.0);
        rule.Action.Should().Be(HomeostasisAction.Log);
        rule.Priority.Should().Be(1.0);
        rule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AddRule_NullName_ShouldThrow()
    {
        Action act = () => _policy.AddRule(null!, "desc", SignalType.Valence, 0, 1, 0.5, HomeostasisAction.Log);
        act.Should().Throw<ArgumentNullException>().WithParameterName("name");
    }

    [Fact]
    public void AddRule_NullDescription_ShouldThrow()
    {
        Action act = () => _policy.AddRule("name", null!, SignalType.Valence, 0, 1, 0.5, HomeostasisAction.Log);
        act.Should().Throw<ArgumentNullException>().WithParameterName("description");
    }

    #endregion

    #region UpdateRule

    [Fact]
    public void UpdateRule_ExistingRule_ShouldUpdateBounds()
    {
        var rule = _policy.AddRule("TestRule", "Description", SignalType.Valence, -1.0, 1.0, 0.0, HomeostasisAction.Log);
        _policy.UpdateRule(rule.Id, lowerBound: -0.5, upperBound: 0.5);

        var updated = _policy.GetRules(false).First(r => r.Id == rule.Id);
        updated.LowerBound.Should().Be(-0.5);
        updated.UpperBound.Should().Be(0.5);
    }

    [Fact]
    public void UpdateRule_NonExistingRule_ShouldNotThrow()
    {
        _policy.UpdateRule(Guid.NewGuid(), lowerBound: 0.5);
    }

    #endregion

    #region SetRuleActive

    [Fact]
    public void SetRuleActive_ShouldToggleActivity()
    {
        var rule = _policy.AddRule("TestRule", "Description", SignalType.Valence, -1.0, 1.0, 0.0, HomeostasisAction.Log);
        _policy.SetRuleActive(rule.Id, false);

        var updated = _policy.GetRules(false).First(r => r.Id == rule.Id);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SetRuleActive_NonExistingRule_ShouldNotThrow()
    {
        _policy.SetRuleActive(Guid.NewGuid(), false);
    }

    #endregion

    #region GetRules

    [Fact]
    public void GetRules_ActiveOnly_ShouldReturnOnlyActive()
    {
        var rule1 = _policy.AddRule("Active", "Desc", SignalType.Valence, -1.0, 1.0, 0.0, HomeostasisAction.Log);
        var rule2 = _policy.AddRule("Inactive", "Desc", SignalType.Stress, 0.0, 1.0, 0.5, HomeostasisAction.Log);
        _policy.SetRuleActive(rule2.Id, false);

        var activeRules = _policy.GetRules(true);
        activeRules.Should().Contain(r => r.Id == rule1.Id);
        activeRules.Should().NotContain(r => r.Id == rule2.Id);
    }

    [Fact]
    public void GetRules_All_ShouldReturnAll()
    {
        var rule = _policy.AddRule("Test", "Desc", SignalType.Valence, -1.0, 1.0, 0.0, HomeostasisAction.Log);
        _policy.SetRuleActive(rule.Id, false);

        var allRules = _policy.GetRules(false);
        allRules.Should().Contain(r => r.Id == rule.Id);
    }

    [Fact]
    public void GetRules_ShouldOrderByPriorityDescending()
    {
        var rule1 = _policy.AddRule("High", "Desc", SignalType.Valence, -1.0, 1.0, 0.0, HomeostasisAction.Log, 2.0);
        var rule2 = _policy.AddRule("Low", "Desc", SignalType.Valence, -1.0, 1.0, 0.0, HomeostasisAction.Log, 1.0);

        var rules = _policy.GetRules(true);
        rules[0].Id.Should().Be(rule1.Id);
        rules[1].Id.Should().Be(rule2.Id);
    }

    #endregion

    #region EvaluatePolicies

    [Fact]
    public void EvaluatePolicies_ValidState_NoViolations_ShouldReturnEmpty()
    {
        var state = new AffectiveState(0.0, 0.5, 0.5, 0.5, DateTime.UtcNow);
        var violations = _policy.EvaluatePolicies(state);

        violations.Should().BeEmpty();
    }

    [Fact]
    public void EvaluatePolicies_NullState_ShouldThrow()
    {
        Action act = () => _policy.EvaluatePolicies(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EvaluatePolicies_BelowLowerBound_ShouldReturnViolation()
    {
        var rule = _policy.AddRule("Test", "Desc", SignalType.Valence, -0.5, 0.5, 0.0, HomeostasisAction.Log);
        var state = new AffectiveState(-0.8, 0.5, 0.5, 0.5, DateTime.UtcNow);

        var violations = _policy.EvaluatePolicies(state);
        violations.Should().ContainSingle();
        violations[0].ViolationType.Should().Be("BelowLowerBound");
        violations[0].Signal.Should().Be(SignalType.Valence);
    }

    [Fact]
    public void EvaluatePolicies_AboveUpperBound_ShouldReturnViolation()
    {
        var rule = _policy.AddRule("Test", "Desc", SignalType.Stress, 0.0, 0.5, 0.25, HomeostasisAction.Log);
        var state = new AffectiveState(0.0, 0.8, 0.5, 0.5, DateTime.UtcNow);

        var violations = _policy.EvaluatePolicies(state);
        violations.Should().ContainSingle();
        violations[0].ViolationType.Should().Be("AboveUpperBound");
        violations[0].Signal.Should().Be(SignalType.Stress);
    }

    [Fact]
    public void EvaluatePolicies_MultipleViolations_ShouldOrderBySeverity()
    {
        var rule1 = _policy.AddRule("Mild", "Desc", SignalType.Valence, -0.1, 0.1, 0.0, HomeostasisAction.Log, 1.0);
        var rule2 = _policy.AddRule("Severe", "Desc", SignalType.Stress, 0.0, 0.1, 0.05, HomeostasisAction.Log, 2.0);
        var state = new AffectiveState(-0.5, 0.9, 0.5, 0.5, DateTime.UtcNow);

        var violations = _policy.EvaluatePolicies(state);
        violations.Should().HaveCount(2);
        violations[0].Signal.Should().Be(SignalType.Stress); // More severe
    }

    #endregion

    #region ApplyCorrectionAsync

    [Fact]
    public async Task ApplyCorrectionAsync_LogAction_ShouldSucceed()
    {
        var state = new AffectiveState(0.0, 0.5, 0.5, 0.5, DateTime.UtcNow);
        var violation = new PolicyViolation(Guid.NewGuid(), "Test", SignalType.Valence, 0.0, -0.5, 0.5, "BelowLowerBound", HomeostasisAction.Log, 0.5, DateTime.UtcNow);
        var mockMonitor = new Mock<IValenceMonitor>();
        mockMonitor.Setup(m => m.GetCurrentState()).Returns(state);

        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        result.Success.Should().BeTrue();
        result.Action.Should().Be(HomeostasisAction.Log);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_AlertAction_ShouldSucceed()
    {
        var state = new AffectiveState(0.0, 0.5, 0.5, 0.5, DateTime.UtcNow);
        var violation = new PolicyViolation(Guid.NewGuid(), "Test", SignalType.Valence, 0.0, -0.5, 0.5, "BelowLowerBound", HomeostasisAction.Alert, 0.5, DateTime.UtcNow);
        var mockMonitor = new Mock<IValenceMonitor>();
        mockMonitor.Setup(m => m.GetCurrentState()).Returns(state);

        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        result.Success.Should().BeTrue();
        result.Action.Should().Be(HomeostasisAction.Alert);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_ThrottleAction_ShouldRecordSignals()
    {
        var state = new AffectiveState(0.0, 0.5, 0.5, 0.5, DateTime.UtcNow);
        var violation = new PolicyViolation(Guid.NewGuid(), "Test", SignalType.Stress, 0.5, 0.0, 0.3, "AboveUpperBound", HomeostasisAction.Throttle, 0.5, DateTime.UtcNow);
        var mockMonitor = new Mock<IValenceMonitor>();
        mockMonitor.Setup(m => m.GetCurrentState()).Returns(state);
        mockMonitor.Setup(m => m.RecordSignal(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<SignalType>()));

        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        mockMonitor.Verify(m => m.RecordSignal("homeostasis_throttle", -0.2, SignalType.Stress), Times.AtLeast(1));
    }

    [Fact]
    public async Task ApplyCorrectionAsync_BoostAction_ShouldRecordSignals()
    {
        var state = new AffectiveState(0.0, 0.5, 0.5, 0.5, DateTime.UtcNow);
        var violation = new PolicyViolation(Guid.NewGuid(), "Test", SignalType.Valence, -0.5, -0.1, 0.0, "BelowLowerBound", HomeostasisAction.Boost, 0.5, DateTime.UtcNow);
        var mockMonitor = new Mock<IValenceMonitor>();
        mockMonitor.Setup(m => m.GetCurrentState()).Returns(state);
        mockMonitor.Setup(m => m.RecordSignal(It.IsAny<string>(), It.IsAny<double>(), It.IsAny<SignalType>()));

        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        mockMonitor.Verify(m => m.RecordSignal("homeostasis_boost", 0.2, SignalType.Valence), Times.AtLeast(1));
    }

    [Fact]
    public async Task ApplyCorrectionAsync_PauseAction_ShouldSucceed()
    {
        var state = new AffectiveState(0.0, 0.5, 0.5, 0.5, DateTime.UtcNow);
        var violation = new PolicyViolation(Guid.NewGuid(), "Test", SignalType.Valence, 0.0, -0.5, 0.5, "BelowLowerBound", HomeostasisAction.Pause, 0.5, DateTime.UtcNow);
        var mockMonitor = new Mock<IValenceMonitor>();
        mockMonitor.Setup(m => m.GetCurrentState()).Returns(state);

        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        result.Success.Should().BeTrue();
        result.Action.Should().Be(HomeostasisAction.Pause);
    }

    [Fact]
    public async Task ApplyCorrectionAsync_ResetAction_ShouldResetMonitor()
    {
        var state = new AffectiveState(0.0, 0.5, 0.5, 0.5, DateTime.UtcNow);
        var violation = new PolicyViolation(Guid.NewGuid(), "Test", SignalType.Valence, 0.0, -0.5, 0.5, "BelowLowerBound", HomeostasisAction.Reset, 0.5, DateTime.UtcNow);
        var mockMonitor = new Mock<IValenceMonitor>();
        mockMonitor.Setup(m => m.GetCurrentState()).Returns(state);
        mockMonitor.Setup(m => m.Reset());

        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        mockMonitor.Verify(m => m.Reset(), Times.Once);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyCorrectionAsync_NullViolation_ShouldThrow()
    {
        var mockMonitor = new Mock<IValenceMonitor>();
        Func<Task> act = async () => await _policy.ApplyCorrectionAsync(null!, mockMonitor.Object);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ApplyCorrectionAsync_NullMonitor_ShouldThrow()
    {
        var violation = new PolicyViolation(Guid.NewGuid(), "Test", SignalType.Valence, 0.0, -0.5, 0.5, "BelowLowerBound", HomeostasisAction.Log, 0.5, DateTime.UtcNow);
        Func<Task> act = async () => await _policy.ApplyCorrectionAsync(violation, null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion
}
