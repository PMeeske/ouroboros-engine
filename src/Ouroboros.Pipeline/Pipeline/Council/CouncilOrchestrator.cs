// <copyright file="CouncilOrchestrator.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Pipeline.Council;

/// <summary>
/// Orchestrates multi-agent council debates using the Round Table protocol.
/// </summary>
public sealed class CouncilOrchestrator : ICouncilOrchestrator
{
    private readonly List<IAgentPersona> _agents = [];
    private readonly object _agentsLock = new();
    private readonly ToolAwareChatModel _llm;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouncilOrchestrator"/> class.
    /// </summary>
    /// <param name="llm">The language model to use for agent interactions.</param>
    public CouncilOrchestrator(ToolAwareChatModel llm)
    {
        ArgumentNullException.ThrowIfNull(llm);
        _llm = llm;
    }

    /// <summary>
    /// Initializes a new instance with default agents.
    /// </summary>
    /// <param name="llm">The language model to use.</param>
    /// <returns>A new CouncilOrchestrator with default agents.</returns>
    public static CouncilOrchestrator CreateWithDefaultAgents(ToolAwareChatModel llm)
    {
        var orchestrator = new CouncilOrchestrator(llm);
        orchestrator.AddAgent(new OptimistAgent());
        orchestrator.AddAgent(new SecurityCynicAgent());
        orchestrator.AddAgent(new PragmatistAgent());
        orchestrator.AddAgent(new TheoristAgent());
        orchestrator.AddAgent(new UserAdvocateAgent());
        return orchestrator;
    }

    /// <inheritdoc />
    public IReadOnlyList<IAgentPersona> Agents
    {
        get { lock (_agentsLock) { return [.._agents]; } }
    }

    /// <inheritdoc />
    public void AddAgent(IAgentPersona agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        lock (_agentsLock)
        {
            if (_agents.Any(a => a.Name == agent.Name))
            {
                throw new InvalidOperationException($"Agent with name '{agent.Name}' already exists in the council.");
            }

            _agents.Add(agent);
        }
    }

    /// <inheritdoc />
    public bool RemoveAgent(string agentName)
    {
        lock (_agentsLock)
        {
            var agent = _agents.FirstOrDefault(a => a.Name == agentName);
            if (agent is null)
            {
                return false;
            }

            _agents.Remove(agent);
            return true;
        }
    }

    /// <inheritdoc />
    public Task<Result<CouncilDecision, string>> ConveneCouncilAsync(
        CouncilTopic topic,
        CancellationToken ct = default)
        => ConveneCouncilAsync(topic, CouncilConfig.Default, ct);

    /// <inheritdoc />
    public async Task<Result<CouncilDecision, string>> ConveneCouncilAsync(
        CouncilTopic topic,
        CouncilConfig config,
        CancellationToken ct = default)
    {
        List<IAgentPersona> snapshot;
        lock (_agentsLock) { snapshot = [.._agents]; }

        if (snapshot.Count == 0)
        {
            return Result<CouncilDecision, string>.Failure("No agents registered in the council.");
        }

        var transcript = new List<DebateRound>();
        var agentProposals = new Dictionary<string, AgentContribution>();

        try
        {
            // Phase 1: Proposal
            var proposalRound = await ExecuteProposalPhaseAsync(snapshot, topic, config, ct);
            if (proposalRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(proposalRound.Error);
            }

            transcript.Add(proposalRound.Value);
            foreach (var contribution in proposalRound.Value.Contributions)
            {
                agentProposals[contribution.AgentName] = contribution;
            }

            // Phase 2: Challenge
            var challengeRound = await ExecuteChallengePhaseAsync(snapshot, topic, agentProposals, config, ct);
            if (challengeRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(challengeRound.Error);
            }

            transcript.Add(challengeRound.Value);

            // Phase 3: Refinement
            var refinementRound = await ExecuteRefinementPhaseAsync(
                snapshot, topic, agentProposals, challengeRound.Value.Contributions, config, ct);
            if (refinementRound.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(refinementRound.Error);
            }

            transcript.Add(refinementRound.Value);

            // Phase 4: Voting
            var votingResult = await ExecuteVotingPhaseAsync(snapshot, topic, transcript, config, ct);
            if (votingResult.IsFailure)
            {
                return Result<CouncilDecision, string>.Failure(votingResult.Error);
            }

            var (votes, votingRound) = votingResult.Value;
            transcript.Add(votingRound);

            // Phase 5: Synthesis
            var decision = await SynthesizeDecisionAsync(topic, transcript, votes, config, ct);
            return decision;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return Result<CouncilDecision, string>.Failure($"Council debate failed: {ex.Message}");
        }
    }

