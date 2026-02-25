// <copyright file="Commands.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions.Agent.Dispatch;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Agent.Dispatch;

/// <summary>
/// Dispatches a prompt through the ConsolidatedMind (Society of Mind).
/// </summary>
public sealed record ProcessMindCommand(
    string Prompt,
    bool Complex = false) : ICommand<MindResponse>;

/// <summary>
/// Selects the optimal model for a given prompt via SmartModelOrchestrator.
/// </summary>
public sealed record SelectModelCommand(
    string Prompt,
    Dictionary<string, object>? Context = null) : ICommand<Result<OrchestratorDecision, string>>;

/// <summary>
/// Creates a plan for a given goal via MetaAIPlannerOrchestrator.
/// </summary>
public sealed record CreatePlanCommand(
    string Goal,
    Dictionary<string, object>? Context = null) : ICommand<Result<Ouroboros.Agent.MetaAI.Plan, string>>;

/// <summary>
/// Executes a previously created plan via MetaAIPlannerOrchestrator.
/// </summary>
public sealed record ExecutePlanCommand(
    Ouroboros.Agent.MetaAI.Plan Plan) : ICommand<Result<Ouroboros.Agent.MetaAI.PlanExecutionResult, string>>;
