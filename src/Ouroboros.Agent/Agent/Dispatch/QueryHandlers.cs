// <copyright file="QueryHandlers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions.Agent.Dispatch;

namespace Ouroboros.Agent.Dispatch;

/// <summary>
/// Handles <see cref="ClassifyUseCaseQuery"/> by routing to the IModelOrchestrator.
/// </summary>
public sealed class ClassifyUseCaseQueryHandler
    : IQueryHandler<ClassifyUseCaseQuery, UseCase>
{
    private readonly IModelOrchestrator _orchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassifyUseCaseQueryHandler"/> class.
    /// </summary>
    public ClassifyUseCaseQueryHandler(IModelOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <inheritdoc />
    public Task<UseCase> HandleAsync(ClassifyUseCaseQuery query, CancellationToken ct = default)
    {
        return Task.FromResult(_orchestrator.ClassifyUseCase(query.Prompt));
    }
}

/// <summary>
/// Handles <see cref="GetOrchestratorMetricsQuery"/> by looking up the named orchestrator.
/// </summary>
public sealed class GetOrchestratorMetricsQueryHandler
    : IQueryHandler<GetOrchestratorMetricsQuery, IReadOnlyDictionary<string, PerformanceMetrics>>
{
    private readonly IModelOrchestrator _modelOrchestrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetOrchestratorMetricsQueryHandler"/> class.
    /// </summary>
    public GetOrchestratorMetricsQueryHandler(IModelOrchestrator modelOrchestrator)
    {
        _modelOrchestrator = modelOrchestrator;
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, PerformanceMetrics>> HandleAsync(
        GetOrchestratorMetricsQuery query,
        CancellationToken ct = default)
    {
        return Task.FromResult(_modelOrchestrator.GetMetrics());
    }
}
