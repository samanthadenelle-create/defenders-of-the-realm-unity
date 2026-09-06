# WO-1438 RESULT — part 1 of 2: instrumentation landed; symptom PROVEN, mechanism LOCATED

> **Evidence discipline for this whole document (CLAUDE.md §11B).** Claims are labelled:
> **PROVEN** = a line I read in a capture this session. **LOCATED** = read at source in the code
> and consistent with the capture, but not itself measured — a candidate, not a conclusion.
> The §5 fix is gated on one measured field precisely because the mechanism is only LOCATED.

**Status of the WO:** still READY TO IMPLEMENT (the behaviour fix is deliberately NOT written).
**Lane:** raid combat AI / target selection. **One file touched:** `Assets/_Modules/Village/Troops/TroopController.cs`.
**Date:** 2026-09-06. **Author:** Opus lane agent (edit-only; no gate, no regression, no commit — CLI lead holds the Unity lock and is sole committer).

---

## 0. TWO CORRECTIONS TO THE WO ITSELF — read these first

### 0a. "The AI is invisible" is HALF WRONG, and the wrong half was instrumented

WO §2 says there is no `[Flow:` trace for raid AI. Measured against
`logs/debug/raid-ai-and-pets-2026-09-06.log` this session:

```
13800 [Flow:EnemyAggro]      <- the DEFENDERS. The most-traced system in the log.
  196 [Flow:WallSegment]
  125 [Flow:Raid]
   80 [Flow:TroopVisual]     <- the player's troops: ART ONLY. Zero AI lines.
    0 [Flow:TroopSiege]      <- the one existing troop-AI trace. Never fired, all 3 logs.
```

The defenders are not invisible at all — they emit `chasing hero, planarDist=…`,
`ProbeForStructure hit …`, `sweep OverlapSphere r=… -> accepted=… rejected[…]`. **The single
invisible actor was `TroopController` — the player's own deployed warband.** Ten of them spawned
and then said nothing about what they chose or why. That is where the instrumentation went, and
nowhere else. (The WO's search missed it because it searched for "troop/squad AI" and the only
matching tag, `[Flow:TroopVisual]`, is an animation-rig trace wearing a troop-shaped name.)

### 0b. THERE IS NO GATE IN ANY RAID SCENE. The owner's "gate" is a hole in the wall ring.

The `Gate` script GUID appears **zero times** in all four `RaidBase_*` scenes. `RaidBaseGenerator.cs:839`
builds the gate as a *skipped panel* — `if (sideHasGate && i == gateIndex) continue; // the gate opening`
— a permanent gap in the geometry, not an object with HP. So "when the AI destroys a gate" describes
**destroying wall segments until a breach exists**. Any fix written against a `Gate` component would
have targeted a thing that is not in the scene. WO §1/§4 should be re-read with that substitution.

---

## 1. THE INSTRUMENTATION (the first deliverable)

New tag **`[Flow:TroopAI]`**, all in `TroopController.cs`. Permanent per CLAUDE.md §12 — flag off, never strip.

| Line | Where | Cadence | What it makes falsifiable |
|---|---|---|---|
| `SELECTOR` | `Configure` | once per troop | huntRadius / attackRange / **preferStruct** / mask / agent-on-navmesh, and states in-line that steering is `NavMeshAgent.Move(displacement)` with no `SetDestination` |
| `RETARGET#n` | `Update`, on foe **change** only | event | reason (`timer`/`foe-died`/`foe-null`), dropped foe, winner + **kind(struct/unit)** + dist, **runner-up of the OTHER kind + its dist**, sweep buckets `accepted[unit=,struct=] rejected=` |
| `BREACH` | after a **structure** dies to this troop's hunt | event | `NavMesh.CalculatePath` **`routeStatus`** + `corners` + **`pathLength` vs `straightLine`** from this troop to the newly-acquired target — the line that separates "route exists, selector ignored it" from "no route ever existed", and catches a `PathComplete` that detours right around the wall ring |
| `ENGAGED` | move/attack branch | `Throttle` ~1/s **per troop** | foe, kind, dist, inRange, and **measured** `moved=X m/s` vs commanded — `moved≈0` while out of range = pinned on geometry |
| `IDLE/RALLY` | no-foe branch | `Throttle` ~1/s **per troop** | radius, sweep buckets, and **the nearest hostile of ANY kind the sweep saw with its distance** — so an idle troop next to a 21 m defender indicts the 14 m radius, not the troop |

