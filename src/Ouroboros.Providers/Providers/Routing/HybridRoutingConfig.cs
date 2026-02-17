// <copyright file="HybridRoutingConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Routing;

/// <summary>
/// Configuration for hybrid model routing based on task type.
/// </summary>
/// <param name="DefaultModel">Default model for simple/unknown tasks.</param>
/// <param name="ReasoningModel">Optional model for reasoning tasks (uses DefaultModel if null).</param>
/// <param name="PlanningModel">Optional model for planning tasks (uses DefaultModel if null).</param>
/// <param name="CodingModel">Optional model for code generation tasks (uses DefaultModel if null).</param>
/// <param name="FallbackModel">Optional fallback model when primary models fail (uses DefaultModel if null).</param>
/// <param name="DetectionStrategy">Strategy for detecting task types (default: Heuristic).</param>
public record HybridRoutingConfig(
    Ouroboros.Abstractions.Core.IChatCompletionModel DefaultModel,
    Ouroboros.Abstractions.Core.IChatCompletionModel? ReasoningModel = null,
    Ouroboros.Abstractions.Core.IChatCompletionModel? PlanningModel = null,
    Ouroboros.Abstractions.Core.IChatCompletionModel? CodingModel = null,
    Ouroboros.Abstractions.Core.IChatCompletionModel? FallbackModel = null,
    TaskDetectionStrategy DetectionStrategy = TaskDetectionStrategy.Heuristic);