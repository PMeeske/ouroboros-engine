// <copyright file="CallOperation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.LawsOfForm;

/// <summary>
/// Spencer-Brown's Call — re-entry from a boundary.
/// MeTTa form: <c>(call $name)</c>.
/// </summary>
/// <remarks>
/// <para>
/// In Laws of Form, the Call axiom is <c>call(call(x)) = x</c> — calling
/// twice returns to the original state. The Call evaluates the current
/// state of a named distinction without altering it.
/// </para>
/// </remarks>
public sealed class CallOperation
{
    private readonly DistinctionTracker _tracker;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="tracker">The tracker holding distinction state.</param>
    public CallOperation(DistinctionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        _tracker = tracker;
    }

    /// <summary>
    /// Gets the symbol name registered in the <see cref="GroundedRegistry"/>.
    /// </summary>
    public static string Name => "call";

    /// <summary>
    /// Reads the current state of the named distinction.
    /// </summary>
    /// <param name="distinctionName">The distinction to evaluate.</param>
    /// <returns>The current state.</returns>
    public DistinctionState Execute(string distinctionName)
    {
        ArgumentNullException.ThrowIfNull(distinctionName);
        return _tracker.Get(distinctionName);
    }

    /// <summary>
    /// Returns a <see cref="GroundedOperation"/> delegate suitable for
    /// registration in the foundation's <see cref="GroundedRegistry"/>.
    /// </summary>
    /// <returns>The grounded delegate.</returns>
    public GroundedOperation AsGroundedOperation()
    {
        return (IAtomSpace _, Expression args) =>
        {
            if (args.Children.Count < 2)
            {
                return new[] { Atom.Sym(DistinctionState.Void.ToString()) };
            }

            string name = args.Children[1].ToSExpr();
            DistinctionState state = Execute(name);
            return new[] { Atom.Sym(state.ToString()) };
        };
    }
}
