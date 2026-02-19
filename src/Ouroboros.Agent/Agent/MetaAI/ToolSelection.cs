// <copyright file="ToolSelection.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents the result of LLM-based tool selection with extracted arguments.
/// </summary>
/// <param name="ToolName">The name of the selected tool.</param>
/// <param name="ArgumentsJson">JSON string containing tool arguments extracted by the LLM.</param>
public sealed record ToolSelection(string ToolName, string ArgumentsJson);
