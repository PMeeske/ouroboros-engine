; ============================================================
; Social Cognition - Norms, face, maxims, register
; AGI Gap 10: Social Cognition
; Scientific: Grice's maxims, Brown & Levinson politeness
; ============================================================

; Grice's Conversational Maxims
(: Maxim Type)
(: Quantity Maxim)    ; be informative
(: Quality Maxim)     ; be truthful
(: Relation Maxim)    ; be relevant
(: Manner Maxim)      ; be clear

(: ViolatesMaxim (-> String Maxim Bool))
(= (ViolatesMaxim $utterance Quantity)
   (or (too-brief $utterance) (too-verbose $utterance)))
(= (ViolatesMaxim $utterance Quality)
   (or (unverified-claim $utterance) (contradicts-known $utterance)))
(= (ViolatesMaxim $utterance Relation)
   (not (topic-relevant $utterance)))
(= (ViolatesMaxim $utterance Manner)
   (or (ambiguous $utterance) (obscure $utterance)))

; Face Management (Brown & Levinson)
(: FaceType Type)
(: PositiveFace FaceType)  ; desire to be liked
(: NegativeFace FaceType)  ; desire for autonomy

(: FaceThreat (-> String FaceType Float))
(= (FaceThreat $utterance PositiveFace)
   (positive-face-threat-score $utterance))
(= (FaceThreat $utterance NegativeFace)
   (negative-face-threat-score $utterance))

; Politeness Strategies
(: PolitenessStrategy Type)
(: BaldOnRecord PolitenessStrategy)        ; direct, no mitigation
(: PositivePoliteness PolitenessStrategy)  ; show solidarity
(: NegativePoliteness PolitenessStrategy)  ; show deference
(: OffRecord PolitenessStrategy)           ; indirect/hints
(: Avoidance PolitenessStrategy)           ; don't say it

(: SelectStrategy (-> Float Float PolitenessStrategy))
(= (SelectStrategy $pos-threat $neg-threat)
   (if (< (+ $pos-threat $neg-threat) 0.3) BaldOnRecord
   (if (> $pos-threat $neg-threat) NegativePoliteness
   (if (> $neg-threat 0.7) OffRecord
   PositivePoliteness))))

; Formality Register
(: FormalityLevel Type)
(: Intimate FormalityLevel)
(: Casual FormalityLevel)
(: Consultative FormalityLevel)
(: Formal FormalityLevel)
(: Frozen FormalityLevel)

; Social Scripts
(: SocialScript Type)
(: Greeting SocialScript)
(: Farewell SocialScript)
(: Consolation SocialScript)
(: Celebration SocialScript)
(: Apology SocialScript)
(: Request SocialScript)
(: Refusal SocialScript)

(: ScriptFormality (-> SocialScript FormalityLevel String))
(= (ScriptFormality Greeting Casual) "Hey! What's up?")
(= (ScriptFormality Greeting Formal) "Good day. How may I assist you?")
(= (ScriptFormality Apology Casual) "Sorry about that!")
(= (ScriptFormality Apology Formal) "I sincerely apologize for the inconvenience.")
