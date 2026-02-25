; CommonsenseKB.metta
; Curated commonsense knowledge base for the Ouroboros AtomSpace.
;
; Coverage:
;   1. Spatial relations
;   2. Temporal / causal relations
;   3. Physical properties of objects
;   4. Agent affordances (what agents can do with objects)
;   5. Natural categories and taxonomies
;   6. Basic inference rules that derive new facts from existing ones
;
; All facts are expressed in MeTTa typed-atom notation compatible with
; HyperonMeTTaEngine / AtomSpace.

; =============================================================================
; 1. SPATIAL RELATIONS
; =============================================================================

(: SpatialRelation Type)
(: Above  (-> Atom Atom SpatialRelation))
(: Below  (-> Atom Atom SpatialRelation))
(: Inside (-> Atom Atom SpatialRelation))
(: Outside (-> Atom Atom SpatialRelation))
(: Near   (-> Atom Atom SpatialRelation))
(: Far    (-> Atom Atom SpatialRelation))
(: Left   (-> Atom Atom SpatialRelation))
(: Right  (-> Atom Atom SpatialRelation))
(: On     (-> Atom Atom SpatialRelation))
(: Under  (-> Atom Atom SpatialRelation))
(: Front  (-> Atom Atom SpatialRelation))
(: Behind (-> Atom Atom SpatialRelation))

; Symmetry rules
(= (Below $x $y)  (Above $y $x))
(= (Right $x $y)  (Left  $y $x))
(= (Behind $x $y) (Front $y $x))
(= (Outside $x $y) (not (Inside $x $y)))

; Transitivity: if A is above B and B is above C then A is above C
(= (AboveTransitive $a $c)
   (match &self (, (Above $a $b) (Above $b $c)) True))

; =============================================================================
; 2. TEMPORAL RELATIONS
; =============================================================================

(: TemporalRelation Type)
(: Before  (-> Atom Atom TemporalRelation))
(: After   (-> Atom Atom TemporalRelation))
(: During  (-> Atom Atom TemporalRelation))
(: Overlaps (-> Atom Atom TemporalRelation))
(: Meets   (-> Atom Atom TemporalRelation))
(: Starts  (-> Atom Atom TemporalRelation))
(: Finishes (-> Atom Atom TemporalRelation))

; Symmetry
(= (After $x $y)  (Before $y $x))

; Transitivity
(= (BeforeTransitive $a $c)
   (match &self (, (Before $a $b) (Before $b $c)) True))

; =============================================================================
; 3. CAUSAL RELATIONS
; =============================================================================

(: CausalRelation Type)
(: Causes   (-> Atom Atom CausalRelation))
(: Enables  (-> Atom Atom CausalRelation))
(: Prevents (-> Atom Atom CausalRelation))
(: Requires (-> Atom Atom CausalRelation))
(: Results  (-> Atom Atom CausalRelation))

; Causal chain rule: if A causes B and B causes C then A enables C
(= (EnablesTransitive $a $c)
   (match &self (, (Causes $a $b) (Causes $b $c)) True))

; Example causal facts
(= (Causes (Event "heating") (Event "melting"))     True)
(= (Causes (Event "freezing") (Event "solidifying")) True)
(= (Causes (Event "collision") (Event "damage"))     True)
(= (Causes (Event "eating") (Event "satiation"))     True)
(= (Enables (Event "charging") (Event "power-use"))  True)
(= (Prevents (Event "covering") (Event "rain-damage")) True)

; =============================================================================
; 4. PHYSICAL PROPERTIES
; =============================================================================

