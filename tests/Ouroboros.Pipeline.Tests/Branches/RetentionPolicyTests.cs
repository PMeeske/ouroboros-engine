using Ouroboros.Pipeline.Branches;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
public sealed class RetentionPolicyTests
{
    [Fact]
    public void ByAge_SetsMaxAge()
    {
        // Arrange
        var maxAge = TimeSpan.FromDays(30);

        // Act
        var policy = RetentionPolicy.ByAge(maxAge);

        // Assert
        policy.MaxAge.Should().Be(maxAge);
        policy.MaxCount.Should().BeNull();
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void ByCount_SetsMaxCount()
    {
        // Arrange & Act
        var policy = RetentionPolicy.ByCount(5);

        // Assert
        policy.MaxCount.Should().Be(5);
        policy.MaxAge.Should().BeNull();
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void Combined_SetsBothAgeAndCount()
    {
        // Arrange & Act
        var policy = RetentionPolicy.Combined(TimeSpan.FromDays(7), 10);

        // Assert
        policy.MaxAge.Should().Be(TimeSpan.FromDays(7));
        policy.MaxCount.Should().Be(10);
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void KeepAll_HasNoConstraints()
    {
        // Act
        var policy = RetentionPolicy.KeepAll();

        // Assert
        policy.MaxAge.Should().BeNull();
        policy.MaxCount.Should().BeNull();
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void KeepAtLeastOne_DefaultsToTrue()
    {
        // Act
        var policy = new RetentionPolicy();

        // Assert
        policy.KeepAtLeastOne.Should().BeTrue();
    }

    [Fact]
    public void KeepAtLeastOne_CanBeDisabled()
    {
        // Act
        var policy = new RetentionPolicy { KeepAtLeastOne = false };

        // Assert
        policy.KeepAtLeastOne.Should().BeFalse();
    }

    [Fact]
    public void ByAge_WithSmallDuration_SetsCorrectly()
    {
        // Act
        var policy = RetentionPolicy.ByAge(TimeSpan.FromMinutes(30));

        // Assert
        policy.MaxAge.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var policy1 = RetentionPolicy.Combined(TimeSpan.FromDays(7), 10);
        var policy2 = RetentionPolicy.Combined(TimeSpan.FromDays(7), 10);

        // Assert
        policy1.Should().Be(policy2);
    }
}
