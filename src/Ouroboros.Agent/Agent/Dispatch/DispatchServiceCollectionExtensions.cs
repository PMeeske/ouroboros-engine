// <copyright file="DispatchServiceCollectionExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Core.Dispatch;

namespace Ouroboros.Agent.Dispatch;

/// <summary>
/// DI extensions for registering Engine-layer CQRS dispatch (orchestrator commands and queries).
/// </summary>
public static class AgentDispatchServiceCollectionExtensions
{
    /// <summary>
    /// Registers the orchestrator CQRS dispatcher and all command/query handlers from the Agent assembly.
    /// </summary>
    public static IServiceCollection AddOuroborosAgentDispatch(this IServiceCollection services)
    {
        services.AddOuroborosDispatch(Assembly.GetExecutingAssembly());
        return services;
    }
}
