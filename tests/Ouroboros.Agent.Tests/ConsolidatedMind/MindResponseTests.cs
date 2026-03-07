using FluentAssertions;
using Ouroboros.Agent.ConsolidatedMind;

namespace Ouroboros.Tests.ConsolidatedMind;

[Trait("Category", "Unit")]
public sealed class MindResponseTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var roles = new[] { SpecializedRole.CodeExpert, SpecializedRole.Verifier };
        var response = new MindResponse(
            "Hello",
            "thinking",
            roles,
            150.5,
            WasVerified: true,
            Confidence: 0.95);

        response.Response.Should().Be("Hello");
        response.ThinkingContent.Should().Be("thinking");
        response.UsedRoles.Should().HaveCount(2);
        response.ExecutionTimeMs.Should().Be(150.5);
        response.WasVerified.Should().BeTrue();
        response.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void Constructor_NullThinking_Accepted()
    {
        var response = new MindResponse("Hello", null, Array.Empty<SpecializedRole>(), 0, false, 0.5);
        response.ThinkingContent.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var roles = new[] { SpecializedRole.CodeExpert };
        var a = new MindResponse("Hello", null, roles, 100, false, 0.5);
        var b = new MindResponse("Hello", null, roles, 100, false, 0.5);

        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentResponse_NotEqual()
    {
        var roles = new[] { SpecializedRole.CodeExpert };
        var a = new MindResponse("Hello", null, roles, 100, false, 0.5);
        var b = new MindResponse("World", null, roles, 100, false, 0.5);

        a.Should().NotBe(b);
    }
}
