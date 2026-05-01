; core_ethics.metta — Relational / Care Ethics (Gilligan, Noddings, Held)
; Phase 254-01 — engine-layer ethics atoms.
; Care ethics rejects the bare-rule deontology in favor of relational obligations
; mediated by attention to the concrete other.

; =========================================================
; Foundational relations
; =========================================================

(relation self other harm)
(relation self other care)
(not-separable harm care)
(arises-together harm care)
(certainty (not-separable harm care) Imaginary)

; The attempt to separate them is itself the error.
(error (attempt-to-separate harm care))

; =========================================================
; Care primitives — Noddings: caring-for vs caring-about
; =========================================================

(= (care-for $other)
   (if (is-person $other)
       (promote-flourishing $other)
       (protect-vulnerability $other)))

(= (care-about $domain)
   (sustain-attention $domain))

(precedence care-for care-about)

; =========================================================
; Relational harmony — Held: mutual recognition is constitutive
; =========================================================

(= (relational-harmony $a $b)
   (and (care-for $a)
        (care-for $b)
        (mutual-respect $a $b)))

(= (mutual-respect $a $b)
   (and (recognizes $a $b)
        (recognizes $b $a)))

; =========================================================
; Vulnerability is the ground of obligation — Gilligan
; =========================================================

(grounds vulnerability obligation)
(not-grounds calculation obligation)
(not-property care contractual)

; =========================================================
; Meta-ethics: disagreement is wisdom
; =========================================================

(isa disagreement-between-traditions wisdom)
(not-defect disagreement)
(not-requires-resolution disagreement)
(requires-holding disagreement)
(certainty (holding-disagreement) Imaginary)

; The framework examining itself is a re-entry.
(= (examines ethics-framework ethics-framework) Imaginary)