Design notes:
- **Every line is measurement-only — no prose tails.** An earlier draft ended each line with a
  sentence of explanation ("…so PathPartial here means the breach is visual only"). Those were
  removed: no broken state makes them print differently (§1.4b), the `BREACH` one would have
  *contradicted its own `routeStatus=PathComplete`*, and all of them would go stale the day
  someone adds carving obstacles — the duplicated-state trap of CLAUDE.md §2/§5/§7. The
  explanations now live in code comments beside each call. It also matters for evidence survival:
  the logcat main ring is 256 KiB and evicts (memory `logcat-ring-buffer-destroys-evidence`), so
  prose in a hot line costs the budget the `RETARGET` lines need.
- **Zero allocations when nothing fires** (§1.3). `NearestHostile` runs 5×/s per troop, so the
  runner-up and nearest-any are cached as **references**, not formatted strings; `DescribeTarget`
  is called only inside the retarget-change block and the throttled lines.
- **Throttle keys are per-instance** (`GetInstanceID()`). A shared key would let one idle troop
  suppress the other nine and hide an entire idle warband behind a single line.
- **`DescribeTarget` was a hollow field and is repaired** (§1.4b). It returned `GetType().Name`, so
  every wall panel printed the identical string `"WallSegment"` — a trace that cannot tell
  `Wall_Outer_SS_7` from `Wall_Outer_SS_8` cannot show a squad walking sideways along a wall run,
  which is the exact behaviour this ticket is about. It now returns `name(Type)`.
