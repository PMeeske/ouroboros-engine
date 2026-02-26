// <copyright file="MeTTaParsingHelpersTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

// MeTTaParsingHelpers is an internal class and cannot be tested from
// an external test assembly without InternalsVisibleTo.
// These tests are skipped pending a decision on exposing internals.

namespace Ouroboros.Tests.MeTTaAgents;

[Trait("Category", "Unit")]
public class MeTTaParsingHelpersTests
{
    // Intentionally left empty - MeTTaParsingHelpers is internal.
    // To re-enable these tests, add [assembly: InternalsVisibleTo("Ouroboros.Agent.Tests")]
    // to the Ouroboros.Agent project's AssemblyInfo.cs.
}
