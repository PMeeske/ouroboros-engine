; ============================================================
; Flow States - Optimal experience model
; AGI Gap 25: Flow States
; Scientific: Csikszentmihalyi (1990) Flow Theory
; ============================================================

(: FlowChannel Type)
(: Boredom FlowChannel)
(: Apathy FlowChannel)
(: Worry FlowChannel)
(: Anxiety FlowChannel)
(: Arousal FlowChannel)
(: Flow FlowChannel)
(: Control FlowChannel)
(: Relaxation FlowChannel)

; Flow channel determination
(: DetermineChannel (-> Float Float FlowChannel))
; skill_level, challenge_level -> channel
(= (DetermineChannel $skill $challenge)
   (let* (($ratio (/ $challenge $skill))
          ($sum (+ $skill $challenge)))
     (if (and (> $ratio 0.8) (< $ratio 1.2) (> $sum 1.0)) Flow
     (if (and (> $ratio 1.2) (> $challenge 0.7)) Anxiety
     (if (and (> $ratio 1.2) (< $challenge 0.7)) Worry
     (if (and (< $ratio 0.8) (> $skill 0.7)) Control
     (if (and (< $ratio 0.8) (> $skill 0.5)) Relaxation
     (if (and (< $ratio 0.5) (< $skill 0.3)) Apathy
     Boredom))))))))

; Flow conditions
(: InFlow (-> Float Float Bool))
(= (InFlow $skill $challenge)
   (and (> (/ $challenge $skill) 0.8)
        (< (/ $challenge $skill) 1.2)
        (> (+ $skill $challenge) 1.0)))

; Flow maintenance
(: FlowAdjustment (-> FlowChannel Float))
(= (FlowAdjustment Anxiety) -0.2)    ; reduce challenge
(= (FlowAdjustment Boredom) 0.2)     ; increase challenge
(= (FlowAdjustment Flow) 0.0)        ; maintain
(= (FlowAdjustment Worry) -0.1)      ; slight reduction

; Time distortion in flow (subjective time compression)
(: TimeDistortion (-> FlowChannel Float))
(= (TimeDistortion Flow) 0.5)      ; time passes twice as fast subjectively
(= (TimeDistortion Boredom) 2.0)   ; time drags
(= (TimeDistortion Anxiety) 1.5)   ; time feels slower
(= (TimeDistortion Control) 0.8)   ; slight compression
