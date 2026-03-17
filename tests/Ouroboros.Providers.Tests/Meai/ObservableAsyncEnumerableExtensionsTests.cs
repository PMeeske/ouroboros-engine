using System.Reactive.Linq;
using System.Reactive.Subjects;
using FluentAssertions;
using Ouroboros.Providers.Meai;
using Xunit;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class ObservableAsyncEnumerableExtensionsTests
{
    [Fact]
    public async Task ToAsyncEnumerable_WithItems_YieldsAllItems()
    {
        // Arrange
        var source = new[] { 1, 2, 3 }.ToObservable();
        var results = new List<int>();

        // Act
        await foreach (var item in source.ToAsyncEnumerable())
        {
            results.Add(item);
        }

        // Assert
        results.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task ToAsyncEnumerable_WithEmptyObservable_YieldsNothing()
    {
        // Arrange
        var source = Observable.Empty<int>();
        var results = new List<int>();

        // Act
        await foreach (var item in source.ToAsyncEnumerable())
        {
            results.Add(item);
        }

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void ToAsyncEnumerable_WithNullSource_ThrowsArgumentNullException()
    {
        // Act & Assert
        FluentActions.Invoking(() =>
        {
            IObservable<int>? source = null;
            return source!.ToAsyncEnumerable();
        }).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ToAsyncEnumerable_WithCancellation_StopsEnumeration()
    {
        // Arrange
        var subject = new Subject<int>();
        var cts = new CancellationTokenSource();
        var results = new List<int>();

        // Act
        var enumeration = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in subject.ToAsyncEnumerable(cts.Token))
                {
                    results.Add(item);
                    if (results.Count >= 2)
                    {
                        cts.Cancel();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        });

        subject.OnNext(1);
        subject.OnNext(2);

        // Give time for async processing
        await Task.Delay(100);

        subject.OnNext(3);

        await enumeration;

        // Assert
        results.Should().HaveCountLessThanOrEqualTo(3);
        results.Should().Contain(1);
        results.Should().Contain(2);
    }

    [Fact]
    public async Task ToAsyncEnumerable_WithError_ThrowsFromChannel()
    {
        // Arrange
        var subject = new Subject<int>();
        var results = new List<int>();

        // Act
        var task = Task.Run(async () =>
        {
            await foreach (var item in subject.ToAsyncEnumerable())
            {
                results.Add(item);
            }
        });

        subject.OnNext(1);
        await Task.Delay(50);
        subject.OnError(new InvalidOperationException("Test error"));

        // Assert
        Func<Task> act = () => task;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test error");
    }

    [Fact]
    public async Task ToAsyncEnumerable_WithSubject_YieldsItemsAsTheyArrive()
    {
        // Arrange
        var subject = new Subject<string>();
        var results = new List<string>();

        // Act
        var enumeration = Task.Run(async () =>
        {
            await foreach (var item in subject.ToAsyncEnumerable())
            {
                results.Add(item);
            }
        });

        subject.OnNext("hello");
        await Task.Delay(50);
        subject.OnNext("world");
        await Task.Delay(50);
        subject.OnCompleted();

        await enumeration;

        // Assert
        results.Should().BeEquivalentTo(new[] { "hello", "world" });
    }
}
