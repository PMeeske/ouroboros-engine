; ============================================================
; File: AgentTeamTest.metta
; Purpose: Test agent definitions for development and CI
; Domain: agents (test)
;
; These agents use the LocalMock provider so they work
; without any external services running.
; ============================================================

; --- Test Agent Definitions ---

(AgentDef "test-coder-01" LocalMock "mock-coder-v1" Coder
  "You are a test code generation agent."
  4096 0.3)

(AgentDef "test-reviewer-01" LocalMock "mock-reviewer-v1" Reviewer
  "You are a test code review agent."
  2048 0.2)

(AgentDef "test-planner-01" LocalMock "mock-planner-v1" Planner
  "You are a test planning agent."
  4096 0.5)

(AgentDef "test-reasoner-01" LocalMock "mock-reasoner-v1" Reasoner
  "You are a test reasoning agent."
  8192 0.4)

; --- Test Capabilities ---

(HasCapability "test-coder-01" CodeGeneration)
(HasCapability "test-coder-01" ToolUse)
(HasCapability "test-reviewer-01" CodeReview)
(HasCapability "test-reviewer-01" Reasoning)
(HasCapability "test-planner-01" Planning)
(HasCapability "test-planner-01" Summarization)
(HasCapability "test-reasoner-01" Reasoning)
(HasCapability "test-reasoner-01" SymbolicReasoning)

; --- Test Configuration ---

(MaxConcurrentAgents 10)

; --- Test Pipeline ---

(= (test-code-pipeline $task)
   (Pipeline "test-planner-01" "test-coder-01" "test-reviewer-01"))

; --- Test Budget ---

(AgentBudget "test-coder-01" 100000)
(AgentTokenUsage "test-coder-01" 0 100000)

; --- Validation Queries ---
; These queries can verify the test ontology is loaded correctly:
;
; !(match &self (AgentDef $id $prov $model $role $p $t $te) $id)
;   → Should return: "test-coder-01" "test-reviewer-01" "test-planner-01" "test-reasoner-01"
;
; !(match &self (HasCapability "test-coder-01" $cap) $cap)
;   → Should return: CodeGeneration ToolUse
;
; !(team-for-goal "implement feature")
;   → Should return agents for Planning, CodeGeneration, CodeReview, Reasoning
