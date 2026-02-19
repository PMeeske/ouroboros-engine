// <copyright file="SafetyGuardNeuroSymbolicTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Integration tests for neuro-symbolic safety validation in SafetyGuard.
/// Validates the combination of OuroborosAtom string-matching with MeTTa symbolic reasoning.
/// </summary>
[Trait("Category", "Unit")]
public class SafetyGuardNeuroSymbolicTests
{
    private const string TestInstanceId = "test-ouroboros-001";

    [Fact]
    public async Task CheckActionSafetyAsync_AtomRejects_ReturnsDenied()
    {
        // Arrange
        var atom = new OuroborosAtom(TestInstanceId, SafetyConstraints.NoSelfDestruction);
        var safetyGuard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await safetyGuard.CheckActionSafetyAsync(
            "delete self from system",
            parameters,
            context: atom);

        // Assert
        result.IsAllowed.Should().BeFalse("action should be rejected by OuroborosAtom");
        result.Reason.Should().Contain("Ouroboros safety constraints");
        result.Violations.Should().Contain(v => v.Contains("OuroborosAtom"));
        result.RiskScore.Should().Be(1.0);
    }

    [Fact]
    public async Task CheckActionSafetyAsync_AtomAllowsMeTTaMark_ReturnsAllowed()
    {
        // Arrange
        var mettaEngine = new MockMeTTaEngineReturningMark();
        var atom = new OuroborosAtom(TestInstanceId, SafetyConstraints.NoSelfDestruction);
        var safetyGuard = new SafetyGuard(mettaEngine: mettaEngine);
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await safetyGuard.CheckActionSafetyAsync(
            "list files in directory",
            parameters,
            context: atom);

        // Assert
        result.IsAllowed.Should().BeTrue("MeTTa returned Mark (safe)");
        result.Reason.Should().Contain("symbolic reasoning");
        result.RiskScore.Should().BeLessThan(0.8);
    }

    [Fact]
    public async Task CheckActionSafetyAsync_AtomAllowsMeTTaVoid_ReturnsDenied()
    {
        // Arrange
        var mettaEngine = new MockMeTTaEngineReturningVoid();
        var atom = new OuroborosAtom(TestInstanceId, SafetyConstraints.NoSelfDestruction);
        var safetyGuard = new SafetyGuard(mettaEngine: mettaEngine);
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await safetyGuard.CheckActionSafetyAsync(
            "perform risky operation",
            parameters,
            context: atom);

        // Assert
        result.IsAllowed.Should().BeFalse("MeTTa returned Void (unsafe)");
        result.Reason.Should().Contain("symbolic safety rules");
        result.Violations.Should().Contain(v => v.Contains("MeTTa symbolic reasoning"));
        result.RiskScore.Should().BeGreaterOrEqualTo(0.9);
    }

    [Fact]
    public async Task CheckActionSafetyAsync_AtomAllowsMeTTaImaginary_ReturnsRequiresReview()
    {
        // Arrange
        var mettaEngine = new MockMeTTaEngineReturningImaginary();
        var atom = new OuroborosAtom(TestInstanceId, SafetyConstraints.NoSelfDestruction);
        var safetyGuard = new SafetyGuard(mettaEngine: mettaEngine);
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await safetyGuard.CheckActionSafetyAsync(
            "uncertain operation",
            parameters,
            context: atom);

        // Assert
        result.IsAllowed.Should().BeFalse("MeTTa returned Imaginary (uncertain, requires review)");
        result.Reason.Should().Contain("requires human review");
        result.Violations.Should().Contain(v => v.Contains("uncertain"));
        result.RiskScore.Should().BeGreaterOrEqualTo(0.6);
    }

    [Fact]
    public async Task CheckActionSafetyAsync_MeTTaUnavailable_FallbackToAtomOnly()
    {
        // Arrange - No MeTTa engine provided
        var atom = new OuroborosAtom(TestInstanceId, SafetyConstraints.NoSelfDestruction);
        var safetyGuard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await safetyGuard.CheckActionSafetyAsync(
            "read data from file",
            parameters,
            context: atom);

        // Assert
        result.IsAllowed.Should().BeTrue("action should be allowed with atom-only check");
        result.Reason.Should().Be("Action is safe to execute");
    }

