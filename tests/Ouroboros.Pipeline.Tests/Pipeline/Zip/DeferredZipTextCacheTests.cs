namespace Ouroboros.Tests.Pipeline.Zip;

using Ouroboros.Pipeline.Ingestion.Zip;

[Trait("Category", "Unit")]
public class DeferredZipTextCacheTests
{
    [Fact]
    public void Store_AndTryTake_ReturnsStoredText()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        DeferredZipTextCache.Store(id, "hello world");

        // Act
        var found = DeferredZipTextCache.TryTake(id, out var text);

        // Assert
        found.Should().BeTrue();
        text.Should().Be("hello world");
    }

    [Fact]
    public void TryTake_RemovesItem_SecondCallReturnsFalse()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        DeferredZipTextCache.Store(id, "content");

        // Act
        DeferredZipTextCache.TryTake(id, out _);
        var secondResult = DeferredZipTextCache.TryTake(id, out _);

        // Assert
        secondResult.Should().BeFalse();
    }

    [Fact]
    public void TryPeek_ReturnsTextWithoutRemoving()
    {
        // Arrange
        var id = Guid.NewGuid().ToString();
        DeferredZipTextCache.Store(id, "persistent");

        // Act
        var peekResult = DeferredZipTextCache.TryPeek(id, out var peekedText);
        var takeResult = DeferredZipTextCache.TryTake(id, out var takenText);

        // Assert
        peekResult.Should().BeTrue();
        peekedText.Should().Be("persistent");
        takeResult.Should().BeTrue();
        takenText.Should().Be("persistent");
    }

    [Fact]
    public void TryTake_OnMissingId_ReturnsFalse()
    {
        // Act
        var result = DeferredZipTextCache.TryTake("nonexistent-" + Guid.NewGuid(), out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryPeek_OnMissingId_ReturnsFalse()
    {
        // Act
        var result = DeferredZipTextCache.TryPeek("nonexistent-" + Guid.NewGuid(), out _);

        // Assert
        result.Should().BeFalse();
    }
}
