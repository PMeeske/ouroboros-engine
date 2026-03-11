using FluentAssertions;
using Ouroboros.Network.Persistence;
using Xunit;

namespace Ouroboros.Tests.Network.Persistence;

[Trait("Category", "Unit")]
public class WalCompactorTests
{
    [Fact]
    public async Task CompactAsync_WithNullPath_ShouldReturnFailure()
    {
        var result = await WalCompactor.CompactAsync(null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public async Task CompactAsync_WithEmptyPath_ShouldReturnFailure()
    {
        var result = await WalCompactor.CompactAsync(string.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public async Task CompactAsync_WithNonExistentFile_ShouldReturnFailure()
    {
        var result = await WalCompactor.CompactAsync("/nonexistent/path/wal.dat");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task CompactAsync_ShouldSupportCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await WalCompactor.CompactAsync("/nonexistent/path.dat", cts.Token);

        result.IsFailure.Should().BeTrue();
    }
}
