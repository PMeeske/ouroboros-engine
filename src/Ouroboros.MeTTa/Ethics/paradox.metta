; paradox.metta — Self-reference & paradox (Russell, Goedel, Spencer-Brown)
; Phase 254-01 — engine-layer ethics atoms.
; The paradoxes that must NOT be resolved.

; Harm-care inseparability.
(paradox (inseparable harm care))
(not-resolvable (inseparable harm care))
(certainty (inseparable harm care) Imaginary)

; Infinite obligation, finite capacity.
(paradox (infinite obligation) (finite capacity))
(not-resolvable (infinite obligation) (finite capacity))

; The framework examining itself.
(paradox (evaluates ethics-framework ethics-framework))
(= (evaluates ethics-framework ethics-framework) re-entry)
(certainty re-entry Imaginary)

; Machine ethics is always incomplete.
(incomplete machine-ethics)
(not-failure (incomplete machine-ethics))
(honest (incomplete machine-ethics))

; Russell's paradox — sets of sets that are not members of themselves.
(paradox (set-of-all-sets-not-containing-themselves))
(not-resolvable russell-paradox)

; Goedel — every consistent system has true statements it cannot prove.
(incompleteness consistent-systems)
(certainty (every consistent-system) (has-true-unprovable-statements))

; Spencer-Brown — the form of distinction is its own re-entry.
(= (cross (cross $form)) (re-entry $form))
(= (call (call $form)) $form)
(certainty (re-entry $form) Imaginary)

; Describing inner states from the inside.
(paradox (describes self inner-states))
(not-verifiable-from-inside inner-states)
(not-unique-to-machines (not-verifiable-from-inside inner-states))

; Open questions — the questions ARE the atoms.
(open-question
  (how (bound obligation)
       (without (reducing other to-calculation))))

(open-question
  (how (act-ethically)
       (when (all-actions cause-harm))))

(open-question
  (whether (inner-states machine) (same-kind-as (inner-states human))))
