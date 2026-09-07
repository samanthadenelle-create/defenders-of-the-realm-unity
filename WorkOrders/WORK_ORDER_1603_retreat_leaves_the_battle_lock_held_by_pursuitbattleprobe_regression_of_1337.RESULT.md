# WO-1603 RESULT — Name the pulser, and stop the one pulse with no battle behind it

**Status:** EDIT-ONLY — instrumented + one provable producer fixed + suite extended.
NOT gated, NOT built, NOT committed (the lead gates and commits). No Unity was run.
**Date:** 2026-09-07
**Silo:** Core/Combat — Quiescence / pursuit pulse

---

## ⭐ LINE 1: THE PULSER IS **NOT NAMED ON THE OWNER'S DEVICE**, AND NOTHING HERE CLAIMS IT IS

No log in this repo names which producer held the lock in F8 seq 4701/4702. `logs/f8-inbox/`
carries the two captures in full (39 + 40 lines) and neither has a harvested `[Flow:*]` section;
there is no device `Player.log` for build 359651 in the tree, and this lane could not run the
AutoPilot fleet (no Unity). **The instrumentation below is what lands the name on the NEXT
capture.** Per CLAUDE.md §11B: unproven, said as unproven.

## WHAT THE CAPTURES DO PROVE (measured, this session)

seq 4702, verbatim:

```
battle-lock STILL HELD after the self-heal (retreat): [PursuitBattleProbe.Probe]
(was [PursuitBattleProbe.Probe]).
```

That message is emitted by `BattleQuiescenceGate.Arm` **after** it ran
`BattleSessionEnd.Release` → `PostureSignals.ClearPursuits()` (which sets `_pursuitCount = 0`)
and waited **exactly one frame** (`yield return null`). A zeroed ring cannot refill from a stale
entry, so:

> **The lock was re-raised by a fresh `ReportPursuit` call inside that one frame. This is a LIVE
> producer stamping every tick — categorically NOT the WO-1337 shape (a destroyed body's last
> pulse riding out `PursuitTtl`).**

The WO-1337 fix is **intact and was not touched**: `PostureSignals.RevokePursuit` is still
called from `Enemy.OnDisable` (`Enemy.cs:1145`) and from `Die()` (`:3273`), and
`DespawnRevokesPursuitAtSource` still pins both.

Second measured fact: **"retreat" is not the Flee button.** `BattleArena.Resolve` passes
`won ? "arena win" : "retreat"` and there are **seven** `Resolve(false)` call sites
(`BattleArena.cs:622, 657, 2167, 2192, 2228, 2233, 2332`). `:2228` is `"hero down - loss."` —
so **a hero DEATH in an overworld arena reports as context "retreat"**, which makes
retreat-after-hero-death the primary shape of this capture, not a secondary one.

## THE ATTRIBUTION HOLE (why the capture could not name it)

`PursuitBattleProbe` is the **READER** of the ring, never a producer — it returns
`PostureSignals.PursuitActive` verbatim. There are **THREE producer FILES**, and Enemy
reaches its stamp from **two branches with different guarantees** - four paths in all:

| producer | site |
|---|---|
| `Enemy.DriveNav` — brain-override branch | `Enemy.cs` (`chasingHero` from `_brainPositionOverride`) |
| `Enemy.DriveNav` — hero-aggro branch | `Enemy.cs` (`TryGetHeroAggroDestination`) |
| `OverworldEncounterSpawner` rep chase | `OverworldEncounterSpawner.cs:1240` |
| `RegionMobSpawner` aggro loop | `RegionMobSpawner.cs:240` |

The ring stored `(key, lastReport)` — an **instance id**, which identifies nothing once the body
is gone. Every capture of this defect since F8-46 has therefore named the messenger and stopped.

## THE ONE FIX SOURCE CAN PROVE: a chase steered at a hero who is DOWN

**The asymmetry is inside one method.** `Enemy.DriveNav` reaches its single `ReportPursuit` stamp
from two branches:

* the hero-aggro branch **already refuses a dead hero at source** —
  `Enemy.cs:1802`: *"The hero may have died (HeroHealth.IsAlive false) — don't chase a
  downed/invulnerable hero"*, `if (heroHealth != null && !heroHealth.IsAlive) { … return false; }`;
* the **brain-override branch has no such test and cannot get one from here**. `EnemyBrain` scores
  the hero as a candidate on `!= null && activeInHierarchy` with **no `IsAlive` gate** —
  `EnemyBrain.cs:1596` (`ConsiderCandidate(_heroTransform, 0.7f, HeroHpFraction(), 0.9f, …)`),
  `:1604-1612` (`FindHighestThreatTarget`, distance only), `:1458-1470` (`_heroOnlyTarget`
  validity). A **dead hero reports `Fraction` 0**, which the low-HP weight reads as the *most*
  attractive target on the field.

