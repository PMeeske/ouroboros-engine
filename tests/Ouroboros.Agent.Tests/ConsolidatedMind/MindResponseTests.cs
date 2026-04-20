// <copyright file="MindResponseTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class MindResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var roles = new[] { SpecializedRole.CodeExpert, SpecializedRole.Verifier };

        // Act
        var response = new MindResponse(
            "test response",
            "thinking content",
            roles,
            ExecutionTimeMs: 150.5,
            WasVerified: true,
            Confidence: 0.95);

        // Assert
        response.Response.Should().Be("test response");
        response.ThinkingContent.Should().Be("thinking content");
        response.UsedRoles.Should().BeEquivalentTo(roles);
        response.ExecutionTimeMs.Should().Be(150.5);
        response.WasVerified.Should().BeTrue();
        response.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void ThinkingContent_CanBeNull()
    {
        // Act
        var response = new MindResponse(
            "response",
            null,
            new[] { SpecializedRole.QuickResponse },
            100.0,
            false,
            0.8);

        // Assert
        response.ThinkingContent.Should().BeNull();
    }

    [Fact]
    public void Record_Equality_WorksCorrectly()
    {
        // Arrange
        var roles = new[] { SpecializedRole.DeepReasoning };
        var r1 = new MindResponse("a", null, roles, 100.0, false, 0.5);
        var r2 = new MindResponse("a", null, roles, 100.0, false, 0.5);

        // Assert — same reference for UsedRoles means structural equality
        r1.Should().Be(r2);
    }

    [Fact]
    public void With_Expression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new MindResponse(
            "response",
            null,
            new[] { SpecializedRole.QuickResponse },
            50.0,
            false,
            0.9);

        // Act
        var modified = original with { WasVerified = true, Confidence = 0.5 };

        // Assert
        modified.WasVerified.Should().BeTrue();
        modified.Confidence.Should().Be(0.5);
        modified.Response.Should().Be(original.Response);
    }
}
