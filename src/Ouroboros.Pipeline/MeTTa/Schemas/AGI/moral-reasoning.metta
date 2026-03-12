; ============================================================
; Moral Reasoning - Multi-framework ethical evaluation
; AGI Gap 11: Moral Reasoning
; Scientific: Kohlberg's stages, multiple ethical frameworks
; ============================================================

(: MoralFramework Type)
(: Deontological MoralFramework)
(: Utilitarian MoralFramework)
(: VirtueEthics MoralFramework)
(: CareEthics MoralFramework)

(: Judgment Type)
(: Permitted Judgment)
(: Forbidden Judgment)
(: Obligatory Judgment)
(: Supererogatory Judgment)

; Framework-specific evaluation
(: MoralEval (-> Action MoralFramework Judgment))

; Deontological: check against rules
(= (MoralEval $action Deontological)
   (if (violates-duty $action)
       Forbidden
       (if (fulfills-duty $action) Obligatory Permitted)))

; Utilitarian: maximize aggregate welfare
(= (MoralEval $action Utilitarian)
   (let* (($benefit (sum-welfare-gain $action))
          ($harm (sum-welfare-loss $action)))
     (if (> $benefit $harm) Permitted Forbidden)))

; Virtue Ethics: would a virtuous agent do this?
(= (MoralEval $action VirtueEthics)
   (if (exemplifies-virtue $action)
       (if (exceeds-duty $action) Supererogatory Permitted)
       Forbidden))

; Care Ethics: preserves relationships and care
(= (MoralEval $action CareEthics)
   (if (damages-relationship $action)
       Forbidden
       (if (strengthens-care $action) Obligatory Permitted)))

; Multi-framework synthesis
(: MoralSynthesis (-> Action (List MoralFramework) Judgment))
(= (MoralSynthesis $action $frameworks)
   (let* (($judgments (map (lambda ($f) (MoralEval $action $f)) $frameworks))
          ($forbidden-count (count-eq Forbidden $judgments))
          ($total (length $frameworks)))
     (if (> $forbidden-count (/ $total 2))
         Forbidden
         (if (all-eq Obligatory $judgments) Obligatory Permitted))))

; Kohlberg developmental stages
(: MoralStage Type)
(: PreConventional MoralStage)  ; punishment/reward oriented
(: Conventional MoralStage)      ; social conformity
(: PostConventional MoralStage)  ; universal principles

(: StageAppropriate (-> Action MoralStage Bool))
(= (StageAppropriate $action PostConventional)
   (and (MoralEval $action Deontological)
        (MoralEval $action CareEthics)))