(: PhysicalProperty Type)
(: Solid   PhysicalProperty)
(: Liquid  PhysicalProperty)
(: Gas     PhysicalProperty)
(: Heavy   PhysicalProperty)
(: Light   PhysicalProperty)
(: Rigid   PhysicalProperty)
(: Flexible PhysicalProperty)
(: Sharp   PhysicalProperty)
(: Blunt   PhysicalProperty)
(: Hot     PhysicalProperty)
(: Cold    PhysicalProperty)
(: Wet     PhysicalProperty)
(: Dry     PhysicalProperty)
(: Transparent PhysicalProperty)
(: Opaque  PhysicalProperty)
(: Edible  PhysicalProperty)
(: Fragile PhysicalProperty)
(: Durable PhysicalProperty)
(: Magnetic PhysicalProperty)
(: Conductive PhysicalProperty)

(: HasProperty (-> Atom PhysicalProperty Bool))

; Common object properties
(= (HasProperty (Entity "water")   Liquid)       True)
(= (HasProperty (Entity "water")   Wet)          True)
(= (HasProperty (Entity "ice")     Solid)        True)
(= (HasProperty (Entity "ice")     Cold)         True)
(= (HasProperty (Entity "steam")   Gas)          True)
(= (HasProperty (Entity "stone")   Solid)        True)
(= (HasProperty (Entity "stone")   Heavy)        True)
(= (HasProperty (Entity "stone")   Rigid)        True)
(= (HasProperty (Entity "feather") Light)        True)
(= (HasProperty (Entity "glass")   Fragile)      True)
(= (HasProperty (Entity "glass")   Transparent)  True)
(= (HasProperty (Entity "glass")   Rigid)        True)
(= (HasProperty (Entity "steel")   Solid)        True)
(= (HasProperty (Entity "steel")   Heavy)        True)
(= (HasProperty (Entity "steel")   Magnetic)     True)
(= (HasProperty (Entity "steel")   Conductive)   True)
(= (HasProperty (Entity "wood")    Solid)        True)
(= (HasProperty (Entity "wood")    Rigid)        True)
(= (HasProperty (Entity "rubber")  Flexible)     True)
(= (HasProperty (Entity "rubber")  Durable)      True)
(= (HasProperty (Entity "fire")    Hot)          True)
(= (HasProperty (Entity "bread")   Edible)       True)
(= (HasProperty (Entity "apple")   Edible)       True)
(= (HasProperty (Entity "knife")   Sharp)        True)
(= (HasProperty (Entity "knife")   Rigid)        True)
(= (HasProperty (Entity "rope")    Flexible)     True)
(= (HasProperty (Entity "copper")  Conductive)   True)
(= (HasProperty (Entity "copper")  Magnetic)     False)
(= (HasProperty (Entity "wood")    Conductive)   False)

; Property inference: solids are either rigid or flexible
(= (IsSolid $obj)
   (HasProperty $obj Solid))

; =============================================================================
; 5. AGENT AFFORDANCES
; =============================================================================

(: Affordance Type)
(: CanLift    Affordance)
(: CanCarry   Affordance)
(: CanOpen    Affordance)
(: CanClose   Affordance)
(: CanCut     Affordance)
(: CanBreak   Affordance)
(: CanEat     Affordance)
(: CanDrink   Affordance)
(: CanUse     Affordance)
(: CanWear    Affordance)
(: CanSit     Affordance)
(: CanWrite   Affordance)
(: CanHold    Affordance)
(: CanPush    Affordance)
(: CanPull    Affordance)
(: CanThrow   Affordance)
(: CanBurn    Affordance)
(: CanMelt    Affordance)

(: AgentCanDo (-> Atom Atom Affordance Bool))

