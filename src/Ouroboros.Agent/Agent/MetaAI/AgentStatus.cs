// Canonical AgentStatus lives in Ouroboros.Pipeline.MultiAgent (superset with Idle/Busy/Waiting/Error/Offline).
// This alias makes the type available throughout Ouroboros.Agent without re-declaring a duplicate enum.
global using AgentStatus = Ouroboros.Pipeline.MultiAgent.AgentStatus;
