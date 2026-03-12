; ============================================================
; Habit Formation - Cue-Routine-Reward loops
; AGI Gap 18: Habit Formation
; Scientific: Duhigg Habit Loop, Procedural Memory
; ============================================================

(: Habit Type)
(: HabitLoop (-> String String String Habit))
; Fields: cue, routine, reward

(: Automaticity (-> Habit Int Float))
; Automaticity increases with repetitions
(= (Automaticity (HabitLoop $cue $routine $reward) $reps)
   (- 1.0 (exp (- (* 0.05 $reps)))))  ; asymptotic approach to 1.0

(: IsAutomatic (-> Habit Int Bool))
(= (IsAutomatic $habit $reps)
   (> (Automaticity $habit $reps) 0.85))

; Execution path selection
(: ExecutionPath Type)
(: FastPath ExecutionPath)        ; automatic, no deliberation
(: DeliberatePath ExecutionPath)  ; conscious, council debate

(: SelectPath (-> Habit Int ExecutionPath))
(= (SelectPath $habit $reps)
   (if (IsAutomatic $habit $reps) FastPath DeliberatePath))

; Habit strength factors
(: HabitStrength (-> Float Float Float Float))
; frequency, recency, context_stability -> strength
(= (HabitStrength $freq $recency $stability)
   (* $freq (* $recency $stability)))

; Cue detection
(: MatchesCue (-> String Habit Bool))
(= (MatchesCue $context (HabitLoop $cue $routine $reward))
   (contains $context $cue))

; Habit competition (when multiple habits match)
(: DominantHabit (-> (List (Habit Int)) Habit))
(= (DominantHabit $habits)
   (argmax (lambda (($h $r)) (Automaticity $h $r)) $habits))
