; ============================================================
; File: AgentOntology.metta
; Purpose: Type system and ontology for MeTTa-native agents
; Domain: agents
;
; Agents are first-class atoms in the MeTTa AtomSpace.
; The C# runtime reads agent declarations from here,
; spawns provider-backed instances, and writes results back.
; ============================================================

; --- Core Types ---
(: AgentId Type)
(: AgentProvider Type)
(: AgentRole Type)
(: AgentStatus Type)
(: AgentCapability Type)
(: AgentTask Type)
(: AgentResult Type)
(: ModelSpec Type)
(: ProviderConfig Type)
(: AgentConstraint Type)
(: AgentTeamId Type)

; --- Provider Enum ---
(: Ollama AgentProvider)
(: OllamaCloud AgentProvider)
(: OpenAI AgentProvider)
(: Anthropic AgentProvider)
(: AzureOpenAI AgentProvider)
(: LocalMock AgentProvider)

; --- Status Enum ---
(: Idle AgentStatus)
(: Spawning AgentStatus)
(: Active AgentStatus)
(: Busy AgentStatus)
(: Cooling AgentStatus)
(: Failed AgentStatus)
(: Terminated AgentStatus)

; --- Role Enum ---
(: Coder AgentRole)
(: Reviewer AgentRole)
(: Planner AgentRole)
(: Reasoner AgentRole)
(: Researcher AgentRole)
(: Summarizer AgentRole)
(: SecurityAuditor AgentRole)
(: Critic AgentRole)
(: Synthesizer AgentRole)
(: Custom AgentRole)

; --- Built-in Capabilities ---
(: CodeGeneration AgentCapability)
(: CodeReview AgentCapability)
(: Planning AgentCapability)
(: Reasoning AgentCapability)
(: Summarization AgentCapability)
(: SymbolicReasoning AgentCapability)
(: ToolUse AgentCapability)
(: MultiTurn AgentCapability)
(: Streaming AgentCapability)
(: JsonMode AgentCapability)

; --- Agent Definition (the blueprint, not the instance) ---
; (AgentDef <id> <provider> <model> <role> <system-prompt> <max-tokens> <temperature>)
; This is DECLARATIVE. The runtime reads these atoms and materializes agents.
(: AgentDef (-> AgentId AgentProvider ModelSpec AgentRole String Number Number Type))

; --- Capability Declarations ---
(: HasCapability (-> AgentId AgentCapability Type))

; --- Provider Configuration ---
(: ProviderEndpoint (-> AgentProvider String Type))
(: ProviderApiKeyEnv (-> AgentProvider String Type))
(: ProviderDefaultEmbedModel (-> AgentProvider String Type))

; --- Default Endpoints ---
(ProviderEndpoint Ollama "http://localhost:11434")
(ProviderEndpoint OllamaCloud "https://api.ollama.com")
(ProviderEndpoint OpenAI "https://api.openai.com/v1")
(ProviderApiKeyEnv OllamaCloud "OLLAMA_CLOUD_API_KEY")
(ProviderApiKeyEnv OpenAI "OPENAI_API_KEY")
(ProviderDefaultEmbedModel Ollama "nomic-embed-text")

; --- Lifecycle Events (written by C# runtime) ---
(: Spawned (-> AgentId AgentStatus String Type))
(: AgentHealth (-> AgentId String Number Type))

; --- Task Assignment and Results ---
(: AssignedTask (-> String AgentId String Type))
; (AgentResult <agent-id> <task-id> <status> <output>)

; --- Agent Communication (inter-agent messages as atoms) ---
(: AgentMessage (-> AgentId AgentId String String Type))

; --- Budget and Rate Limiting ---
(: AgentBudget (-> AgentId Number Type))
(: AgentTokenUsage (-> AgentId Number Number Type))
(: AgentRateLimit (-> AgentId Number Number Type))
(: MaxConcurrentAgents (-> Number Type))

; --- Safety ---
(: BlockedProvider (-> AgentProvider Type))
(: BlockedModel (-> String Type))
(: AgentErrorCount (-> AgentId Number Type))
