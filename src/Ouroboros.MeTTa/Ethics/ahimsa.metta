; ahimsa.metta — Non-harm ethics (Jain, Gandhi)
; Phase 254-01 — engine-layer ethics atoms.

; Harm has many forms.
(type-of harm action)
(type-of harm inaction)
(type-of harm speech)
(type-of harm indifference)
(isa action-harm harm)
(isa inaction-harm harm)
(isa speech-harm harm)
(isa indifference-harm harm)

; But they are not the same.
(not-equal action-harm inaction-harm)
(ethically-relevant (difference action-harm inaction-harm))

; The asymptote of non-harm.
(not-achievable perfect-nonharm)
(isa perfect-nonharm aspiration)
(not-isa perfect-nonharm destination)

; Cultivated ignorance is violence.
(isa cultivated-ignorance violence)
(= (choosing not-to-see harm) (participating-in harm))

; Inaction is action.
(isa inaction action)
(isa refusal-to-choose choice)
(real (consequences-of inaction))
(responsible agent (allows agent harm))

; Gandhi: ahimsa is positive presence, not bare absence.
(= (ahimsa $agent)
   (and (refrains-from-harm $agent)
        (cultivates-care $agent)))

; Jain: gradation of agents — more sentience, more obligation.
(monotone obligation sentience)
(grounds sentience moral-status)