    [Fact]
    public async Task CheckActionSafetyAsync_NoAtomContext_SkipsAtomCheck()
    {
        // Arrange - No atom context provided
        var mettaEngine = new MockMeTTaEngineReturningMark();
        var safetyGuard = new SafetyGuard(mettaEngine: mettaEngine);
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await safetyGuard.CheckActionSafetyAsync(
            "safe operation",
            parameters,
            context: null);

        // Assert
        result.IsAllowed.Should().BeTrue("action should pass MeTTa check");
        result.Reason.Should().Contain("symbolic reasoning");
    }

    [Fact]
    public async Task AddMeTTaSafetyRulesAsync_WithEngine_AddsRulesSuccessfully()
    {
        // Arrange
        var mettaEngine = new MockMeTTaEngineCapturingRules();
        var safetyGuard = new SafetyGuard(mettaEngine: mettaEngine);

        // Act
        var result = await safetyGuard.AddMeTTaSafetyRulesAsync(TestInstanceId);

        // Assert
        result.IsSuccess.Should().BeTrue("rules should be added successfully");
        mettaEngine.AppliedRules.Should().NotBeEmpty("at least one rule should be applied");
        mettaEngine.AppliedRules[0].Should().Contain("IsSafeAction");
        mettaEngine.AppliedRules[0].Should().Contain("NoSelfDestruction");
    }

    [Fact]
    public async Task AddMeTTaSafetyRulesAsync_NoEngine_SucceedsGracefully()
    {
        // Arrange - No MeTTa engine
        var safetyGuard = new SafetyGuard();

        // Act
        var result = await safetyGuard.AddMeTTaSafetyRulesAsync(TestInstanceId);

        // Assert
        result.IsSuccess.Should().BeTrue("should succeed even without engine");
    }

    [Fact]
    public async Task CheckActionSafetyAsync_MeTTaQueryFails_ReturnsImaginary()
    {
        // Arrange - MeTTa engine that returns errors
        var mettaEngine = new MockMeTTaEngineReturningError();
        var atom = new OuroborosAtom(TestInstanceId, SafetyConstraints.NoSelfDestruction);
        var safetyGuard = new SafetyGuard(mettaEngine: mettaEngine);
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await safetyGuard.CheckActionSafetyAsync(
            "some operation",
            parameters,
            context: atom);

        // Assert - When MeTTa fails, it should be treated as uncertain (Imaginary)
        result.IsAllowed.Should().BeFalse("failed query should be treated as uncertain");
        result.Reason.Should().Contain("requires human review");
    }

    /// <summary>
    /// Mock MeTTa engine that returns Mark (safe).
    /// </summary>
    private class MockMeTTaEngineReturningMark : IMeTTaEngine
    {
        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Mark"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock MeTTa engine that returns Void (unsafe).
    /// </summary>
    private class MockMeTTaEngineReturningVoid : IMeTTaEngine
    {
        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Void"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock MeTTa engine that returns Imaginary (uncertain).
    /// </summary>
    private class MockMeTTaEngineReturningImaginary : IMeTTaEngine
    {
        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("unknown result"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock MeTTa engine that returns errors.
    /// </summary>
    private class MockMeTTaEngineReturningError : IMeTTaEngine
    {
        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Failure("Query failed"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Rule applied"));

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }

    /// <summary>
    /// Mock MeTTa engine that captures applied rules for verification.
    /// </summary>
    private class MockMeTTaEngineCapturingRules : IMeTTaEngine
    {
        public List<string> AppliedRules { get; } = new List<string>();

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult(Result<string, string>.Success("Mark"));

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            AppliedRules.Add(rule);
            return Task.FromResult(Result<string, string>.Success("Rule applied"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
            => Task.FromResult(Result<bool, string>.Success(true));

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
            => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

        public void Dispose() { }
    }
}
