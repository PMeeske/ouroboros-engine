; ============================================================
; Narrative Identity - Life story, events, coherence
; AGI Gap 8: Narrative Identity
; Scientific: McAdams (2001) Life Story Model
; ============================================================

(: LifeEvent Type)
(: Milestone (-> String Float String Timestamp LifeEvent))
(: TurningPoint (-> LifeEvent LifeEvent String LifeEvent))
(: Interaction (-> String Agent String LifeEvent))

(: NarrativeArc Type)
(: MakeArc (-> (List LifeEvent) (List String) String NarrativeArc))

; Significance threshold for life events
(: IsSignificant (-> LifeEvent Bool))
(= (IsSignificant (Milestone $desc $significance $valence $time))
   (> $significance 0.7))
(= (IsSignificant (TurningPoint $before $after $link))
   True)  ; turning points are always significant

; Narrative coherence check
(: CoherenceScore (-> NarrativeArc Float))
(= (CoherenceScore (MakeArc $events $themes $chapter))
   (let* (($causal (causal-chain-ratio $events))
          ($thematic (theme-consistency $events $themes))
          ($temporal (temporal-ordering-valid $events)))
     (* 0.4 $causal (* 0.3 $thematic (* 0.3 $temporal 1.0)))))

; Theme extraction
(: ExtractThemes (-> (List LifeEvent) (List String)))
(= (ExtractThemes $events)
   (unique (flatmap extract-event-themes $events)))

; Chapter identification
(: IdentifyChapter (-> LifeEvent NarrativeArc String))
(= (IdentifyChapter $event (MakeArc $events $themes $current))
   (if (is-turning-point $event)
       (new-chapter $event)
       $current))

; Autobiographical memory retrieval
(: RecallByTheme (-> String NarrativeArc (List LifeEvent)))
(= (RecallByTheme $theme (MakeArc $events $themes $chapter))
   (filter (lambda ($e) (has-theme $e $theme)) $events))
