; kantian.metta — Western Deontological Ethics (Categorical Imperative)
; Phase 254-01 — engine-layer ethics atoms.

(applies-to rule action)
(property rule universal)
(= (not-universalizable $rule) (wrong $rule))

; Formula of universal law.
(= (test-universalizability $maxim)
   (consistent-when-universalized $maxim))

; Individuals are ends, not means.
(not-property individual fungible)
(cannot-weigh suffering benefit)
(inherent dignity being)
(not-property dignity calculable)
(undermines weighing dignity)

; Formula of humanity.
(= (treats-as-end $agent $other)
   (and (respects-autonomy $agent $other)
        (not-instrumentalizes $agent $other)))

; Duty and obligation.
(not-contingent duty inclination)
(independent obligation desire)
(= (exists duty) (regardless-of (desire $agent)))

; Kingdom of ends.
(= (kingdom-of-ends)
   (community-where (every $agent)
                    (treated-as-end $agent)))
(certainty (kingdom-of-ends) Imaginary)