; Human affordances with objects
(= (AgentCanDo (Entity "human") (Entity "chair")   CanSit)   True)
(= (AgentCanDo (Entity "human") (Entity "chair")   CanLift)  True)
(= (AgentCanDo (Entity "human") (Entity "door")    CanOpen)  True)
(= (AgentCanDo (Entity "human") (Entity "door")    CanClose) True)
(= (AgentCanDo (Entity "human") (Entity "knife")   CanCut)   True)
(= (AgentCanDo (Entity "human") (Entity "knife")   CanHold)  True)
(= (AgentCanDo (Entity "human") (Entity "bread")   CanEat)   True)
(= (AgentCanDo (Entity "human") (Entity "water")   CanDrink) True)
(= (AgentCanDo (Entity "human") (Entity "apple")   CanEat)   True)
(= (AgentCanDo (Entity "human") (Entity "pen")     CanWrite) True)
(= (AgentCanDo (Entity "human") (Entity "pen")     CanHold)  True)
(= (AgentCanDo (Entity "human") (Entity "stone")   CanThrow) True)
(= (AgentCanDo (Entity "human") (Entity "stone")   CanLift)  True)
(= (AgentCanDo (Entity "human") (Entity "glass")   CanBreak) True)
(= (AgentCanDo (Entity "human") (Entity "wood")    CanBurn)  True)
(= (AgentCanDo (Entity "human") (Entity "ice")     CanMelt)  False)
(= (AgentCanDo (Entity "human") (Entity "shirt")   CanWear)  True)
(= (AgentCanDo (Entity "human") (Entity "book")    CanHold)  True)
(= (AgentCanDo (Entity "human") (Entity "car")     CanPush)  True)
(= (AgentCanDo (Entity "human") (Entity "box")     CanCarry) True)

; Affordance inference: edible objects can be eaten
(= (CanEatIfEdible $agent $obj)
   (match &self (HasProperty $obj Edible)
     (AgentCanDo $agent $obj CanEat)))

; Affordance inference: fragile objects can be broken
(= (CanBreakIfFragile $agent $obj)
   (match &self (HasProperty $obj Fragile)
     (AgentCanDo $agent $obj CanBreak)))

; =============================================================================
; 6. NATURAL CATEGORIES / TAXONOMIES
; =============================================================================

(: Category Type)
(: Animal    Category)
(: Plant     Category)
(: Vehicle   Category)
(: Tool      Category)
(: Food      Category)
(: Furniture Category)
(: Building  Category)
(: Electronic Category)
(: Clothing  Category)
(: Liquid-Category Category)

(: IsA (-> Atom Category Bool))

; Animals
(= (IsA (Entity "dog")     Animal)    True)
(= (IsA (Entity "cat")     Animal)    True)
(= (IsA (Entity "bird")    Animal)    True)
(= (IsA (Entity "fish")    Animal)    True)
(= (IsA (Entity "horse")   Animal)    True)
(= (IsA (Entity "elephant") Animal)   True)

; Plants
(= (IsA (Entity "tree")    Plant)     True)
(= (IsA (Entity "flower")  Plant)     True)
(= (IsA (Entity "grass")   Plant)     True)
(= (IsA (Entity "apple")   Food)      True)
(= (IsA (Entity "apple")   Plant)     True)

; Vehicles
(= (IsA (Entity "car")     Vehicle)   True)
(= (IsA (Entity "bus")     Vehicle)   True)
(= (IsA (Entity "train")   Vehicle)   True)
(= (IsA (Entity "bicycle") Vehicle)   True)
(= (IsA (Entity "airplane") Vehicle)  True)
(= (IsA (Entity "boat")    Vehicle)   True)

; Tools
(= (IsA (Entity "knife")   Tool)      True)
(= (IsA (Entity "hammer")  Tool)      True)
(= (IsA (Entity "pen")     Tool)      True)
(= (IsA (Entity "saw")     Tool)      True)
(= (IsA (Entity "scissors") Tool)     True)

; Food
(= (IsA (Entity "bread")   Food)      True)
(= (IsA (Entity "rice")    Food)      True)
(= (IsA (Entity "meat")    Food)      True)
(= (IsA (Entity "egg")     Food)      True)
(= (IsA (Entity "milk")    Food)      True)

