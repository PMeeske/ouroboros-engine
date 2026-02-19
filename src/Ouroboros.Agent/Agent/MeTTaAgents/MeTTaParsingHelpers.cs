// <copyright file="MeTTaParsingHelpers.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Agent.MeTTaAgents;

/// <summary>
/// Shared helpers for parsing MeTTa output.
/// </summary>
internal static class MeTTaParsingHelpers
{
    /// <summary>
    /// Regex pattern for matching AgentDef atoms in MeTTa output.
    /// Pattern handles escaped quotes and backslashes in string fields.
    /// Format: (AgentDef "id" Provider "model" Role "prompt" tokens temp)
    /// </summary>
    public const string AgentDefPattern = @"\(AgentDef\s+""((?:[^""\\]|\\.)+)""\s+(\w+)\s+""((?:[^""\\]|\\.)+)""\s+(\w+)\s+""((?:[^""\\]|\\.)*)""\s+(\d+)\s+([\d.]+)\)";

    /// <summary>
    /// Escapes a string for safe interpolation into MeTTa queries or facts.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>Escaped text safe for MeTTa.</returns>
    public static string EscapeMeTTaString(string text)
        => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
