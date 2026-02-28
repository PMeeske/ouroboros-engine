; grammar_atoms.metta - MeTTa grammar validation, correction, and composition rules
; for the Dynamic LLM-Backed ANTLR Parser system.
;
; This file uses upstream Hyperon's full MeTTa feature set including
; backward chaining and higher-order functions.

; =============================================================================
; Grammar Atom Type Definitions
; =============================================================================

(: GrammarAtom Type)
(: Terminal Type)
(: NonTerminal Type)
(: Production Type)
(: Grammar Type)

; Terminal: a named token or regex pattern
(: MkTerminal (-> String TerminalDef))
(: MkRegexTerminal (-> String String TerminalDef))  ; name, regex-pattern

; Non-terminal: a named grammar rule
(: MkNonTerminal (-> String NonTerminalDef))

; Production: a non-terminal and its right-hand side alternatives
(: MkProduction (-> String (List (List String)) ProductionDef))

; Full grammar: name + start rule + list of productions
(: MkGrammar (-> String String (List ProductionDef) GrammarDef))

; =============================================================================
; Grammar Metadata
; =============================================================================

(: Precedence (-> String Int PrecedenceAtom))
(: Associativity Type)
(: LeftAssoc Associativity)
(: RightAssoc Associativity)
(: NonAssoc Associativity)
(: AssocOf (-> String Associativity AssocAtom))

; =============================================================================
; Validation: Left Recursion Detection
; =============================================================================

; A production is directly left-recursive if the first symbol of any
; alternative is the same as the production's non-terminal name.
(: is-left-recursive (-> ProductionDef Bool))
(= (is-left-recursive (MkProduction $name $alternatives))
   (any-alternative-starts-with $name $alternatives))

(: any-alternative-starts-with (-> String (List (List String)) Bool))
(= (any-alternative-starts-with $name Nil) False)
(= (any-alternative-starts-with $name (Cons $alt $rest))
   (if (starts-with-symbol $name $alt)
       True
       (any-alternative-starts-with $name $rest)))

(: starts-with-symbol (-> String (List String) Bool))
(= (starts-with-symbol $name Nil) False)
(= (starts-with-symbol $name (Cons $first $rest))
   (== $name $first))

; =============================================================================
; Validation: Unreachable Rules
; =============================================================================

; A rule is reachable if it is the start symbol or referenced by another reachable rule.
(: is-reachable (-> String String (List ProductionDef) Bool))
(= (is-reachable $rule $start $grammar)
   (if (== $rule $start)
       True
       (any-production-references $rule $start $grammar $grammar)))

(: any-production-references (-> String String (List ProductionDef) (List ProductionDef) Bool))
(= (any-production-references $rule $start Nil $full-grammar) False)
(= (any-production-references $rule $start (Cons (MkProduction $name $alts) $rest) $full-grammar)
   (if (and (references-symbol $rule $alts)
            (is-reachable $name $start $full-grammar))
       True
       (any-production-references $rule $start $rest $full-grammar)))

(: references-symbol (-> String (List (List String)) Bool))
(= (references-symbol $sym Nil) False)
(= (references-symbol $sym (Cons $alt $rest))
   (if (member $sym $alt)
       True
       (references-symbol $sym $rest)))

(: member (-> String (List String) Bool))
(= (member $x Nil) False)
(= (member $x (Cons $y $rest))
   (if (== $x $y)
       True
       (member $x $rest)))

; =============================================================================
; Validation: FIRST Set Conflict Detection
; =============================================================================

; Compute the first terminal of an alternative (simplified)
(: first-of (-> (List String) (List ProductionDef) String))
(= (first-of Nil $grammar) "EPSILON")
(= (first-of (Cons $sym $rest) $grammar)
   (if (is-terminal-name $sym)
       $sym
       (first-of-nonterminal $sym $grammar)))

; Check if a symbol name starts with uppercase (terminal convention)
(: is-terminal-name (-> String Bool))
(= (is-terminal-name $name)
   (starts-with-uppercase $name))

