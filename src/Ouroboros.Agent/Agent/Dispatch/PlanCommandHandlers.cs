// <copyright file="PlanCommandHandlers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions.Agent.Dispatch;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Dispatch;

/// <summary>
/// Handles <see cref="CreatePlanCommand"/> by routing to the MetaAI planner.
/// </summary>
public sealed class CreatePlanCommandHandler
    : ICommandHandler<CreatePlanCommand, Result<Plan, string>>
{
    private readonly IMetaAIPlannerOrchestrator _planner;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePlanCommandHandler"/> class.
    /// </summary>
    public CreatePlanCommandHandler(IMetaAIPlannerOrchestrator planner)
    {
        _planner = planner;
    }

    /// <inheritdoc />
    public Task<Result<Plan, string>> HandleAsync(CreatePlanCommand command, CancellationToken ct = default)
    {
        return _planner.PlanAsync(command.Goal, command.Context, ct);
    }
}

/// <summary>
/// Handles <see cref="ExecutePlanCommand"/> by routing to the MetaAI planner.
/// </summary>
public sealed class ExecutePlanCommandHandler
    : ICommandHandler<ExecutePlanCommand, Result<PlanExecutionResult, string>>
{
    private readonly IMetaAIPlannerOrchestrator _planner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutePlanCommandHandler"/> class.
    /// </summary>
    public ExecutePlanCommandHandler(IMetaAIPlannerOrchestrator planner)
    {
        _planner = planner;
    }

    /// <inheritdoc />
    public Task<Result<PlanExecutionResult, string>> HandleAsync(
        ExecutePlanCommand command,
        CancellationToken ct = default)
    {
        return _planner.ExecuteAsync(command.Plan, ct);
    }
}
