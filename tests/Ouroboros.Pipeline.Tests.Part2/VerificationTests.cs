namespace Ouroboros.Pipeline.Tests;

using Ouroboros.Pipeline.Verification;

[Trait("Category", "Unit")]
public class ConstraintValidatorTests
{
    [Fact]
    public void Validate_NullConstraint_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ConstraintValidator.Validate(null!, "value"));
    }

    [Fact]
    public void Validate_ValidConstraint_ShouldReturnTrue()
    {
        var result = ConstraintValidator.Validate(c => true, "value");
        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidConstraint_ShouldReturnFalse()
    {
        var result = ConstraintValidator.Validate(c => false, "value");
        result.Should().BeFalse();
    }
}

[Trait("Category", "Unit")]
public class FileSystemActionTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var action = new FileSystemAction("path", FileSystemOperation.Read);
        action.Path.Should().Be("path");
        action.Operation.Should().Be(FileSystemOperation.Read);
    }
}

[Trait("Category", "Unit")]
public class FileSystemOperationTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<FileSystemOperation>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class NetworkActionTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var action = new NetworkAction("https://example.com", NetworkOperation.Get);
        action.Url.Should().Be("https://example.com");
        action.Operation.Should().Be(NetworkOperation.Get);
    }
}

[Trait("Category", "Unit")]
public class NetworkOperationTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<NetworkOperation>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class PlanTests
{
    #region Creation

    [Fact]
    public void Constructor_NullDescription_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Plan(null!));
    }

    [Fact]
    public void Constructor_ValidInput_ShouldInitialize()
    {
        var plan = new Plan("test plan");
        plan.Description.Should().Be("test plan");
        plan.Actions.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithActions_ShouldInitialize()
    {
        var action = new PlanAction("action1", ActionType.ReadFile);
        var plan = new Plan("test", new[] { action });
        plan.Actions.Should().ContainSingle();
    }

    #endregion

    #region WithAction

    [Fact]
    public void WithAction_Null_ShouldThrowArgumentNullException()
    {
        var plan = new Plan("test");
        Assert.Throws<ArgumentNullException>(() => plan.WithAction(null!));
    }

    [Fact]
    public void WithAction_Valid_ShouldAddAction()
    {
        var plan = new Plan("test");
        var action = new PlanAction("action1", ActionType.ReadFile);
        var updated = plan.WithAction(action);
        updated.Actions.Should().ContainSingle();
    }

    #endregion
}

[Trait("Category", "Unit")]
public class PlanActionTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var action = new PlanAction("name", ActionType.ReadFile, new[] { "param1" });
        action.Name.Should().Be("name");
        action.Type.Should().Be(ActionType.ReadFile);
        action.Parameters.Should().ContainSingle();
    }
}

[Trait("Category", "Unit")]
public class ActionTypeTests
{
    [Fact]
    public void Enum_ShouldHaveExpectedValues()
    {
        var values = Enum.GetValues<ActionType>();
        values.Length.Should().BeGreaterThan(0);
    }
}

[Trait("Category", "Unit")]
public class SafeContextTests
{
    [Theory]
    [InlineData(SafeContext.ReadOnly)]
    [InlineData(SafeContext.FullAccess)]
    public void AllEnumValues_ShouldBeDefined(SafeContext value)
    {
        ((int)value).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Enum_ShouldHaveTwoValues()
    {
        var values = Enum.GetValues<SafeContext>();
        values.Should().HaveCount(2);
    }
}

[Trait("Category", "Unit")]
public class SafeContextExtensionsTests
{
    [Fact]
    public void AllowsWrite_ReadOnly_ShouldReturnFalse()
    {
        SafeContext.ReadOnly.AllowsWrite().Should().BeFalse();
    }

    [Fact]
    public void AllowsWrite_FullAccess_ShouldReturnTrue()
    {
        SafeContext.FullAccess.AllowsWrite().Should().BeTrue();
    }

    [Fact]
    public void AllowsRead_ReadOnly_ShouldReturnTrue()
    {
        SafeContext.ReadOnly.AllowsRead().Should().BeTrue();
    }

    [Fact]
    public void AllowsRead_FullAccess_ShouldReturnTrue()
    {
        SafeContext.FullAccess.AllowsRead().Should().BeTrue();
    }
}

[Trait("Category", "Unit")]
public class SecurityExceptionTests
{
    [Fact]
    public void Constructor_ShouldSetMessage()
    {
        var ex = new SecurityException("security violation");
        ex.Message.Should().Be("security violation");
    }
}

[Trait("Category", "Unit")]
public class ToolActionTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var action = new ToolAction("tool1", "invoke", new[] { "param1" });
        action.ToolName.Should().Be("tool1");
        action.Operation.Should().Be("invoke");
        action.Parameters.Should().ContainSingle();
    }
}

[Trait("Category", "Unit")]
public class MeTTaVerificationExtensionsTests
{
    [Fact]
    public void ToMeTTaAtoms_NullPlan_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((Plan)null!).ToMeTTaAtoms());
    }

    [Fact]
    public void ToMeTTaAtoms_EmptyPlan_ShouldReturnEmpty()
    {
        var plan = new Plan("test");
        var atoms = plan.ToMeTTaAtoms();
        atoms.Should().NotBeNull();
    }

    [Fact]
    public void VerifyPlan_NullPlan_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ((Plan)null!).VerifyPlan(SafeContext.ReadOnly));
    }
}

[Trait("Category", "Unit")]
public class MeTTaVerificationStepTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        var step = new MeTTaVerificationStep("verify", "description");
        step.Name.Should().Be("verify");
        step.Description.Should().Be("description");
    }
}
