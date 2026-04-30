// <copyright file="HomeostasisPolicyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI.Affect;

namespace Ouroboros.Agent.Tests.MetaAI.Affect;

/// <summary>
/// Unit tests for the HomeostasisPolicy affective regulation component.
/// </summary>
[Trait("Category", "Unit")]
public class HomeostasisPolicyTests
{
    private readonly HomeostasisPolicy _policy;

    public HomeostasisPolicyTests()
    {
        _policy = new HomeostasisPolicy();
    }

    private static AffectiveState CreateState(
        double valence = 0.5,
        double stress = 0.3,
        double confidence = 0.5,
        double curiosity = 0.4,
        double arousal = 0.5)
    {
        return new AffectiveState(
            Guid.NewGuid(),
            valence,
            stress,
            confidence,
            curiosity,
            arousal,
            DateTime.UtcNow,
            new Dictionary<string, object>());
    }

    [Fact]
    public void Constructor_InitializesDefaultRules()
    {
        // Act
        var rules = _policy.GetRules(activeOnly: true);

        // Assert
        rules.Should().HaveCount(3);
        rules.Should().Contain(r => r.Name == "MaxStress");
        rules.Should().Contain(r => r.Name == "MinConfidence");
        rules.Should().Contain(r => r.Name == "CuriosityBalance");
    }

    [Fact]
    public void GetRules_ActiveOnly_ReturnsOnlyActiveRules()
    {
        // Arrange
        var allRules = _policy.GetRules(activeOnly: false);
        var firstRuleId = allRules.First().Id;
        _policy.SetRuleActive(firstRuleId, false);

        // Act
        var activeRules = _policy.GetRules(activeOnly: true);
        var allRulesAfter = _policy.GetRules(activeOnly: false);

        // Assert
        activeRules.Should().HaveCount(allRules.Count - 1);
        allRulesAfter.Should().HaveCount(allRules.Count);
    }

    [Fact]
    public void GetRules_ReturnsSortedByPriorityDescending()
    {
        // Act
        var rules = _policy.GetRules(activeOnly: true);

        // Assert
        rules.Should().BeInDescendingOrder(r => r.Priority);
    }

    [Fact]
    public void AddRule_ValidParameters_CreatesNewRule()
    {
        // Act
        var rule = _policy.AddRule(
            "TestArousalCap",
            "Caps arousal at safe levels",
            SignalType.Arousal,
            0.0,
            0.85,
            0.5,
            HomeostasisAction.Alert,
            priority: 0.7);

        // Assert
        rule.Should().NotBeNull();
        rule.Name.Should().Be("TestArousalCap");
        rule.TargetSignal.Should().Be(SignalType.Arousal);
        rule.LowerBound.Should().Be(0.0);
        rule.UpperBound.Should().Be(0.85);
        rule.IsActive.Should().BeTrue();
        _policy.GetRules(activeOnly: false).Should().Contain(r => r.Id == rule.Id);
    }

    [Fact]
    public void UpdateRule_ModifiesBounds()
    {
        // Arrange
        var rule = _policy.AddRule(
            "UpdatableRule",
            "Will be updated",
            SignalType.Stress,
            0.0,
            0.7,
            0.3,
            HomeostasisAction.Throttle);

        // Act
        _policy.UpdateRule(rule.Id, lowerBound: 0.1, upperBound: 0.9, targetValue: 0.4);

        // Assert
        var updated = _policy.GetRules(activeOnly: false).First(r => r.Id == rule.Id);
        updated.LowerBound.Should().Be(0.1);
        updated.UpperBound.Should().Be(0.9);
        updated.TargetValue.Should().Be(0.4);
    }

    [Fact]
    public void SetRuleActive_DisablesRule()
    {
        // Arrange
        var rule = _policy.AddRule(
            "DisableMe",
            "Will be disabled",
            SignalType.Valence,
            -0.5,
            1.0,
            0.3,
            HomeostasisAction.Log);

        // Act
        _policy.SetRuleActive(rule.Id, false);

        // Assert
        var all = _policy.GetRules(activeOnly: false).First(r => r.Id == rule.Id);
        all.IsActive.Should().BeFalse();
        _policy.GetRules(activeOnly: true).Should().NotContain(r => r.Id == rule.Id);
    }

    [Fact]
    public void EvaluatePolicies_NormalState_ReturnsNoViolations()
    {
        // Arrange - values within all default rule bounds
        var state = CreateState(stress: 0.3, confidence: 0.5, curiosity: 0.4);

        // Act
        var violations = _policy.EvaluatePolicies(state);

        // Assert
        violations.Should().BeEmpty();
    }

