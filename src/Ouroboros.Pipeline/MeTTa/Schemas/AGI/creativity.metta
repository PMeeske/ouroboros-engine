; ============================================================
; Creativity - Blending, bisociation, divergent thinking
; AGI Gap 13: Creativity
; Scientific: Fauconnier & Turner Conceptual Blending, Koestler Bisociation
; ============================================================

; Conceptual Space
(: ConceptualSpace Type)
(: MakeSpace (-> String (List String) (List (String String String)) ConceptualSpace))
; Fields: name, concepts, relations

; Conceptual Blending (Fauconnier & Turner)
(: Blend Type)
(: MakeBlend (-> ConceptualSpace ConceptualSpace (List (String String)) String Blend))
; Fields: space1, space2, mappings, emergent_concept

; Blend quality
(: BlendQuality (-> Blend Float))
(= (BlendQuality (MakeBlend $s1 $s2 $mappings $emergent))
   (let* (($coverage (/ (length $mappings) (max (concept-count $s1) (concept-count $s2))))
          ($novelty (novelty-score $emergent $s1 $s2))
          ($coherence (mapping-coherence $mappings)))
     (/ (+ $coverage (+ $novelty $coherence)) 3.0)))

; Bisociation (Koestler)
(: Bisociation Type)
(: MakeBisociation (-> String String String Float Bisociation))
; Fields: frame1, frame2, connection, surprise_level

(: BisociationSurprise (-> Float Float Float))
; distance_between_frames, connection_strength -> surprise
(= (BisociationSurprise $distance $strength)
   (* $distance $strength))  ; high distance + strong connection = surprise

; Divergent Thinking
(: DivergentMode Type)
(: Fluency DivergentMode)        ; generate many ideas
(: Flexibility DivergentMode)    ; generate varied ideas
(: Originality DivergentMode)    ; generate novel ideas
(: Elaboration DivergentMode)    ; develop ideas in detail

; Creativity evaluation (Novelty + Value + Surprise)
(: CreativityScore (-> Float Float Float Float))
(= (CreativityScore $novelty $value $surprise)
   (/ (+ (* 0.4 $novelty) (+ (* 0.3 $value) (* 0.3 $surprise))) 1.0))

; SCAMPER operators for creative transformation
(: Transform (-> ScamperOp String String))
(= (Transform Substitute $concept) (find-substitute $concept))
(= (Transform Combine $concept) (find-combination $concept))
(= (Transform Adapt $concept) (find-adaptation $concept))
(= (Transform Modify $concept) (find-modification $concept))
(= (Transform Eliminate $concept) (find-elimination $concept))
(= (Transform Reverse $concept) (find-reversal $concept))
