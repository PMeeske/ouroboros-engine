using FluentAssertions;
using Ouroboros.Pipeline.Ingestion.Zip;

namespace Ouroboros.Tests.Zip;

[Trait("Category", "Unit")]
public class DeferredZipTextCacheTests
{
    // Use unique IDs per test to avoid cross-test interference since the cache is static
    private static string UniqueId() => Guid.NewGuid().ToString("N");

    [Fact]
    public void Store_ThenTryPeek_ReturnsText()
    {
        // Arrange
        var id = UniqueId();
        DeferredZipTextCache.Store(id, "hello world");

        // Act
        var found = DeferredZipTextCache.TryPeek(id, out var text);

        // Assert
        found.Should().BeTrue();
        text.Should().Be("hello world");

        // Cleanup
        DeferredZipTextCache.TryTake(id, out _);
    }

    [Fact]
    public void Store_ThenTryTake_ReturnsAndRemovesText()
    {
        // Arrange
        var id = UniqueId();
        DeferredZipTextCache.Store(id, "take me");

        // Act
        var found = DeferredZipTextCache.TryTake(id, out var text);

        // Assert
        found.Should().BeTrue();
        text.Should().Be("take me");

        // Verify it was removed
        var foundAgain = DeferredZipTextCache.TryTake(id, out _);
        foundAgain.Should().BeFalse();
    }

    [Fact]
    public void TryPeek_NonExistentId_ReturnsFalse()
    {
        // Arrange
        var id = UniqueId();

        // Act
        var found = DeferredZipTextCache.TryPeek(id, out var text);

        // Assert
        found.Should().BeFalse();
    }

    [Fact]
    public void TryTake_NonExistentId_ReturnsFalseAndEmptyString()
    {
        // Arrange
        var id = UniqueId();

        // Act
        var found = DeferredZipTextCache.TryTake(id, out var text);

        // Assert
        found.Should().BeFalse();
        text.Should().BeEmpty();
    }

    [Fact]
    public void TryPeek_DoesNotRemoveEntry()
    {
        // Arrange
        var id = UniqueId();
        DeferredZipTextCache.Store(id, "persistent");

        // Act
        DeferredZipTextCache.TryPeek(id, out _);
        DeferredZipTextCache.TryPeek(id, out _);
        var found = DeferredZipTextCache.TryPeek(id, out var text);

        // Assert
        found.Should().BeTrue();
        text.Should().Be("persistent");

        // Cleanup
        DeferredZipTextCache.TryTake(id, out _);
    }

    [Fact]
    public void Store_OverwritesPreviousValue()
    {
        // Arrange
        var id = UniqueId();
        DeferredZipTextCache.Store(id, "original");

        // Act
        DeferredZipTextCache.Store(id, "updated");
        DeferredZipTextCache.TryPeek(id, out var text);

        // Assert
        text.Should().Be("updated");

        // Cleanup
        DeferredZipTextCache.TryTake(id, out _);
    }

    [Fact]
    public void Store_EmptyString_StoresSuccessfully()
    {
        // Arrange
        var id = UniqueId();

        // Act
        DeferredZipTextCache.Store(id, string.Empty);
        var found = DeferredZipTextCache.TryPeek(id, out var text);

        // Assert
        found.Should().BeTrue();
        text.Should().BeEmpty();

        // Cleanup
        DeferredZipTextCache.TryTake(id, out _);
    }

    [Fact]
    public void MultipleEntries_AreIndependent()
    {
        // Arrange
        var id1 = UniqueId();
        var id2 = UniqueId();
        DeferredZipTextCache.Store(id1, "first");
        DeferredZipTextCache.Store(id2, "second");

        // Act
        DeferredZipTextCache.TryTake(id1, out var text1);

        // Assert
        text1.Should().Be("first");
        DeferredZipTextCache.TryPeek(id2, out var text2);
        text2.Should().Be("second");

        // Cleanup
        DeferredZipTextCache.TryTake(id2, out _);
    }

    [Fact]
    public void Store_LargeText_StoresSuccessfully()
    {
        // Arrange
        var id = UniqueId();
        var largeText = new string('x', 100_000);

        // Act
        DeferredZipTextCache.Store(id, largeText);
        var found = DeferredZipTextCache.TryTake(id, out var text);

        // Assert
        found.Should().BeTrue();
        text.Should().HaveLength(100_000);
    }
}
