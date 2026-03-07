namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class DagSaveResultTests
{
    [Fact]
    public void Ctor_SetsProperties()
    {
        var errors = new List<string> { "error1", "error2" };
        var result = new DagSaveResult(10, 5, errors);

        result.NodesSaved.Should().Be(10);
        result.EdgesSaved.Should().Be(5);
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void Ctor_EmptyErrors()
    {
        var result = new DagSaveResult(0, 0, Array.Empty<string>());
        result.Errors.Should().BeEmpty();
    }
}
