// <copyright file="ReentryOperation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.LawsOfForm;

/// <summary>
/// Spencer-Brown's Re-entry — the self-referential form, where a
/// distinction crosses itself. Always yields
/// <see cref="DistinctionState.Imaginary"/>.
/// MeTTa form: <c>(reentry $name)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Re-entry is the axiom that gives Laws of Form its third value
/// (Imaginary): <c>cross(cross(x))</c> within the same context creates
/// an oscillating form whose value is neither Mark nor Void. This
/// operation forces the named distinction into the Imaginary state and
/// returns it.
/// </para>
/// </remarks>
public sealed class ReentryOperation
{
    private readonly DistinctionTracker _tracker;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="tracker">The tracker holding distinction state.</param>
    public ReentryOperation(DistinctionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        _tracker = tracker;
    }

    /// <summary>
    /// Gets the symbol name registered in the <see cref="GroundedRegistry"/>.
    /// </summary>
    public static string Name => "reentry";

    /// <summary>
    /// Forces the distinction into the Imaginary state.
    /// </summary>
    /// <param name="distinctionName">The distinction to mark imaginary.</param>
    /// <returns>Always <see cref="DistinctionState.Imaginary"/>.</returns>
    public DistinctionState Execute(string distinctionName)
    {
        ArgumentNullException.ThrowIfNull(distinctionName);
        _tracker.Set(distinctionName, DistinctionState.Imaginary);
        return DistinctionState.Imaginary;
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
                return new[] { Atom.Sym(DistinctionState.Imaginary.ToString()) };
            }

            string name = args.Children[1].ToSExpr();
            DistinctionState state = Execute(name);
            return new[] { Atom.Sym(state.ToString()) };
        };
    }
}
