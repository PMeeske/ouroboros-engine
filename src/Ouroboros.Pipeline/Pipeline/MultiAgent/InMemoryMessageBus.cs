using System.Threading.Channels;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Provides a thread-safe in-memory implementation of the message bus for inter-agent communication.
/// </summary>
public sealed class InMemoryMessageBus : IMessageBus, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions;
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<AgentMessage>> _pendingMessages;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<AgentMessage>> _pendingRequests;
    private readonly Channel<AgentMessage> _messageChannel;
    private readonly ConcurrentQueue<AgentMessage> _messageHistory;
    private readonly int _maxHistorySize;
    private readonly CancellationTokenSource _disposalTokenSource;
    private readonly Task _processingTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryMessageBus"/> class.
    /// </summary>
    /// <param name="maxHistorySize">The maximum number of messages to retain in history.</param>
    public InMemoryMessageBus(int maxHistorySize = 1000)
    {
        if (maxHistorySize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHistorySize), "Max history size must be non-negative.");
        }

        _subscriptions = new ConcurrentDictionary<Guid, Subscription>();
        _pendingMessages = new ConcurrentDictionary<Guid, ConcurrentQueue<AgentMessage>>();
        _pendingRequests = new ConcurrentDictionary<Guid, TaskCompletionSource<AgentMessage>>();
        _messageChannel = Channel.CreateUnbounded<AgentMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _messageHistory = new ConcurrentQueue<AgentMessage>();
        _maxHistorySize = maxHistorySize;
        _disposalTokenSource = new CancellationTokenSource();
        _processingTask = ProcessMessagesAsync(_disposalTokenSource.Token);
    }

    /// <summary>
    /// Gets the current number of active subscriptions.
    /// </summary>
    public int SubscriptionCount => _subscriptions.Count;

    /// <summary>
    /// Gets the current number of messages in history.
    /// </summary>
    public int MessageHistoryCount => _messageHistory.Count;

    /// <summary>
    /// Gets an immutable snapshot of the message history.
    /// </summary>
    public ImmutableList<AgentMessage> MessageHistory => _messageHistory.ToImmutableList();

    /// <inheritdoc/>
    public async Task PublishAsync(AgentMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _messageChannel.Writer.WriteAsync(message, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AgentMessage> RequestAsync(AgentMessage request, TimeSpan timeout, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (request.Type != MessageType.Request)
        {
            throw new ArgumentException("Message must be of type Request.", nameof(request));
        }

        if (request.CorrelationId is null)
        {
            throw new ArgumentException("Request message must have a correlation ID.", nameof(request));
        }

        Guid correlationId = request.CorrelationId.Value;
        TaskCompletionSource<AgentMessage> tcs = new TaskCompletionSource<AgentMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pendingRequests.TryAdd(correlationId, tcs))
        {
            throw new InvalidOperationException($"A request with correlation ID {correlationId} is already pending.");
        }

        try
        {
            await PublishAsync(request, ct).ConfigureAwait(false);

            using CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            Task completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token)).ConfigureAwait(false);

            if (completedTask == tcs.Task)
            {
                return await tcs.Task.ConfigureAwait(false);
            }

            throw new TimeoutException($"Request timed out after {timeout.TotalMilliseconds}ms.");
        }
        finally
        {
            _pendingRequests.TryRemove(correlationId, out _);
        }
    }

    /// <inheritdoc/>
    public Subscription Subscribe(Guid agentId, string? topicFilter, Func<AgentMessage, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        Subscription subscription = new Subscription(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            TopicFilter: topicFilter,
            Handler: handler);

        if (!_subscriptions.TryAdd(subscription.Id, subscription))
        {
            throw new InvalidOperationException("Failed to add subscription. Please try again.");
        }

        // Ensure pending message queue exists for this agent
        _pendingMessages.GetOrAdd(agentId, _ => new ConcurrentQueue<AgentMessage>());

        return subscription;
    }

    /// <inheritdoc/>
    public void Unsubscribe(Guid subscriptionId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _subscriptions.TryRemove(subscriptionId, out _);
    }

    /// <inheritdoc/>
    public IReadOnlyList<AgentMessage> GetPendingMessages(Guid agentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_pendingMessages.TryGetValue(agentId, out ConcurrentQueue<AgentMessage>? queue))
        {
            List<AgentMessage> messages = new List<AgentMessage>();
            while (queue.TryDequeue(out AgentMessage? message))
            {
                messages.Add(message);
            }

            return messages.AsReadOnly();
        }

        return Array.Empty<AgentMessage>();
    }

    /// <summary>
    /// Gets all active subscriptions as an immutable list.
    /// </summary>
    /// <returns>An immutable list of all active subscriptions.</returns>
    public ImmutableList<Subscription> GetAllSubscriptions()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _subscriptions.Values.ToImmutableList();
    }

    /// <summary>
    /// Gets all subscriptions for a specific agent.
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent.</param>
    /// <returns>A list of subscriptions for the agent.</returns>
    public IReadOnlyList<Subscription> GetSubscriptionsForAgent(Guid agentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _subscriptions.Values
            .Where(s => s.AgentId == agentId)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Clears all message history.
    /// </summary>
    public void ClearHistory()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (_messageHistory.TryDequeue(out _))
        {
            // Drain the queue
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _messageChannel.Writer.Complete();
        _disposalTokenSource.Cancel();

        try
        {
            _processingTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException)
        {
            // Ignore cancellation exceptions during disposal
        }

        // Complete all pending requests with cancellation
        foreach (KeyValuePair<Guid, TaskCompletionSource<AgentMessage>> kvp in _pendingRequests)
        {
            kvp.Value.TrySetCanceled();
        }

        _pendingRequests.Clear();
        _disposalTokenSource.Dispose();
    }

    private async Task ProcessMessagesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (AgentMessage message in _messageChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                await ProcessMessageAsync(message).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected during disposal
        }
    }

    private async Task ProcessMessageAsync(AgentMessage message)
    {
        // Add to history
        AddToHistory(message);

        // Handle response messages for pending requests
        if (message.Type == MessageType.Response && message.CorrelationId.HasValue)
        {
            if (_pendingRequests.TryRemove(message.CorrelationId.Value, out TaskCompletionSource<AgentMessage>? tcs))
            {
                tcs.TrySetResult(message);
            }
        }

        // Get matching subscriptions ordered by priority
        List<Subscription> matchingSubscriptions = _subscriptions.Values
            .Where(s => s.Matches(message))
            .ToList();

        // Dispatch to all matching subscriptions
        List<Task> dispatchTasks = new List<Task>();
        foreach (Subscription subscription in matchingSubscriptions)
        {
            dispatchTasks.Add(DispatchToSubscriptionAsync(message, subscription));
        }

        // Store as pending for agents without active handlers
        if (message.ReceiverId.HasValue && matchingSubscriptions.Count == 0)
        {
            ConcurrentQueue<AgentMessage> queue = _pendingMessages.GetOrAdd(
                message.ReceiverId.Value,
                _ => new ConcurrentQueue<AgentMessage>());
            queue.Enqueue(message);
        }

        await Task.WhenAll(dispatchTasks).ConfigureAwait(false);
    }

    private async Task DispatchToSubscriptionAsync(AgentMessage message, Subscription subscription)
    {
        try
        {
            await subscription.Handler(message).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Log error in production; for now, we silently handle to prevent one handler from affecting others
            // Consider adding an error event or callback mechanism
        }
    }

    private void AddToHistory(AgentMessage message)
    {
        _messageHistory.Enqueue(message);

        // Trim history if needed
        while (_messageHistory.Count > _maxHistorySize && _messageHistory.TryDequeue(out _))
        {
            // Continue trimming
        }
    }
}