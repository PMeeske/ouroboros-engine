namespace Ouroboros.Tests.Pipeline.Council;

using Ouroboros.Pipeline.Council;

[Trait("Category", "Unit")]
public class CouncilTopicTests
{
    [Fact]
    public void Simple_CreatesTopicWithEmptyBackgroundAndConstraints()
    {
        var topic = CouncilTopic.Simple("Should we refactor?");

        topic.Question.Should().Be("Should we refactor?");
        topic.Background.Should().BeEmpty();
        topic.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void WithBackground_SetsQuestionAndBackground()
    {
        var topic = CouncilTopic.WithBackground("What to do?", "Context here");

        topic.Question.Should().Be("What to do?");
        topic.Background.Should().Be("Context here");
        topic.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var constraints = new List<string> { "C1", "C2" };
        var topic = new CouncilTopic("Q?", "BG", constraints);

        topic.Question.Should().Be("Q?");
        topic.Background.Should().Be("BG");
        topic.Constraints.Should().HaveCount(2);
    }
}