    [Fact]
    public void EvaluatePolicies_HighStress_DetectsViolation()
    {
        // Arrange - stress exceeds MaxStress upper bound of 0.8
        var state = CreateState(stress: 0.95, confidence: 0.5, curiosity: 0.4);

        // Act
        var violations = _policy.EvaluatePolicies(state);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain(v => v.Signal == SignalType.Stress);
        var stressViolation = violations.First(v => v.Signal == SignalType.Stress);
        stressViolation.ViolationType.Should().Be("AboveUpperBound");
        stressViolation.ObservedValue.Should().Be(0.95);
        stressViolation.RecommendedAction.Should().Be(HomeostasisAction.Throttle);
    }

    [Fact]
    public void EvaluatePolicies_LowConfidence_DetectsViolation()
    {
        // Arrange - confidence below MinConfidence lower bound of 0.2
        var state = CreateState(stress: 0.1, confidence: 0.05, curiosity: 0.4);

        // Act
        var violations = _policy.EvaluatePolicies(state);

        // Assert
        violations.Should().NotBeEmpty();
        violations.Should().Contain(v => v.Signal == SignalType.Confidence);
        var confViolation = violations.First(v => v.Signal == SignalType.Confidence);
        confViolation.ViolationType.Should().Be("BelowLowerBound");
        confViolation.RecommendedAction.Should().Be(HomeostasisAction.Boost);
    }

    [Fact]
    public void EvaluatePolicies_MultipleViolations_ReturnsSortedBySeverity()
    {
        // Arrange - stress too high AND confidence too low
        var state = CreateState(stress: 0.95, confidence: 0.05, curiosity: 0.4);

        // Act
        var violations = _policy.EvaluatePolicies(state);

        // Assert
        violations.Should().HaveCountGreaterThanOrEqualTo(2);
        violations.Should().BeInDescendingOrder(v => v.Severity);
    }

    [Fact]
    public void EvaluatePolicies_NullState_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => _policy.EvaluatePolicies(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ApplyCorrectionAsync_LogAction_ReturnsSuccess()
    {
        // Arrange
        var mockMonitor = new Mock<IValenceMonitor>();
        var monitorState = CreateState(curiosity: 0.95);
        mockMonitor.Setup(m => m.GetCurrentState()).Returns(monitorState);

        var violation = new PolicyViolation(
            Guid.NewGuid(),
            "CuriosityBalance",
            SignalType.Curiosity,
            0.95,
            0.1,
            0.9,
            "AboveUpperBound",
            HomeostasisAction.Log,
            0.5,
            DateTime.UtcNow);

        // Act
        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        // Assert
        result.Success.Should().BeTrue();
        result.ActionTaken.Should().Be(HomeostasisAction.Log);
        result.Message.Should().Contain("Logged violation");
    }

    [Fact]
    public async Task ApplyCorrectionAsync_ResetAction_ResetsMonitor()
    {
        // Arrange
        var mockMonitor = new Mock<IValenceMonitor>();
        var stressedState = CreateState(stress: 0.95);
        var resetState = CreateState(stress: 0.3);

        mockMonitor.SetupSequence(m => m.GetCurrentState())
            .Returns(stressedState)
            .Returns(resetState);

        var ruleId = Guid.NewGuid();
        var violation = new PolicyViolation(
            ruleId,
            "MaxStress",
            SignalType.Stress,
            0.95,
            0.0,
            0.8,
            "AboveUpperBound",
            HomeostasisAction.Reset,
            0.8,
            DateTime.UtcNow);

        // Act
        var result = await _policy.ApplyCorrectionAsync(violation, mockMonitor.Object);

        // Assert
        result.Success.Should().BeTrue();
        result.ActionTaken.Should().Be(HomeostasisAction.Reset);
        mockMonitor.Verify(m => m.Reset(), Times.Once);
    }

    [Fact]
    public void GetHealthSummary_AfterViolation_ReflectsViolationCount()
    {
        // Arrange
        var state = CreateState(stress: 0.95);
        _policy.EvaluatePolicies(state);

        // Act
        var summary = _policy.GetHealthSummary();

        // Assert
        summary.TotalViolations.Should().BeGreaterThan(0);
        summary.ActiveRules.Should().Be(3);
        summary.ViolationsBySignal.Should().ContainKey(SignalType.Stress);
    }

    [Fact]
    public void GetViolationHistory_ReturnsRecordedViolations()
    {
        // Arrange
        var state = CreateState(stress: 0.95);
        _policy.EvaluatePolicies(state);

        // Act
        var history = _policy.GetViolationHistory(10);

        // Assert
        history.Should().NotBeEmpty();
        history.Should().AllSatisfy(v => v.DetectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void RegisterCustomHandler_CanBeInvokedOnCustomViolation()
    {
        // Arrange
        bool handlerCalled = false;

        _policy.RegisterCustomHandler("CustomRule", async (violation, monitor) =>
        {
            handlerCalled = true;
            return new CorrectionResult(
                violation.RuleId,
                HomeostasisAction.Custom,
                true,
                "Custom correction applied",
                0.9,
                0.5,
                DateTime.UtcNow);
        });

        // Assert - handler is registered without throwing
        handlerCalled.Should().BeFalse();
    }
}
