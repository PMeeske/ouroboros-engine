// <copyright file="Queries.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using Ouroboros.Abstractions.Agent.Dispatch;

namespace Ouroboros.Agent.Dispatch;

/// <summary>
/// Classifies a prompt into a use case via SmartModelOrchestrator.
/// </summary>
public sealed record ClassifyUseCaseQuery(
    string Prompt) : IQuery<UseCase>;

/// <summary>
/// Returns performance metrics from an orchestrator.
/// </summary>
public sealed record GetOrchestratorMetricsQuery(
    string OrchestratorName) : IQuery<IReadOnlyDictionary<string, PerformanceMetrics>>;

/// <summary>
/// Checks readiness of an orchestrator.
/// </summary>
public sealed record ValidateReadinessQuery(
    string OrchestratorName) : IQuery<Result<bool, string>>;
