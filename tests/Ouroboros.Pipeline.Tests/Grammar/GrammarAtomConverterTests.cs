// <copyright file="GrammarAtomConverterTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Grammar;

namespace Ouroboros.Tests.Grammar;

public class GrammarAtomConverterTests
{
    [Theory]
    [InlineData(GrammarIssueKind.LeftRecursion, "LeftRecursion")]
    [InlineData(GrammarIssueKind.UnreachableRule, "UnreachableRule")]
    [InlineData(GrammarIssueKind.FirstSetConflict, "FirstSetConflict")]
    [InlineData(GrammarIssueKind.MissingRule, "MissingRule")]
    [InlineData(GrammarIssueKind.SyntaxError, "SyntaxError")]
    [InlineData(GrammarIssueKind.Ambiguity, "Ambiguity")]
    [InlineData(GrammarIssueKind.Unspecified, "Unspecified")]
    public void ToMeTTaAtomName_ShouldMapCorrectly(GrammarIssueKind kind, string expected)
    {
        GrammarAtomConverter.ToMeTTaAtomName(kind).Should().Be(expected);
    }
}
