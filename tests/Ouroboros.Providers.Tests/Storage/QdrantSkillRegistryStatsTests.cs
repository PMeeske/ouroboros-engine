using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.Database.Storage;

/// <summary>
/// Unit tests for QdrantSkillRegistryStats record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class QdrantSkillRegistryStatsTests
{
    [Fact]
    public void Stats_WithValidData_SetsAllProperties()
    {
        // Act
        var stats = new QdrantSkillRegistryStats(
            TotalSkills: 10,
            AverageSuccessRate: 0.85,
            TotalExecutions: 500,
            MostUsedSkill: "code_generation",
            MostSuccessfulSkill: "bug_fix",
            ConnectionString: "http://localhost:6334",
            CollectionName: "skills",
            IsConnected: true);

        // Assert
        stats.TotalSkills.Should().Be(10);
        stats.AverageSuccessRate.Should().Be(0.85);
        stats.TotalExecutions.Should().Be(500);
        stats.MostUsedSkill.Should().Be("code_generation");
        stats.MostSuccessfulSkill.Should().Be("bug_fix");
        stats.ConnectionString.Should().Be("http://localhost:6334");
        stats.CollectionName.Should().Be("skills");
        stats.IsConnected.Should().BeTrue();
    }

    [Fact]
    public void Stats_WithNullOptionalFields_Accepted()
    {
        // Act
        var stats = new QdrantSkillRegistryStats(
            TotalSkills: 0,
            AverageSuccessRate: 0,
            TotalExecutions: 0,
            MostUsedSkill: null,
            MostSuccessfulSkill: null,
            ConnectionString: "http://localhost:6334",
            CollectionName: "skills",
            IsConnected: false);

        // Assert
        stats.TotalSkills.Should().Be(0);
        stats.MostUsedSkill.Should().BeNull();
        stats.MostSuccessfulSkill.Should().BeNull();
        stats.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void Stats_Equality_WorksCorrectly()
    {
        // Arrange
        var stats1 = new QdrantSkillRegistryStats(5, 0.75, 100, "skill1", "skill2", "http://localhost", "collection", true);
        var stats2 = new QdrantSkillRegistryStats(5, 0.75, 100, "skill1", "skill2", "http://localhost", "collection", true);

        // Assert
        stats1.Should().Be(stats2);
    }
}