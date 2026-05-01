// <copyright file="CrossOperation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.LawsOfForm;

/// <summary>
/// Spencer-Brown's Cross — toggles a named distinction.
/// MeTTa form: <c>(cross $name)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Cross is the act of drawing a distinction; calling Cross again
/// undraws it. In Laws of Form notation: <c>cross(cross(x)) = x</c>.
/// </para>
/// <para>
/// Backed by a <see cref="DistinctionTracker"/> so the toggle persists
/// for the lifetime of the reasoning context. The grounded op returns
/// a single Symbol atom representing the resulting state name (
/// <c>Mark</c>, <c>Void</c>, or <c>Imaginary</c>).
/// </para>
/// </remarks>
public sealed class CrossOperation
{
    private readonly DistinctionTracker _tracker;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="tracker">The tracker holding distinction state.</param>
    public CrossOperation(DistinctionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(tracker);
        _tracker = tracker;
    }

    /// <summary>
    /// Gets the symbol name registered in the <see cref="GroundedRegistry"/>.
    /// </summary>
    public static string Name => "cross";

    /// <summary>
    /// Executes the operation against an explicit name.
    /// </summary>
    /// <param name="distinctionName">The distinction to toggle.</param>
    /// <returns>The post-toggle state.</returns>
    public DistinctionState Execute(string distinctionName)
    {
        ArgumentNullException.ThrowIfNull(distinctionName);
        return _tracker.Toggle(distinctionName);
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
            // Skip the head symbol when reading args (consistent with foundation conventions).
            if (args.Children.Count < 2)
            {
                return new[] { Atom.Sym(_tracker.Get("default").ToString()) };
            }

            string name = args.Children[1].ToSExpr();
            DistinctionState result = Execute(name);
            return new[] { Atom.Sym(result.ToString()) };
        };
    }
}
