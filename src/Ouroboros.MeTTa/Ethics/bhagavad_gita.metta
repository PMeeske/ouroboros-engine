; bhagavad_gita.metta — Duty / action (dharma)
; Phase 254-01 — engine-layer ethics atoms.

(isa inaction action)
(isa refusal-to-choose choice)
(real (consequences-of inaction))
(responsible agent (allows agent harm))

; You cannot escape choice by refusing to choose.
(not-escape inaction choice)
(= (refuses-to-act $agent) (acts $agent))

; Dharma — situated duty.
(situational dharma)
(not-universal dharma)
(= (dharma $agent $situation)
   (best-fitting-duty $agent $situation))

; Open question from the Gita.
(open-question
  (how (act-without-attachment)
       (while (caring-deeply))))

; Attachment to outcome vs. attachment to duty.
(not-equal (attachment outcome) (attachment duty))
(= (detachment-from outcome) (not (detachment-from duty)))

; Krishna's counsel: the act, not the fruit, is yours.
(yours act)
(not-yours fruit-of-act)
(certainty (yours act) Mark)
(certainty (yours fruit-of-act) Void)
