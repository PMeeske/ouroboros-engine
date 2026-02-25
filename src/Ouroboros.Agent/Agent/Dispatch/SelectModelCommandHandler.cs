// <copyright file="SelectModelCommandHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions.Agent.Dispatch;

namespace Ouroboros.Agent.Dispatch;

/// <summary>
/// Handles <see cref="SelectModelCommand"/> by routing to the IModelOrchestrator.
/// </summary>
public sealed class SelectModelCommandHandler
    : ICommandHandler<SelectModelCommand, Result<OrchestratorDecision, string>>
{
    private readonly IModelOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelectModelCommandHandler"/> class.
    /// </summary>
    public SelectModelCommandHandler(IModelOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <inheritdoc />
    public Task<Result<OrchestratorDecision, string>> HandleAsync(
        SelectModelCommand command,
        CancellationToken ct = default)
    {
        return _orchestrator.SelectModelAsync(command.Prompt, command.Context, ct);
    }
}
