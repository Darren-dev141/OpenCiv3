---- MODULE CulturalVictory ----
EXTENDS Naturals

(* One candidate and its strongest eligible rival. Each EndTurn supplies a
   post-accrual snapshot. Other victory conditions and tie-breaking are
   outside this model. The bounds cover the Standard and Huge thresholds. *)
VARIABLES enabled, limit, city, empire, rival, won, notices
vars == <<enabled, limit, city, empire, rival, won, notices>>
Limits == {100000, 160000}
Cities == {19999, 20000, 20001}
Empires == {99999, 100000, 100001, 159999, 160000, 160001}
Rivals == {0, 50000, 50001, 52632, 80000, 80001, 84211}

Qualifies(c, e, r) ==
    enabled /\ (c >= 20000 \/ (e >= limit /\ e >= 2 * r))

Init ==
    /\ enabled \in BOOLEAN
    /\ limit \in Limits
    /\ city = 0
    /\ empire = 0
    /\ rival = 0
    /\ won = FALSE
    /\ notices = 0

EndTurn ==
    /\ city' \in Cities
    /\ empire' \in Empires
    /\ rival' \in Rivals
    /\ UNCHANGED <<enabled, limit>>
    /\ won' = (won \/ Qualifies(city', empire', rival'))
    /\ notices' = IF ~won /\ won' THEN notices + 1 ELSE notices

Spec == Init /\ [][EndTurn]_vars

DisabledStaysOpen == ~enabled => ~won
EligibleWinsThisTurn == Qualifies(city, empire, rival) => won
ExactlyOneNotice == notices = (IF won THEN 1 ELSE 0)
LegalDeclaration ==
    [][(~won /\ won') =>
        enabled /\ (city' >= 20000 \/ (empire' >= limit /\ empire' >= 2 * rival'))]_vars
WinnerStaysDeclared == [][won => won']_vars
====
