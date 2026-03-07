namespace Ouroboros.Tests.Pipeline.Branches;

using Ouroboros.Pipeline.Branches;

[Trait("Category", "Unit")]
public class RetentionPolicyTests
{
    [Fact]
    public void ByAge_SetsMaxAge()
    {
        var policy = RetentionPolicy.ByAge(TimeSpan.FromDays(7));
        policy.MaxAge.Should().Be(TimeSpan.FromDays(7));
        policy.MaxCount.Should().BeNull();
    }

    [Fact]
    public void ByCount_SetsMaxCount()
    {
        var policy = RetentionPolicy.ByCount(5);
        policy.MaxCount.Should().Be(5);
        policy.MaxAge.Should().BeNull();
    }

    [Fact]
    public void Combined_SetsBothMaxAgeAndMaxCount()
    {
        var policy = RetentionPolicy.Combined(TimeSpan.FromDays(30), 10);
        policy.MaxAge.Should().Be(TimeSpan.FromDays(30));
        policy.MaxCount.Should().Be(10);
    }

    [Fact]
    public void KeepAll_HasNoConstraints()
    {
        var policy = RetentionPolicy.KeepAll();
        policy.MaxAge.Should().BeNull();
        policy.MaxCount.Should().BeNull();
    }

    [Fact]
    public void KeepAtLeastOne_DefaultsToTrue()
    {
        var policy = RetentionPolicy.KeepAll();
        policy.KeepAtLeastOne.Should().BeTrue();
    }
}
