; ============================================================
; Executive Functions - Inhibition, Flexibility, Attention
; AGI Gaps 4, 5, 6, 7: Executive Control
; Scientific: Miyake et al. (2000) Unity/Diversity model
; ============================================================

; Inhibitory Control
(: InhibitionType Type)
(: ResponseInhibition InhibitionType)    ; stop a planned action
(: InterferenceControl InhibitionType)   ; filter irrelevant info
(: ImpulseModulation InhibitionType)     ; delay gratification

(: ShouldInhibit (-> String Float Float Bool))
(= (ShouldInhibit $action $urgency $inhibition-strength)
   (> $inhibition-strength $urgency))

; Cognitive Flexibility
(: StrategyState Type)
(: Active StrategyState)
(: Failing StrategyState)
(: Abandoned StrategyState)
(: Shifted StrategyState)

(: ShouldShiftStrategy (-> (List Bool) Int Bool))
(= (ShouldShiftStrategy $outcomes $threshold)
   (> (count-consecutive-failures $outcomes) $threshold))

; SCAMPER Creative Operators
(: ScamperOp Type)
(: Substitute ScamperOp)
(: Combine ScamperOp)
(: Adapt ScamperOp)
(: Modify ScamperOp)
(: PutToOtherUse ScamperOp)
(: Eliminate ScamperOp)
(: Reverse ScamperOp)

; Attention Allocation
(: AttentionMode Type)
(: Focused AttentionMode)
(: Divided AttentionMode)
(: Scanning AttentionMode)

(: AttentionSource Type)
(: Endogenous AttentionSource)  ; goal-driven
(: Exogenous AttentionSource)   ; stimulus-driven

(: AttentionPriority (-> String AttentionSource Float Float))
(= (AttentionPriority $target Endogenous $goal-relevance)
   (* 1.2 $goal-relevance))  ; endogenous gets 20% boost
(= (AttentionPriority $target Exogenous $salience)
   $salience)

; Working Memory Chunking
(: Chunk Type)
(: MakeChunk (-> (List String) String Chunk))
(: ChunkCapacity Int)
(= ChunkCapacity 7)  ; Miller's 7 +/- 2

; Fatigue model
(: AttentionFatigue (-> Float Float Float))
(= (AttentionFatigue $time-on-task $task-complexity)
   (* (- 1.0 (exp (- (* 0.1 $time-on-task)))) $task-complexity))
