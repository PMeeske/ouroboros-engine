; NanoAtoms.metta - Symbolic representation of self-consuming nano-scale atoms
; Extends Ouroboros.metta with nano-context thought stream processing
; for low-token-budget Ollama models (0.5b-3b params, 2K-8K context)

; =============================================================================
; NanoAtom Core Types
; =============================================================================

; The NanoAtom is a self-consuming atomic processor
(: NanoAtom Type)
(: NanoAtomInstance (-> String NanoAtom))

; NanoAtom phases (mini ouroboros cycle: receive → process → digest → emit)
(: NanoPhase Type)
(: NanoIdle NanoPhase)
(: NanoReceive NanoPhase)
(: NanoProcess NanoPhase)
(: NanoDigest NanoPhase)    ; The ouroboros bite - self-consumption
(: NanoEmit NanoPhase)

; Phase transitions (the nano cycle)
(: NanoTransition (-> NanoPhase NanoPhase Bool))
(= (NanoTransition NanoIdle NanoReceive) True)
(= (NanoTransition NanoReceive NanoProcess) True)
(= (NanoTransition NanoProcess NanoDigest) True)   ; Process → self-consume
(= (NanoTransition NanoDigest NanoEmit) True)
(= (NanoTransition NanoEmit NanoIdle) True)        ; Cycle back to idle

; Invalid transitions
(= (NanoTransition $from $to) False)

; =============================================================================
; ThoughtFragment Types
; =============================================================================

(: ThoughtFragment Type)
(: Fragment (-> String String Number ThoughtFragment))  ; content, source, estimated-tokens

(: FragmentSource Type)
(: UserSource FragmentSource)
(: GoalDecomposerSource FragmentSource)
(: DigestSource FragmentSource)
(: ContextSource FragmentSource)

; Fragment complexity (maps to SubGoalComplexity)
(: FragmentComplexity Type)
(: SimpleFragment FragmentComplexity)
(: ModerateFragment FragmentComplexity)
(: ComplexFragment FragmentComplexity)
(: ExpertFragment FragmentComplexity)

; =============================================================================
; DigestFragment Types (self-consumed output)
; =============================================================================

(: DigestFragment Type)
(: Digest (-> String String Number Number DigestFragment))  ; content, atomId, compression-ratio, confidence

; Digest quality levels (from SelfCritique confidence pattern)
(: DigestQuality Type)
(: HighQualityDigest DigestQuality)
(: MediumQualityDigest DigestQuality)
(: LowQualityDigest DigestQuality)

; Quality inference from confidence
(: infer-quality (-> Number DigestQuality))
(= (infer-quality $conf) (if (>= $conf 0.8) HighQualityDigest
                          (if (>= $conf 0.5) MediumQualityDigest
                              LowQualityDigest)))

; =============================================================================
; ThoughtStream Types
; =============================================================================

(: ThoughtStream Type)
(: Stream (-> String ThoughtStream))

(: StreamState Type)
(: Flowing StreamState)
(: Paused StreamState)
(: Completed StreamState)
(: Faulted StreamState)

; =============================================================================
; ConsolidatedAction Types
; =============================================================================

(: ConsolidatedAction Type)
(: Action (-> String String Number ConsolidatedAction))  ; content, action-type, confidence

(: ActionType Type)
(: ResponseAction ActionType)
(: PlanAction ActionType)
(: CritiqueAction ActionType)
(: CodeAction ActionType)

; =============================================================================
; NanoAtom Relations
; =============================================================================

; State tracking
(: InPhase (-> NanoAtom NanoPhase Bool))
(: HasTokenBudget (-> NanoAtom Number Bool))
(: AtomModel (-> NanoAtom String Bool))           ; Which Ollama model this atom uses

; Processing relations
(: Processes (-> NanoAtom ThoughtFragment Bool))
(: Digests (-> NanoAtom DigestFragment Bool))       ; Self-consumption relation
(: Emits (-> NanoAtom DigestFragment Bool))

; Stream relations
(: FlowsThrough (-> ThoughtFragment ThoughtStream Bool))
(: StreamContains (-> ThoughtStream NanoAtom Bool))
(: StreamState (-> ThoughtStream StreamState Bool))

; Consolidation relations
(: ConsolidatesTo (-> DigestFragment ConsolidatedAction Bool))
(: SourceOf (-> ConsolidatedAction ThoughtStream Bool))

; =============================================================================
; Self-Consumption Rules (The Ouroboros Bite)
; =============================================================================

; An atom that processes a fragment MUST digest it before emitting
(= (can-emit $atom $digest)
   (and
     (InPhase $atom NanoDigest)
     (Digests $atom $digest)))