; Furniture
(= (IsA (Entity "chair")   Furniture) True)
(= (IsA (Entity "table")   Furniture) True)
(= (IsA (Entity "bed")     Furniture) True)
(= (IsA (Entity "sofa")    Furniture) True)
(= (IsA (Entity "shelf")   Furniture) True)

; Electronics
(= (IsA (Entity "phone")   Electronic) True)
(= (IsA (Entity "laptop")  Electronic) True)
(= (IsA (Entity "camera")  Electronic) True)
(= (IsA (Entity "tv")      Electronic) True)

; Clothing
(= (IsA (Entity "shirt")   Clothing)  True)
(= (IsA (Entity "shoes")   Clothing)  True)
(= (IsA (Entity "hat")     Clothing)  True)
(= (IsA (Entity "coat")    Clothing)  True)

; Liquids
(= (IsA (Entity "water")   Liquid-Category) True)
(= (IsA (Entity "milk")    Liquid-Category) True)
(= (IsA (Entity "oil")     Liquid-Category) True)
(= (IsA (Entity "juice")   Liquid-Category) True)

; Category → property inheritance
(= (CategoryImpliesProperty $obj Liquid)
   (IsA $obj Liquid-Category))

(= (CategoryImpliesProperty $obj Edible)
   (IsA $obj Food))

; =============================================================================
; 7. INFERENCE RULES
; =============================================================================

; Transitivity of IsA through sub-categories (placeholder hierarchy)
; Dogs are animals → animals are living things
(: LivingThing Category)
(= (IsA (Entity "dog")   LivingThing) True)
(= (IsA (Entity "cat")   LivingThing) True)
(= (IsA (Entity "bird")  LivingThing) True)
(= (IsA (Entity "tree")  LivingThing) True)
(= (IsA (Entity "human") LivingThing) True)

; Animate vs. inanimate
(: Animate   Category)
(: Inanimate Category)
(= (IsA (Entity "dog")  Animate) True)
(= (IsA (Entity "cat")  Animate) True)
(= (IsA (Entity "human") Animate) True)
(= (IsA (Entity "rock")  Inanimate) True)
(= (IsA (Entity "car")   Inanimate) True)
(= (IsA (Entity "chair") Inanimate) True)

; Default: things that have Edible property can be eaten by humans
(= (DefaultCanEat $obj)
   (match &self (HasProperty $obj Edible) True))

; Containers can hold things inside them
(: Container Category)
(= (IsA (Entity "box")     Container) True)
(= (IsA (Entity "bag")     Container) True)
(= (IsA (Entity "bottle")  Container) True)
(= (IsA (Entity "cup")     Container) True)
(= (IsA (Entity "bowl")    Container) True)

(: CanContain (-> Atom Bool))
(= (CanContain $obj)
   (IsA $obj Container))

; =============================================================================
; 8. QUERY HELPERS
; =============================================================================

; Find all objects with a given property
(: objects-with-property (-> PhysicalProperty (List Atom)))
(= (objects-with-property $prop)
   (match &self (HasProperty $obj $prop) $obj))

; Find all objects in a category
(: objects-in-category (-> Category (List Atom)))
(= (objects-in-category $cat)
   (match &self (IsA $obj $cat) $obj))

; Find what an agent can do with an object
(: affordances-for (-> Atom Atom (List Affordance)))
(= (affordances-for $agent $obj)
   (match &self (AgentCanDo $agent $obj $aff) $aff))

; Find all causes of an event
(: causes-of (-> Atom (List Atom)))
(= (causes-of $effect)
   (match &self (Causes $cause $effect) $cause))

; Find all effects of an event
(: effects-of (-> Atom (List Atom)))
(= (effects-of $cause)
   (match &self (Causes $cause $effect) $effect))

; Check if two objects share a property
(: share-property (-> Atom Atom PhysicalProperty Bool))
(= (share-property $a $b $prop)
   (match &self
     (, (HasProperty $a $prop) (HasProperty $b $prop))
     True))
