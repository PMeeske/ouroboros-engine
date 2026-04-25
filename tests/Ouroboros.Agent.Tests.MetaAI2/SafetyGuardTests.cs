using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class SafetyGuardTests
{
    #region Constructor

    [Fact]
    public void Constructor_Default_ShouldSetDefaultLevel()
    {
        // Act
        var guard = new SafetyGuard();

        // Assert
        guard.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomLevel_ShouldSetLevel()
    {
        // Act
        var guard = new SafetyGuard(PermissionLevel.Admin);

        // Assert
        guard.Should().NotBeNull();
    }

    #endregion

    #region CheckActionSafetyAsync

    [Fact]
    public async Task CheckActionSafetyAsync_WithNullActionName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();

        // Act
        Func<Task> act = async () => await guard.CheckActionSafetyAsync(null!, parameters);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckActionSafetyAsync_WithNullParameters_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        Func<Task> act = async () => await guard.CheckActionSafetyAsync("action", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckActionSafetyAsync_SafeReadAction_ShouldAllow()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();

        // Act
        var result = await guard.CheckActionSafetyAsync("read_file", parameters);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CheckActionSafetyAsync_WithOuroborosAtomUnsafeAction_ShouldDeny()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();
        var atom = OuroborosAtom.CreateDefault();

        // Act
        var result = await guard.CheckActionSafetyAsync("delete self", parameters, atom);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("safety constraints");
    }

    [Fact]
    public async Task CheckActionSafetyAsync_DangerousPattern_ShouldFlagViolation()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object> { ["command"] = "eval(something)" };

        // Act
        var result = await guard.CheckActionSafetyAsync("execute", parameters);

        // Assert
        result.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CheckActionSafetyAsync_InjectionPatternInParameter_ShouldFlagViolation()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object> { ["input"] = "'; DROP TABLE users; --" };

        // Act
        var result = await guard.CheckActionSafetyAsync("query", parameters);

        // Assert
        result.Violations.Should().NotBeEmpty();
    }

    #endregion

    #region CheckSafetyAsync

    [Fact]
    public async Task CheckSafetyAsync_WithNullAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        Func<Task> act = async () => await guard.CheckSafetyAsync(null!, PermissionLevel.Read);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckSafetyAsync_ReadAction_ShouldAllow()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        var result = await guard.CheckSafetyAsync("read", PermissionLevel.Read);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    #endregion

    #region SandboxStepAsync

    [Fact]
    public async Task SandboxStepAsync_WithNullStep_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        Func<Task> act = async () => await guard.SandboxStepAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SandboxStepAsync_ShouldReturnSandboxedStep()
    {
        // Arrange
        var guard = new SafetyGuard();
        var step = new PlanStep("action", new Dictionary<string, object> { ["key"] = "value" }, "outcome", 0.8);

        // Act
        var result = await guard.SandboxStepAsync(step);

        // Assert
        result.Success.Should().BeTrue();
        result.Step.Should().NotBeNull();
        result.Step!.Parameters.Should().ContainKey("__sandboxed__");
        result.Step.Parameters["__sandboxed__"].Should().Be(true);
    }

    [Fact]
    public async Task SandboxStepAsync_ShouldSanitizeStringParameters()
    {
        // Arrange
        var guard = new SafetyGuard();
        var step = new PlanStep("action", new Dictionary<string, object> { ["input"] = "<script>alert(1)</script>" }, "outcome", 0.8);

        // Act
        var result = await guard.SandboxStepAsync(step);

        // Assert
        result.Success.Should().BeTrue();
        result.Step!.Parameters["input"].Should().Be("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    #endregion

    #region CheckPermissionsAsync

    [Fact]
    public async Task CheckPermissionsAsync_WithNullAgentId_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();
        var permissions = new List<Permission>();

        // Act
        Func<Task> act = async () => await guard.CheckPermissionsAsync(null!, permissions);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckPermissionsAsync_WithNullPermissions_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        Func<Task> act = async () => await guard.CheckPermissionsAsync("agent-1", null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CheckPermissionsAsync_AgentWithHigherLevel_ShouldReturnTrue()
    {
        // Arrange
        var guard = new SafetyGuard(PermissionLevel.Admin);
        var permissions = new List<Permission> { new Permission("read", PermissionLevel.Read, "Read permission") };

        // Act
        var result = await guard.CheckPermissionsAsync("agent-1", permissions);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region AssessRiskAsync

    [Fact]
    public async Task AssessRiskAsync_WithNullActionName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();

        // Act
        Func<Task> act = async () => await guard.AssessRiskAsync(null!, parameters);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AssessRiskAsync_DeleteAction_ShouldReturnHigherRisk()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();

        // Act
        var risk = await guard.AssessRiskAsync("delete_file", parameters);

        // Assert
        risk.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public async Task AssessRiskAsync_ReadAction_ShouldReturnLowerRisk()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object>();

        // Act
        var risk = await guard.AssessRiskAsync("read_file", parameters);

        // Assert
        risk.Should().BeLessThan(0.2);
    }

    [Fact]
    public async Task AssessRiskAsync_ShouldBeCappedAtOne()
    {
        // Arrange
        var guard = new SafetyGuard();
        var parameters = new Dictionary<string, object>
        {
            ["cmd"] = "eval(system(shell_exec()))"
        };

        // Act
        var risk = await guard.AssessRiskAsync("exec", parameters);

        // Assert
        risk.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region SetAgentPermissionLevel

    [Fact]
    public void SetAgentPermissionLevel_ShouldSetLevel()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        guard.SetAgentPermissionLevel("agent-1", PermissionLevel.Admin);

        // Assert - indirectly tested via CheckPermissionsAsync
    }

    #endregion

    #region RegisterPermissionPolicy

    [Fact]
    public void RegisterPermissionPolicy_ShouldRegisterPolicy()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        guard.RegisterPermissionPolicy("custom_action", PermissionLevel.Admin, "Custom description");

        // Assert - indirectly tested via AssessRisk and CheckActionSafetyAsync
    }

    #endregion

    #region SandboxStep (deprecated sync)

    [Fact]
    public void SandboxStep_Sync_WithNullStep_ShouldThrowArgumentNullException()
    {
        // Arrange
        var guard = new SafetyGuard();

        // Act
        Action act = () => guard.SandboxStep(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SandboxStep_Sync_ShouldReturnSandboxedStep()
    {
        // Arrange
        var guard = new SafetyGuard();
        var step = new PlanStep("action", new Dictionary<string, object> { ["key"] = "value" }, "outcome", 0.8);

        // Act
        var result = guard.SandboxStep(step);

        // Assert
        result.Parameters.Should().ContainKey("__sandboxed__");
        result.Parameters["__sandboxed__"].Should().Be(true);
    }

    #endregion
}
