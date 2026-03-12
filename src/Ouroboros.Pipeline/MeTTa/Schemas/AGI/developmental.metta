; ============================================================
; Developmental Stages - Maturation model
; AGI Gap 17: Developmental Stages
; Scientific: Piaget + Dreyfus Skill Acquisition
; ============================================================

(: DevelopmentalStage Type)
(: Nascent DevelopmentalStage)
(: Developing DevelopmentalStage)
(: Competent DevelopmentalStage)
(: Proficient DevelopmentalStage)
(: Expert DevelopmentalStage)
(: Wise DevelopmentalStage)

; Stage thresholds (performance score)
(: StageThreshold (-> DevelopmentalStage Float))
(= (StageThreshold Nascent) 0.0)
(= (StageThreshold Developing) 0.2)
(= (StageThreshold Competent) 0.4)
(= (StageThreshold Proficient) 0.6)
(= (StageThreshold Expert) 0.8)
(= (StageThreshold Wise) 0.95)

; Stage determination
(: DetermineStage (-> Float DevelopmentalStage))
(= (DetermineStage $score)
   (if (>= $score 0.95) Wise
   (if (>= $score 0.8) Expert
   (if (>= $score 0.6) Proficient
   (if (>= $score 0.4) Competent
   (if (>= $score 0.2) Developing
   Nascent))))))

; Learning rate by stage (S-curve)
(: LearningRate (-> DevelopmentalStage Float))
(= (LearningRate Nascent) 0.3)       ; slow start
(= (LearningRate Developing) 0.8)    ; rapid learning
(= (LearningRate Competent) 1.0)     ; peak learning
(= (LearningRate Proficient) 0.6)    ; plateau
(= (LearningRate Expert) 0.2)        ; refinement only
(= (LearningRate Wise) 0.1)          ; deep insight, slow

; Milestone tracking
(: Milestone Type)
(: MakeMilestone (-> String DevelopmentalStage String Milestone))

; Stage-appropriate capability gating
(: CanPerform (-> String DevelopmentalStage Bool))
(= (CanPerform "autonomous-action" $stage)
   (>= (StageThreshold $stage) 0.4))   ; requires Competent+
(= (CanPerform "teach-others" $stage)
   (>= (StageThreshold $stage) 0.8))   ; requires Expert+
(= (CanPerform "self-modify" $stage)
   (>= (StageThreshold $stage) 0.95))  ; requires Wise
