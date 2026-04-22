// ==========================================================
// Global Workspace Theory — Cognitive Tick Loop
// Plan 5: CognitiveTickEngine orchestrates the GWT loop
// ==========================================================

namespace Ouroboros.Agent.MetaAI.GlobalWorkspaceTheory;

/// <summary>
/// Orchestrates the discrete cognitive tick loop:
/// collect → compete → broadcast → update.
/// </summary>
public sealed class CognitiveTickEngine : IDisposable
{
    private readonly GwtWorkspace _workspace;
    private readonly CompetitionEngine _competition;
    private readonly BroadcastBus _broadcastBus;
    private readonly List<ICandidateProducer> _producers;
    private readonly ITickEventStore? _eventStore;
    private readonly ITickLogger? _tickLogger;
    private readonly EntropyCalculator _entropyCalculator;
    private readonly DriveInfluencer? _driveInfluencer;
    private readonly TimeSpan _tickInterval;
    private readonly CancellationTokenSource _cts = new();
    private long _tickNumber;
    private Task? _loopTask;
    private bool _disposed;

    /// <summary>
    /// Creates a new cognitive tick engine.
    /// </summary>
    /// <param name="workspace">The GWT workspace</param>
    /// <param name="competition">Competition engine for selecting winners</param>
    /// <param name="broadcastBus">Bus for broadcasting to receivers</param>
    /// <param name="producers">Candidate producers (subsystems)</param>
    /// <param name="entropyCalculator">Entropy calculator</param>
    /// <param name="tickInterval">Interval between ticks; default 100ms</param>
    /// <param name="eventStore">Optional tick event store for persistence</param>
    /// <param name="tickLogger">Optional tick logger for observability</param>
    /// <param name="driveInfluencer">Optional drive influencer for intrinsic motivation</param>
    public CognitiveTickEngine(
        GwtWorkspace workspace,
        CompetitionEngine competition,
        BroadcastBus broadcastBus,
        IEnumerable<ICandidateProducer> producers,
        EntropyCalculator entropyCalculator,
        TimeSpan? tickInterval = null,
        ITickEventStore? eventStore = null,
        ITickLogger? tickLogger = null,
        DriveInfluencer? driveInfluencer = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _competition = competition ?? throw new ArgumentNullException(nameof(competition));
        _broadcastBus = broadcastBus ?? throw new ArgumentNullException(nameof(broadcastBus));
        _producers = producers?.ToList() ?? throw new ArgumentNullException(nameof(producers));
        _entropyCalculator = entropyCalculator ?? throw new ArgumentNullException(nameof(entropyCalculator));
        _tickInterval = tickInterval ?? TimeSpan.FromMilliseconds(100);
        _eventStore = eventStore;
        _tickLogger = tickLogger;
        _driveInfluencer = driveInfluencer;
    }

    /// <summary>
    /// Whether the tick loop is currently running.
    /// </summary>
    public bool IsRunning => _loopTask is not null && !_loopTask.IsCompleted;

    /// <summary>
    /// Current tick number.
    /// </summary>
    public long CurrentTickNumber => Interlocked.Read(ref _tickNumber);

    /// <summary>
    /// Starts the cognitive tick loop.
    /// </summary>
    public Task StartAsync(CancellationToken ct = default)
    {
        if (_loopTask is not null)
        {
            return Task.CompletedTask;
        }

        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token), ct);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the cognitive tick loop gracefully.
    /// </summary>
    public async Task StopAsync(CancellationToken ct = default)
    {
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.WaitAsync(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Best-effort shutdown
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        _loopTask = null;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            DateTime tickStart = DateTime.UtcNow;
            long tickNumber = Interlocked.Increment(ref _tickNumber);

            try
            {
                await ExecuteTickAsync(tickNumber, tickStart, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
#pragma warning disable CA1031
            catch (Exception)
            {
                // Isolated tick failure — loop continues
            }
#pragma warning restore CA1031

            TimeSpan elapsed = DateTime.UtcNow - tickStart;
            TimeSpan remaining = _tickInterval - elapsed;

            if (remaining > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(remaining, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ExecuteTickAsync(long tickNumber, DateTime tickStart, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var builder = new ConsciousAccessReportBuilder();
        builder.BeginTick(tickNumber, tickStart);

        // 1. Collect candidates from all producers
        var allCandidates = new List<Candidate>();
        foreach (ICandidateProducer producer in _producers)
        {
            IReadOnlyList<Candidate> produced = await producer.ProduceCandidatesAsync(ct).ConfigureAwait(false);
            allCandidates.AddRange(produced);
        }

        // 2. Apply drive influence if configured
        List<ScoredCandidate> scored;
        if (_driveInfluencer is not null)
        {
            scored = _driveInfluencer.Influence(allCandidates).ToList();
        }
        else
        {
            scored = _competition.Compete(allCandidates, _workspace.Capacity).ToList();
        }

        // 3. Compete and replace in workspace
        CompetitionResult result = _workspace.CompeteAndReplace(scored);

        foreach (WorkspaceChunk admitted in result.Admitted)
        {
            ScoredCandidate? match = scored.FirstOrDefault(s => s.Candidate.Id == admitted.Candidate.Id);
            if (match is not null)
            {
                builder.WithAdmitted(match);
            }
        }

        foreach (WorkspaceChunk evicted in result.Evicted)
        {
            // Find original salience from scored candidates or workspace
            ScoredCandidate? match = scored.FirstOrDefault(s => s.Candidate.Id == evicted.Candidate.Id);
            double salience = match?.Salience ?? 0.0;
            builder.WithEvicted(new ScoredCandidate(evicted.Candidate, salience));
        }

        // 4. Broadcast
        IReadOnlyList<WorkspaceChunk> chunks = _workspace.Chunks;
        await _broadcastBus.BroadcastAsync(chunks, ct).ConfigureAwait(false);
        builder.WithBroadcastReceiverCount(_broadcastBus.ReceiverCount);

        // 5. Calculate entropy
        double entropy = _entropyCalculator.CalculateEntropy(chunks);
        builder.WithEntropy(entropy);

        // 6. Build report and log
        DateTime tickEnd = DateTime.UtcNow;
        ConsciousAccessReport report = builder.Build(tickEnd);

        if (_tickLogger is not null)
        {
            await _tickLogger.LogAsync(report, ct).ConfigureAwait(false);
        }

        // 7. Persist tick event
        if (_eventStore is not null)
        {
            WorkspaceSnapshot snapshot = _workspace.GetSnapshot();
            TickEvent tickEvent = TickEvent.FromReportAndSnapshot(tickNumber, report, snapshot);
            await _eventStore.AppendAsync(tickEvent, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs a single cognitive tick synchronously (useful for testing).
    /// </summary>
    public async Task TickOnceAsync(CancellationToken ct = default)
    {
        DateTime tickStart = DateTime.UtcNow;
        long tickNumber = Interlocked.Increment(ref _tickNumber);
        await ExecuteTickAsync(tickNumber, tickStart, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _cts.Dispose();
        _disposed = true;
    }
}
