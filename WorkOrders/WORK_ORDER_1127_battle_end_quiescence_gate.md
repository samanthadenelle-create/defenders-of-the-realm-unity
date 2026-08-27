# WORK ORDER 1127 — Battle-end quiescence gate: assert the world is back to baseline, and name what isn't

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
`BattleQuiescenceGate` (Core/Combat) + `BattleQuiescenceRegression` (registered suite), armed from
`BattleArena` resolve on BOTH outcomes. All six suite groups green, including the originating defect:
`[defect-2026-08-20] timeScale 0.04 reported with the value AND the 4% speed`. Gates: `COMPILE_GATE_OK`;
DataRegression **211/215** with the 4 known-red baseline and nothing new.
⚠ §5.4 PARTIAL: the gate is armed at the shared resolve seam so it covers the in-place arena AND any
scene-load battle routed through `BattleArena`, but only the in-place path has actually been exercised —
no dungeon/garrison battle was run against it.
**Minted:** 2026-08-20 (CLI seat) — banner bumped 1127 → 1128 in the SAME edit
**Lane:** Village / Arena + Core diagnostics. Touches battle resolve and adds one Core-side checker.
No scenes, no gameplay balance, no economy.
**Provenance:** owner device session 2026-08-20 09:16 (*"After fight was over im back in town but
controls frozen for movement"*), `logs/device/2026-08-20-town-freeze.log`; and the owner's
architecture ruling later that morning.

---

## 1. THE CAPTURED DEFECT THAT MOTIVATES THIS

```
[Flow:HeroOwner] scene='Main_Castle_Overworld' owner=HeroLocomotion ownerCC=none
                 scriptedMove=off ... inputSuppressed=False timeScale=0.04 dt=0.0013
```

Input was never blocked. `HeroLocomotion` still owned the hero. **The world was running at 4 % speed**
and stayed there for **182 consecutive samples across three minutes**, beginning at the exact instant
the battle resolved:

```
09:16:56.78  [Flow:BattleArena] VICTORY BURST FIRED stars=3
09:16:57.08  [Flow:HudKit]      posture hostile(postbattle)->modal
09:16:57.2x  timeScale=0.04 ... and never returns
```

`0.04` is this project's own hit-stop value (`HitTier.Medium`, and the `Enemy.cs:2837` death stop).
The leak itself is FIXED (`6879abd60`: a deadline watchdog on the unscaled clock + an `OnDisable`
restore in `HitStopManager`).

**This ticket is not that fix.** It is the answer to the question the incident actually raised:
*nothing asserted the world was back to normal when the battle ended*, so a broken global went
unnoticed for three minutes until the player tried to walk.

## 2. ⛔ THE RULING — AND THE ARCHITECTURE IT REJECTED (do not re-litigate)

The owner proposed a full scene swap: town → save JSON → tear down → load a battle-arena scene →
reward → tear down → reload town, *"and then we can be consistent and always confirm tear down
between."* After weighing the evidence she **ruled for the contract, not the swap** (*"i agree with
your logic and agree on solution"*).

**Why the swap was rejected — this is load-bearing, and it is measured, not asserted:**

- **It would NOT have caught this bug.** `Time.timeScale` is an ENGINE GLOBAL.
  `SceneManager.LoadScene` does not reset it. 0.04 would have ridden through the save, the teardown,
  the arena scene, the reward screen and the reload — a frozen town *after two loading screens*.
- **A scene load destroys scene objects, and almost nothing that has actually leaked here is one.**
  `DontDestroyOnLoad`: **350 call sites across 212 files** (`HitStopManager` among them). Mutable
  statics in the Vfx + Arena modules **alone**: **~290**. Engine globals: all of them. The Echo-modal
  handle that broke the FTUE and the `PanelManager.AnyOpen` class of bug are in the same category.
- **The cost is real and was measured on the owner's Seeker**, three transitions, consistent:
  `LoadScene committing 09:04:01.340` → `home hub loaded 09:04:05.090` = **3.75 s**, and ~5 s to a
  steady frame (first `[Flow:Perf]` reads `fps=36`). Round trip ≈ **7.5 s**. That session had **13
  battles** — about **90 seconds of loading screens** for wandering-encounter skirmishes. A full hub
  reload also re-runs the migration writer, the vendor restore, the injector re-skin and the navmesh,
  each of which has had its own defect.

**What the owner was right about, and what this ticket buys:** "always confirm teardown between" is
the good idea, and it is separable from the swap.

**⚠ The gate must NOT assume the in-place arena.** The dungeon / garrison / raid paths
(`Dungeon_*.unity`, `Garrison_*.unity`, `RaidBase_*.unity`) are genuine scene loads TODAY. A contract
bound to one architecture covers neither well; this one lives at the battle-resolve **boundary** and
must hold for both. That is also what keeps the door open if the swap is ever revisited for a
different reason (Seeker memory headroom, decoupling arena content from the hub bake) — those are
legitimate arguments; correctness is not one of them.

## 3. WHAT TO BUILD

A single Core-side checker, called at battle resolve, that asserts the world is at baseline and
**FlowTrace.Fail**s naming exactly which invariant is wrong.

**Invariants (each must be individually named in the failure text — a gate that says "something is
wrong" costs a debugging session):**

| # | invariant | why it is on the list |
|---|---|---|
| 1 | `Time.timeScale == 1` (within epsilon) | THE captured defect. Also the cheapest possible check for the most player-visible failure. |
| 2 | No battle lock still held (`BattleLock.IsInBattle()` false) | a stuck lock suppresses combat input and gates the HUD out of Battle forever |
| 3 | Combat input not left gated | `[Flow:Combat] input gated` is the town-side symptom of a half-exited battle |
| 4 | No modal handle still open (`PanelManager.AnyOpen` false once the reward screen closes) | the Echo-modal FTUE cascade was exactly this, and it is invisible until a tap goes nowhere |
| 5 | No orphaned arena actors left alive | the in-place arena spawns at an offset; survivors are invisible to the player and still tick |
| 6 | Hero owner is `HeroLocomotion`, no foreign mover | `owner=FOREIGN-CC` is a known dungeon-side movement failure and belongs in the same net |

**Design constraints:**

- **Report, never "repair" silently.** The gate's job is to make a broken teardown LOUD. Where a
  safe restore is obvious (timeScale) it may restore — but it must `FlowTrace.Fail` first and say it
  did. A gate that quietly fixes things trains everyone to stop reading it and hides the real owner
  of the bug.
- **Run it slightly AFTER resolve, on the unscaled clock.** Some of these legitimately settle over a
  frame or two (posture transitions, the reward modal's own open). A one-shot check on the resolve
  frame will false-positive. Use a short unscaled grace, and re-check rather than sampling once.
- **The reward screen is EXPECTED to be open** during part of this. Invariant 4 is checked when the
  reward screen closes, not while it is up — otherwise the gate fails on correct behaviour, which is
  the fastest way to get a gate ignored.
- **Guard every probe** (`Guard.Try`). This is diagnostics; a throw inside the checker must never
  take down a battle resolve.
- **No new `Time.timeScale` OWNER.** There are already seven writers of that global
  (`HitStopManager`, `CombatFeedbackManager`, `ArenaDeathCam`, `WaveCelebrationManager`,
  `PauseController`, `HeroHitReaction`, `GameOverScreen`). Adding an eighth that fights them is worse
  than the disease. The gate observes; only the documented restore in invariant 1 writes.

## 4. DO NOT

- Do not implement the scene swap. §2 is the ruling, with the measurements behind it.
- Do not strip or quieten any existing FlowTrace (CLAUDE.md §12, owner ruling 2026-08-09).
- Do not weaken `HitStopManager`'s deadline watchdog (`6879abd60`) — the gate is a second net at a
  different level, not a replacement for the owner fixing its own leak.
- Do not add balance or economy changes here. Mana regen, ability wind-ups and the arena-vs-world
  kill-reward asymmetry are all open owner questions from the same session and belong in their own
  tickets.

## 5. ACCEPTANCE CRITERIA

1. A registered regression suite proves the gate **FAILS the known-bad state** for every invariant —
   set each one wrong in turn and require a named failure. *A gate that does not fail the known-bad
   state is not a gate* (the WO-1124 lesson, and the reason that ticket's suite exists).
2. The suite proves the gate **PASSES a clean teardown**, so it cannot become a permanent red that
   everyone learns to ignore.
3. The 2026-08-20 defect is reproducible against the gate: with `timeScale` pinned at 0.04 the gate
   fails and its message names `timeScale`, the value, and that the world is at 4 % speed.
4. The gate is wired into battle resolve and fires on BOTH paths — the in-place arena and a genuine
   scene-load battle.
5. `COMPILE_GATE_OK` + `DataRegression` at the known-red baseline with nothing new.
6. Owner felt-verifies on device: fight in town, win, and walk away (PO closes, §13).