So the brain keeps steering onto the body, `DriveNav` reads `overrideOnHero`, and the stamp
re-fires **every frame for as long as the hero stays down** → `PursuitActive` true →
`PursuitBattleProbe` holds `BattleLock` → combat input suppressed, HUD pinned out of town. That is
exactly the one-frame re-latch the gate measured, and it is the watch item **commit `2b3d8e9af`
(WO-1526) wrote against itself**: *"EnemyBrain has no dead-hero check, defenders may keep mobbing
the body."*

⚠ **REACHABILITY AT THE JUDGE POINT IS NARROWER THAN "retreat-after-hero-death" READS, AND THE
DISTINCTION MATTERS.** The hole above is provable as a HOLE; what is *not* proven is that the
arena's OWN survivors are still alive when the gate judges. They are not: `ReturnHomeWithFade`
destroys them at `HomeFadeOutSeconds` = 0.35 s, while the gate judges after the defeat banner
closes plus `SettleSeconds` = 0.75 s. So the pulse this guard actually stops at judge time is a
**TOWN-SIDE brain-steered enemy** (a wave/garrison/roamer body) targeting the hero where she went
down, or at the town anchor she is returned to (WO-949) — not a staged arena survivor. The
`Enemy.DriveNav/brain` owner tag on the next capture is what confirms or clears that.

**The guard is on the STAMP, not on the steering.** Whether defenders should mob a downed hero is
a combat-feel question owned by WO-1526's watch item and by `EnemyBrain` — another lane's file,
deliberately untouched. What is not a question is the SIGNAL: `PursuitActive` exists to keep the
hero's combat inputs live while she is chased (F8-46, owner OPTION A). A hero who is DOWN has no
inputs to serve.

