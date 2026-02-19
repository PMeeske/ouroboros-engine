; ============================================================
; File: AgentOrchestration.metta
; Purpose: Rules for agent spawning, routing, delegation
; Domain: agents
;
; These rules are evaluated by the C# MeTTaAgentRuntime
; to make decisions about agent lifecycle and task routing.
; ============================================================

; --- Spawn Rule: Only spawn if not already active ---
(= (should-spawn $agent)
   (and
     (match &self (AgentDef $agent $provider $model $role $prompt $tokens $temp) True)
     (not (match &self (Spawned $agent Active $time) True))))

; --- Capability Routing: Find agent for a capability ---
(= (agent-for-capability $cap)
   (match &self
     (HasCapability $agent $cap)
     (match &self (Spawned $agent Active $t) $agent)))

; --- Find best agent for a task type ---
(= (best-agent-for $task-type)
   (match &self
     (HasCapability $agent $task-type)
     (match &self (AgentHealth $agent Healthy $latency)
       $agent)))

; --- Delegation: Planner delegates to specialists ---
(= (can-delegate $from $to $cap)
   (and
     (HasCapability $to $cap)
     (Spawned $to Active $t)
     (not (= $from $to))))

; --- Team Composition: Auto-build a team for a goal ---
(= (team-for-goal $goal)
   (superpose
     (agent-for-capability Planning)
     (agent-for-capability CodeGeneration)
     (agent-for-capability CodeReview)
     (agent-for-capability Reasoning)))

; --- Pipeline: Chain agents for code tasks ---
;     Planner -> Coder -> Reviewer -> (accept | revise)
(= (code-pipeline $task)
   (let $planner (agent-for-capability Planning)
     (let $coder (agent-for-capability CodeGeneration)
       (let $reviewer (agent-for-capability CodeReview)
         (Pipeline $planner $coder $reviewer)))))

; --- Self-Healing: Restart failed agents ---
(= (should-restart $agent)
   (match &self (Spawned $agent Failed $time) True))

; --- Load Balancing: Prefer idle agents ---
(= (prefer-idle $cap)
   (match &self
     (HasCapability $agent $cap)
     (match &self (Spawned $agent Active $t)
       (not (match &self (AssignedTask $task $agent $desc) True))
       $agent)))

; --- Safety: Never spawn agents that violate constraints ---
(= (safe-to-spawn $agent)
   (and
     (should-spawn $agent)
     (match &self (AgentDef $agent $prov $model $role $prompt $tok $temp) True)
     (not (match &self (BlockedProvider $prov) True))
     (not (match &self (BlockedModel $model) True))))

; --- Cost Control: Track token usage ---
(= (within-budget $agent)
   (match &self (AgentTokenUsage $agent $used $budget)
     (< $used $budget)))

; --- Self-Healing: Terminate agents that exceed error thresholds ---
(= (should-terminate $agent)
   (match &self (AgentErrorCount $agent $count)
     (> $count 5)))

; --- Provider Safety ---
(= (provider-allowed $prov)
   (not (match &self (BlockedProvider $prov) True)))

; --- Model Safety ---
(= (model-allowed $model)
   (not (match &self (BlockedModel $model) True)))

; --- Ethics: Agents inherit Ouroboros safety constraints ---
; Safety validation now checks provider/model allowlists rather than prompt scanning
(= (agent-respects-safety $agent)
   (and
     (match &self (AgentDef $agent $prov $model $role $prompt $tok $temp) True)
     (provider-allowed $prov)
     (model-allowed $model)))

; --- Revision Detection ---
(= (needs-revision $task)
   (match &self (AgentResult $reviewer $task Failed $reason) True))

; --- Active Agent Count ---
(= (count-active-agents)
   (match &self (Spawned $agent Active $t)
     (collapse (superpose $agent))))

; --- Concurrency Check ---
(= (can-spawn-more)
   (match &self (MaxConcurrentAgents $max)
     (< (count-active-agents) $max)))
