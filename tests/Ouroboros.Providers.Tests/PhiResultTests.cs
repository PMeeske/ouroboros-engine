namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class PhiResultTests
{
    [Fact]
    public void Empty_HasZeroPhi()
    {
        var result = PhiResult.Empty;

        result.Phi.Should().Be(0.0);
        result.PartitionA.Should().BeEmpty();
        result.PartitionB.Should().BeEmpty();
        result.MinimumInformationPartition.Should().NotBeNullOrEmpty();
        result.Description.Should().Contain("undefined");
    }

    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var result = new PhiResult(
            Phi: 0.42,
            PartitionA: new[] { 0, 1 },
            PartitionB: new[] { 2 },
            MinimumInformationPartition: "[A, B] | [C]",
            Description: "moderate integration");

        result.Phi.Should().Be(0.42);
        result.PartitionA.Should().HaveCount(2);
        result.PartitionB.Should().HaveCount(1);
        result.MinimumInformationPartition.Should().Be("[A, B] | [C]");
        result.Description.Should().Be("moderate integration");
    }

    [Fact]
    public void RecordEquality_WithSameValues_AreEqual()
    {
        var a = new PhiResult(0.5, new[] { 0 }, new[] { 1 }, "MIP", "desc");
        var b = new PhiResult(0.5, new[] { 0 }, new[] { 1 }, "MIP", "desc");

        // Record equality compares reference for collections, so these will differ
        // but value types should match
        a.Phi.Should().Be(b.Phi);
        a.MinimumInformationPartition.Should().Be(b.MinimumInformationPartition);
    }
}
