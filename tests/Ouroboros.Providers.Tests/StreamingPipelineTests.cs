#pragma warning disable CA2000 // Test file - ownership is managed by test lifecycle
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class StreamingPipelineTests : IDisposable
{
    private readonly CollectiveMind _mind;
    private readonly StreamingPipeline _sut;

    public StreamingPipelineTests()
    {
        _mind = CollectiveMindFactory.CreateLocal();
        _sut = new StreamingPipeline(_mind);
    }

    [Fact]
    public void From_ReturnsSameInstance()
    {
        var result = _sut.From("test prompt");
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void OnlyThinking_ReturnsSameInstance()
    {
        var result = _sut.OnlyThinking();
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void OnlyContent_ReturnsSameInstance()
    {
        var result = _sut.OnlyContent();
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void Transform_ReturnsSameInstance()
    {
        var result = _sut.Transform(s => s.ToUpperInvariant());
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void Buffer_ReturnsSameInstance()
    {
        var result = _sut.Buffer(TimeSpan.FromMilliseconds(100));
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void Throttle_ReturnsSameInstance()
    {
        var result = _sut.Throttle(TimeSpan.FromMilliseconds(50));
        result.Should().BeSameAs(_sut);
    }

    [Fact]
    public void Execute_ReturnsObservable()
    {
        var observable = _sut.Execute("test prompt");
        observable.Should().NotBeNull();
    }

    [Fact]
    public void FluentChaining_WorksCorrectly()
    {
        // Verify fluent API allows chaining multiple operations
        var result = _sut
            .From("test")
            .OnlyContent()
            .Transform(s => s.Trim())
            .Buffer(TimeSpan.FromMilliseconds(100));

        result.Should().BeSameAs(_sut);
    }

    public void Dispose()
    {
        _mind.Dispose();
    }
}
