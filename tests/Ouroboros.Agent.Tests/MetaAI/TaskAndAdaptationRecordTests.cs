using FluentAssertions;
using Ouroboros.Agent.MetaAI;
using Xunit;
using PlanStep = Ouroboros.Agent.PlanStep;
using Plan = Ouroboros.Agent.MetaAI.Plan;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class AbstractTaskTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var preconditions = new List<string> { "has-input", "is-valid" };
        var decompositions = new List<TaskDecomposition>
        {
            new("analyze", new List<string> { "parse", "validate" }, new List<string> { "parse < validate" })
        };

        var task = new AbstractTask("BuildSystem", preconditions, decompositions);

        task.Name.Should().Be("BuildSystem");
        task.Preconditions.Should().HaveCount(2);
        task.Preconditions.Should().Contain("has-input");
        task.PossibleDecompositions.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithEmptyCollections_ShouldWork()
    {
        var task = new AbstractTask("Simple", new List<string>(), new List<TaskDecomposition>());

        task.Name.Should().Be("Simple");
        task.Preconditions.Should().BeEmpty();
        task.PossibleDecompositions.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_SameCollectionRef_ShouldBeEqual()
    {
        var preconditions = new List<string> { "a" };
        var decompositions = new List<TaskDecomposition>();
        var a = new AbstractTask("T", preconditions, decompositions);
        var b = new AbstractTask("T", preconditions, decompositions);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class AdaptationActionTests
{
    [Fact]
    public void Create_WithMinimalProperties_ShouldSetDefaults()
    {
        var action = new AdaptationAction(AdaptationStrategy.Retry, "Temporary failure");

        action.Strategy.Should().Be(AdaptationStrategy.Retry);
        action.Reason.Should().Be("Temporary failure");
        action.RevisedPlan.Should().BeNull();
        action.ReplacementStep.Should().BeNull();
    }

    [Fact]
    public void Create_WithRevisedPlan_ShouldSetIt()
    {
        var plan = new Plan(
            "revised goal",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var action = new AdaptationAction(
            AdaptationStrategy.Replan,
            "Plan needs revision",
            RevisedPlan: plan);

        action.Strategy.Should().Be(AdaptationStrategy.Replan);
        action.RevisedPlan.Should().NotBeNull();
        action.RevisedPlan!.Goal.Should().Be("revised goal");
    }

    [Fact]
    public void Create_WithReplacementStep_ShouldSetIt()
    {
        var step = new PlanStep("new-step", new Dictionary<string, object>(), "better outcome", 0.9);

        var action = new AdaptationAction(
            AdaptationStrategy.ReplaceStep,
            "Step failed",
            ReplacementStep: step);

        action.ReplacementStep.Should().NotBeNull();
        action.ReplacementStep!.Action.Should().Be("new-step");
    }
}

[Trait("Category", "Unit")]
public class AdaptationTriggerTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        Func<PlanExecutionContext, bool> condition = ctx => ctx.CurrentStepIndex > 5;

        var trigger = new AdaptationTrigger("high-index", condition, AdaptationStrategy.Abort);

        trigger.Name.Should().Be("high-index");
        trigger.Condition.Should().BeSameAs(condition);
        trigger.Strategy.Should().Be(AdaptationStrategy.Abort);
    }
}

[Trait("Category", "Unit")]
public class ApprovalRequestTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var parameters = new Dictionary<string, object>
        {
            ["target"] = "production",
            ["count"] = 5
        };

        var request = new ApprovalRequest("req-1", "deploy", parameters, "Need to ship hotfix", now);

        request.RequestId.Should().Be("req-1");
        request.Action.Should().Be("deploy");
        request.Parameters.Should().HaveCount(2);
        request.Parameters["target"].Should().Be("production");
        request.Rationale.Should().Be("Need to ship hotfix");
        request.RequestedAt.Should().Be(now);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var now = DateTime.UtcNow;
        var p = new Dictionary<string, object> { ["k"] = "v" };
        var a = new ApprovalRequest("r1", "act", p, "reason", now);
        var b = new ApprovalRequest("r1", "act", p, "reason", now);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class ApprovalResponseTests
{
    [Fact]
    public void Create_Approved_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var response = new ApprovalResponse("req-1", true, null, null, now);

        response.RequestId.Should().Be("req-1");
        response.Approved.Should().BeTrue();
        response.Reason.Should().BeNull();
        response.Modifications.Should().BeNull();
        response.RespondedAt.Should().Be(now);
    }

    [Fact]
    public void Create_Rejected_WithReason_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var mods = new Dictionary<string, object> { ["target"] = "staging" };
        var response = new ApprovalResponse("req-1", false, "Not ready", mods, now);

        response.Approved.Should().BeFalse();
        response.Reason.Should().Be("Not ready");
        response.Modifications.Should().HaveCount(1);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var now = DateTime.UtcNow;
        var a = new ApprovalResponse("r1", true, null, null, now);
        var b = new ApprovalResponse("r1", true, null, null, now);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class TaskDecompositionTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var subtasks = new List<string> { "parse", "validate", "transform" };
        var constraints = new List<string> { "parse < validate", "validate < transform" };

        var decomposition = new TaskDecomposition("process", subtasks, constraints);

        decomposition.AbstractTask.Should().Be("process");
        decomposition.SubTasks.Should().HaveCount(3);
        decomposition.OrderingConstraints.Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithEmptyCollections_ShouldWork()
    {
        var decomposition = new TaskDecomposition("simple", new List<string>(), new List<string>());

        decomposition.SubTasks.Should().BeEmpty();
        decomposition.OrderingConstraints.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameReferences_ShouldBeEqual()
    {
        var subtasks = new List<string> { "a" };
        var constraints = new List<string>();
        var a = new TaskDecomposition("t", subtasks, constraints);
        var b = new TaskDecomposition("t", subtasks, constraints);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class ConcretePlanTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var steps = new List<string> { "step1", "step2", "step3" };
        var plan = new ConcretePlan("BuildSystem", steps);

        plan.AbstractTaskName.Should().Be("BuildSystem");
        plan.ConcreteSteps.Should().HaveCount(3);
        plan.ConcreteSteps[0].Should().Be("step1");
    }

    [Fact]
    public void Create_WithEmptySteps_ShouldWork()
    {
        var plan = new ConcretePlan("NoOp", new List<string>());
        plan.ConcreteSteps.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameReferences_ShouldBeEqual()
    {
        var steps = new List<string> { "a" };
        var a = new ConcretePlan("T", steps);
        var b = new ConcretePlan("T", steps);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class TaskAssignmentTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var step = new PlanStep("analyze", new Dictionary<string, object>(), "results", 0.85);
        var assignment = new TaskAssignment("task-1", "agent-A", step, now, TaskAssignmentStatus.InProgress);

        assignment.TaskId.Should().Be("task-1");
        assignment.AgentId.Should().Be("agent-A");
        assignment.Step.Action.Should().Be("analyze");
        assignment.AssignedAt.Should().Be(now);
        assignment.Status.Should().Be(TaskAssignmentStatus.InProgress);
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        var now = DateTime.UtcNow;
        var step = new PlanStep("a", new Dictionary<string, object>(), "o", 0.5);
        var a = new TaskAssignment("t1", "ag1", step, now, TaskAssignmentStatus.Pending);
        var b = new TaskAssignment("t1", "ag1", step, now, TaskAssignmentStatus.Pending);
        a.Should().Be(b);
    }
}

[Trait("Category", "Unit")]
public class SubIssueAssignmentTests
{
    [Fact]
    public void Create_WithMinimalProperties_ShouldSetDefaults()
    {
        var now = DateTime.UtcNow;
        var assignment = new SubIssueAssignment(
            IssueNumber: 42,
            Title: "Fix login bug",
            Description: "Users cannot login",
            AssignedAgentId: "agent-1",
            BranchName: "epic/42-fix-login",
            Branch: null,
            Status: SubIssueStatus.Pending,
            CreatedAt: now);

        assignment.IssueNumber.Should().Be(42);
        assignment.Title.Should().Be("Fix login bug");
        assignment.Description.Should().Be("Users cannot login");
        assignment.AssignedAgentId.Should().Be("agent-1");
        assignment.BranchName.Should().Be("epic/42-fix-login");
        assignment.Branch.Should().BeNull();
        assignment.Status.Should().Be(SubIssueStatus.Pending);
        assignment.CreatedAt.Should().Be(now);
        assignment.CompletedAt.Should().BeNull();
        assignment.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Create_WithOptionalProperties_ShouldSetThem()
    {
        var now = DateTime.UtcNow;
        var completed = now.AddHours(2);
        var assignment = new SubIssueAssignment(
            IssueNumber: 43,
            Title: "Failed task",
            Description: "desc",
            AssignedAgentId: "agent-2",
            BranchName: "epic/43",
            Branch: null,
            Status: SubIssueStatus.Failed,
            CreatedAt: now,
            CompletedAt: completed,
            ErrorMessage: "Timeout");

        assignment.CompletedAt.Should().Be(completed);
        assignment.ErrorMessage.Should().Be("Timeout");
        assignment.Status.Should().Be(SubIssueStatus.Failed);
    }
}
