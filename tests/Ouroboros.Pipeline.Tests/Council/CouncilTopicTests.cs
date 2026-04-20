using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

[Trait("Category", "Unit")]
public class CouncilTopicTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var constraints = new List<string> { "Must be secure", "Must be fast" };

        // Act
        var topic = new CouncilTopic("Should we refactor?", "Code is complex", constraints);

        // Assert
        topic.Question.Should().Be("Should we refactor?");
        topic.Background.Should().Be("Code is complex");
        topic.Constraints.Should().BeEquivalentTo(constraints);
    }

    [Fact]
    public void Simple_CreatesTopicWithQuestionOnly()
    {
        // Act
        var topic = CouncilTopic.Simple("Should we use microservices?");

        // Assert
        topic.Question.Should().Be("Should we use microservices?");
        topic.Background.Should().BeEmpty();
        topic.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void WithBackground_CreatesTopicWithQuestionAndBackground()
    {
        // Act
        var topic = CouncilTopic.WithBackground(
            "Should we migrate?",
            "Current system is monolithic");

        // Assert
        topic.Question.Should().Be("Should we migrate?");
        topic.Background.Should().Be("Current system is monolithic");
        topic.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        // Act
        var topic1 = CouncilTopic.Simple("Question");
        var topic2 = CouncilTopic.Simple("Question");

        // Assert
        topic1.Should().Be(topic2);
    }

    [Fact]
    public void RecordEquality_WithDifferentQuestions_AreNotEqual()
    {
        // Act
        var topic1 = CouncilTopic.Simple("Question A");
        var topic2 = CouncilTopic.Simple("Question B");

        // Assert
        topic1.Should().NotBe(topic2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        // Arrange
        var original = CouncilTopic.Simple("Original question");

        // Act
        var modified = original with { Background = "Added background" };

        // Assert
        modified.Question.Should().Be("Original question");
        modified.Background.Should().Be("Added background");
    }
}
