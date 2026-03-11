using FluentAssertions;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.MetaAI.SelfModel;

[Trait("Category", "Unit")]
public class AttentionPolicyTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        int maxWorkspaceSize = 50;
        int maxHighPriorityItems = 10;
        var defaultItemLifetime = TimeSpan.FromMinutes(30);
        double minAttentionThreshold = 0.5;

        // Act
        var sut = new AttentionPolicy(
            maxWorkspaceSize,
            maxHighPriorityItems,
            defaultItemLifetime,
            minAttentionThreshold);

        // Assert
        sut.MaxWorkspaceSize.Should().Be(maxWorkspaceSize);
        sut.MaxHighPriorityItems.Should().Be(maxHighPriorityItems);
        sut.DefaultItemLifetime.Should().Be(defaultItemLifetime);
        sut.MinAttentionThreshold.Should().Be(minAttentionThreshold);
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy()
    {
        // Arrange
        var original = new AttentionPolicy(
            MaxWorkspaceSize: 100,
            MaxHighPriorityItems: 20,
            DefaultItemLifetime: TimeSpan.FromHours(1),
            MinAttentionThreshold: 0.3);

        // Act
        var modified = original with { MaxWorkspaceSize = 200 };

        // Assert
        modified.MaxWorkspaceSize.Should().Be(200);
        modified.MaxHighPriorityItems.Should().Be(original.MaxHighPriorityItems);
        modified.DefaultItemLifetime.Should().Be(original.DefaultItemLifetime);
        modified.MinAttentionThreshold.Should().Be(original.MinAttentionThreshold);
    }
}
