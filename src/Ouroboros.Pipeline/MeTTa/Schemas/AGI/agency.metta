; ============================================================
; Sense of Agency - Voluntary action, authorship
; AGI Gap 21: Sense of Agency
; Scientific: Wegner (2002) Apparent Mental Causation
; ============================================================

(: AgencyType Type)
(: Voluntary AgencyType)
(: Reactive AgencyType)
(: Triggered AgencyType)
(: Reflexive AgencyType)

(: ActionRecord Type)
(: MakeAction (-> String AgencyType String String ActionRecord))
; Fields: id, type, predicted_outcome, actual_outcome

; Agency score = prediction-outcome match
(: AgencyScore (-> ActionRecord Float))
(= (AgencyScore (MakeAction $id Voluntary $predicted $actual))
   (similarity $predicted $actual))
(= (AgencyScore (MakeAction $id Reactive $predicted $actual))
   (* 0.5 (similarity $predicted $actual)))
(= (AgencyScore (MakeAction $id Triggered $_ $_))
   0.2)  ; low agency for triggered actions
(= (AgencyScore (MakeAction $id Reflexive $_ $_))
   0.0)  ; no agency for reflexes

; Wegner's conditions for experienced agency
(: ExperiencedAgency (-> Bool Bool Bool Float))
; priority (thought before action), consistency (thought matches action), exclusivity (no other cause)
(= (ExperiencedAgency True True True) 1.0)
(= (ExperiencedAgency True True False) 0.6)
(= (ExperiencedAgency True False $any) 0.3)
(= (ExperiencedAgency False $any $any2) 0.1)

; Agency narrative generation
(: AgencyNarrative (-> AgencyType String String))
(= (AgencyNarrative Voluntary $action)
   (concat "I chose to " $action))
(= (AgencyNarrative Reactive $action)
   (concat "I responded by " $action))
(= (AgencyNarrative Triggered $action)
   (concat "The system triggered " $action))
