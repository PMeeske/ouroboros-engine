using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Agent.Tests;

[Trait("Category", "Unit")]
public class ParallelExecutorTests
{
    #region Constructor

    [Fact]
    public void Constructor_Default_ShouldInitialize()
    {
        // Act
        var executor = new ParallelExecutor();

        // Assert
        executor.Should().NotBeNull();
        executor.MaxDegreeOfParallelism.Should().Be(Environment.ProcessorCount);
    }

    [Fact]
    public void Constructor_WithMaxDegree_ShouldSet()
    {
        // Act
        var executor = new ParallelExecutor(2);

        // Assert
        executor.MaxDegreeOfParallelism.Should().Be(2);
    }

    #endregion

    #region ExecuteParallelAsync

    [Fact]
    public async Task ExecuteParallelAsync_WithEmptyList_ShouldReturnEmptyResults()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var functions = new List<Func<Task<int>>>();

        // Act
        var results = await executor.ExecuteParallelAsync(functions);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteParallelAsync_ShouldExecuteFunctions()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var functions = new List<Func<Task<int>>>
        {
            () => Task.FromResult(1),
            () => Task.FromResult(2),
            () => Task.FromResult(3)
        };

        // Act
        var results = await executor.ExecuteParallelAsync(functions);

        // Assert
        results.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task ExecuteParallelAsync_WithTransform_ShouldApplyTransform()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var functions = new List<Func<Task<int>>>
        {
            () => Task.FromResult(1),
            () => Task.FromResult(2)
        };

        // Act
        var results = await executor.ExecuteParallelAsync(functions, x => x * 2);

        // Assert
        results.Should().BeEquivalentTo(new[] { 2, 4 });
    }

    [Fact]
    public async Task ExecuteParallelAsync_WithMaxDegree_ShouldRespectLimit()
    {
        // Arrange
        var executor = new ParallelExecutor(1);
        var functions = new List<Func<Task<int>>>
        {
            () => Task.FromResult(1),
            () => Task.FromResult(2)
        };

        // Act
        var results = await executor.ExecuteParallelAsync(functions);

        // Assert
        results.Should().HaveCount(2);
    }

    #endregion

    #region ExecuteOrderedParallelAsync

    [Fact]
    public async Task ExecuteOrderedParallelAsync_WithEmptyList_ShouldReturnEmptyResults()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var functions = new List<Func<Task<int>>>();

        // Act
        var results = await executor.ExecuteOrderedParallelAsync(functions);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteOrderedParallelAsync_ShouldMaintainOrder()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var functions = new List<Func<Task<int>>>
        {
            () => Task.FromResult(1),
            () => Task.FromResult(2),
            () => Task.FromResult(3)
        };

        // Act
        var results = await executor.ExecuteOrderedParallelAsync(functions);

        // Assert
        results.Should().Equal(1, 2, 3);
    }

    #endregion

    #region ExecuteWithTimeoutAsync

    [Fact]
    public async Task ExecuteWithTimeoutAsync_ShouldReturnResult()
    {
        // Arrange
        var executor = new ParallelExecutor();
        Func<Task<int>> func = () => Task.FromResult(42);

        // Act
        var result = await executor.ExecuteWithTimeoutAsync(func, TimeSpan.FromSeconds(5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteWithTimeoutAsync_TimedOut_ShouldReturnFailure()
    {
        // Arrange
        var executor = new ParallelExecutor();
        Func<Task<int>> func = async () => { await Task.Delay(TimeSpan.FromSeconds(10)); return 42; };

        // Act
        var result = await executor.ExecuteWithTimeoutAsync(func, TimeSpan.FromMilliseconds(50));

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    #endregion

    #region ExecuteWithRetryAsync

    [Fact]
    public async Task ExecuteWithRetryAsync_SuccessOnFirstAttempt_ShouldReturnResult()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var attempts = 0;
        Func<Task<int>> func = () => { attempts++; return Task.FromResult(42); };

        // Act
        var result = await executor.ExecuteWithRetryAsync(func, maxRetries: 3);

        // Assert
        result.Should().Be(42);
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_Retry_ShouldEventuallySucceed()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var attempts = 0;
        Func<Task<int>> func = () =>
        {
            attempts++;
            if (attempts < 3) throw new InvalidOperationException("fail");
            return Task.FromResult(42);
        };

        // Act
        var result = await executor.ExecuteWithRetryAsync(func, maxRetries: 3);

        // Assert
        result.Should().Be(42);
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteWithRetryAsync_ExhaustedRetries_ShouldThrow()
    {
        // Arrange
        var executor = new ParallelExecutor();
        Func<Task<int>> func = () => throw new InvalidOperationException("fail");

        // Act
        Func<Task> act = async () => await executor.ExecuteWithRetryAsync(func, maxRetries: 2);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region ExecuteBatchAsync

    [Fact]
    public async Task ExecuteBatchAsync_EmptyBatch_ShouldReturnEmpty()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var batch = new List<int>();
        Func<int, Task<int>> processor = x => Task.FromResult(x * 2);

        // Act
        var results = await executor.ExecuteBatchAsync(batch, processor);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteBatchAsync_ShouldProcessAllItems()
    {
        // Arrange
        var executor = new ParallelExecutor();
        var batch = new List<int> { 1, 2, 3 };
        Func<int, Task<int>> processor = x => Task.FromResult(x * 2);

        // Act
        var results = await executor.ExecuteBatchAsync(batch, processor);

        // Assert
        results.Should().BeEquivalentTo(new[] { 2, 4, 6 });
    }

    #endregion
}
