// <copyright file="LazyVectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Lazy;

[Trait("Category", "Unit")]
public sealed class LazyVectorTests
{
    private static readonly VectorHandle TestHandle =
        new("qdrant", "docs", "vec-1", 3);

    private static readonly float[] TestVector = { 0.1f, 0.2f, 0.3f };

    [Fact]
    public void IsLoaded_Initially_IsFalse()
    {
        // Arrange
        var store = Substitute.For<IHandleAwareVectorStore>();
        var lazy = new LazyVector(TestHandle, store);

        // Assert
        lazy.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_FirstCall_FetchesFromStore()
    {
        // Arrange
        var store = Substitute.For<IHandleAwareVectorStore>();
        store.FetchAsync(TestHandle, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<float[], string>.Success(TestVector)));

        var lazy = new LazyVector(TestHandle, store);

        // Act
        var result = await lazy.GetAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(TestVector);
        lazy.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_SecondCall_UsesCacheNotStore()
    {
        // Arrange
        var store = Substitute.For<IHandleAwareVectorStore>();
        store.FetchAsync(TestHandle, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<float[], string>.Success(TestVector)));

        var lazy = new LazyVector(TestHandle, store);

        // Act — call twice
        await lazy.GetAsync();
        await lazy.GetAsync();

        // Assert — store called only once
        await store.Received(1).FetchAsync(TestHandle, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WhenStoreFails_ReturnsFailureAndDoesNotCache()
    {
        // Arrange
        var store = Substitute.For<IHandleAwareVectorStore>();
        store.FetchAsync(TestHandle, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<float[], string>.Failure("connection refused")));

        var lazy = new LazyVector(TestHandle, store);

        // Act
        var result = await lazy.GetAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        lazy.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task GetAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var store = Substitute.For<IHandleAwareVectorStore>();
        var lazy = new LazyVector(TestHandle, store);

        await lazy.DisposeAsync();

        await lazy.Invoking(async l => await l.GetAsync())
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task DisposeAsync_ClearsCachedData()
    {
        // Arrange
        var store = Substitute.For<IHandleAwareVectorStore>();
        store.FetchAsync(TestHandle, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<float[], string>.Success(TestVector)));

        var lazy = new LazyVector(TestHandle, store);
        await lazy.GetAsync();

        lazy.IsLoaded.Should().BeTrue();

        // Act
        await lazy.DisposeAsync();

        // Assert
        lazy.IsLoaded.Should().BeFalse();
    }

    [Fact]
    public void Handle_ReturnsConstructorValue()
    {
        var store = Substitute.For<IHandleAwareVectorStore>();
        var lazy = new LazyVector(TestHandle, store);
        lazy.Handle.Should().Be(TestHandle);
    }
}
