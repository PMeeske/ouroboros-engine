using System.Reactive.Subjects;
using FluentAssertions;
using Ouroboros.Providers.Meai;

namespace Ouroboros.Tests.Meai;

[Trait("Category", "Unit")]
public sealed class ObservableAsyncEnumerableExtensionsTests
{
    [Fact]
    public void ToAsyncEnumerable_NullSource_ThrowsArgumentNullException()
    {
        // Arrange
        IObservable<int> source = null!;

        // Act
        var act = async () =>
        {
            await foreach (var _ in source.ToAsyncEnumerable())
            {
            }
        };

        // Assert
        act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ToAsyncEnumerable_ConvertsObservableItemsToAsyncEnumerable()
    {
        // Arrange
        var subject = new Subject<int>();
        var asyncEnum = subject.ToAsyncEnumerable();

        _ = Task.Run(async () =>
        {
            await Task.Delay(10);
            subject.OnNext(1);
            subject.OnNext(2);
            subject.OnNext(3);
            subject.OnCompleted();
        });

        // Act
        var results = new List<int>();
        await foreach (var item in asyncEnum)
        {
            results.Add(item);
        }

        // Assert
        results.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public async Task ToAsyncEnumerable_CompletesWhenObservableCompletes()
    {
        // Arrange
        var subject = new Subject<string>();
        var asyncEnum = subject.ToAsyncEnumerable();

        _ = Task.Run(async () =>
        {
            await Task.Delay(10);
            subject.OnNext("hello");
            subject.OnCompleted();
        });

        // Act
        var results = new List<string>();
        await foreach (var item in asyncEnum)
        {
            results.Add(item);
        }

        // Assert
        results.Should().ContainSingle().Which.Should().Be("hello");
    }
}
