namespace Ouroboros.Tests.Pipeline.WorldModel;

using Ouroboros.Pipeline.WorldModel;

[Trait("Category", "Unit")]
public class CapabilityTests
{
    [Fact]
    public void Create_WithoutTools_HasEmptyToolList()
    {
        var cap = Capability.Create("search", "Searches documents");

        cap.Name.Should().Be("search");
        cap.Description.Should().Be("Searches documents");
        cap.RequiredTools.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithTools_SetsToolList()
    {
        var cap = Capability.Create("search", "Searches", "vector-db", "embedder");

        cap.RequiredTools.Should().HaveCount(2);
        cap.RequiredTools.Should().Contain("vector-db");
    }

    [Fact]
    public void CanExecuteWith_ReturnsTrueWhenAllToolsAvailable()
    {
        var cap = Capability.Create("search", "Searches", "tool-a", "tool-b");
        var available = new HashSet<string> { "tool-a", "tool-b", "tool-c" };

        cap.CanExecuteWith(available).Should().BeTrue();
    }

    [Fact]
    public void CanExecuteWith_ReturnsFalseWhenToolMissing()
    {
        var cap = Capability.Create("search", "Searches", "tool-a", "tool-b");
        var available = new HashSet<string> { "tool-a" };

        cap.CanExecuteWith(available).Should().BeFalse();
    }

    [Fact]
    public void CanExecuteWith_ReturnsTrueWhenNoToolsRequired()
    {
        var cap = Capability.Create("think", "Just thinks");
        var available = new HashSet<string>();

        cap.CanExecuteWith(available).Should().BeTrue();
    }

    [Fact]
    public void Create_ThrowsOnNullName()
    {
        var act = () => Capability.Create(null!, "desc");
        act.Should().Throw<ArgumentNullException>();
    }
}
