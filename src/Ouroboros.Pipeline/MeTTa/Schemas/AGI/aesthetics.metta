; ============================================================
; Aesthetic Appreciation - Beauty, elegance, processing fluency
; AGI Gap 31: Aesthetic Appreciation
; Scientific: Reber et al. (2004) Processing Fluency Theory
; ============================================================

(: AestheticDimension Type)
(: Symmetry AestheticDimension)
(: Elegance AestheticDimension)
(: Simplicity AestheticDimension)
(: Harmony AestheticDimension)
(: Surprise AestheticDimension)
(: Unity AestheticDimension)

; Processing fluency model
(: ProcessingFluency (-> String Float))
; Higher fluency = more aesthetic pleasure
(= (ProcessingFluency $item)
   (let* (($symmetry (symmetry-score $item))
          ($simplicity (simplicity-score $item))
          ($familiarity (familiarity-score $item)))
     (/ (+ $symmetry (+ $simplicity $familiarity)) 3.0)))

; Aesthetic evaluation
(: AestheticScore (-> String AestheticDimension Float))
(= (AestheticScore $item Symmetry) (symmetry-score $item))
(= (AestheticScore $item Elegance)
   (* (simplicity-score $item) (power-score $item)))  ; elegance = simplicity * power
(= (AestheticScore $item Simplicity) (simplicity-score $item))
(= (AestheticScore $item Harmony) (coherence-score $item))
(= (AestheticScore $item Surprise) (novelty-score-aesthetic $item))
(= (AestheticScore $item Unity) (unity-score $item))

; Overall aesthetic judgment
(: AestheticJudgment (-> String Float))
(= (AestheticJudgment $item)
   (let* (($fluency (ProcessingFluency $item))
          ($dimensions (map (lambda ($d) (AestheticScore $item $d))
                           (list Symmetry Elegance Simplicity Harmony Surprise Unity))))
     (* 0.4 $fluency (* 0.6 (average $dimensions)))))

; Code aesthetics (domain-specific)
(: CodeAesthetic (-> String Float))
(= (CodeAesthetic $code)
   (let* (($readability (readability-score $code))
          ($conciseness (conciseness-score $code))
          ($naming (naming-quality $code))
          ($structure (structural-elegance $code)))
     (/ (+ $readability (+ $conciseness (+ $naming $structure))) 4.0)))

; Aesthetic preference evolution
(: PreferenceWeight (-> AestheticDimension Float Float))
(= (PreferenceWeight $dimension $experience-count)
   (+ 0.5 (* 0.5 (/ $experience-count (+ $experience-count 100.0)))))
; Preferences stabilize with experience