; Simplified: find first terminal of a non-terminal by looking up its first production
(: first-of-nonterminal (-> String (List ProductionDef) String))
(= (first-of-nonterminal $name Nil) "UNKNOWN")
(= (first-of-nonterminal $name (Cons (MkProduction $pname $alts) $rest))
   (if (== $name $pname)
       (first-of-first-alt $alts $rest)
       (first-of-nonterminal $name $rest)))

(: first-of-first-alt (-> (List (List String)) (List ProductionDef) String))
(= (first-of-first-alt Nil $grammar) "EPSILON")
(= (first-of-first-alt (Cons $alt $rest) $grammar) (first-of $alt $grammar))

; Check for FIRST set conflicts: two alternatives in the same production
; that start with the same terminal.
(: has-first-conflict (-> ProductionDef (List ProductionDef) Bool))
(= (has-first-conflict (MkProduction $name $alts) $grammar)
   (has-duplicate-firsts $alts $grammar Nil))

(: has-duplicate-firsts (-> (List (List String)) (List ProductionDef) (List String) Bool))
(= (has-duplicate-firsts Nil $grammar $seen) False)
(= (has-duplicate-firsts (Cons $alt $rest) $grammar $seen)
   (let $f (first-of $alt $grammar)
     (if (member $f $seen)
         True
         (has-duplicate-firsts $rest $grammar (Cons $f $seen)))))

; =============================================================================
; Correction: Left Recursion Removal
; =============================================================================

; Transform A -> A alpha | beta into:
;   A  -> beta A'
;   A' -> alpha A' | epsilon
(: fix-left-recursion (-> ProductionDef (List ProductionDef)))
(= (fix-left-recursion (MkProduction $name $alts))
   (let* (($recursive (filter-recursive $name $alts))
          ($non-recursive (filter-non-recursive $name $alts))
          ($prime-name (concat $name "_prime")))
     (if (== $recursive Nil)
         ; No left recursion, return unchanged
         (Cons (MkProduction $name $alts) Nil)
         ; Fix: create two new productions
         (Cons
           (MkProduction $name (map-append-symbol $prime-name $non-recursive))
           (Cons
             (MkProduction $prime-name
               (append
                 (map-tail-append-symbol $name $prime-name $recursive)
                 (Cons (Cons "/* epsilon */" Nil) Nil)))
             Nil)))))

; Filter alternatives that start with the production name (recursive)
(: filter-recursive (-> String (List (List String)) (List (List String))))
(= (filter-recursive $name Nil) Nil)
(= (filter-recursive $name (Cons $alt $rest))
   (if (starts-with-symbol $name $alt)
       (Cons $alt (filter-recursive $name $rest))
       (filter-recursive $name $rest)))

; Filter alternatives that do NOT start with the production name
(: filter-non-recursive (-> String (List (List String)) (List (List String))))
(= (filter-non-recursive $name Nil) Nil)
(= (filter-non-recursive $name (Cons $alt $rest))
   (if (starts-with-symbol $name $alt)
       (filter-non-recursive $name $rest)
       (Cons $alt (filter-non-recursive $name $rest))))

; Append a symbol to the end of each alternative
(: map-append-symbol (-> String (List (List String)) (List (List String))))
(= (map-append-symbol $sym Nil) Nil)
(= (map-append-symbol $sym (Cons $alt $rest))
   (Cons (append $alt (Cons $sym Nil)) (map-append-symbol $sym $rest)))

; Remove the leading recursive symbol and append the prime symbol
(: map-tail-append-symbol (-> String String (List (List String)) (List (List String))))
(= (map-tail-append-symbol $name $prime Nil) Nil)
(= (map-tail-append-symbol $name $prime (Cons (Cons $head $tail) $rest))
   (Cons (append $tail (Cons $prime Nil))
         (map-tail-append-symbol $name $prime $rest)))

; =============================================================================
; Grammar Composition
; =============================================================================

