using FluentAssertions;
using Ouroboros.Interop.Hosting;
using Xunit;

namespace Ouroboros.Tests.Hosting;

[Trait("Category", "Unit")]
public sealed class MinimalHostTests
{
    [Fact]
    public async Task BuildAsync_WithEmptyArgs_ReturnsHost()
    {
        // Act
        var host = await MinimalHost.BuildAsync(Array.Empty<string>());

        // Assert
        host.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildAsync_WithArgs_DoesNotThrow()
    {
        // Act & Assert
        await FluentActions.Invoking(() => MinimalHost.BuildAsync(new[] { "--test", "value" }))
            .Should().NotThrowAsync();
    }
}
