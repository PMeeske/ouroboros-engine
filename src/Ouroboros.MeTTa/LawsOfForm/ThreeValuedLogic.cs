// <copyright file="ThreeValuedLogic.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.MeTTa.LawsOfForm;

/// <summary>
/// 3-valued logic over <see cref="DistinctionState"/>: Void, Mark, Imaginary.
/// </summary>
/// <remarks>
/// <para>
/// The truth tables are Kleene-style with Imaginary as the third value.
/// Where one operand is Imaginary and the other does not determine the
/// outcome, the result is Imaginary.
/// </para>
/// </remarks>
public static class ThreeValuedLogic
{
    /// <summary>
    /// Logical NOT — toggles Void/Mark, fixed point on Imaginary.
    /// </summary>
    /// <param name="x">Input value.</param>
    /// <returns>Negation result.</returns>
    public static DistinctionState Not(DistinctionState x) => x switch
    {
        DistinctionState.Mark => DistinctionState.Void,
        DistinctionState.Void => DistinctionState.Mark,
        _ => DistinctionState.Imaginary,
    };

    /// <summary>
    /// Logical AND with Kleene semantics.
    /// </summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns>Conjunction result.</returns>
    public static DistinctionState And(DistinctionState a, DistinctionState b)
    {
        if (a == DistinctionState.Void || b == DistinctionState.Void)
        {
            return DistinctionState.Void;
        }

        if (a == DistinctionState.Imaginary || b == DistinctionState.Imaginary)
        {
            return DistinctionState.Imaginary;
        }

        return DistinctionState.Mark;
    }

    /// <summary>
    /// Logical OR with Kleene semantics.
    /// </summary>
    /// <param name="a">First operand.</param>
    /// <param name="b">Second operand.</param>
    /// <returns>Disjunction result.</returns>
    public static DistinctionState Or(DistinctionState a, DistinctionState b)
    {
        if (a == DistinctionState.Mark || b == DistinctionState.Mark)
        {
            return DistinctionState.Mark;
        }

        if (a == DistinctionState.Imaginary || b == DistinctionState.Imaginary)
        {
            return DistinctionState.Imaginary;
        }

        return DistinctionState.Void;
    }

    /// <summary>
    /// Material implication via NOT/OR.
    /// </summary>
    /// <param name="antecedent">Antecedent.</param>
    /// <param name="consequent">Consequent.</param>
    /// <returns>Implication result.</returns>
    public static DistinctionState Implies(DistinctionState antecedent, DistinctionState consequent) =>
        Or(Not(antecedent), consequent);

    /// <summary>
    /// Returns every (a, b, AND, OR) row of the truth table — used by tests.
    /// </summary>
    /// <returns>The full truth table.</returns>
    public static IReadOnlyList<TruthRow> FullTable()
    {
        DistinctionState[] all = { DistinctionState.Void, DistinctionState.Mark, DistinctionState.Imaginary };
        List<TruthRow> rows = new(all.Length * all.Length);

        foreach (DistinctionState a in all)
        {
            foreach (DistinctionState b in all)
            {
                rows.Add(new TruthRow(a, b, And(a, b), Or(a, b), Implies(a, b)));
            }
        }

        return rows;
    }
}

/// <summary>
/// One row of the 3-valued truth table.
/// </summary>
/// <param name="A">First operand.</param>
/// <param name="B">Second operand.</param>
/// <param name="And">A AND B.</param>
/// <param name="Or">A OR B.</param>
/// <param name="Implies">A IMPLIES B.</param>
public sealed record TruthRow(
    DistinctionState A,
    DistinctionState B,
    DistinctionState And,
    DistinctionState Or,
    DistinctionState Implies);
