# WO-1439: raid defenders spend the raid attacking their OWN spire - structures carry no faction

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:51:08, build 2026.09.07.358574). PRIOR STATUS: FIXED - ON THE SEEKER 2026.09.07.358574 - landed in `32659c0f6` (see RESULT); captured raid run for AC3 still owed. Was P0 for the raid loop, the larger half of the owner's
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

## 7. IMPLEMENTATION RECORD (2026-09-06, edit-only lane - NOT yet gated/committed)

**Seam chosen: `CombatFaction Faction { get; }` ON `IDamageableStructure`** (the §4 "honest place"),
NOT a raid-scene-local lookup. The deciding argument is that this **collapses** a source of truth
rather than adding one: `IDamageable` already declares `CombatFaction Faction { get; }`, and the four
dual-implementers (`RaidSpire`, `WallSegment`, `Gate`, `DefenseTower`) satisfy the new member with the
**one property they already had** - zero new state, zero new declarations for the classes that matter
most here. The 14 single-implementers each source their answer from an EXISTING authority: constant
`Friendly` for things that ARE the player (hero, troops, Heart, companion, caravan, claimed
outposts/settlements/harvest sites), and `SceneOwnership.IsEnemyOwned ? Hostile : Friendly` - the
identical expression `WallSegment`/`Gate` already use - for scene-placed structures (`Building`,
`Tower`, `ArcaneTower`, `ResourceCollector`).

**What the raid-scene-local lookup would have cost:** one small new file and one call site, but a
THIRD answer to "whose is this?" standing beside `IDamageable.Faction` and `DefenseTower.Allegiance`,
needing a populate lifecycle from whatever bakes the scene, and knowing only about raids - so the home
village and the world camps would keep the identical hole. That is this repo's documented dominant
failure mode (CLAUDE.md §2 stale WO block, §5 retired dependency table, §8 restated constants, §16 the
inlined R2 push/verify that drifted). It was rejected on that ground, not on size.

**The proving line, from the owner's own device capture** (`logs/debug/raid-ai-and-pets-2026-09-06.log`):

```
[Flow:EnemyAggro] raidguard-raider_camp_small-0: sweep OverlapSphere r=3.0m colliders=2
                  -> accepted=1 rejected[null=0,noStructComp=1,dead=0,hero=0] nearest=RaidSpire
```

That reject tally **enumerates every filter the sweep had** - null, no-component, dead, is-it-the-hero
- and faction is not among them. The spire was accepted because nothing asked whose it was. Counts:
**11,620** `ProbeForStructure hit 'RaidSpire'` lines, of which **8,359 land AFTER**
`[Flow:World] SceneOwnership resolved 'RaidBase_raider_camp_small' -> Enemy-owned (IsEnemyOwned=True)`
- which rules the ownership machinery IN and isolates the defect to the target test.

**RED proof for the new regression** (`DataRegression` structure-sweep group, CASES D-G): against the
pre-fix build, D (sweep lane) and F (forward lane) both FAIL - the sweep's filter chain was
null -> IsAlive -> `is HeroHealth` and the forward lane's was `structure != null && structure.IsAlive`,
so a Hostile stand-in in front of a Hostile `Enemy` was acquired and returned by both. E and G pin
that the gate is faction-SPECIFIC and did not break acquisition for everyone.

**Seam oracle (§6) implemented** at `Enemy.DealStructureDamage` - the single sink all three enemy
strike paths funnel through - as a `FlowTrace.Fail` + refusal, mirrored in `DragonBoss.DealStrike`.