; Merge two grammars by combining their production lists.
; If a non-terminal appears in both, the second grammar's version is suffixed.
(: compose-grammars (-> GrammarDef GrammarDef GrammarDef))
(= (compose-grammars
     (MkGrammar $name1 $start1 $prods1)
     (MkGrammar $name2 $start2 $prods2))
   (let* (($conflicts (find-name-conflicts $prods1 $prods2))
          ($renamed (rename-productions $prods2 $conflicts))
          ($merged (append $prods1 $renamed)))
     (MkGrammar (concat $name1 "_" $name2) $start1 $merged)))

; Find production names that exist in both grammar lists
(: find-name-conflicts (-> (List ProductionDef) (List ProductionDef) (List String)))
(= (find-name-conflicts Nil $p2) Nil)
(= (find-name-conflicts (Cons (MkProduction $name $alts) $rest) $p2)
   (if (has-production-named $name $p2)
       (Cons $name (find-name-conflicts $rest $p2))
       (find-name-conflicts $rest $p2)))

(: has-production-named (-> String (List ProductionDef) Bool))
(= (has-production-named $name Nil) False)
(= (has-production-named $name (Cons (MkProduction $pname $alts) $rest))
   (if (== $name $pname)
       True
       (has-production-named $name $rest)))

; Rename conflicting productions by suffixing with "_ext"
(: rename-productions (-> (List ProductionDef) (List String) (List ProductionDef)))
(= (rename-productions Nil $conflicts) Nil)
(= (rename-productions (Cons (MkProduction $name $alts) $rest) $conflicts)
   (if (member $name $conflicts)
       (Cons (MkProduction (concat $name "_ext") $alts)
             (rename-productions $rest $conflicts))
       (Cons (MkProduction $name $alts)
             (rename-productions $rest $conflicts))))

; =============================================================================
; Master Validation
; =============================================================================

; Validate a full grammar, returning a list of issues.
(: validate-grammar (-> GrammarDef (List String)))
(= (validate-grammar (MkGrammar $name $start $prods))
   (append
     (check-left-recursion $prods)
     (append
       (check-unreachable $start $prods $prods)
       (check-first-conflicts $prods $prods))))

(: check-left-recursion (-> (List ProductionDef) (List String)))
(= (check-left-recursion Nil) Nil)
(= (check-left-recursion (Cons $prod $rest))
   (if (is-left-recursive $prod)
       (Cons (concat "LEFT_RECURSION:" (production-name $prod))
             (check-left-recursion $rest))
       (check-left-recursion $rest)))

(: check-unreachable (-> String (List ProductionDef) (List ProductionDef) (List String)))
(= (check-unreachable $start Nil $full) Nil)
(= (check-unreachable $start (Cons $prod $rest) $full)
   (if (is-reachable (production-name $prod) $start $full)
       (check-unreachable $start $rest $full)
       (Cons (concat "UNREACHABLE:" (production-name $prod))
             (check-unreachable $start $rest $full))))

(: check-first-conflicts (-> (List ProductionDef) (List ProductionDef) (List String)))
(= (check-first-conflicts Nil $grammar) Nil)
(= (check-first-conflicts (Cons $prod $rest) $grammar)
   (if (has-first-conflict $prod $grammar)
       (Cons (concat "FIRST_CONFLICT:" (production-name $prod))
             (check-first-conflicts $rest $grammar))
       (check-first-conflicts $rest $grammar)))

; Extract the name from a production
(: production-name (-> ProductionDef String))
(= (production-name (MkProduction $name $alts)) $name)

; =============================================================================
; Proven Grammar Storage
; =============================================================================

; Proven grammars are stored as atoms in the space
(: ProvenGrammar (-> String String ProvenGrammarAtom))
; Usage: (ProvenGrammar "description of what it parses" "grammar .g4 content")

; Query for a proven grammar by description keyword match
(: find-proven-grammar (-> String (List ProvenGrammarAtom)))
(= (find-proven-grammar $keyword)
   (collapse (match &self
     (ProvenGrammar $desc $g4)
     (if (contains $desc $keyword)
         (ProvenGrammar $desc $g4)
         (empty)))))