    // Debate phase methods delegate to shared implementations in CouncilOrchestratorArrows
    // to eliminate duplication while preserving the instance-method API surface.

    private Task<Result<DebateRound, string>> ExecuteProposalPhaseAsync(
        List<IAgentPersona> agents, CouncilTopic topic, CouncilConfig config, CancellationToken ct)
        => CouncilOrchestratorArrows.ExecuteProposalPhaseAsync(_llm, agents, topic, config, ct);

    private Task<Result<DebateRound, string>> ExecuteChallengePhaseAsync(
        List<IAgentPersona> agents, CouncilTopic topic, Dictionary<string, AgentContribution> proposals,
        CouncilConfig config, CancellationToken ct)
        => CouncilOrchestratorArrows.ExecuteChallengePhaseAsync(_llm, agents, topic, proposals, config, ct);

    private Task<Result<DebateRound, string>> ExecuteRefinementPhaseAsync(
        List<IAgentPersona> agents, CouncilTopic topic, Dictionary<string, AgentContribution> proposals,
        IReadOnlyList<AgentContribution> challenges, CouncilConfig config, CancellationToken ct)
        => CouncilOrchestratorArrows.ExecuteRefinementPhaseAsync(_llm, agents, topic, proposals, challenges, config, ct);

    private Task<Result<(Dictionary<string, AgentVote> Votes, DebateRound Round), string>> ExecuteVotingPhaseAsync(
        List<IAgentPersona> agents, CouncilTopic topic, IReadOnlyList<DebateRound> transcript,
        CouncilConfig config, CancellationToken ct)
        => CouncilOrchestratorArrows.ExecuteVotingPhaseAsync(_llm, agents, topic, transcript, config, ct);

    private async Task<Result<CouncilDecision, string>> SynthesizeDecisionAsync(
        CouncilTopic topic,
        List<DebateRound> transcript,
        Dictionary<string, AgentVote> votes,
        CouncilConfig config,
        CancellationToken ct)
    {
        // Calculate weighted vote totals
        var voteGroups = votes.Values
            .GroupBy(v => v.Position.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(v => v.Weight));

        var totalWeight = voteGroups.Values.Sum();

        if (votes.Count == 0)
            return Result<CouncilDecision, string>.Failure("No votes received from council members");

        if (totalWeight <= 0)
            return Result<CouncilDecision, string>.Failure("No weighted votes received");

        var majorityPosition = voteGroups.OrderByDescending(g => g.Value).FirstOrDefault();
        if (majorityPosition.Key == null)
            return Result<CouncilDecision, string>.Failure("No positions found in votes");

        // Check for consensus
        var consensusReached = majorityPosition.Value >= totalWeight * config.ConsensusThreshold;

        // Identify minority opinions
        var minorityOpinions = new List<MinorityOpinion>();
        if (config.EnableMinorityReport)
        {
            var minorityVotes = votes.Values
                .Where(v => !v.Position.Equals(majorityPosition.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var vote in minorityVotes)
            {
                minorityOpinions.Add(new MinorityOpinion(
                    vote.AgentName,
                    vote.Position,
                    vote.Rationale,
                    []));
            }
        }

        // Generate synthesis using LLM
        var synthesisPrompt = BuildSynthesisPrompt(topic, transcript, votes, majorityPosition.Key, consensusReached);
        var (conclusion, _) = await _llm.GenerateWithToolsAsync(synthesisPrompt, ct);

        var confidence = consensusReached ? majorityPosition.Value / totalWeight : 0.5;

        var synthesisRound = new DebateRound(
            Phase: DebatePhase.Synthesis,
            RoundNumber: 1,
            Contributions: [new AgentContribution("Orchestrator", conclusion)],
            Timestamp: DateTime.UtcNow);

        transcript.Add(synthesisRound);

        var decision = new CouncilDecision(
            Conclusion: conclusion,
            Votes: votes,
            Transcript: transcript,
            Confidence: confidence,
            MinorityOpinions: minorityOpinions);

        return Result<CouncilDecision, string>.Success(decision);
    }

    private static string BuildSynthesisPrompt(
        CouncilTopic topic,
        List<DebateRound> transcript,
        Dictionary<string, AgentVote> votes,
        string majorityPosition,
        bool consensusReached)
        => CouncilOrchestratorArrows.BuildSynthesisPrompt(topic, transcript, votes, majorityPosition, consensusReached);
}
