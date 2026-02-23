// <copyright file="ProcessMindCommandHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions.Agent.Dispatch;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Agent.Dispatch;

/// <summary>
/// Handles <see cref="ProcessMindCommand"/> by routing to the ConsolidatedMind.
/// </summary>
public sealed class ProcessMindCommandHandler : ICommandHandler<ProcessMindCommand, MindResponse>
{
    private readonly ConsolidatedMind.ConsolidatedMind _mind;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessMindCommandHandler"/> class.
    /// </summary>
    public ProcessMindCommandHandler(ConsolidatedMind.ConsolidatedMind mind)
    {
        _mind = mind;
    }

    /// <inheritdoc />
    public Task<MindResponse> HandleAsync(ProcessMindCommand command, CancellationToken ct = default)
    {
        return command.Complex
            ? _mind.ProcessComplexAsync(command.Prompt, ct)
            : _mind.ProcessAsync(command.Prompt, ct);
    }
}
