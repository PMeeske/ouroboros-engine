; GrammarAtoms.metta - Type definitions for ANTLR grammar atoms
; Used by the Dynamic LLM-Backed Parser system for grammar representation,
; validation metadata, and proven grammar storage within the engine's AtomSpace.
;
; Full validation, correction, and composition logic lives in the upstream
; Hyperon sidecar (tools/hyperon-sidecar/grammar_atoms.metta). This file
; defines the atom types shared between the engine and the sidecar.

; =============================================================================
; Grammar Element Types
; =============================================================================

(: GrammarElement Type)
(: Terminal (-> String GrammarElement))
(: RegexTerminal (-> String String GrammarElement))  ; name, pattern
(: NonTerminal (-> String GrammarElement))

; =============================================================================
; Grammar Structure
; =============================================================================

(: Production Type)
(: MkProduction (-> String (List String) Production))  ; rule-name, alternative symbols

(: Grammar Type)
(: MkGrammar (-> String String Grammar))  ; grammar-name, start-rule

; Relate a grammar to its productions
(: HasProduction (-> Grammar Production Bool))

; =============================================================================
; Grammar Issue Classification
; =============================================================================

(: GrammarIssue Type)
(: LeftRecursion GrammarIssue)
(: UnreachableRule GrammarIssue)
(: FirstSetConflict GrammarIssue)
(: MissingRule GrammarIssue)
(: SyntaxError GrammarIssue)
(: Ambiguity GrammarIssue)

; An issue found in a specific rule
(: IssueIn (-> String GrammarIssue IssueAtom))

; =============================================================================
; Grammar Evolution State
; =============================================================================

(: GrammarState Type)
(: Candidate GrammarState)       ; Fresh from LLM, unvalidated
(: Validated GrammarState)       ; Passed MeTTa validation
(: Corrected GrammarState)       ; Had issues, was corrected
(: Compiled GrammarState)        ; Successfully compiled via Roslyn
(: Proven GrammarState)          ; Successfully parsed real input
(: Failed GrammarState)          ; Could not converge

; State tracking for a grammar
(: GrammarInState (-> String GrammarState Bool))

; State transitions (the grammar lifecycle)
(: GrammarTransition (-> GrammarState GrammarState Bool))
(= (GrammarTransition Candidate Validated) True)
(= (GrammarTransition Candidate Corrected) True)
(= (GrammarTransition Candidate Failed) True)
(= (GrammarTransition Corrected Validated) True)
(= (GrammarTransition Corrected Failed) True)
(= (GrammarTransition Validated Compiled) True)
(= (GrammarTransition Validated Failed) True)
(= (GrammarTransition Compiled Proven) True)
(= (GrammarTransition Compiled Failed) True)

; Invalid transitions
(= (GrammarTransition $from $to) False)

; =============================================================================
; Proven Grammar Storage
; =============================================================================

(: ProvenGrammar Type)
(: MkProvenGrammar (-> String String String ProvenGrammar))  ; id, description, g4-content

; Relation: grammar was generated from description
(: GeneratedFrom (-> String String Bool))  ; grammar-id, description

; Relation: grammar successfully parsed this input
(: ParsedInput (-> String String Bool))  ; grammar-id, sample-input-hash

; =============================================================================
; Evolution Metrics
; =============================================================================

(: EvolutionAttempt Type)
(: MkAttempt (-> String Number Number EvolutionAttempt))  ; grammar-id, attempt-number, timestamp

(: AttemptOutcome Type)
(: AttemptSuccess AttemptOutcome)
(: AttemptFailure AttemptOutcome)
(: AttemptCorrected AttemptOutcome)

(: HasOutcome (-> EvolutionAttempt AttemptOutcome Bool))

; =============================================================================
; LLM Generation Metadata
; =============================================================================

(: GenerationSource Type)
(: OllamaSource (-> String GenerationSource))  ; model-name
(: CachedSource GenerationSource)
(: ComposedSource GenerationSource)

(: GeneratedBy (-> String GenerationSource Bool))  ; grammar-id, source

; Temperature used for generation
(: GenerationTemperature (-> String Number Bool))  ; grammar-id, temperature