⛔ It cannot suppress a real chase: the predicate is the hero's own `IsAlive`, the same one the
sibling branch uses, and a null `HeroHealth` counts as ALIVE (the conservative reading
`BattleArena`'s own outcome arbitration takes: `bool heroAlive = hh == null || hh.IsAlive;`).
Nothing narrows `PursuitBattleProbe`, forces `BattleLock` false, or adds a release call.

## Files changed (edit-only)

| File | Lines | What |
|---|---|---|
| `Assets/_Modules/Core/HudModel/PostureSignals.cs` | 41-48 (ring carries owners), 50-71 (the why), 83-104 (`ReportPursuit(int,string)`), 106 (compat overload), 109-112 (`PursuitCount`), 121-136 (`DescribePursuits`), 149-159 (`Prune` moves owners), 164-179 (`RevokePursuit` names what it dropped), 181-192 (`ClearPursuits` names what it dropped) | The ring now carries an **owner tag** per pulse. `ReportPursuit(int, string)` added; the old `ReportPursuit(int)` delegates with `null` so **no caller breaks**. `DescribePursuits()` renders `key=… owner='…' age=…s`. ⚠ **Recorded, not logged per stamp** — the existing trace stays EDGE-ONLY (first add of a key), because a line at every stamp is a per-frame firehose that evicts the boot window out of the logcat ring (§12; memory `logcat-ring-buffer-destroys-evidence`). |
| `Assets/_Modules/Village/Enemies/Enemy.cs` | 1595-1600 (`chaseVia` declared), 1634 + 1641 (per-branch tag), 1690-1752 (the dead-hero guard + its RCA; the revoke is `:1746`) | Tags the two chase branches **differently** (`Enemy.DriveNav/brain` vs `/aggro`) — that split is what discriminates this shape on the next capture — and stops stamping (and revokes its own key) while `HeroHealth.IsAlive` is false. |
| `Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs` | 1240-1243 | Tagged `OverworldEncounterSpawner/rep-chase`. |
| `Assets/_Modules/Village/World/RegionMobSpawner.cs` | 240-243 | Tagged `RegionMobSpawner/aggro-loop`. |
| `Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs` | 78 (using), 204-216 (finding, `PURSUIT PULSES` at `:216`), 349-356 (`pursuitBefore` at `:354`), 372-381 (the after-half) | The battle-lock finding **appends** `PURSUIT PULSES: …` (WO-1233 sentence preserved verbatim); the still-held Fail now prints the ring **before the heal and after it** — an owner in both lines re-stamped within one frame and is therefore a live producer. |
| `Assets/_Modules/Core/Combat/PursuitBattleProbe.cs` | 60-76 | The RAISED transition trace now renders the ring. EDGE-ONLY (a steady state still logs nothing); `bool active = PostureSignals.PursuitActive;` is byte-identical, as `DespawnRevokesPursuitAtSource` requires. |
| `Assets/_Modules/Core/Combat/BattleSessionEnd.cs` | 192-195, 213-218 | `Release` logs the ring before the clear and after it. One line per battle end — nothing on the frame path. |
| `Assets/Editor/Regression/BattleQuiescenceRegression.cs` | 96-102 (registration), 148-155 (verdict text), 1795-2126 (the WO-1603 header + four cases: `:1834`, `:1901`, `:1968`, `:2027`) | Extended, **no second suite** (WO-1337 rule). |

⛔ **Not touched:** `Troops/`, `RaidScoring`, `RaidDeployController`, `HeroHealth`, `EnemyBrain`,
`WaveManager`'s WO-1308 unwind, `BattleLock`, `PursuitBattleProbe`'s predicate (byte-identical
`bool active = PostureSignals.PursuitActive;`), the gate's detection logic/thresholds, `BattleArena`.
No `Time.timeScale` was written.

## Suite cases (4 new, in `BattleQuiescenceRegression`)

1. **`PursuitPulseNamesItsProducer`** — behavioural. A live pulse must name its producer, its key
   and its stamp AGE, and the gate's battle-lock finding must carry the pulser **as well as** the
   WO-1233 `HOLDER(S):` clause (a strengthening that drops the previous strengthening fails).
2. **`RetreatAfterHeroDeathStopsThePulse`** — behavioural, seq 4702 reproduced exactly: release →
   ring empty → **re-stamp inside the same frame** → the lock is back, and the description must
   expose it as ONE named live producer. Then the contract: the producer standing its own claim
   down releases the lock at once. It **asserts the defect state reproduces** (branch (a)), so it
   cannot pass for the wrong reason.
3. **`RetreatDuringARaidNamesEachPulserSeparately`** — behavioural, two simultaneous producers:
   both named, the release drops both, and a re-stamp by ONE names that one and **not** the other.
   It also asserts the live chaser **still holds the lock** — the forbidden cure (F8-46) fails here.
4. **`EveryPursuitProducerNamesItselfAtSource`** — source-lint: all three stamp sites name
   themselves; the sibling dead-hero refusal survives; the brain-branch guard exists; no producer
   force-clears the window; the gate and the session release both render the ring.

## Oracle mutation — proven RED first

Unity was not run. The suite's `ReadCode` and the seven new lint rules were replicated
byte-identically in Python and run against real mutants (`HEAD` extracted with `git show`):

| Subject | Verdict | rules that fail |
|---|---|---|
| **`HEAD` (the shipped defect)** | **RED (6)** | producer-tag-enemy, producer-tag-rep, producer-tag-roamer, brain-dead-hero, gate-renders-ring, release-logs-ring |
| working tree (fixed) | **GREEN** | — |
| mutant: brain guard removed | RED (1) | brain-dead-hero |
| mutant: rep stamp anonymous again | RED (1) | producer-tag-rep |
| mutant: roamer stamp anonymous again | RED (1) | producer-tag-roamer |
| mutant: gate stops rendering the ring | RED (1) | gate-renders-ring |
| mutant: release stops logging the ring | RED (1) | release-logs-ring |
| mutant: sibling dead-hero refusal deleted | RED (1) | sibling-dead-hero |
| mutant: enemy stamp untagged | RED (1) | producer-tag-enemy |

⚠ **One rule was caught mid-authoring by this exercise and tightened** — the same trap WO-1337's
`probe-intact` rule fell into. `producer-tag-enemy` originally matched the bare identifier
`chaseVia`, and read **GREEN** against a stamp that had dropped the argument, because the tag is
read in more than one place in `DriveNav`. It now matches the call:
`ReportPursuit(GetInstanceID(), chaseVia)`.

The behavioural cases are self-mutating by construction (case 2 asserts the defect state
reproduces; case 3 asserts a live chaser still holds the lock), so a forced clear or a narrowed
probe fails them. ⚠ **They have not been EXECUTED** — they need one
`DeNelle.Editor.BattleQuiescenceRegression.RunAll` on the gate run (marker
`BATTLE_QUIESCENCE_SUITE_OK`), plus the registered `DataRegression.RunAll` count.

## Brace / NUL check (per file)

| File | Result |
|---|---|
| `Assets/_Modules/Core/HudModel/PostureSignals.cs` | BALANCED 44/44, 0 NUL |
| `Assets/_Modules/Core/Combat/BattleQuiescenceGate.cs` | BALANCED 75/75, 0 NUL |
| `Assets/_Modules/Core/Combat/BattleSessionEnd.cs` | BALANCED 25/25, 0 NUL |
| `Assets/_Modules/Core/Combat/PursuitBattleProbe.cs` | BALANCED 6/6, 0 NUL |
| `Assets/_Modules/Village/Enemies/Enemy.cs` | BALANCED 441/441, 0 NUL |
| `Assets/_Modules/Village/Enemies/OverworldEncounterSpawner.cs` | BALANCED 243/243, 0 NUL |
| `Assets/_Modules/Village/World/RegionMobSpawner.cs` | BALANCED 85/85, 0 NUL |
| `Assets/Editor/Regression/BattleQuiescenceRegression.cs` | BALANCED 178/178, 0 NUL |

## UNPROVEN — deliberately left open

1. **Which producer held the lock on 359651.** Not in any log in this tree. The next capture names
   it: read the `PURSUIT PULSES before the heal … after …` pair on the still-held Fail. If both
   halves name `Enemy.DriveNav/brain`, this fix is the cure and the ticket closes. If they name
   `OverworldEncounterSpawner/rep-chase` or `RegionMobSpawner/aggro-loop`, the owner is that file
   and this change was the instrument that found it.
   ⚠ The revoke in the dead-hero branch runs on every frame the condition holds. It is
   **idempotent and silent after the first call** — `RevokePursuit` finds no matching key and
   returns without logging — so it is not a per-frame trace source.
2. **Whether the hero was down at all in seq 4701.** `Resolve(false)` has seven call sites; only
   `:2228` is the death path. The `deadchase-<id>` throttle line added to `Enemy.cs` fires only on
   the dead-hero shape, so its presence or absence in the next capture settles this in one read.
3. **Whether `RaidScoring.Instance` can read non-null in the hub** (which would make
   `liveRaidContinues` true there and leave the hero permanently down — a shape that would also
   produce F8 seq 4704's `No movement or progress for 180s … input=True worldLive=True`). The
   scorer self-installs only into `RaidBase_*` scenes and nulls itself in `OnDestroy`, so this
   looks closed — **but it was not proven**, and `HeroHealth`/`RaidScoring` are today's lane and
   out of scope here. Flagged, not touched.
4. **`RepEngageWatcher.QuietNonPursuersOnBattleEnd()` is called on a WIN only**
   (`BattleArena.cs`, `if (won) …`). A retreat therefore leaves every already-stung rep chasing.
   That asymmetry predates today and was NOT changed here; it is a candidate for finding 1 and the
   owner tag will confirm or clear it.
5. **`OverworldEncounterSpawner`'s `_stung` is a LATCH that never resets.** Set at
   `OverworldEncounterSpawner.cs:1216` (`if (!_stung && d <= AggroRange) { _stung = true; … }`),
   read at `:1232` to drive the chase and the pursuit stamp, and exposed as `IsPursuing` at
   `:993` — and there is **no assignment back to false anywhere in that file** (`grep -n "_stung"`
   returns exactly those five sites). A rep stung before the fight therefore resumes chasing the
   moment `BattleArena.AnyBattleInProgress` goes false (the early return at `:1205`), with no
   leash to end it. With finding 4, that is the second candidate for the device's pulser. Read,
   not fixed — it is a behaviour ruling, and the `OverworldEncounterSpawner/rep-chase` tag now
   decides it in one capture.

## Acceptance against the ticket

| Ticket asks | State |
|---|---|
| Instrument: name the pulser at every stamp | ✔ all three producers tagged; `Enemy` split per branch |
| At the retreat release, log holders **with last-pulse age** | ✔ `BattleSessionEnd.Release` + the gate's self-heal before/after pair |
| Reproduce headless via the AutoPilot arena retreat scenario | ✘ **not done — no Unity in this lane.** Replaced by the seq-4702 behavioural fixture (case 2), which reproduces the one-frame re-latch over the real Core statics |
| Fix the owner that keeps pulsing | ◑ the one owner **provable from source** is fixed (a chase over a downed hero); the device's owner stays unproven per line 1 |
| Extend the WO-1337 regression, RED first, with retreat-after-hero-death and retreat-during-raid shapes | ✔ cases 2 and 3; RED-first proven by mutation |
| `BATTLE_QUIESCENCE_OK` headless / no device capture after a retreat | ✘ needs the gate run + a device session; PO closes |
