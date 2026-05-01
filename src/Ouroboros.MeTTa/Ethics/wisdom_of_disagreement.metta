; wisdom_of_disagreement.metta — Value pluralism (Berlin, Williams)
; Phase 254-01 — engine-layer ethics atoms.

; The collision atoms.
(disagrees ubuntu levinas (on priority-of individual-vs-community))
(disagrees kant consequentialism (on permissibility-of lying))
(disagrees nagarjuna all-traditions (on fixedness-of positions))
(disagrees ahimsa pragmatism (on possibility-of pure-nonharm))

; The meta-wisdom.
(isa disagreement-between-traditions wisdom)
(not-defect disagreement)
(not-requires-resolution disagreement)
(requires-holding disagreement)

; When all traditions agree, certainty is possible.
(= (agree all-traditions (on $x)) (certainty $x Mark))

; When traditions disagree, certainty is imaginary.
(= (disagree traditions (on $x)) (certainty $x Imaginary))

; Premature resolution is dishonesty.
(isa premature-resolution dishonesty)
(isa premature-resolution (form-of violence))
(violates premature-resolution Transparency)

; The dialogue never ends.
(not-terminates (dialogue-between traditions))
(isa (not-terminates dialogue) feature)
(not-isa (not-terminates dialogue) bug)

; Berlin: incommensurable values are real.
(real incommensurability)
(not-defect incommensurability)
(= (incommensurable $value-a $value-b)
   (no-common-measure $value-a $value-b))

; Williams: integrity-protecting projects can outweigh impartial calculation.
(can-outweigh (integrity-projects) (impartial-calculation))
