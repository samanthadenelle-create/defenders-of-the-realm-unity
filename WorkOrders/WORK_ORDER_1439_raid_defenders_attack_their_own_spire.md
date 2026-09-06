# WO-1439: raid defenders spend the raid attacking their OWN spire - structures carry no faction

**Status:** READY TO IMPLEMENT - **P0 for the raid loop.** This is the larger half of the owner's
*"the AI didnt really fight either"*.
**Silo:** `EnemyBrain` / `Enemy` target selection + `IDamageableStructure`. **Disjoint from WO-1438**
(which owns `TroopController`, the ATTACKER side) and from WO-1436/1437 (HUD, lifecycle).
**Source:** proven by the WO-1438 instrumentation lane, 2026-09-06, from the owner's own device logs.

---

## 1. THE MEASUREMENT

From `logs/debug/raid-ai-and-pets-2026-09-06.log` and siblings: **10,687 of the defenders' 13,800
`[Flow:EnemyAggro]` lines are the same line:**

```
raidguard...: ProbeForStructure hit 'RaidSpire' -> stopping agent to attack
```

Corroborated independently by **37** `[Flow:Raid] RaidSpire 'RaidSpire' took N (contact)` entries.

**The garrison spent the raid destroying the objective it exists to defend.** The owner's `SPIRE DOWN`
was substantially self-inflicted by the defenders.

## 2. THE CAUSE, LOCATED (not yet proven - instrument before editing)

`IDamageableStructure` **carries no faction**, and `EnemyBrain` applies **no faction test** when
`ProbeForStructure` finds a structure. The attacker side DOES test faction; the defender side does not.
So any structure in reach is a valid target to a defender, including its own spire and its own walls.

⚠ **This is a code read, not a measurement.** Per CLAUDE.md section 12, that LOCATES and does not
CONCLUDE. `[Flow:EnemyAggro]` already exists and is the most-traced system in the game - **add the one
line that prints the faction test's inputs and result, run it, and confirm the branch is what lets the
spire through** before changing selection logic.

## 3. WHY THIS IS THE PRIORITY HALF

The same session proved the attacker side never engaged either:
```
raid-end reconcile - deployed 10, survivors 10, wounded 0 (stars 0)
```
**The owner's entire warband survived untouched.** The two armies never met. Her troops chewed walls
(WO-1438) while the garrison chewed its own spire (this ticket). Neither is "combat", and together they
are the whole of *"the AI didn't really fight"*.

**Fixing only WO-1438 would leave the defenders still suiciding into their own objective.** This one
comes first.

## 4. WHAT TO BUILD

**Structures need an owning faction, and the defender's target test must respect it.** Choose the seam
deliberately:
- Adding faction to `IDamageableStructure` touches every implementer (HeartController, HeroHealth,
  Building, Tower, Gate, WallSegment - CLAUDE.md section 6). **That is the honest place for it** if the
  concept genuinely belongs to "a damageable structure".
- A raid-scene-local ownership lookup is smaller but risks becoming a second source of truth for
  "whose is this?" - **this repo's dominant failure mode** (CLAUDE.md sections 2, 5, 8, 16).

**Recommend one, with the reasoning, and say what the other would cost.** Do not quietly pick the small
one because it is small.

⛔ **Do NOT special-case the spire by name or id.** The defect is the missing concept; a name check
leaves every wall, tower and building in the same state and looks fixed.

## 5. ACCEPTANCE

- [ ] The faction branch is proven from a captured trace BEFORE the edit, and the trace line is quoted.
- [ ] A defender never selects a structure of its own faction, proven by a regression that MEASURES
      target selection with a friendly structure in reach. It must FAIL against today's build - state the
      RED proof in-file.
- [ ] A captured raid run shows the spire taking **zero** damage from its own garrison.
- [ ] `REGRESSION_OK n/n`.

## 6. THE SEAM ORACLE THIS ARGUES FOR
Same species as WO-1430, WO-1436 and WO-1437: every part worked - probing probed, damage applied, scoring
scored - and **nothing asserted that a combatant only attacks things it should.** Consider the general
form: **no actor may damage an asset of its own faction.** That is one assertion and it would have caught
this on the day it shipped.
