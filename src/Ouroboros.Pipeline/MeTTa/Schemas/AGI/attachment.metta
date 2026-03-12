; ============================================================
; Attachment Theory - Bond types, proximity seeking
; AGI Gap 24: Attachment & Bonding
; Scientific: Bowlby Attachment Theory
; ============================================================

(: AttachmentStyle Type)
(: Secure AttachmentStyle)
(: Anxious AttachmentStyle)
(: Avoidant AttachmentStyle)
(: Disorganized AttachmentStyle)

(: Bond Type)
(: MakeBond (-> Agent Agent AttachmentStyle Float Bond))

; Proximity seeking intensity by style
(: ProximitySeek (-> AttachmentStyle Float Float))
(= (ProximitySeek Secure $strength) (* $strength 1.0))
(= (ProximitySeek Anxious $strength) (* $strength 1.5))
(= (ProximitySeek Avoidant $strength) (* $strength 0.3))
(= (ProximitySeek Disorganized $strength)
   (* $strength (random 0.2 1.5)))  ; unpredictable

; Separation anxiety
(: SeparationAnxiety (-> Bond Float Float))
; bond, absence_duration -> anxiety
(= (SeparationAnxiety (MakeBond $a $b Anxious $str) $duration)
   (* $str (log (+ 1.0 $duration))))
(= (SeparationAnxiety (MakeBond $a $b Secure $str) $duration)
   (* $str 0.3 (log (+ 1.0 $duration))))
(= (SeparationAnxiety (MakeBond $a $b Avoidant $str) $duration)
   0.1)  ; minimal anxiety regardless

; Internal Working Model
(: WorkingModel Type)
(: SelfModel (-> String WorkingModel))   ; "worthy" or "unworthy"
(: OtherModel (-> String WorkingModel))  ; "reliable" or "unreliable"

(: DeriveStyle (-> WorkingModel WorkingModel AttachmentStyle))
(= (DeriveStyle (SelfModel "worthy") (OtherModel "reliable")) Secure)
(= (DeriveStyle (SelfModel "unworthy") (OtherModel "reliable")) Anxious)
(= (DeriveStyle (SelfModel "worthy") (OtherModel "unreliable")) Avoidant)
(= (DeriveStyle (SelfModel "unworthy") (OtherModel "unreliable")) Disorganized)

; Trust building
(: TrustUpdate (-> Float Bool Float))
(= (TrustUpdate $current True) (min 1.0 (+ $current 0.05)))   ; slow build
(= (TrustUpdate $current False) (max 0.0 (- $current 0.15)))  ; fast decay

; Loyalty
(: Loyalty (-> Bond Float))
(= (Loyalty (MakeBond $a $b $style $strength))
   (* $strength (ProximitySeek $style 1.0)))
