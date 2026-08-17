# WORK ORDER 1048 — A "Leave Dungeon" exit on every floor deletes the risk of descending

**Status:** SPEC — ⚠ §2 ambiguity must be confirmed with the owner before implementation
**Minted:** 2026-08-17 (UI seat) — provenance stack bumped 1048 → 1049 in the same edit
**Lane:** Dungeon exits / descent risk. ⚠ Touches an explicitly-guarded "never strand the player" path.
**Provenance:** owner F8 **seq=2515** (`flagged`), scene `dg_bonecrypt`, verbatim:
*"**LEave still on all steps with an exit portal in dungeons**"*. ⚠ **"still"** = this has been raised
before; treat as a recurrence, not a new report.

---

## 1. The mechanism

`DungeonController.HydrateExits()` (`:643-690`) places **one NORMAL exit in the entry room**:

```csharp
var normalExit = DungeonExitInteractable.Spawn(pos, () => ExitToVillage().Forget(), "Leave Dungeon");
```

It is called at `:364`, inside the run/floor hydration sequence (`HydrateCheckpoints` →
`HydrateEncounters` → `HydrateChests` → `HydrateExits` → …). **If that sequence runs per floor, every
floor gets its own "Leave Dungeon" arch** — which matches what the owner is seeing.

## 2. ⛔ CONFIRM WHAT "ALL STEPS" MEANS BEFORE TOUCHING ANYTHING

The flag is terse and **"steps" is ambiguous** in this codebase:

- **dungeon FLOORS** (the natural read — scene is `dg_bonecrypt`, and floor-to-floor descent shipped in
  WO-930), or
- **stair steps / traversal links** (`DressTraversalLinks` runs in the same sequence), or
- **tutorial steps** (unlikely here, but "steps" is that system's word)

⚠ **Do not guess.** The three readings imply different fixes, and a wrong one either strands players or
changes nothing. **Confirm with the owner, and record the answer in the RESULT.**

## 3. Why it matters, assuming FLOORS — this is a design defect, not clutter

**An exit on every floor removes the risk of descending.** The player can bail instantly from any
depth, so going deeper costs nothing but time.

That directly undercuts two things the project is building on:

- the **torch / oil / darkness risk-reward system** (~90% built, memory `dungeon-pillar-roadmap`) — its
  entire premise is that depth is a *commitment*
- **WO-1041 §3's "deeper = better"** gem tiering, which pays out for **elected risk**. If there is no
  risk to elect, the reward curve is paying for nothing

In WC3 terms: creeping is a decision because retreat has a cost. Free retreat from any depth turns the
dungeon into a corridor you can leave at any moment.

## 4. ⛔ THE COUNTER-CONSTRAINT — NEVER make a run un-leavable

The same function guards the opposite failure, explicitly (`:688`):

> `FlowTrace.Warn("Dungeon", "HydrateExits: entry room has no bounds — NORMAL exit NOT placed (run could be un-leavable!)")`

…and `DungeonExitInteractable` carries a whole arming discipline against accidental exits — a spawn
grace, a sustained-clear arm, and **WO-987's Obsidian confirm** (*"Leave only after 'Continue to
exit'"*), plus `OffsetExitFromSpawn` / `ExitSpawnClearance` so the hero cannot materialise inside the
archway and self-exit.

⚠ **A player trapped in a dungeon with no way out is far worse than a player who can leave too
easily.** So this is a question of **where and when** the exit exists, never of removing exits.

**Shapes worth considering (owner's call):** exit only on the **entry floor**; exits on floors already
cleared; a **one-way** descent with extraction at depth costing something; or keep them all and let the
gem tiering carry the risk instead. ⚠ Each interacts with checkpoints (`HydrateCheckpoints` runs in the
same sequence) — **read that before choosing**, or a fix here silently changes checkpoint semantics.

## 5. Prior art — read before re-treading

`WORK_ORDER_1007_dungeon_exit_real_asset.md` · `WORK_ORDER_1008_dungeon_exit_beacon_reads_as_light.md` ·
WO-987 (the exit confirm). ⚠ **"still" in the flag means something here was already addressed and the
owner is seeing it again** — check whether one of these covered it and regressed, exactly as WO-962 did
for WO-1036.

## 6. Acceptance criteria

- [ ] The meaning of "all steps" is **confirmed with the owner** and recorded (§2)
- [ ] Exit placement matches the owner's ruling
- [ ] ⛔ **Every run remains leavable** — no reachable state strands the player, proven across all four
      content dungeons (`dg_bonecrypt`, `dg_ember_deep`, `dg_sunken_vault`, `dg_stairwell_probe`)
- [ ] The `"run could be un-leavable!"` warning still fires when bounds are missing — **do not silence
      the guard** (§12)
- [ ] WO-987's confirm, the spawn grace and the arming latch are all **unchanged**
- [ ] Checkpoint semantics unchanged unless the ruling explicitly changes them (§4)

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. Headless descent through **every floor of all four dungeons**: assert the exit count per floor
   matches the ruling **and** that a leave path always exists
3. Owner felt-verifies in `dg_bonecrypt` — the scene she flagged + closes (§13)
