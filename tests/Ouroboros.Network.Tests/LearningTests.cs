namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class LearningTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var ts = DateTimeOffset.UtcNow;
        var learning = new Learning(
            "id1", "skill", "Learned X", "During task Y", 0.9, 5, ts);

        learning.Id.Should().Be("id1");
        learning.Category.Should().Be("skill");
        learning.Content.Should().Be("Learned X");
        learning.Context.Should().Be("During task Y");
        learning.Confidence.Should().Be(0.9);
        learning.Epoch.Should().Be(5);
        learning.Timestamp.Should().Be(ts);
    }
}
