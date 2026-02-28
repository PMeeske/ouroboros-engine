// <copyright file="GrammarEvolutionException.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Grammar;

/// <summary>
/// Exception thrown when the adaptive grammar evolution process fails to
/// converge on a valid grammar within the allowed number of attempts.
/// </summary>
public sealed class GrammarEvolutionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GrammarEvolutionException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="description">The grammar description that was attempted.</param>
    /// <param name="attempts">Number of attempts made.</param>
    public GrammarEvolutionException(string message, string description, int attempts)
        : base(message)
    {
        Description = description;
        Attempts = attempts;
    }

    /// <summary>
    /// Gets the grammar description that was requested.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the number of evolution attempts made.
    /// </summary>
    public int Attempts { get; }
}
