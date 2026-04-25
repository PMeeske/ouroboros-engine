using Ouroboros.Agent.MetaAI;
using Ouroboros.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class OrchestrationExtensionsTests
{
    #region OrchestrationTracing

    [Fact]
    public void Trace_Start_ShouldNotThrow()
    {
        // Arrange
        var context = new Mock<IActiveContext>();

        // Act
        var span = OrchestrationTracing.StartSpan("test-span", context.Object);

        // Assert
        span.Should().NotBeNull();
    }

    [Fact]
    public void Trace_RecordEvent_ShouldNotThrow()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        var span = OrchestrationTracing.StartSpan("test-span", context.Object);

        // Act
        OrchestrationTracing.RecordEvent(span, "test-event");

        // Assert
        // No exception
    }

    [Fact]
    public void Trace_RecordMetric_ShouldNotThrow()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        var span = OrchestrationTracing.StartSpan("test-span", context.Object);

        // Act
        OrchestrationTracing.RecordMetric(span, "metric", 42.0);

        // Assert
        // No exception
    }

    [Fact]
    public void Trace_EndSpan_ShouldNotThrow()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        var span = OrchestrationTracing.StartSpan("test-span", context.Object);

        // Act
        OrchestrationTracing.EndSpan(span);

        // Assert
        // No exception
    }

    [Fact]
    public void Trace_EndSpan_NullSpan_ShouldNotThrow()
    {
        // Act
        OrchestrationTracing.EndSpan(null!);

        // Assert
        // No exception
    }

    [Fact]
    public void Trace_CreateChildSpan_ShouldNotThrow()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        var parent = OrchestrationTracing.StartSpan("parent", context.Object);

        // Act
        var child = OrchestrationTracing.CreateChildSpan(parent, "child", context.Object);

        // Assert
        child.Should().NotBeNull();
    }

    [Fact]
    public void Trace_CreateChildSpan_NullParent_ShouldNotThrow()
    {
        // Arrange
        var context = new Mock<IActiveContext>();

        // Act
        var child = OrchestrationTracing.CreateChildSpan(null!, "child", context.Object);

        // Assert
        child.Should().NotBeNull();
    }

    #endregion

    #region OrchestrationInstrumentationExtensions

    [Fact]
    public void Instrument_WithNullFactory_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new Mock<IActiveContext>();

        // Act
        Action act = () => OrchestrationInstrumentationExtensions.Instrument<string>(null!, context.Object, "name");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Instrument_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => OrchestrationInstrumentationExtensions.Instrument(() => "result", null!, "name");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Instrument_WithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var context = new Mock<IActiveContext>();

        // Act
        Action act = () => OrchestrationInstrumentationExtensions.Instrument(() => "result", context.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Instrument_WithValidParams_ShouldExecute()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        Func<string> factory = () => "result";

        // Act
        var result = OrchestrationInstrumentationExtensions.Instrument(factory, context.Object, "test");

        // Assert
        result.Should().Be("result");
    }

    [Fact]
    public void InstrumentAsync_WithValidParams_ShouldExecute()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        Func<Task<string>> factory = () => Task.FromResult("result");

        // Act
        var result = OrchestrationInstrumentationExtensions.InstrumentAsync(factory, context.Object, "test");

        // Assert
        result.Result.Should().Be("result");
    }

    [Fact]
    public void InstrumentWithMetrics_WithValidParams_ShouldExecute()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        var metrics = new Dictionary<string, double> { ["latency"] = 100 };
        Func<string> factory = () => "result";

        // Act
        var result = OrchestrationInstrumentationExtensions.InstrumentWithMetrics(factory, context.Object, "test", metrics);

        // Assert
        result.Should().Be("result");
    }

    [Fact]
    public void InstrumentWithMetricsAsync_WithValidParams_ShouldExecute()
    {
        // Arrange
        var context = new Mock<IActiveContext>();
        var metrics = new Dictionary<string, double> { ["latency"] = 100 };
        Func<Task<string>> factory = () => Task.FromResult("result");

        // Act
        var result = OrchestrationInstrumentationExtensions.InstrumentWithMetricsAsync(factory, context.Object, "test", metrics);

        // Assert
        result.Result.Should().Be("result");
    }

    #endregion

    #region OrchestrationCacheExtensions

    [Fact]
    public void GetOrCreateCached_WithNullCache_ShouldExecuteFactory()
    {
        // Arrange
        Func<string> factory = () => "result";

        // Act
        var result = OrchestrationCacheExtensions.GetOrCreateCached(null!, "key", factory, TimeSpan.FromMinutes(1));

        // Assert
        result.Should().Be("result");
    }

    [Fact]
    public void GetOrCreateCached_WithNullFactory_ShouldThrow()
    {
        // Arrange
        var cache = new Mock<IMemoryCache>();

        // Act
        Action act = () => OrchestrationCacheExtensions.GetOrCreateCached(cache.Object, "key", null!, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetOrCreateCached_WithNullKey_ShouldThrow()
    {
        // Arrange
        var cache = new Mock<IMemoryCache>();
        Func<string> factory = () => "result";

        // Act
        Action act = () => OrchestrationCacheExtensions.GetOrCreateCached(cache.Object, null!, factory, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetOrCreateCachedAsync_WithNullCache_ShouldExecuteFactory()
    {
        // Arrange
        Func<Task<string>> factory = () => Task.FromResult("result");

        // Act
        var result = OrchestrationCacheExtensions.GetOrCreateCachedAsync(null!, "key", factory, TimeSpan.FromMinutes(1));

        // Assert
        result.Result.Should().Be("result");
    }

    [Fact]
    public void GetOrCreateCachedAsync_WithNullFactory_ShouldThrow()
    {
        // Arrange
        var cache = new Mock<IMemoryCache>();

        // Act
        Func<Task> act = async () => await OrchestrationCacheExtensions.GetOrCreateCachedAsync(cache.Object, "key", null!, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void GetOrCreateCachedAsync_WithNullKey_ShouldThrow()
    {
        // Arrange
        var cache = new Mock<IMemoryCache>();
        Func<Task<string>> factory = () => Task.FromResult("result");

        // Act
        Func<Task> act = async () => await OrchestrationCacheExtensions.GetOrCreateCachedAsync(cache.Object, null!, factory, TimeSpan.FromMinutes(1));

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void RemoveFromCache_WithNullCache_ShouldNotThrow()
    {
        // Act
        OrchestrationCacheExtensions.RemoveFromCache(null!, "key");

        // Assert
        // No exception
    }

    [Fact]
    public void RemoveFromCache_WithNullKey_ShouldNotThrow()
    {
        // Arrange
        var cache = new Mock<IMemoryCache>();

        // Act
        OrchestrationCacheExtensions.RemoveFromCache(cache.Object, null!);

        // Assert
        // No exception
    }

    [Fact]
    public void ClearCache_WithNullCache_ShouldNotThrow()
    {
        // Act
        OrchestrationCacheExtensions.ClearCache(null!);

        // Assert
        // No exception
    }

    #endregion

    #region OptionExtensions

    [Fact]
    public void GetValueOrDefault_Some_ShouldReturnValue()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void GetValueOrDefault_None_ShouldReturnDefault()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.GetValueOrDefault();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetValueOrDefault_NoneWithCustomDefault_ShouldReturnCustom()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.GetValueOrDefault(99);

        // Assert
        result.Should().Be(99);
    }

    [Fact]
    public void ToOption_NonNull_ShouldReturnSome()
    {
        // Arrange
        var value = "test";

        // Act
        var result = value.ToOption();

        // Assert
        result.IsSome.Should().BeTrue();
        result.Value.Should().Be("test");
    }

    [Fact]
    public void ToOption_Null_ShouldReturnNone()
    {
        // Arrange
        string? value = null;

        // Act
        var result = value.ToOption();

        // Assert
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Where_SomeMatchingPredicate_ShouldReturnSome()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var result = option.Where(x => x > 10);

        // Assert
        result.IsSome.Should().BeTrue();
    }

    [Fact]
    public void Where_SomeNonMatchingPredicate_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var result = option.Where(x => x > 10);

        // Assert
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Where_None_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.Where(x => x > 10);

        // Assert
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Map_Some_ShouldTransformValue()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var result = option.Map(x => x * 2);

        // Assert
        result.IsSome.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public void Map_None_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.Map(x => x * 2);

        // Assert
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Bind_Some_ShouldFlatten()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var result = option.Bind(x => Option<int>.Some(x * 2));

        // Assert
        result.IsSome.Should().BeTrue();
        result.Value.Should().Be(10);
    }

    [Fact]
    public void Bind_None_ShouldReturnNone()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.Bind(x => Option<int>.Some(x * 2));

        // Assert
        result.IsNone.Should().BeTrue();
    }

    [Fact]
    public void Match_Some_ShouldExecuteSomeFunction()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var result = option.Match(x => x * 2, () => 0);

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public void Match_None_ShouldExecuteNoneFunction()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.Match(x => x * 2, () => 99);

        // Assert
        result.Should().Be(99);
    }

    [Fact]
    public void Do_Some_ShouldExecuteAction()
    {
        // Arrange
        var option = Option<int>.Some(5);
        var sideEffect = 0;

        // Act
        option.Do(x => sideEffect = x);

        // Assert
        sideEffect.Should().Be(5);
    }

    [Fact]
    public void Do_None_ShouldNotExecuteAction()
    {
        // Arrange
        var option = Option<int>.None;
        var sideEffect = 0;

        // Act
        option.Do(x => sideEffect = x);

        // Assert
        sideEffect.Should().Be(0);
    }

    [Fact]
    public void DoIfNone_None_ShouldExecuteAction()
    {
        // Arrange
        var option = Option<int>.None;
        var sideEffect = false;

        // Act
        option.DoIfNone(() => sideEffect = true);

        // Assert
        sideEffect.Should().BeTrue();
    }

    [Fact]
    public void DoIfNone_Some_ShouldNotExecuteAction()
    {
        // Arrange
        var option = Option<int>.Some(5);
        var sideEffect = false;

        // Act
        option.DoIfNone(() => sideEffect = true);

        // Assert
        sideEffect.Should().BeFalse();
    }

    [Fact]
    public void IsDefined_Some_ShouldReturnTrue()
    {
        // Arrange
        var option = Option<int>.Some(5);

        // Act
        var result = option.IsDefined;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDefined_None_ShouldReturnFalse()
    {
        // Arrange
        var option = Option<int>.None;

        // Act
        var result = option.IsDefined;

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