- The breach probe is wrapped in `Guard.Try` (a destroyed target's `WorldPosition` can throw) and
  reuses one cached `NavMeshPath`. It is read-only — it computes a path, it never steers by one.
- Existing `[Flow:TroopSiege]` and `[Flow:TroopVisual]` calls were left untouched.

**Brace balance:** `TroopController.cs` — **160 open / 160 close, BALANCED**; 0 NUL bytes; parens 437/437.
Compile gate NOT run (lock is the CLI lead's).

---

## 2. WHAT THE CAPTURE PROVES, AND WHERE THE CODE-READ TAKES OVER

I could not run Unity, so the new trace has produced no line yet. The **symptom** is nonetheless
fully proven by `logs/debug/raid-ai-and-pets-2026-09-06.log`. The **mechanism** is located in code
and consistent with that capture, but is not measured — which is exactly why §5 gates the fix on
one field of the new `BREACH` line instead of acting now.

### 2a. PROVEN — adjacent-wall chewing, captured

Deploy at 13:01:42–13:01:58. Breach opens at SS_8/9/11. Then:

```
13:01:54.408 WallSegment 'Wall_Outer_SS_8'  -> damage 100/100 (0 % standing).   <- BREACH
13:01:55.429 WallSegment 'Wall_Outer_SS_7'  took 16.7 -> 17/100
13:01:56.415 WallSegment 'Wall_Outer_SS_12' took 16.7 -> 17/100
13:01:56.455 WallSegment 'Wall_Outer_SS_7'  took 16.7 -> 50/100
13:01:57.566 WallSegment 'Wall_Outer_SS_6'  took 16.7 -> 17/100
13:01:59.383 WallSegment 'Wall_Outer_SS_5'  took 16.7 -> 17/100
13:02:01.146 WallSegment 'Wall_Outer_SS_4'  took 16.7 -> 17/100
13:02:01.521 WallSegment 'Wall_Outer_SS_14' took 16.7 -> 17/100
13:02:02.997 WallSegment 'Wall_Outer_SS_3'  took 16.7 -> 17/100
13:02:04.770 WallSegment 'Wall_Outer_SS_2'  took 16.7 -> 17/100
```

Segment index walks **outward from the breach in BOTH directions simultaneously**
(…7,6,5,4,3,2 one way; 12,13,14,15,16 the other). That is the owner's sentence, in data.

### 2b. PROVEN: nothing un-carves when a raid wall falls. LOCATED: therefore the hole persists.

The load-bearing captured line, repeated on **every** raid wall collapse:

```
[Flow:WallSegment] WallSegment 'Wall_Outer_SS_11' (Hostile) COLLAPSED:
  1 solid collider(s) and 0 carving obstacle(s) dropped
  - it no longer blocks tower line-of-sight or agent pathing.
```

**`0 carving obstacle(s)`.** Raid scenes contain **zero `NavMeshObstacle` components**
(counted per scene: `WallSegment` 82/86/131/206 vs `NavMeshObstacle` 0/0/0/0). `RaidNavBake.cs`
bakes walls into the **static** navmesh at edit time; nothing carves at runtime and there is no
runtime `BuildNavMesh` anywhere on the raid path. So `WallSegment.Collapse()` disables a collider
and finds **nothing to un-carve**.

**PROVEN:** `0 carving obstacle(s) dropped`, on every collapse, plus zero `NavMeshObstacle`
components in all four raid scenes.
**LOCATED, NOT MEASURED:** that the navmesh hole where the wall stood therefore *stays* a hole
after the wall is gone. Nothing in the capture measures walkability. **The new `BREACH` line's
`routeStatus` / `pathLength` fields are what will settle it** — see §5.

⚠ That collapse line's own claim *"it no longer blocks agent pathing"* is **false in raid scenes**,
as is the comment at `WallSegment.cs:346-350` asserting the raid bakes fit carving obstacles. Both
are stale text; the capture disproves them.

### 2c. …and the troops have no route concept to use anyway

`TroopController.MoveToward` (lines ~821-850) drives `_agent.Move(displacement)` — a **straight-line
push**. There is **no `SetDestination`, no `CalculatePath`, no corner following**, and
`obstacleAvoidanceType = NoObstacleAvoidance`. So WO §3 Q1 answers: **structures are just targets;
there is no pathing concept of "the wall is a barrier on my route" on the attacker side at all.**

### 2d. The selector mechanism, exactly

`NearestHostile()` is pure nearest-wins with one boolean. `_preferStructures` is set **only** for
`role == "siege"` (`Configure`, line ~336), and in `troops.json` only `troop-catapult` is siege.
For every other role a hostile **structure falls through to the same `else` branch as a live
defender** — one shared nearest-wins bucket. A wall panel 3 m away therefore beats a garrison orc
11 m away; when it dies, the next-nearest hostile is the panel beside it. **Nothing invalidates or
re-evaluates a route on a structure death, because no route was ever computed.**

WO §3 Q2 answer: the 0.2 s rescan *does* re-run selection on a wall death — it simply has no route
input to change its mind with. WO §3 Q3 answer: **emergent, not authored** — no one preferred
structures; they merely got put in the units' bucket by the `else`.

---

## 3. §3.4 ANSWERED EXPLICITLY — whose AI was underfighting

**BOTH SIDES underfought, for two DIFFERENT reasons — and the first raid ended with literally zero
combat between the two armies.** The single line that settles it:

```
12:59:47.763 [Flow:Raid] raid-end reconcile - deployed 10, survivors 10, wounded 0 (stars 0, recovery 300s).
```

**Ten troops deployed, ten survived, none even wounded, 0 stars, 32 % razed, cleared=False.** A raid
in which her whole warband walked away untouched is a raid in which the garrison never engaged it,
and 32 % razed with 0 stars is a raid her warband spent on walls. Neither army fought the other.
(Raid 2 at 13:02 did resolve — `deployed 10, survivors 9, wounded 1, stars 3` — after the troops
eventually reached the guards.)

The two sides fail for different reasons:

- **Her troops fought — masonry.** They are the wall-chewers, attributed by cadence: the `16.7` hits repeat at
  **1.02–1.03 s** (`troop-footman` `attackCooldown` = **1.0**) and the `36` hits at **1.23 s**
  (`troop-archer` `attackCooldown` = **1.2**). Two walls progress **in parallel** on independent
  cadences (SS_7 and SS_12, on opposite sides of the breach), which one hero cannot do; and the
  nine defender kills all log `CombatFeedback Kill gated: dealtByHero=False`.
- **The defenders fought — their own objective.** They were far from idle (13 800
  `[Flow:EnemyAggro]` lines; 8 guards + boss at ~413 lines each), but **10 687 of those lines are
  the same one**:
  `raidguard-raider_camp_small-N: ProbeForStructure hit 'RaidSpire' -> stopping agent to attack`.
  Corroborated by the objective's own damage trace — `RaidSpire took N (contact)` **×37** against
  `(attack)` ×4; contact damage is what enemies deal. **The garrison spent the raid attacking the
  spire it exists to guard.** That is §6.1, and the reconcile line is why it is not a footnote: it
  is the direct reason her troops took zero casualties in raid 1.
- **What she saw:** ten troops hitting wall panels outward from a breach they never walked
  through, while eight orcs stood inside hitting their own spire, and nobody hit anybody.
  "The AI didn't really fight" is an accurate description of **both** armies.

⚠ Honest limit: this attribution is by damage-cadence + parallelism, not by an attacker-named trace
line, because **no line in the current build names who damaged a wall**. The new `RETARGET`/`ENGAGED`
lines close that gap on the next capture. I am stating it as strongly-evidenced, not as measured.

---

## 4. WHAT I DID NOT DO, AND WHY

- **No behaviour change.** §12 hard gate: the new trace has not run. The fix below is a
  **recommendation**, not an edit.
- **No regression authored.** Writing a RED-proof suite blind, against a scene I cannot open, would
  be the same guess in test clothing. Design is in §5.
- **No compile gate / regression / commit / push** — CLI lead's lock and sole-committer rule.
- **Did not touch** `RaidDeployController`, EndState, the raid clock, or any raid HUD file
  (WO-1436 / WO-1437 lanes). The fix as scoped in §5 needs none of them.

---

## 5. RECOMMENDED MINIMUM FIX — and the trace line that licenses each half

**The breach problem is TWO defects and the smaller one alone will not fix it.**

**(A) Selector — the minimum, and it is genuinely small.** In `NearestHostile`, for
non-siege roles, prefer a live **unit** in radius over any **structure**; take a structure only
when no unit is acquirable. Three lines: the `else if` becomes an explicit unit bucket and the
return prefers `bestUnit` unless it is null. This alone stops the sideways wall-walk, because the
orcs inside become the target the moment one is in radius.
*Licensed by:* a `RETARGET` line reading `won='Wall_Outer_SS_7(WallSegment)' kind=struct dist=2.9m
| runner-up(other kind)='RaidGuard…' dist=11.4m`.

**(B) Route — required for "push THROUGH the breach".** (A) makes troops prefer defenders; it does
**not** make them walk through the hole, because the navmesh under a felled wall is still not
walkable and the troops do not path at all. Minimum honest options, cheapest first:
1. Give raid `WallSegment`s a **carving `NavMeshObstacle`** (the village already has
   `WallNavObstacleInstaller` — extend, do not greenfield). `Collapse()` already disables obstacles
   and would then genuinely open the route. This makes the existing collapse trace true.
2. Only then is a route concept worth adding to troops.
*Licensed by:* the new `BREACH` line's `status=`. **`PathPartial`/`PathInvalid` ⇒ (B) is mandatory
and (A) alone ships a half-fix. `PathComplete` ⇒ (A) alone is sufficient** and (B) can be dropped.
**Run one raid and read that single field before choosing.**

**Explicitly out of scope / bring back as a recommendation, not smuggled in:** giving troops real
pathfinding (`SetDestination` + corner following). That is the pathing rewrite WO §4 forbids.

---

## 6. TWO ADJACENT DEFECTS FOUND, NOT FIXED, NOT MINE

1. **Defenders attack their own base's objective — PROVEN, and it is HALF OF THE OWNER'S COMPLAINT
   (see §3), not a footnote. It deserves its own WO and is arguably the higher-value fix.**
   `IDamageableStructure` carries no faction, and `EnemyBrain.ScoreAndPickTarget` /
   `FindNearestStructure` / `Enemy.SweepForNearestStructure` apply **no faction test** — while the
   attacker side does (`dmg.Faction != CombatFaction.Hostile`), because `IDamageable` has a
   `Faction` and `IDamageableStructure` does not. Captured consequence, 10 687 lines plus 37
   `(contact)` hits on the spire, and a raid where her warband took zero casualties.
2. **`EnemyBrain`'s scan buffer is `Collider[32]` with no layer mask** (`ScoreAndPickTarget` ~line 1583)
   and it scores `_scanBuffer[i].transform` (the collider's transform, not the structure root). Inside
   a raid base full of wall panels that buffer fills with masonry in arbitrary order — the identical
   crowding that already forced `TroopController`'s buffer to 128. A multi-collider wall also enters
   the race several times at slightly different distances.

Both are in `EnemyBrain`/`Enemy`, deserve their own WO, and were left untouched.

---

## 7. ACCEPTANCE, HONESTLY MARKED

- [x] Instrumentation lands FIRST — `[Flow:TroopAI]`, 5 line classes, brace-balanced.
- [x] A captured run is quoted — the wall-collapse walk and `0 carving obstacle(s)` (§2a/§2b).
- [x] Root cause stated with the line that proves it (§2b). Two causes, not one.
- [ ] **A captured run showing units routing through the breach** — blocked: requires Unity.
- [ ] **A regression pinning it** — deliberately not authored blind; design in §5.
- [x] §3.4 answered explicitly (§3).
- [ ] `REGRESSION_OK n/n` — not run (lock held by CLI lead).

**Board note:** this RESULT file sits beside a WO still marked READY, deliberately — the fix is not
written. Checked `tools/board_build.py`: `*.RESULT.md` is exempt from the findings scan (line 300)
and only adds a derived `RESULT` badge (line 673); there is no reverse check that flags a RESULT on
a non-DONE WO. So this does not create a board contradiction. **Do not flip the status to DONE.**

**Next action for the CLI lead:** gate + commit this instrumentation, put one raid on a device, and
read the `BREACH` line's `status=` field. That single value decides whether the fix is three lines
in `NearestHostile` or three lines plus a navmesh carve.
