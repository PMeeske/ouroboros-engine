; ToolCallAtoms.metta - Type definitions for MCP tool call dispatching
; Extends ToolSignatures.metta (backward chaining) and ActionTypes.metta (permissions).
;
; These atoms represent the formal specification for:
;   1. Tool call intents parsed from LLM output (any format)
;   2. MCP protocol request/response mapping
;   3. Evolutionary retry mutation tracking
;   4. Permission guards for tool execution

; =============================================================================
; Tool Call Intent Types
; =============================================================================

(: ToolCallIntent Type)
(: MkToolCall (-> String String ToolCallIntent))              ; tool-name, args-json
(: MkToolChain (-> ToolCallIntent ToolCallIntent ToolCallIntent)) ; first, second → chained

; The format in which the LLM expressed the tool call
(: ToolCallFormat Type)
(: XmlTagFormat ToolCallFormat)                                ; <tool_call>{...}</tool_call>
(: JsonToolCallsFormat ToolCallFormat)                         ; {"tool_calls":[...]}
(: BracketLegacyFormat ToolCallFormat)                         ; [TOOL:name args]
(: NativeApiFormat ToolCallFormat)                             ; Ollama /api/chat native

; Link a tool call intent to the format it was parsed from
(: ParsedFrom (-> ToolCallIntent ToolCallFormat Bool))

; =============================================================================
; Tool Call Result Types
; =============================================================================

(: ToolCallResult Type)
(: MkToolResult (-> String String Bool ToolCallResult))        ; tool-name, output, is-error
(: MkChainResult (-> ToolCallResult ToolCallResult ToolCallResult)) ; chained results

; =============================================================================
; MCP Protocol Mapping
; =============================================================================

(: McpRequest Type)
(: MkMcpToolsListRequest McpRequest)
(: MkMcpToolCallRequest (-> String String McpRequest))         ; tool-name, arguments-json

(: McpResponse Type)
(: MkMcpToolsListResponse (-> (List McpToolDef) McpResponse))
(: MkMcpToolCallResponse (-> String Bool McpResponse))         ; content, is-error

(: McpToolDef Type)
(: MkMcpToolDef (-> String String String McpToolDef))          ; name, description, input-schema-json

; =============================================================================
; Permission Guards (extends ActionTypes.metta SafeContext)
; =============================================================================

; Check if a tool call is allowed in a given safety context
(: ToolCallAllowed (-> ToolCallIntent SafeContext Bool))

; FullAccess: all tool calls are allowed
(= (ToolCallAllowed $intent FullAccess) True)

; ReadOnly: delegate to ActionTypes.metta Allowed predicate
(= (ToolCallAllowed (MkToolCall $name $args) ReadOnly)
   (Allowed (ToolAction $name) ReadOnly))

; Chained calls: both must be allowed
(= (ToolCallAllowed (MkToolChain $first $second) $ctx)
   (and (ToolCallAllowed $first $ctx)
        (ToolCallAllowed $second $ctx)))

; =============================================================================
; Tool Call Discovery (extends ToolSignatures.metta backward chaining)
; =============================================================================

; Resolve a tool call intent from a tool signature match
(: resolve-tool-call (-> String String ToolCallIntent))
(= (resolve-tool-call $input-type $output-type)
   (match &self
      (: $tool (-> $input-type $output-type))
      (MkToolCall (atom-to-string $tool) "{}")))

; Resolve a chained tool call through intermediate types
(= (resolve-tool-call $input-type $output-type)
   (match &self
      (: $tool (-> $mid $output-type))
      (MkToolChain
         (resolve-tool-call $input-type $mid)
         (MkToolCall (atom-to-string $tool) "{}"))))

; =============================================================================
; Evolutionary Retry Mutation Tracking
; =============================================================================

(: RetryMutation Type)
(: MkRetryMutation (-> String String Number RetryMutation))    ; attempt-id, strategy-name, generation

(: MutationOutcome Type)
(: MutationSuccess MutationOutcome)
(: MutationFailure MutationOutcome)
(: MutationEvolved MutationOutcome)                            ; mutated and retried successfully

; Link a mutation attempt to its outcome
(: HasMutationOutcome (-> RetryMutation MutationOutcome Bool))

; Mutation strategy types (mirrors C# IMutationStrategy implementations)
(: MutationStrategy Type)
(: FormatHintStrategy MutationStrategy)                        ; add format instructions to prompt
(: FormatSwitchStrategy MutationStrategy)                      ; switch between XML/JSON/Bracket
(: ToolSimplificationStrategy MutationStrategy)                ; reduce tool count/complexity
(: TemperatureStrategy MutationStrategy)                       ; adjust sampling temperature

; Track which strategy was used for a mutation
(: UsedStrategy (-> RetryMutation MutationStrategy Bool))

; =============================================================================
; Evolutionary Retry State Machine
; =============================================================================

(: RetryState Type)
(: InitialAttempt RetryState)
(: Mutating RetryState)
(: Converged RetryState)
(: Exhausted RetryState)

; Valid state transitions
(: RetryTransition (-> RetryState RetryState Bool))
(= (RetryTransition InitialAttempt Mutating) True)
(= (RetryTransition InitialAttempt Converged) True)            ; success on first try
(= (RetryTransition Mutating Mutating) True)                   ; next generation
(= (RetryTransition Mutating Converged) True)                  ; mutation worked
(= (RetryTransition Mutating Exhausted) True)                  ; max generations reached
(= (RetryTransition $from $to) False)                          ; all others invalid

; =============================================================================
; Fitness Tracking for Evolutionary Selection
; =============================================================================

; Fitness of a mutation attempt (0.0 = complete failure, 1.0 = perfect success)
(: MutationFitness (-> RetryMutation Number Bool))

; Selection pressure: prefer strategies with higher historical fitness
(: StrategyFitness (-> MutationStrategy Number Bool))
