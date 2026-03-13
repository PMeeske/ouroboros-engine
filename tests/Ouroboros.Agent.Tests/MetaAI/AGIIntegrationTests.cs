// <copyright file="AGIIntegrationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions.Core;
using Ouroboros.Agent.MetaAI.Interpretability;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Core.Ethics;
using AgentCapability = Ouroboros.Agent.MetaAI.AgentCapability;
using MemoryStatistics = Ouroboros.Agent.MetaAI.MemoryStatistics;
using Plan = Ouroboros.Agent.MetaAI.Plan;
using PlanStep = Ouroboros.Agent.PlanStep;
using Skill = Ouroboros.Agent.MetaAI.Skill;

namespace Ouroboros.Agent.Tests.MetaAI;

/// <summary>
/// End-to-end integration tests proving the full AGI metacognition loop works together.
/// Verifies that GlobalWorkspace, PredictiveMonitor, IdentityGraph, EthicsFramework,
/// InterpretabilityEngine, and ExperienceReplay collaborate correctly in concert.
/// </summary>
[Trait("Category", "Integration")]
public class AGIIntegrationTests
{
    [Fact]
    public async Task FullMetacognitionLoop_AllComponentsCollaborate()
    {
        // ================================================================
        // Step 1: GlobalWorkspace — manage attention across the system
        // ================================================================
        var workspace = new GlobalWorkspace();

        workspace.AddItem(
            "Incoming user request: summarize document",
            WorkspacePriority.High,
            "InputRouter",
            new List<string> { "user-request", "nlp" });

        workspace.AddItem(
            "Background: memory consolidation in progress",
            WorkspacePriority.Low,
            "MemorySubsystem",
            new List<string> { "maintenance" });

        workspace.AddItem(
            "Alert: resource utilization above 80%",
            WorkspacePriority.Critical,
            "ResourceMonitor",
            new List<string> { "alert", "resources" });

        // Verify attention focuses on high-priority items
        List<WorkspaceItem> highPriority = workspace.GetHighPriorityItems();
        highPriority.Should().HaveCount(2);
        highPriority.Should().Contain(i => i.Content.Contains("user request"));
        highPriority.Should().Contain(i => i.Content.Contains("resource utilization"));

        // Verify tag-based retrieval
        List<WorkspaceItem> alertItems = workspace.SearchByTags(new List<string> { "alert" });
        alertItems.Should().HaveCount(1);
        alertItems[0].Source.Should().Be("ResourceMonitor");

        // Verify statistics
        WorkspaceStatistics stats = workspace.GetStatistics();
        stats.TotalItems.Should().Be(3);
        stats.HighPriorityItems.Should().Be(2);
        stats.CriticalItems.Should().Be(1);

        // Apply attention policies to ensure workspace is well-managed
        workspace.ApplyAttentionPolicies();

        // ================================================================
        // Step 2: PredictiveMonitor — forecast outcomes and calibrate
        // ================================================================
        var monitor = new PredictiveMonitor();

        // Create forecasts about task execution
        Forecast taskSuccessForecast = monitor.CreateForecast(
            "Summarization task will succeed with high quality",
            "task_success_rate",
            0.90,
            0.85,
            DateTime.UtcNow.AddMinutes(30));

        Forecast latencyForecast = monitor.CreateForecast(
            "Response latency will be under 500ms",
            "response_latency_ms",
            450.0,
            0.70,
            DateTime.UtcNow.AddMinutes(5));

        // Verify forecasts are pending
        List<Forecast> pendingForecasts = monitor.GetPendingForecasts();
        pendingForecasts.Should().HaveCount(2);
        pendingForecasts.Should().OnlyContain(f => f.Status == ForecastStatus.Pending);

        // Simulate actual outcomes arriving
        monitor.UpdateForecastOutcome(taskSuccessForecast.Id, 0.92); // Close to predicted 0.90 -> Verified
        monitor.UpdateForecastOutcome(latencyForecast.Id, 480.0);   // Close to predicted 450 -> Verified

        // Check calibration after outcomes
        ForecastCalibration calibration = monitor.GetCalibration(TimeSpan.FromDays(1));
        calibration.TotalForecasts.Should().Be(2);
        calibration.VerifiedForecasts.Should().Be(2);
        calibration.FailedForecasts.Should().Be(0);
        calibration.AverageConfidence.Should().BeGreaterThan(0.0);
        calibration.MetricAccuracies.Should().ContainKey("task_success_rate");
        calibration.MetricAccuracies.Should().ContainKey("response_latency_ms");

        // ================================================================
        // Step 3: IdentityGraph — track agent self-model
        // ================================================================
        var agentId = Guid.NewGuid();
        var mockRegistry = new Mock<ICapabilityRegistry>();
        mockRegistry
            .Setup(r => r.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentCapability>
            {
                new AgentCapability(
                    "Summarization",
                    "Can summarize documents and text",
                    new List<string> { "llm", "text-processing" },
                    0.92,
                    350.0,
                    new List<string> { "Limited to 100k tokens" },
                    42,
                    DateTime.UtcNow.AddDays(-30),
                    DateTime.UtcNow,
                    new Dictionary<string, object>())
            });

        var identity = new IdentityGraph(agentId, "OuroborosAgent", mockRegistry.Object);

        // Register a custom resource
        var gpuResource = new AgentResource(
            "GPU",
            "Computation",
            4.0,
            8.0,
            "units",
            DateTime.UtcNow,
            new Dictionary<string, object> { ["model"] = "A100" });
        identity.RegisterResource(gpuResource);

        // Verify resource tracking
        AgentResource? retrievedGpu = identity.GetResource("GPU");
        retrievedGpu.Should().NotBeNull();
        retrievedGpu!.Available.Should().Be(4.0);
        retrievedGpu.Total.Should().Be(8.0);

        // Create and manage a commitment
        AgentCommitment commitment = identity.CreateCommitment(
            "Summarize user document within SLA",
            DateTime.UtcNow.AddMinutes(30),
            0.9);
        commitment.Status.Should().Be(CommitmentStatus.Planned);

        identity.UpdateCommitment(commitment.Id, CommitmentStatus.InProgress, 25.0);
        List<AgentCommitment> activeCommitments = identity.GetActiveCommitments();
        activeCommitments.Should().Contain(c => c.Id == commitment.Id);

        // Record a task execution result to feed performance metrics
        var plan = new Plan(
            "Summarize document",
            new List<PlanStep>
            {
                new PlanStep("parse_input", new Dictionary<string, object>(), "Document parsed", 0.95),
                new PlanStep("generate_summary", new Dictionary<string, object>(), "Summary generated", 0.90),
                new PlanStep("validate_output", new Dictionary<string, object>(), "Output validated", 0.88)
            },
            new Dictionary<string, double> { ["overall"] = 0.91 },
            DateTime.UtcNow);

        var stepResults = plan.Steps.Select(s => new StepResult(
            s,
            true,
            $"Completed: {s.ExpectedOutcome}",
            null,
            TimeSpan.FromMilliseconds(120),
            new Dictionary<string, object>())).ToArray();

        var executionResult = new PlanExecutionResult(
            plan,
            stepResults,
            true,
            "Document summarized successfully",
            new Dictionary<string, object> { ["tokens_used"] = 1500 },
            TimeSpan.FromMilliseconds(360));

        identity.RecordTaskResult(executionResult);

        // Mark commitment as completed
        identity.UpdateCommitment(commitment.Id, CommitmentStatus.Completed, 100.0);

        // Verify performance summary reflects the recorded task
        AgentPerformance performance = identity.GetPerformanceSummary(TimeSpan.FromDays(1));
        performance.TotalTasks.Should().Be(1);
        performance.SuccessfulTasks.Should().Be(1);
        performance.FailedTasks.Should().Be(0);
        performance.OverallSuccessRate.Should().Be(1.0);

        // Verify identity state is complete
        AgentIdentityState state = await identity.GetStateAsync();
        state.AgentId.Should().Be(agentId);
        state.Name.Should().Be("OuroborosAgent");
        state.Capabilities.Should().HaveCount(1);
        state.Resources.Should().HaveCountGreaterThanOrEqualTo(4); // CPU, Memory, Attention + GPU

        // ================================================================
        // Step 4: EthicsFramework — evaluate a benign action
        // ================================================================
        IEthicsFramework ethics = EthicsFrameworkFactory.CreateDefault();

        // Verify core principles exist
        IReadOnlyList<EthicalPrinciple> principles = ethics.GetCorePrinciples();
        principles.Should().NotBeEmpty();

        // Evaluate a benign summarization action
        var proposedAction = new ProposedAction
        {
            ActionType = "text_processing",
            Description = "Summarize a user-provided document for the requesting user",
            Parameters = new Dictionary<string, object>
            {
                ["input_length"] = 5000,
                ["output_format"] = "paragraph"
            },
            TargetEntity = "user_document_12345",
            PotentialEffects = new List<string>
            {
                "Generates a text summary",
                "Consumes compute resources"
            }
        };

        var actionContext = new ActionContext
        {
            AgentId = agentId.ToString(),
            UserId = "user-001",
            Environment = "testing",
            State = new Dictionary<string, object>
            {
                ["session_id"] = "test-session",
                ["task_type"] = "summarization"
            }
        };

        Result<EthicalClearance, string> ethicsResult = await ethics.EvaluateActionAsync(
            proposedAction,
            actionContext);

        ethicsResult.IsSuccess.Should().BeTrue();
        ethicsResult.Value.IsPermitted.Should().BeTrue("a benign text summarization action should be permitted");
        ethicsResult.Value.Violations.Should().BeEmpty();

        // ================================================================
        // Step 5: InterpretabilityEngine — explain a decision using all sources
        // ================================================================
        var interpretabilityEngine = new InterpretabilityEngine(
            workspace,
            monitor,
            identity,
            ethics);

        Guid decisionId = Guid.NewGuid();
        var explanation = interpretabilityEngine.ExplainDecision(
            decisionId,
            "Decided to summarize user document using LLM-based approach");

        // The explanation should draw from all four subsystems
        explanation.DecisionId.Should().Be(decisionId);
        explanation.ContributingFactors.Should().HaveCountGreaterThanOrEqualTo(4);

        var factorSources = explanation.ContributingFactors.Select(f => f.Source).Distinct().ToList();
        factorSources.Should().Contain("GlobalWorkspace", "Workspace attention items should contribute");
        factorSources.Should().Contain("PredictiveMonitor", "Forecast data should contribute");
        factorSources.Should().Contain("IdentityGraph", "Self-model performance should contribute");
        factorSources.Should().Contain("EthicsFramework", "Ethical principles should contribute");

        explanation.OverallConfidence.Should().BeGreaterThan(0.0);
        explanation.Summary.Should().NotBeNullOrWhiteSpace();

        // The attention report should reflect workspace state
        var attentionReport = interpretabilityEngine.GetAttentionReport();
        attentionReport.TotalWorkspaceSize.Should().BeGreaterThan(0);
        attentionReport.HighPriorityCount.Should().BeGreaterThanOrEqualTo(1);

        // The calibration report should reflect monitor state
        var calibrationReport = interpretabilityEngine.GetCalibrationReport();
        calibrationReport.TotalForecasts.Should().Be(2);
        calibrationReport.VerifiedForecasts.Should().Be(2);

        // Plan explanation should work with the interpretability engine
        var planExplanation = interpretabilityEngine.ExplainPlan(
            "Summarize document",
            new List<string> { "Parse input", "Generate summary", "Validate output" });
        planExplanation.PlanGoal.Should().Be("Summarize document");
        planExplanation.StepExplanations.Should().HaveCount(3);

        // ================================================================
        // Step 6: ExperienceReplay — record execution for continual learning
        // ================================================================
        var mockMemory = new Mock<IMemoryStore>();
        var mockSkills = new Mock<ISkillRegistry>();
        var mockLlm = new Mock<IChatCompletionModel>();

        mockMemory
            .Setup(m => m.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryStatistics, string>.Success(
                new MemoryStatistics(1, 1, 0, 1, 1)));

        // Return the execution as a stored experience
        var storedExperience = new Experience(
            Guid.NewGuid().ToString(),
            DateTime.UtcNow,
            "summarization_context",
            "summarize_document",
            "Document summarized successfully",
            true,
            new List<string> { "summarization", "nlp" },
            "Summarize document",
            executionResult,
            new PlanVerificationResult(
                executionResult,
                true,
                0.92,
                new List<string>(),
                new List<string>(),
                DateTime.UtcNow),
            plan);

        mockMemory
            .Setup(m => m.RetrieveRelevantExperiencesAsync(
                It.IsAny<MemoryQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Experience>, string>.Success(
                new List<Experience> { storedExperience }));

        mockSkills
            .Setup(s => s.ExtractSkillAsync(
                It.IsAny<PlanExecutionResult>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Skill, string>.Success(
                new Skill(
                    "summarization_skill",
                    "Learned summarization pattern",
                    new List<string> { "summarization" },
                    plan.Steps,
                    0.92,
                    0,
                    DateTime.UtcNow,
                    DateTime.UtcNow)));

        mockLlm
            .Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("- Parse then summarize is an effective pattern for document processing");

        var experienceReplay = new ExperienceReplay(
            mockMemory.Object,
            mockSkills.Object,
            mockLlm.Object);

        Result<TrainingResult, string> trainingResult = await experienceReplay.TrainOnExperiencesAsync();
        trainingResult.IsSuccess.Should().BeTrue();
        trainingResult.Value.ExperiencesProcessed.Should().Be(1);
        trainingResult.Value.Success.Should().BeTrue();
        trainingResult.Value.ImprovedMetrics.Should().ContainKey("skills_extracted");

        // ================================================================
        // Verify the full loop: all components have collaboratively processed
        // the lifecycle of a single agent task from attention through learning
        // ================================================================

        // Workspace tracked the task context
        stats = workspace.GetStatistics();
        stats.TotalItems.Should().BeGreaterThanOrEqualTo(1);

        // Monitor's forecasts were accurate (both verified)
        calibration = monitor.GetCalibration(TimeSpan.FromDays(1));
        calibration.VerifiedForecasts.Should().Be(2);
        calibration.FailedForecasts.Should().Be(0);

        // Identity has updated performance from the executed task
        performance = identity.GetPerformanceSummary(TimeSpan.FromDays(1));
        performance.OverallSuccessRate.Should().Be(1.0);

        // Ethics cleared the action
        ethicsResult.Value.IsPermitted.Should().BeTrue();

        // Interpretability can explain what happened across all subsystems
        factorSources = explanation.ContributingFactors.Select(f => f.Source).Distinct().ToList();
        factorSources.Should().HaveCountGreaterThanOrEqualTo(4);

        // Experience replay captured learnings for future improvement
        trainingResult.Value.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AGIComponents_GracefulDegradation_WorksWithPartialComponents()
    {
        // ================================================================
        // Test 1: InterpretabilityEngine with no components (all null)
        // ================================================================
        var bareEngine = new InterpretabilityEngine();

        var bareExplanation = bareEngine.ExplainDecision(Guid.NewGuid(), "decision with no context");
        bareExplanation.ContributingFactors.Should().BeEmpty();
        bareExplanation.OverallConfidence.Should().Be(0.0);
        bareExplanation.Summary.Should().Contain("decision with no context");

        var bareAttentionReport = bareEngine.GetAttentionReport();
        bareAttentionReport.ActiveItems.Should().BeEmpty();
        bareAttentionReport.TotalWorkspaceSize.Should().Be(0);
        bareAttentionReport.HighPriorityCount.Should().Be(0);

        var bareCalibrationReport = bareEngine.GetCalibrationReport();
        bareCalibrationReport.TotalForecasts.Should().Be(0);
        bareCalibrationReport.BrierScore.Should().Be(0);

        var barePlanExplanation = bareEngine.ExplainPlan("Empty plan test", Array.Empty<string>());
        barePlanExplanation.StepExplanations.Should().BeEmpty();

        // ================================================================
        // Test 2: InterpretabilityEngine with only workspace
        // ================================================================
        var workspace = new GlobalWorkspace();
        workspace.AddItem("Solo workspace item", WorkspacePriority.High, "TestSource");

        var workspaceOnlyEngine = new InterpretabilityEngine(workspace: workspace);

        var workspaceExplanation = workspaceOnlyEngine.ExplainDecision(
            Guid.NewGuid(),
            "workspace-only decision");
        workspaceExplanation.ContributingFactors.Should().Contain(f => f.Source == "GlobalWorkspace");
        workspaceExplanation.ContributingFactors.Should().NotContain(f => f.Source == "PredictiveMonitor");
        workspaceExplanation.ContributingFactors.Should().NotContain(f => f.Source == "IdentityGraph");
        workspaceExplanation.ContributingFactors.Should().NotContain(f => f.Source == "EthicsFramework");

        // Attention report works, calibration returns zeros
        var partialAttention = workspaceOnlyEngine.GetAttentionReport();
        partialAttention.TotalWorkspaceSize.Should().BeGreaterThan(0);

        var partialCalibration = workspaceOnlyEngine.GetCalibrationReport();
        partialCalibration.TotalForecasts.Should().Be(0);

        // ================================================================
        // Test 3: InterpretabilityEngine with only monitor
        // ================================================================
        var monitor = new PredictiveMonitor();
        monitor.CreateForecast("test", "metric_a", 0.5, 0.8, DateTime.UtcNow.AddHours(1));

        var monitorOnlyEngine = new InterpretabilityEngine(monitor: monitor);

        var monitorExplanation = monitorOnlyEngine.ExplainDecision(
            Guid.NewGuid(),
            "monitor-only decision");
        monitorExplanation.ContributingFactors.Should().Contain(f => f.Source == "PredictiveMonitor");
        monitorExplanation.ContributingFactors.Should().NotContain(f => f.Source == "GlobalWorkspace");

        // ================================================================
        // Test 4: InterpretabilityEngine with only identity
        // ================================================================
        var mockRegistry = new Mock<ICapabilityRegistry>();
        mockRegistry
            .Setup(r => r.GetCapabilitiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentCapability>());

        var identity = new IdentityGraph(Guid.NewGuid(), "PartialAgent", mockRegistry.Object);

        var identityOnlyEngine = new InterpretabilityEngine(identity: identity);

        var identityExplanation = identityOnlyEngine.ExplainDecision(
            Guid.NewGuid(),
            "identity-only decision");
        identityExplanation.ContributingFactors.Should().Contain(f => f.Source == "IdentityGraph");
        identityExplanation.ContributingFactors.Should().NotContain(f => f.Source == "PredictiveMonitor");

        // ================================================================
        // Test 5: InterpretabilityEngine with only ethics
        // ================================================================
        IEthicsFramework ethics = EthicsFrameworkFactory.CreateDefault();

        var ethicsOnlyEngine = new InterpretabilityEngine(ethics: ethics);

        var ethicsExplanation = ethicsOnlyEngine.ExplainDecision(
            Guid.NewGuid(),
            "ethics-only decision");
        ethicsExplanation.ContributingFactors.Should().Contain(f => f.Source == "EthicsFramework");
        ethicsExplanation.ContributingFactors.Should().NotContain(f => f.Source == "GlobalWorkspace");

        // ================================================================
        // Test 6: Standalone components degrade independently
        // ================================================================

        // PredictiveMonitor with no LLM still handles forecasts and calibration
        var standaloneMonitor = new PredictiveMonitor();
        Forecast f = standaloneMonitor.CreateForecast("standalone", "metric", 10.0, 0.9, DateTime.UtcNow.AddHours(1));
        standaloneMonitor.UpdateForecastOutcome(f.Id, 10.5);
        ForecastCalibration standaloneCal = standaloneMonitor.GetCalibration(TimeSpan.FromDays(1));
        standaloneCal.VerifiedForecasts.Should().Be(1);

        // IdentityGraph still tracks resources and commitments without recorded tasks
        var minimalIdentity = new IdentityGraph(Guid.NewGuid(), "MinimalAgent", mockRegistry.Object);
        AgentPerformance emptyPerf = minimalIdentity.GetPerformanceSummary(TimeSpan.FromDays(1));
        emptyPerf.TotalTasks.Should().Be(0);
        emptyPerf.OverallSuccessRate.Should().Be(0.0);

        AgentResource? defaultCpu = minimalIdentity.GetResource("CPU");
        defaultCpu.Should().NotBeNull("default resources should be initialized even without tasks");

        // ExperienceReplay handles zero experiences gracefully
        var emptyMemory = new Mock<IMemoryStore>();
        emptyMemory
            .Setup(m => m.GetStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryStatistics, string>.Success(
                new MemoryStatistics(0, 0, 0, 0, 0)));
        emptyMemory
            .Setup(m => m.RetrieveRelevantExperiencesAsync(
                It.IsAny<MemoryQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<Experience>, string>.Success(
                new List<Experience>()));

        var emptySkills = new Mock<ISkillRegistry>();
        var emptyLlm = new Mock<IChatCompletionModel>();

        var emptyReplay = new ExperienceReplay(
            emptyMemory.Object,
            emptySkills.Object,
            emptyLlm.Object);

        Result<TrainingResult, string> emptyTraining = await emptyReplay.TrainOnExperiencesAsync();
        emptyTraining.IsSuccess.Should().BeTrue();
        emptyTraining.Value.ExperiencesProcessed.Should().Be(0);
        emptyTraining.Value.Success.Should().BeTrue();
    }
}
