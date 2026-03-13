// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.PlanStep;

namespace Ouroboros.Tests.MetaAI;

/// <summary>
/// Complex logic tests for HierarchicalPlanner covering plan decomposition,
/// HTN planning, temporal constraints, plan repair strategies, and explanation generation.
/// </summary>
[Trait("Category", "Unit")]
public class HierarchicalPlannerTests
{
    private readonly Mock<IMetaAIPlannerOrchestrator> _mockOrchestrator;
    private readonly Mock<IChatCompletionModel> _mockLlm;
    private readonly HierarchicalPlanner _sut;

    public HierarchicalPlannerTests()
    {
        _mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        _mockLlm = new Mock<IChatCompletionModel>();
        _sut = new HierarchicalPlanner(_mockOrchestrator.Object, _mockLlm.Object);
    }

    // ================================================================
    // Constructor guard clauses
    // ================================================================

    [Fact]
    public void Constructor_NullOrchestrator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new HierarchicalPlanner(null!, _mockLlm.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("orchestrator");
    }

    [Fact]
    public void Constructor_NullLlm_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new HierarchicalPlanner(_mockOrchestrator.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("llm");
    }

    // ================================================================
    // CreateHierarchicalPlanAsync - input validation
    // ================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateHierarchicalPlanAsync_EmptyGoal_ReturnsFailure(string? goal)
    {
        // Act
        var result = await _sut.CreateHierarchicalPlanAsync(goal!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Goal cannot be empty");
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_MaxDepthLessThanOne_ReturnsFailure()
    {
        // Arrange
        var config = new HierarchicalPlanningConfig(MaxDepth: 0);

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("test goal", config: config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("MaxDepth must be at least 1");
    }

    // ================================================================
    // CreateHierarchicalPlanAsync - planning logic
    // ================================================================

    [Fact]
    public async Task CreateHierarchicalPlanAsync_OrchestratorPlanFails_PropagatesFailure()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Failure("orchestrator error"));

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("build a house");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("orchestrator error");
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_FewSteps_NoDecomposition()
    {
        // Arrange: plan has 2 steps, below default MinStepsForDecomposition (3)
        var topPlan = CreatePlan("goal", 2, confidenceScore: 0.9);
        _mockOrchestrator
            .Setup(o => o.PlanAsync("build a house", It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(topPlan));

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("build a house");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubPlans.Should().BeEmpty("too few steps for decomposition");
        result.Value.TopLevelPlan.Steps.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_ComplexSteps_DecomposesIntoSubPlans()
    {
        // Arrange: plan with 3 steps, one with low confidence (triggers decomposition)
        var steps = new List<PlanStep>
        {
            new("step_a", new Dictionary<string, object>(), "outcome_a", 0.9),
            new("step_b", new Dictionary<string, object>(), "outcome_b", 0.5), // low confidence
            new("step_c", new Dictionary<string, object>(), "outcome_c", 0.9),
        };
        var topPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        var subPlan = CreatePlan("sub", 2, confidenceScore: 0.9);

        int planCallCount = 0;
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .Returns<string, Dictionary<string, object>?, CancellationToken>((goal, ctx, ct) =>
            {
                planCallCount++;
                if (planCallCount == 1)
                    return Task.FromResult(Result<Plan, string>.Success(topPlan));
                return Task.FromResult(Result<Plan, string>.Success(subPlan));
            });

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("goal", config: new HierarchicalPlanningConfig(
            MaxDepth: 2, MinStepsForDecomposition: 3, ComplexityThreshold: 0.7));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubPlans.Should().ContainKey("step_b", "step_b has confidence 0.5 < threshold 0.7");
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_StepWithManyParameters_DecomposedRegardlessOfConfidence()
    {
        // Arrange: step has >3 parameters which triggers IsComplexStep
        var manyParams = new Dictionary<string, object>
        {
            ["p1"] = "v1", ["p2"] = "v2", ["p3"] = "v3", ["p4"] = "v4"
        };
        var steps = new List<PlanStep>
        {
            new("step_a", new Dictionary<string, object>(), "outcome_a", 0.95),
            new("complex_step", manyParams, "outcome_complex", 0.95), // high confidence but many params
            new("step_c", new Dictionary<string, object>(), "outcome_c", 0.95),
        };
        var topPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);
        var subPlan = CreatePlan("sub", 2, confidenceScore: 0.95);

        int callCount = 0;
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .Returns<string, Dictionary<string, object>?, CancellationToken>((g, c, ct) =>
            {
                callCount++;
                return Task.FromResult(Result<Plan, string>.Success(callCount == 1 ? topPlan : subPlan));
            });

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("goal", config: new HierarchicalPlanningConfig(
            MaxDepth: 2, MinStepsForDecomposition: 3, ComplexityThreshold: 0.7));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubPlans.Should().ContainKey("complex_step");
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_MaxDepthOne_NoRecursiveDecomposition()
    {
        // Arrange: even with complex steps, MaxDepth=1 prevents decomposition
        var steps = new List<PlanStep>
        {
            new("step_a", new Dictionary<string, object>(), "a", 0.3),
            new("step_b", new Dictionary<string, object>(), "b", 0.3),
            new("step_c", new Dictionary<string, object>(), "c", 0.3),
        };
        var topPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(topPlan));

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("goal", config: new HierarchicalPlanningConfig(MaxDepth: 1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        // MaxDepth 1 means currentDepth starts at 1 and hits >= MaxDepth immediately
        result.Value.SubPlans.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_SubPlanFailure_SkipsDecomposition()
    {
        // Arrange: top-level plan succeeds but sub-plan fails
        var steps = new List<PlanStep>
        {
            new("step_a", new Dictionary<string, object>(), "a", 0.3),
            new("step_b", new Dictionary<string, object>(), "b", 0.3),
            new("step_c", new Dictionary<string, object>(), "c", 0.3),
        };
        var topPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        int callCount = 0;
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .Returns<string, Dictionary<string, object>?, CancellationToken>((g, c, ct) =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(Result<Plan, string>.Success(topPlan));
                return Task.FromResult(Result<Plan, string>.Failure("sub-planning failed"));
            });

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("goal", config: new HierarchicalPlanningConfig(
            MaxDepth: 3, MinStepsForDecomposition: 3, ComplexityThreshold: 0.7));

        // Assert
        result.IsSuccess.Should().BeTrue("top-level plan is still valid even if decomposition fails");
        result.Value.SubPlans.Should().BeEmpty("sub-plan failures are silently skipped");
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_OrchestratorThrows_ReturnsFailure()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unexpected error"));

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("goal");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Hierarchical planning failed");
        result.Error.Should().Contain("unexpected error");
    }

    [Fact]
    public async Task CreateHierarchicalPlanAsync_CancellationRequested_ReturnsFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .Returns<string, Dictionary<string, object>?, CancellationToken>((g, c, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(Result<Plan, string>.Success(CreatePlan("g", 1)));
            });

        // Act
        var result = await _sut.CreateHierarchicalPlanAsync("goal", ct: cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Hierarchical planning failed");
    }

    // ================================================================
    // ExecuteHierarchicalAsync - plan expansion and execution
    // ================================================================

    [Fact]
    public async Task ExecuteHierarchicalAsync_NullPlan_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.ExecuteHierarchicalAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteHierarchicalAsync_ExpandsSubPlansIntoTopLevel()
    {
        // Arrange: top-level has steps A, B, C; B has a sub-plan with B1, B2
        var topSteps = new List<PlanStep>
        {
            new("A", new Dictionary<string, object>(), "a_out", 0.9),
            new("B", new Dictionary<string, object>(), "b_out", 0.5),
            new("C", new Dictionary<string, object>(), "c_out", 0.9),
        };
        var topPlan = new Plan("goal", topSteps, new Dictionary<string, double>(), DateTime.UtcNow);

        var subBSteps = new List<PlanStep>
        {
            new("B1", new Dictionary<string, object>(), "b1_out", 0.8),
            new("B2", new Dictionary<string, object>(), "b2_out", 0.8),
        };
        var subBPlan = new Plan("sub_b", subBSteps, new Dictionary<string, double>(), DateTime.UtcNow);

        var subPlans = new Dictionary<string, Plan> { ["B"] = subBPlan };
        var hierarchicalPlan = new HierarchicalPlan("goal", topPlan, subPlans, MaxDepth: 2, CreatedAt: DateTime.UtcNow);

        Plan? capturedExpandedPlan = null;
        _mockOrchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Callback<Plan, CancellationToken>((p, ct) => capturedExpandedPlan = p)
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(
                CreateExecutionResult(true, 4)));

        // Act
        var result = await _sut.ExecuteHierarchicalAsync(hierarchicalPlan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedExpandedPlan.Should().NotBeNull();
        // Expanded plan should have A, B1, B2, C (B replaced by sub-plan steps)
        capturedExpandedPlan!.Steps.Select(s => s.Action).Should()
            .ContainInOrder("A", "B1", "B2", "C");
    }

    [Fact]
    public async Task ExecuteHierarchicalAsync_NoSubPlans_KeepsOriginalSteps()
    {
        // Arrange
        var topSteps = new List<PlanStep>
        {
            new("X", new Dictionary<string, object>(), "x", 0.9),
            new("Y", new Dictionary<string, object>(), "y", 0.9),
        };
        var topPlan = new Plan("goal", topSteps, new Dictionary<string, double>(), DateTime.UtcNow);
        var hierarchicalPlan = new HierarchicalPlan("goal", topPlan, new Dictionary<string, Plan>(), 1, DateTime.UtcNow);

        Plan? capturedPlan = null;
        _mockOrchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .Callback<Plan, CancellationToken>((p, ct) => capturedPlan = p)
            .ReturnsAsync(Result<PlanExecutionResult, string>.Success(CreateExecutionResult(true, 2)));

        // Act
        await _sut.ExecuteHierarchicalAsync(hierarchicalPlan);

        // Assert
        capturedPlan!.Steps.Select(s => s.Action).Should().ContainInOrder("X", "Y");
    }

    [Fact]
    public async Task ExecuteHierarchicalAsync_ExecutionFails_PropagatesFailure()
    {
        // Arrange
        var topPlan = new Plan("goal", new List<PlanStep>
        {
            new("A", new Dictionary<string, object>(), "a", 0.9),
        }, new Dictionary<string, double>(), DateTime.UtcNow);
        var plan = new HierarchicalPlan("goal", topPlan, new Dictionary<string, Plan>(), 1, DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PlanExecutionResult, string>.Failure("execution error"));

        // Act
        var result = await _sut.ExecuteHierarchicalAsync(plan);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("execution error");
    }

    [Fact]
    public async Task ExecuteHierarchicalAsync_CancellationBeforeExecution_ReturnsFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var topPlan = new Plan("goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
        var plan = new HierarchicalPlan("goal", topPlan, new Dictionary<string, Plan>(), 1, DateTime.UtcNow);

        // Act
        var result = await _sut.ExecuteHierarchicalAsync(plan, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Hierarchical execution failed");
    }

    // ================================================================
    // PlanHierarchicalAsync (HTN) - task network decomposition
    // ================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlanHierarchicalAsync_EmptyGoal_ReturnsFailure(string? goal)
    {
        // Arrange
        var network = new Dictionary<string, TaskDecomposition>
        {
            ["d1"] = new("root", new List<string> { "a" }, new List<string>())
        };

        // Act
        var result = await _sut.PlanHierarchicalAsync(goal!, network);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Goal cannot be empty");
    }

    [Fact]
    public async Task PlanHierarchicalAsync_NullTaskNetwork_ReturnsFailure()
    {
        // Act
        var result = await _sut.PlanHierarchicalAsync("goal", null!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task network cannot be empty");
    }

    [Fact]
    public async Task PlanHierarchicalAsync_EmptyTaskNetwork_ReturnsFailure()
    {
        // Act
        var result = await _sut.PlanHierarchicalAsync("goal", new Dictionary<string, TaskDecomposition>());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Task network cannot be empty");
    }

    [Fact]
    public async Task PlanHierarchicalAsync_ValidNetwork_BuildsAbstractTaskHierarchy()
    {
        // Arrange: goal -> {prepare, execute}; prepare -> {setup, configure}
        var network = new Dictionary<string, TaskDecomposition>
        {
            ["d1"] = new("deploy_app", new List<string> { "prepare", "execute" }, new List<string>()),
            ["d2"] = new("prepare", new List<string> { "setup", "configure" }, new List<string>()),
        };

        // Act
        var result = await _sut.PlanHierarchicalAsync("deploy_app", network);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;
        plan.Goal.Should().Be("deploy_app");
        plan.AbstractTasks.Should().Contain(t => t.Name == "deploy_app");
        plan.AbstractTasks.Should().Contain(t => t.Name == "prepare");
    }

    [Fact]
    public async Task PlanHierarchicalAsync_CyclicDecompositions_HandledGracefully()
    {
        // Arrange: A -> B, B -> A (cycle)
        var network = new Dictionary<string, TaskDecomposition>
        {
            ["d1"] = new("A", new List<string> { "B" }, new List<string>()),
            ["d2"] = new("B", new List<string> { "A" }, new List<string>()),
        };

        // Act
        var result = await _sut.PlanHierarchicalAsync("A", network);

        // Assert: should not infinite loop, visited set breaks cycles
        result.IsSuccess.Should().BeTrue();
        result.Value.AbstractTasks.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task PlanHierarchicalAsync_PrimitiveSubTasks_IncludedInRefinements()
    {
        // Arrange: goal -> {primitive1, primitive2} where primitives are not in network
        var network = new Dictionary<string, TaskDecomposition>
        {
            ["d1"] = new("build_app", new List<string> { "compile", "link" }, new List<string>()),
        };

        // Act
        var result = await _sut.PlanHierarchicalAsync("build_app", network);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var refinement = result.Value.Refinements.FirstOrDefault(r => r.AbstractTaskName == "build_app");
        refinement.Should().NotBeNull();
        // compile and link are primitive (not in abstractTasks), so added directly
        refinement!.ConcreteSteps.Should().Contain("compile");
        refinement.ConcreteSteps.Should().Contain("link");
    }

    [Fact]
    public async Task PlanHierarchicalAsync_Cancellation_ReturnsCancelledFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var network = new Dictionary<string, TaskDecomposition>
        {
            ["d1"] = new("goal", new List<string> { "a" }, new List<string>()),
        };

        // Act
        var result = await _sut.PlanHierarchicalAsync("goal", network, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("HTN planning was cancelled");
    }

    // ================================================================
    // PlanWithConstraintsAsync - temporal planning
    // ================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PlanWithConstraintsAsync_EmptyGoal_ReturnsFailure(string? goal)
    {
        // Act
        var result = await _sut.PlanWithConstraintsAsync(goal!, new List<TemporalConstraint>());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Goal cannot be empty");
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_OrchestratorFails_PropagatesError()
    {
        // Arrange
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Failure("plan failed"));

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", new List<TemporalConstraint>());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("plan failed");
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_BeforeConstraint_TaskBScheduledAfterTaskA()
    {
        // Arrange
        var steps = new List<PlanStep>
        {
            new("setup", new Dictionary<string, object>(), "setup done", 0.9),
            new("deploy", new Dictionary<string, object>(), "deployed", 0.9),
        };
        var plan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        var constraints = new List<TemporalConstraint>
        {
            new("setup", "deploy", TemporalRelation.Before),
        };

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", constraints);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var setupTask = result.Value.Tasks.First(t => t.Name == "setup");
        var deployTask = result.Value.Tasks.First(t => t.Name == "deploy");
        deployTask.StartTime.Should().BeOnOrAfter(setupTask.EndTime);
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_AfterConstraint_TaskAScheduledAfterTaskB()
    {
        // Arrange
        var steps = new List<PlanStep>
        {
            new("cleanup", new Dictionary<string, object>(), "cleanup done", 0.9),
            new("deploy", new Dictionary<string, object>(), "deployed", 0.9),
        };
        var plan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        // cleanup is "After" deploy -> cleanup depends on deploy
        var constraints = new List<TemporalConstraint>
        {
            new("cleanup", "deploy", TemporalRelation.After),
        };

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", constraints);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var cleanupTask = result.Value.Tasks.First(t => t.Name == "cleanup");
        var deployTask = result.Value.Tasks.First(t => t.Name == "deploy");
        cleanupTask.StartTime.Should().BeOnOrAfter(deployTask.EndTime);
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_MustFinishBefore_CreatesCorrectDependency()
    {
        // Arrange
        var steps = new List<PlanStep>
        {
            new("build", new Dictionary<string, object>(), "built", 0.9),
            new("test", new Dictionary<string, object>(), "tested", 0.9),
        };
        var plan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        var constraints = new List<TemporalConstraint>
        {
            new("build", "test", TemporalRelation.MustFinishBefore),
        };

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", constraints);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var testTask = result.Value.Tasks.First(t => t.Name == "test");
        testTask.Dependencies.Should().Contain("build");
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_DurationConstraint_RespectsDuration()
    {
        // Arrange
        var steps = new List<PlanStep>
        {
            new("long_task", new Dictionary<string, object>(), "done", 0.9),
        };
        var plan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        var customDuration = TimeSpan.FromMinutes(30);
        var constraints = new List<TemporalConstraint>
        {
            new("long_task", "", TemporalRelation.During, customDuration),
        };

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", constraints);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var task = result.Value.Tasks.First(t => t.Name == "long_task");
        (task.EndTime - task.StartTime).Should().Be(customDuration);
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_NullConstraints_TreatedAsEmpty()
    {
        // Arrange
        var steps = new List<PlanStep>
        {
            new("only_step", new Dictionary<string, object>(), "done", 0.9),
        };
        var plan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", null!);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Tasks.Should().HaveCount(1);
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_ChainedDependencies_ScheduledInCorrectOrder()
    {
        // Arrange: A -> B -> C
        var steps = new List<PlanStep>
        {
            new("A", new Dictionary<string, object>(), "a", 0.9),
            new("B", new Dictionary<string, object>(), "b", 0.9),
            new("C", new Dictionary<string, object>(), "c", 0.9),
        };
        var plan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(plan));

        var constraints = new List<TemporalConstraint>
        {
            new("A", "B", TemporalRelation.Before),
            new("B", "C", TemporalRelation.Before),
        };

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", constraints);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var tasks = result.Value.Tasks;
        var taskA = tasks.First(t => t.Name == "A");
        var taskB = tasks.First(t => t.Name == "B");
        var taskC = tasks.First(t => t.Name == "C");

        taskB.StartTime.Should().BeOnOrAfter(taskA.EndTime);
        taskC.StartTime.Should().BeOnOrAfter(taskB.EndTime);
    }

    [Fact]
    public async Task PlanWithConstraintsAsync_Cancellation_ReturnsCancelledFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _sut.PlanWithConstraintsAsync("goal", new List<TemporalConstraint>(), cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Temporal planning was cancelled");
    }

    // ================================================================
    // RepairPlanAsync - plan repair strategies
    // ================================================================

    [Fact]
    public async Task RepairPlanAsync_NullBrokenPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var trace = new ExecutionTrace(new List<ExecutedStep>(), 0, "fail");

        // Act
        var act = () => _sut.RepairPlanAsync(null!, trace, RepairStrategy.Patch);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RepairPlanAsync_NullTrace_ThrowsArgumentNullException()
    {
        // Arrange
        var plan = CreatePlan("goal", 2);

        // Act
        var act = () => _sut.RepairPlanAsync(plan, null!, RepairStrategy.Patch);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RepairPlanAsync_ReplanStrategy_CreatesNewPlanFromOrchestrator()
    {
        // Arrange
        var brokenPlan = CreatePlan("goal", 3);
        var trace = new ExecutionTrace(
            new List<ExecutedStep>
            {
                new("step_0", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
                new("step_1", false, TimeSpan.FromSeconds(2), new Dictionary<string, object>()),
            },
            FailedAtIndex: 1,
            FailureReason: "step_1 timed out");

        var newPlan = CreatePlan("goal", 4);
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(newPlan));

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.Replan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCount(4, "replan creates an entirely new plan");
    }

    [Fact]
    public async Task RepairPlanAsync_ReplanStrategy_IncludesContextAboutFailure()
    {
        // Arrange
        var brokenPlan = CreatePlan("goal", 3);
        var trace = new ExecutionTrace(
            new List<ExecutedStep>
            {
                new("step_0", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
                new("step_1", false, TimeSpan.FromSeconds(2), new Dictionary<string, object>()),
            },
            FailedAtIndex: 1,
            FailureReason: "timeout");

        Dictionary<string, object>? capturedContext = null;
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, Dictionary<string, object>?, CancellationToken>((g, ctx, ct) => capturedContext = ctx)
            .ReturnsAsync(Result<Plan, string>.Success(CreatePlan("goal", 2)));

        // Act
        await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.Replan);

        // Assert
        capturedContext.Should().NotBeNull();
        capturedContext!.Should().ContainKey("failure_reason");
        capturedContext["failure_reason"].Should().Be("timeout");
        capturedContext.Should().ContainKey("failed_step");
        capturedContext["failed_step"].Should().Be("step_1");
    }

    [Fact]
    public async Task RepairPlanAsync_PatchStrategy_ReplacesFailedStepWithAlternative()
    {
        // Arrange
        var steps = new List<PlanStep>
        {
            new("fetch_data", new Dictionary<string, object>(), "data fetched", 0.9),
            new("transform", new Dictionary<string, object> { ["format"] = "json" }, "transformed", 0.8),
            new("save", new Dictionary<string, object>(), "saved", 0.9),
        };
        var brokenPlan = new Plan("process_data", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        var trace = new ExecutionTrace(
            new List<ExecutedStep>
            {
                new("fetch_data", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
                new("transform", false, TimeSpan.FromSeconds(2), new Dictionary<string, object>()),
            },
            FailedAtIndex: 1,
            FailureReason: "transform error");

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.Patch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var repairedSteps = result.Value.Steps;
        repairedSteps.Should().HaveCount(3);
        repairedSteps[0].Action.Should().Be("fetch_data", "steps before failure kept");
        repairedSteps[1].Action.Should().Be("transform_alt", "failed step replaced with alt");
        repairedSteps[1].Parameters.Should().ContainKey("retry").WhoseValue.Should().Be(true);
        repairedSteps[1].ConfidenceScore.Should().BeApproximately(0.64, 0.01, "patched step has 80% confidence");
        repairedSteps[2].Action.Should().Be("save", "steps after failure kept");
    }

    [Fact]
    public async Task RepairPlanAsync_PatchStrategy_FailedAtLastStep_PatchesCorrectly()
    {
        // Arrange
        var steps = new List<PlanStep>
        {
            new("a", new Dictionary<string, object>(), "a_out", 0.9),
            new("b", new Dictionary<string, object>(), "b_out", 0.7),
        };
        var brokenPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        var trace = new ExecutionTrace(
            new List<ExecutedStep>
            {
                new("a", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
                new("b", false, TimeSpan.FromSeconds(2), new Dictionary<string, object>()),
            },
            FailedAtIndex: 1,
            FailureReason: "b failed");

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.Patch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Steps.Should().HaveCount(2);
        result.Value.Steps[1].Action.Should().Be("b_alt");
    }

    [Fact]
    public async Task RepairPlanAsync_CaseBasedStrategy_MultipleFailures_DelegatesToReplan()
    {
        // Arrange: multiple failures -> structural issue -> delegates to replan
        var brokenPlan = CreatePlan("goal", 4);
        var trace = new ExecutionTrace(
            new List<ExecutedStep>
            {
                new("step_0", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
                new("step_1", false, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
                new("step_2", false, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
            },
            FailedAtIndex: 2,
            FailureReason: "multiple errors");

        var newPlan = CreatePlan("goal_replan", 3);
        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(newPlan));

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.CaseBased);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Should have called PlanAsync (replan path) since multiple failures
        _mockOrchestrator.Verify(
            o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RepairPlanAsync_CaseBasedStrategy_SingleFailure_DelegatesToPatch()
    {
        // Arrange: single failure -> try patch
        var steps = new List<PlanStep>
        {
            new("step_0", new Dictionary<string, object>(), "o0", 0.9),
            new("step_1", new Dictionary<string, object>(), "o1", 0.8),
            new("step_2", new Dictionary<string, object>(), "o2", 0.9),
        };
        var brokenPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);
        var trace = new ExecutionTrace(
            new List<ExecutedStep>
            {
                new("step_0", true, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
                new("step_1", false, TimeSpan.FromSeconds(1), new Dictionary<string, object>()),
            },
            FailedAtIndex: 1,
            FailureReason: "single error");

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.CaseBased);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Patch path: should have _alt suffix on the failed step
        result.Value.Steps.Should().Contain(s => s.Action == "step_1_alt");
        // PlanAsync should NOT have been called (patch doesn't call orchestrator)
        _mockOrchestrator.Verify(
            o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RepairPlanAsync_BacktrackStrategy_BacktracksToCheckpoint()
    {
        // Arrange: 6 steps, fails at index 5, checkpoint = max(0, 5-3) = 2
        var steps = Enumerable.Range(0, 6)
            .Select(i => new PlanStep($"step_{i}", new Dictionary<string, object>(), $"out_{i}", 0.8))
            .ToList();
        var brokenPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        var trace = new ExecutionTrace(
            Enumerable.Range(0, 6).Select(i =>
                new ExecutedStep($"step_{i}", i != 5, TimeSpan.FromSeconds(1), new Dictionary<string, object>()))
                .ToList(),
            FailedAtIndex: 5,
            FailureReason: "step_5 failed");

        var alternativeSteps = new List<PlanStep>
        {
            new("alt_step_a", new Dictionary<string, object>(), "alt_out", 0.9),
            new("alt_step_b", new Dictionary<string, object>(), "alt_out", 0.9),
        };
        var alternativePlan = new Plan("alt", alternativeSteps, new Dictionary<string, double>(), DateTime.UtcNow);

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Success(alternativePlan));

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.Backtrack);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // checkpoint=2, so steps 0,1 kept + alternative steps
        result.Value.Steps.Should().HaveCount(4);
        result.Value.Steps[0].Action.Should().Be("step_0");
        result.Value.Steps[1].Action.Should().Be("step_1");
        result.Value.Steps[2].Action.Should().Be("alt_step_a");
        result.Value.Steps[3].Action.Should().Be("alt_step_b");
    }

    [Fact]
    public async Task RepairPlanAsync_BacktrackStrategy_AlternativePlanFails_FallsBackToOriginal()
    {
        // Arrange
        var steps = Enumerable.Range(0, 4)
            .Select(i => new PlanStep($"step_{i}", new Dictionary<string, object>(), $"out_{i}", 0.8))
            .ToList();
        var brokenPlan = new Plan("goal", steps, new Dictionary<string, double>(), DateTime.UtcNow);

        var trace = new ExecutionTrace(
            Enumerable.Range(0, 4).Select(i =>
                new ExecutedStep($"step_{i}", i != 3, TimeSpan.FromSeconds(1), new Dictionary<string, object>()))
                .ToList(),
            FailedAtIndex: 3,
            FailureReason: "fail");

        _mockOrchestrator
            .Setup(o => o.PlanAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, object>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Plan, string>.Failure("alternative planning failed"));

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.Backtrack);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // checkpoint = max(0, 3-3) = 0, so all original steps preserved as fallback
        result.Value.Steps.Select(s => s.Action).Should().ContainInOrder("step_0", "step_1", "step_2", "step_3");
    }

    [Fact]
    public async Task RepairPlanAsync_Cancellation_ReturnsCancelledFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var brokenPlan = CreatePlan("goal", 2);
        var trace = new ExecutionTrace(new List<ExecutedStep>(), 0, "fail");

        // Act
        var result = await _sut.RepairPlanAsync(brokenPlan, trace, RepairStrategy.Patch, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Plan repair was cancelled");
    }

    // ================================================================
    // ExplainPlanAsync - explanation generation
    // ================================================================

    [Fact]
    public async Task ExplainPlanAsync_NullPlan_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.ExplainPlanAsync(null!, ExplanationLevel.Brief);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExplainPlanAsync_BriefLevel_ReturnsSummary()
    {
        // Arrange
        var plan = CreatePlan("deploy application", 5);

        // Act
        var result = await _sut.ExplainPlanAsync(plan, ExplanationLevel.Brief);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("deploy application");
        result.Value.Should().Contain("5 steps");
    }

    [Fact]
    public async Task ExplainPlanAsync_DetailedLevel_IncludesAllSteps()
    {
        // Arrange
        var plan = CreatePlan("goal", 3);

        // Act
        var result = await _sut.ExplainPlanAsync(plan, ExplanationLevel.Detailed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Goal: goal");
        result.Value.Should().Contain("Total Steps: 3");
        result.Value.Should().Contain("1.");
        result.Value.Should().Contain("2.");
        result.Value.Should().Contain("3.");
    }

    [Fact]
    public async Task ExplainPlanAsync_CausalLevel_ExplainsWhyEachStepNeeded()
    {
        // Arrange
        var plan = CreatePlan("goal", 2);

        // Act
        var result = await _sut.ExplainPlanAsync(plan, ExplanationLevel.Causal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("initial step");
        result.Value.Should().Contain("builds on");
    }

    [Fact]
    public async Task ExplainPlanAsync_CounterfactualLevel_ExplainsConsequences()
    {
        // Arrange
        var plan = CreatePlan("build app", 3);

        // Act
        var result = await _sut.ExplainPlanAsync(plan, ExplanationLevel.Counterfactual);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Last step: "would not be achieved"
        result.Value.Should().Contain("would not be achieved");
        // Intermediate steps: "Cannot proceed"
        result.Value.Should().Contain("Cannot proceed");
    }

    [Fact]
    public async Task ExplainPlanAsync_Cancellation_ReturnsCancelledFailure()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var plan = CreatePlan("goal", 1);

        // Act
        var result = await _sut.ExplainPlanAsync(plan, ExplanationLevel.Brief, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Plan explanation was cancelled");
    }

    // ================================================================
    // Helper methods
    // ================================================================

    private static Plan CreatePlan(string goal, int stepCount, double confidenceScore = 0.8)
    {
        var steps = Enumerable.Range(0, stepCount)
            .Select(i => new PlanStep(
                $"step_{i}",
                new Dictionary<string, object>(),
                $"expected_outcome_{i}",
                confidenceScore))
            .ToList();

        return new Plan(goal, steps, new Dictionary<string, double>(), DateTime.UtcNow);
    }

    private static PlanExecutionResult CreateExecutionResult(bool success, int stepCount)
    {
        var plan = CreatePlan("goal", stepCount);
        var stepResults = Enumerable.Range(0, stepCount)
            .Select(i => new StepResult(
                plan.Steps[i],
                success,
                $"output_{i}",
                success ? null : "error",
                TimeSpan.FromSeconds(1),
                new Dictionary<string, object>()))
            .ToList();

        return new PlanExecutionResult(
            plan,
            stepResults,
            success,
            success ? "done" : "failed",
            new Dictionary<string, object>(),
            TimeSpan.FromSeconds(stepCount));
    }
}
