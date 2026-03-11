using FluentAssertions;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Tests.MetaAI;

[Trait("Category", "Unit")]
public class PersistentSkillConfigTests
{
    [Fact]
    public void DefaultConstructor_ShouldSetDefaults()
    {
        // Arrange & Act
        var sut = new PersistentSkillConfig();

        // Assert
        sut.StoragePath.Should().Be("skills.json");
        sut.UseVectorStore.Should().BeTrue();
        sut.CollectionName.Should().Be("ouroboros_skills");
        sut.AutoSave.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithCustomValues_ShouldSetAllProperties()
    {
        // Arrange & Act
        var sut = new PersistentSkillConfig(
            StoragePath: "/data/skills.db",
            UseVectorStore: false,
            CollectionName: "custom_skills",
            AutoSave: false);

        // Assert
        sut.StoragePath.Should().Be("/data/skills.db");
        sut.UseVectorStore.Should().BeFalse();
        sut.CollectionName.Should().Be("custom_skills");
        sut.AutoSave.Should().BeFalse();
    }

    [Fact]
    public void RecordEquality_SameDefaults_ShouldBeEqual()
    {
        // Arrange
        var a = new PersistentSkillConfig();
        var b = new PersistentSkillConfig();

        // Assert
        a.Should().Be(b);
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        // Arrange
        var original = new PersistentSkillConfig();

        // Act
        var modified = original with { StoragePath = "other.json" };

        // Assert
        modified.StoragePath.Should().Be("other.json");
        modified.UseVectorStore.Should().BeTrue();
    }
}
