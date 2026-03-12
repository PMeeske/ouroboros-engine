; ============================================================
; Predictive Processing - Free Energy Principle
; AGI Gap 23: Predictive Processing
; Scientific: Friston's Free Energy Principle, Active Inference
; ============================================================

(: Prediction Type)
(: MakePrediction (-> String Level Float Prediction))

(: Level Type)
(: Sensory Level)
(: Semantic Level)
(: Strategic Level)

(: PredictionError Type)
(: MakeError (-> Prediction Observation Float Float PredictionError))
; Fields: prediction, observation, magnitude, precision

; Free Energy = sum of precision-weighted prediction errors
(: FreeEnergy (-> (List PredictionError) Float))
(= (FreeEnergy $errors)
   (sum (map precision-weighted-surprise $errors)))

(: precision-weighted-surprise (-> PredictionError Float))
(= (precision-weighted-surprise (MakeError $pred $obs $magnitude $precision))
   (* $precision $magnitude))

; Active Inference decision
(: ActiveInference (-> Float Float String))
(= (ActiveInference $free-energy $threshold)
   (if (> $free-energy $threshold)
       "act-to-reduce-surprise"    ; change the world to match prediction
       "update-beliefs"))           ; change beliefs to match the world

; Hierarchical prediction cascade
(: HierarchicalPredict (-> Level String Prediction))
(= (HierarchicalPredict Strategic $context)
   (MakePrediction (strategic-predict $context) Strategic 0.6))
(= (HierarchicalPredict Semantic $context)
   (MakePrediction (semantic-predict $context) Semantic 0.7))
(= (HierarchicalPredict Sensory $context)
   (MakePrediction (sensory-predict $context) Sensory 0.8))

; Precision weighting by context
(: ContextPrecision (-> String Level Float))
(= (ContextPrecision "familiar" $level) 0.9)
(= (ContextPrecision "novel" $level) 0.3)
(= (ContextPrecision "ambiguous" $level) 0.5)

; Belief update magnitude
(: BeliefUpdate (-> PredictionError Float))
(= (BeliefUpdate (MakeError $pred $obs $mag $prec))
   (* $mag (- 1.0 $prec)))  ; low precision = large update
