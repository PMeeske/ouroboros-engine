// <copyright file="ProcessMindCommandHandlerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.Dispatch;

namespace Ouroboros.Tests.Dispatch;

[Trait("Category", "Unit")]
public class ProcessMindCommandHandlerTests
{
    [Fact]
    public void Command_Complex_Flag_IsRespected()
    {
        // Arrange
        var cmdSimple = new ProcessMindCommand("hello", Complex: false);
        var cmdComplex = new ProcessMindCommand("hello", Complex: true);

        // Assert
        cmdSimple.Complex.Should().BeFalse();
        cmdComplex.Complex.Should().BeTrue();
    }

    [Fact]
    public void Command_Prompt_IsSet()
    {
        var cmd = new ProcessMindCommand("test input", Complex: false);
        cmd.Prompt.Should().Be("test input");
    }
}
