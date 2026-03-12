; ============================================================
; Social Emotions - Type hierarchy, triggers, regulation
; AGI Gap 2: Emotional Intelligence
; Scientific: Gross's Process Model of Emotion Regulation
; ============================================================

; Type Hierarchy
(: SocialEmotionType Type)
(: Guilt SocialEmotionType)
(: Shame SocialEmotionType)
(: Pride SocialEmotionType)
(: Gratitude SocialEmotionType)
(: Empathy SocialEmotionType)
(: Jealousy SocialEmotionType)
(: Compassion SocialEmotionType)

; Emotion Structure
(: SocialEmotion Type)
(: MakeSocialEmotion (-> SocialEmotionType Agent String Float SocialEmotion))

; Trigger Conditions (symbolic rules)
(: EmotionTrigger (-> SocialEmotionType (List Condition) Bool))

; Guilt: caused harm AND aware of harm
(= (EmotionTrigger Guilt $conditions)
   (and (member (CausedHarm $agent $target) $conditions)
        (member (AwareOf $agent (CausedHarm $agent $target)) $conditions)))

; Shame: negative self-evaluation AND public exposure
(= (EmotionTrigger Shame $conditions)
   (and (member (NegativeSelfEval $agent $aspect) $conditions)
        (member (PublicExposure $agent $aspect) $conditions)))

; Pride: achieved goal AND attributed to self
(= (EmotionTrigger Pride $conditions)
   (and (member (GoalAchieved $agent $goal) $conditions)
        (member (SelfAttributed $agent $goal) $conditions)))

; Gratitude: received benefit AND attributed to other
(= (EmotionTrigger Gratitude $conditions)
   (and (member (ReceivedBenefit $agent $benefit) $conditions)
        (member (OtherAttributed $agent $benefactor $benefit) $conditions)))

; Empathy: perceived emotion in other AND resonance
(= (EmotionTrigger Empathy $conditions)
   (and (member (PerceivedEmotion $agent $target $emotion) $conditions)
        (member (EmotionalResonance $agent $emotion) $conditions)))

; Regulation Strategies
(: RegulationStrategy Type)
(: Reappraisal RegulationStrategy)
(: Suppression RegulationStrategy)
(: Distraction RegulationStrategy)
(: SituationModification RegulationStrategy)
(: AttentionDeployment RegulationStrategy)

; Regulation effectiveness (symbolic lookup)
(: RegulationEffectiveness (-> RegulationStrategy SocialEmotionType Float))
(= (RegulationEffectiveness Reappraisal Guilt) 0.8)
(= (RegulationEffectiveness Reappraisal Shame) 0.7)
(= (RegulationEffectiveness Suppression $any) 0.3)  ; suppression is generally weak
(= (RegulationEffectiveness Distraction $any) 0.5)
(= (RegulationEffectiveness AttentionDeployment Jealousy) 0.6)

; Empathy computation
(: EmpathyStrength (-> Agent Agent Float))
(= (EmpathyStrength $self $other)
   (* (BondStrength $self $other)
      (EmotionalSensitivity $self)))
