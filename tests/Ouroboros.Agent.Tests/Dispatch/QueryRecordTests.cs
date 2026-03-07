// <copyright file="QueryRecordTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class QueryRecordTests
{
    [Fact]
    public void ClassifyUseCaseQuery_SetsPrompt()
    {
        var q = new ClassifyUseCaseQuery("Classify this");

        q.Prompt.Should().Be("Classify this");
    }

    [Fact]
    public void GetOrchestratorMetricsQuery_SetsName()
    {
        var q = new GetOrchestratorMetricsQuery("Smart");

        q.OrchestratorName.Should().Be("Smart");
    }

    [Fact]
    public void ValidateReadinessQuery_SetsName()
    {
        var q = new ValidateReadinessQuery("MetaAI");

        q.OrchestratorName.Should().Be("MetaAI");
    }
}