; Compression must reduce token count (compression ratio > 1.0)
(= (valid-digest (Digest $content $atomId $compression $confidence))
   (> $compression 1.0))

; A digest with confidence below threshold needs re-processing
(= (needs-reprocess $digest)
   (match &self (Digest $content $atomId $compression $confidence)
     (< $confidence 0.3)))

; =============================================================================
; Stream Consolidation Rules
; =============================================================================

; Ready to consolidate when all streams are completed
(= (ready-to-consolidate)
   (not (match &self (StreamState $stream Flowing) True)))

; Minimum digest count for consolidation
(= (has-enough-digests $min-count)
   (let $digests (collapse (match &self (Digests $atom $digest) $digest))
     (>= (length $digests) $min-count)))

; =============================================================================
; Token Budget Enforcement (Safety)
; =============================================================================

(= (within-token-budget $atom $fragment)
   (match &self
     (and
       (HasTokenBudget $atom $budget)
       (= $fragment (Fragment $content $source $tokens)))
     (<= $tokens $budget)))

; Fragments exceeding budget must be truncated
(= (needs-truncation $atom $fragment)
   (not (within-token-budget $atom $fragment)))

; =============================================================================
; Circuit Breaker State (from ResilientReasoner pattern)
; =============================================================================

(: CircuitState Type)
(: CircuitClosed CircuitState)
(: CircuitOpen CircuitState)
(: CircuitHalfOpen CircuitState)

(: AtomCircuit (-> NanoAtom CircuitState Bool))

; When circuit is open, atom falls back to symbolic digest
(= (should-symbolic-digest $atom)
   (match &self (AtomCircuit $atom CircuitOpen) True))

; When half-open, atom should test with a single fragment
(= (should-probe $atom)
   (match &self (AtomCircuit $atom CircuitHalfOpen) True))

; =============================================================================
; Goal Decomposer Integration
; =============================================================================

; Fragments originating from GoalDecomposer carry routing metadata
(: FragmentTier Type)
(: LocalTier FragmentTier)
(: CloudLightTier FragmentTier)
(: SpecializedTier FragmentTier)
(: CloudPremiumTier FragmentTier)

(: PreferredTier (-> ThoughtFragment FragmentTier Bool))

; Route fragment to appropriate atom based on tier
(= (route-to-local $fragment)
   (PreferredTier $fragment LocalTier))

(= (route-to-cloud $fragment)
   (or (PreferredTier $fragment CloudLightTier)
       (PreferredTier $fragment CloudPremiumTier)))

; =============================================================================
; NanoAtom Chain Orchestration
; =============================================================================

(: NanoAtomChain Type)
(: Chain (-> String NanoAtomChain))
(: ChainState Type)
(: Fragmenting ChainState)
(: Streaming ChainState)
(: Consolidating ChainState)
(: ChainComplete ChainState)

(: ChainInState (-> NanoAtomChain ChainState Bool))

; Chain phase transitions
(= (chain-transition Fragmenting Streaming) True)
(= (chain-transition Streaming Consolidating) True)
(= (chain-transition Consolidating ChainComplete) True)

; =============================================================================
; Metrics and Observability
; =============================================================================

(: AtomMetrics Type)
(: ProcessingTime (-> NanoAtom Number AtomMetrics))  ; atom, milliseconds
(: TokensProcessed (-> NanoAtom Number AtomMetrics))  ; atom, token count
(: CompressionAchieved (-> NanoAtom Number AtomMetrics))  ; atom, ratio

; Aggregate chain metrics
(= (chain-total-tokens $chain)
   (collapse (match &self
     (and (StreamContains $stream $atom)
          (TokensProcessed $atom $tokens))
     $tokens)))

; =============================================================================
; Query Helpers
; =============================================================================

; Find current phase of a nano atom
(: current-nano-phase (-> NanoAtom NanoPhase))
(= (current-nano-phase $atom)
   (match &self (InPhase $atom $phase) $phase))

; Find all atoms in a stream
(: atoms-in-stream (-> ThoughtStream (List NanoAtom)))
(= (atoms-in-stream $stream)
   (collapse (match &self (StreamContains $stream $atom) $atom)))

; Find all digests from a stream
(: digests-from-stream (-> ThoughtStream (List DigestFragment)))
(= (digests-from-stream $stream)
   (collapse (match &self
     (and (StreamContains $stream $atom)
          (Digests $atom $digest))
     $digest)))

; Check if an atom is healthy (circuit not open)
(= (atom-healthy $atom)
   (not (AtomCircuit $atom CircuitOpen)))
