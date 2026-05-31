# Overnight Queue — CLI (2026-05-30 → 31)

> Ordered build queue for the overnight run. **RUN IT ALL — full send (owner: CLI is fast, take the
> whole queue top-down).** Headline priority is still **FINISH THE CASTLE + clear the playtest blockers**
> (so the village is playable and the world is reachable), then everything else in order — but the goal
> is to get through as much of the entire queue as possible this run.
> Each item is an existing WO — spec'd, reconciled to real code, UI-authored. CLI verifies, brace-gates,
> bakes (editor closed), commits per WO. Lane rule: **one writer on `VillageSceneBuilder.cs` at a time**
> (CLAUDE.md §9); world-code (own files) runs parallel to the castle/builder lane.
>
> **Owner directive 2026-05-30: do the whole queue.** Order is by dependency/priority (playable first),
> but don't stop at a tier — keep going through Tier 3 and Tier 4 as far as the run allows. Tier 4
> features: take each as far as it cleanly compiles+bakes (Phase 1 minimum, more if time).

## Already landed this session (context — don't redo)
- `c61a2b1` WO-136 parapet + WO-150 clean 5-building roster
- `7f0c60b` two-scene world (OuterWorld regions + mine nodes) + WO-136 wall-barrier collision
- `be3fe75`/`a33dc87` WO-140 hero animator (Mixamo) + Humanoid rig fix
So: parapet, wall-barrier collision, clean roster, two-scene world, hero animation = **in**. The castle
is *partway*; the queue finishes it.

---

## TIER 1 — PLAYTEST BLOCKERS (do first; the village isn't playable until these clear)

1. **WO-158 — Gates impassable / cannot exit (+ make it 4 gates).** Hero can't leave the castle at any
   point. Add the **north gate** (mesh gap + split `WallBarrier-North` into two spans + north drawbridge),
   and fix the existing 3 gates' exit (drawbridge walkability / opening width ≥ nav agent). Rebake +
   verify interior→exterior path through every gate. **Highest priority — it gates everything outside.**
2. **WO-156 — Camera over the high walls + pivot + wall-fade.** Camera was buried in the wall ("nothing
   opens"). Lift above the parapet looking down, add orbit/pitch, clip-avoidance, and **per-wall
   transparency fade** when a wall occludes the hero (keep full wall height — it's a defense-read signal;
   do NOT lower). Confirm building [F] interactions become reachable.
3. **WO-157 — Strip crystal veins (magenta).** The magenta shards = deleted crystal-vein content
   re-spawning; crystals are world nodes now. Add the vein generator to the WO-150 skip/strip list,
   rebake. No magenta in the village.
4. **WO-163 — Console error triage (now spec'd from the Editor.log).** UI read the log: **no crashes/
   NullRefs.** The spam is **3,351 errors from `AmbientNPC.UpdateAnimator`** driving an animator param
   that doesn't exist (every NPC, every frame) — guard with `HasParameter` / fix the NPC controller.
   Plus a quick **AudioMixer exposed-param** fix (restores volume sliders, helps WO-162) and minor tree/
   VFX shader warnings. **#1 (AmbientNPC) is the priority — it's 99% of the noise + a perf cost.**

## TIER 2 — FINISH THE CASTLE (the headline; WO-136's remaining slices)

5. **WO-136 remaining — drawbridges interactive + rampart access complete.** Parapet + wall collision are
   in; the moat exists. Finish: the **4 drawbridges** as the gate crossings (walkable + NavMesh-linked —
   overlaps WO-158), confirm **stairs + tower access** reach the rampart (hero climbs, walks the full
   walkway, parapet stops the fall-off), all at stone tier. Then **WO-137 rebake** (two-level NavMesh:
   off-mesh links / finer voxel for stairs; enemies excluded from the wall top; spawn→Heart intact).
6. **WO-137 — Castle/rampart rebake.** The dedicated post-castle bake (depends on 5). Two-level NavMesh,
   verify collision-on-real-wall at correct height, four working gates, hero reaches rampart, no magenta.

## TIER 3 — SAFE PARALLEL (world/code lane — own files, do NOT touch `VillageSceneBuilder` while Tier 1–2 bake)

7. **WO-155 — Region enemy spawning.** Data-driven region→enemy tables (Goldfields/Stoneback = living
   Wildlands; Mirewood/Ashwood = Wound-tied) + `ThreatLevel` (danger tier × depth) scaling + the
   Fallout **red-skull** nameplate tell. Built on the existing roster doc + ZoneManager + enemy defs.
8. **WO-135 — P1 bug-triage fixes.** CrystalMine auto-upgrade coin-spend, VFXManager counter drift,
   CrystalMine wave double-subscribe, WaveManager dict leak (+ 4 P2s). Pure code, no builder.

## TIER 4 — IF TIME (bigger features; can start the data/Phase-1 of each, no bake needed)

9. **WO-153 — World Crystal Mine** (renewable region-graded node) — Phase 1 data.
10. **WO-159 — Node Settlements** (claim→harvest→defend→deplete; 3-day razed lockout; deep-region uneven
    terrain) — Phase 1 (node-as-finite-reserve reframe + settlement data). Big; data-first.
11. **WO-160 — Wandering Tribes** (radius-trigger spawn, state-save, **randomized raid size** in a band —
    some easy/some brutal; reduced respawn; all hostile) — Phase 1 records + graph. Pairs with WO-159.

## PARKED — not for overnight (design-led / needs owner or designer)
- **WO-152** — full city redesign (Step 1 = structured, center-anchored, see-from-everywhere layout) —
  **designer is on it**; don't auto-build.
- **WO-161** — Player Home + Pet Home + Store interiors (walk-to-counter; founder/Genesis; cozy player) —
  needs owner design calls (founder mechanic, gear depth).
- **WO-162** — Player music selection (jukebox) — low priority, after the above.
- **WO-154** — Rare timed crystal spawns — after WO-153.
- Polish backlog: pulsing ambient crystals (designer/VFX).

## Coordination / guardrails
- **`VillageSceneBuilder.cs` = single writer.** Tier 1–2 all touch it — serialize them; don't run two
  builder edits at once. Tier 3 (WO-155/135) and Tier 4 data are **own-file**, safe to interleave.
- **Editor closed for every bake** (project lock, CLAUDE.md §3). Brace-gate every `.cs`. Commit by
  explicit path (the EOL-churn trap — never `git add .`).
- **Write a `*.RESULT.md`** per WO completed; UI marks the matching tracking done next session.
- If blocked or a spec is wrong, leave a note in the WO + a RESULT stub rather than guessing.

---

🤖 Queued by the design/UI lane. Priority = playable castle first (exit gates + camera + clean village),
then finish the fortification, then the world/combat code in parallel. Everything references an existing
WO; nothing here is new scope.
