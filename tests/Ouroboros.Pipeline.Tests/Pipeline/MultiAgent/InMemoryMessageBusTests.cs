// <copyright file="InMemoryMessageBusTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Xunit;
using Ouroboros.Pipeline.MultiAgent;

namespace Ouroboros.Tests.Pipeline.MultiAgent;

[Trait("Category", "Unit")]
public sealed class InMemoryMessageBusTests : IDisposable
{
    private readonly InMemoryMessageBus _bus = new(maxHistorySize: 50);

    public void Dispose() => _bus.Dispose();

    [Fact]
    public void Constructor_NegativeHistorySize_Throws()
    {
        Action act = () => _ = new InMemoryMessageBus(maxHistorySize: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_ZeroHistorySize_DoesNotThrow()
    {
        using var bus = new InMemoryMessageBus(maxHistorySize: 0);
        bus.MessageHistoryCount.Should().Be(0);
    }

    [Fact]
    public void SubscriptionCount_InitiallyZero()
    {
        _bus.SubscriptionCount.Should().Be(0);
    }

    [Fact]
    public void Subscribe_IncrementsCount()
    {
        var agentId = Guid.NewGuid();
        _bus.Subscribe(agentId, null, _ => Task.CompletedTask);

        _bus.SubscriptionCount.Should().Be(1);
    }

    [Fact]
    public void Subscribe_NullHandler_Throws()
    {
        Action act = () => _bus.Subscribe(Guid.NewGuid(), null, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Subscribe_ReturnsSubscriptionWithCorrectAgentId()
    {
        var agentId = Guid.NewGuid();
        var sub = _bus.Subscribe(agentId, "topic", _ => Task.CompletedTask);

        sub.AgentId.Should().Be(agentId);
        sub.TopicFilter.Should().Be("topic");
    }

    [Fact]
    public void Unsubscribe_RemovesSubscription()
    {
        var agentId = Guid.NewGuid();
        var sub = _bus.Subscribe(agentId, null, _ => Task.CompletedTask);

        _bus.Unsubscribe(sub.Id);

        _bus.SubscriptionCount.Should().Be(0);
    }

    [Fact]
    public async Task PublishAsync_NullMessage_Throws()
    {
        Func<Task> act = () => _bus.PublishAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_AfterDispose_Throws()
    {
        _bus.Dispose();

        Func<Task> act = () => _bus.PublishAsync(
            AgentMessage.CreateNotification(Guid.NewGuid(), "t", "p"));

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task PublishAsync_MessageAppearsInHistory()
    {
        var msg = AgentMessage.CreateNotification(Guid.NewGuid(), "topic", "payload");
        await _bus.PublishAsync(msg);

        // Allow processing
        await Task.Delay(100);

        _bus.MessageHistoryCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void GetPendingMessages_NoPending_ReturnsEmpty()
    {
        var agentId = Guid.NewGuid();
        var messages = _bus.GetPendingMessages(agentId);

        messages.Should().BeEmpty();
    }

    [Fact]
    public void GetAllSubscriptions_ReturnsAll()
    {
        _bus.Subscribe(Guid.NewGuid(), null, _ => Task.CompletedTask);
        _bus.Subscribe(Guid.NewGuid(), null, _ => Task.CompletedTask);

        var all = _bus.GetAllSubscriptions();

        all.Should().HaveCount(2);
    }

    [Fact]
    public void GetSubscriptionsForAgent_FiltersByAgentId()
    {
        var agentId = Guid.NewGuid();
        _bus.Subscribe(agentId, null, _ => Task.CompletedTask);
        _bus.Subscribe(Guid.NewGuid(), null, _ => Task.CompletedTask);

        var subs = _bus.GetSubscriptionsForAgent(agentId);

        subs.Should().HaveCount(1);
        subs[0].AgentId.Should().Be(agentId);
    }

    [Fact]
    public void ClearHistory_EmptiesHistory()
    {
        _bus.ClearHistory();
        _bus.MessageHistoryCount.Should().Be(0);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        _bus.Dispose();
        _bus.Dispose(); // should not throw
    }

    [Fact]
    public async Task RequestAsync_NullRequest_Throws()
    {
        Func<Task> act = () => _bus.RequestAsync(null!, TimeSpan.FromSeconds(1));
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RequestAsync_NonRequestType_Throws()
    {
        var msg = AgentMessage.CreateNotification(Guid.NewGuid(), "topic", "p");

        Func<Task> act = () => _bus.RequestAsync(msg, TimeSpan.FromSeconds(1));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Request*");
    }

    [Fact]
    public void MessageHistory_ReturnsImmutableSnapshot()
    {
        var history = _bus.MessageHistory;
        history.Should().NotBeNull();
        history.Should().BeEmpty();
    }
}
