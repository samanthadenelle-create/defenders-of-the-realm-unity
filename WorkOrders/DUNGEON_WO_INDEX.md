> ## RECONCILED 2026-08-08 - true status is STALE (routing map still valid, row states are not)
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: this is the canonical map for 8
> dungeon WOs and it carried NO Status line, so it could not tell a reader which rows are done.
> Rows that have LANDED since it was written: **919** (roomforge enclose), **922** (wider rooms),
> **1000** (starter-outpost overhaul, commit 6c740b08), and **1004** (composed-pipeline visual fixes,
> commits fab50709 + 94c23be3 - PARTIAL, sec. 1.3 candle light is still only a seat).
> Row **1001** is authored complete through Phase 2 but its descents do not function at runtime
> (canon 2026-08-08: PathPartial, floor delta 0.00m). Rows **920**, **921** and **1005** are NOT STARTED.
> The build order and overlap reconciliation below remain valid; the row states do not.
> A Status line has been added - the file previously had none.

# Dungeon Work-Order Index (canonical map — UI seat maintains)

**Status:** STALE (reconciled 2026-08-08) - routing/build order valid; 919, 922, 1000 landed, 1004 partial, 1001 authored-but-broken, 920/921/1005 not started.

**Purpose:** one map of every dungeon WO across both number ranges, with overlaps reconciled and a single build order — so CLI runs them as one coherent effort, not 8 competing docs. **Grok's 919–922 are folded in here as guidance;** this index is the canonical reconciliation (UI refines Grok → CLI implements).
**Date:** 2026-08-07 · **Author:** UI seat.

## The set (8 WOs, 5 concerns)

| WO | Concern | Owner-seat | Role in the effort |
|----|---------|-----------|--------------------|
| **919** roomforge enclose (taller walls + ceilings + kill sky) | Enclose (geometry) | Grok | The **wall-height + ceiling** geometry for composed dungeons |
| **1004** composed-pipeline visual fixes | Enclose (materials/lighting) + bugs | UI | Rainbow-floor + stray-marker fixes **+ enclose/relight materials + Env_Candle lighting + fog** for composed |
| **1000** starter-outpost visual overhaul | Enclose (hand-coded outpost) | UI | Same enclose/relight, for the **hand-coded** `KayKitChallengeOutpost` (not composed) |
| **921** fire cosmetic vs hazard | Fire/candles/traps | Grok | Dial torches to **cosmetic candle** (Env_Candle) vs **telegraphed hazard** fire; **THE candle-light home** |
| **922** roomforge wider rooms (Cell 6→10 m) | Room size | Grok | Widen **all** composed rooms — one master `Cell` knob + rebuild/recompose/rebake |
| **920** dungeon stationary camera | Camera | Grok | Default explore cam = **locked OTS, no bounce** (FPV → opt-in); depends on 919 |
| **1005** dungeon UI cohesion | UI | UI | Descend button + EXIT label → **Obsidian kit** |
| **1001** deep-dungeon engine + 3 dungeons | Systems + content | UI | Multi-level descent, enemy families, boss, loot, **oil/darkness**, the 3 themed dungeons |

## Overlap reconciliation (no duplicated work — who owns what)
- **Enclose = 919 (geometry) + 1004 (materials/lighting) as a PAIR, for COMPOSED dungeons.** 919 raises walls + adds ceilings + kills the skybox; 1004 fixes rainbow/markers + lays the stone materials + **candle-VFX lighting + fog**. They run in **one bake wave**, not separately. `1000` is the same enclose/relight for the *hand-coded outpost* (different builder — keep separate).
- **Candles / fire = 921 owns the ruling; 1004 applies it to composed lighting; 1001 #7 owns the trap side.** All three agree: **Env_Candle (subtle TinyFlames wick, ~0.45) = sconce light; big/room fire = a SEPARATE recipe (MediumFlames/WildFire); real traps telegraph.** No conflict — 921 is the policy, 1004 wires the light, 1001 wires the traps. (Env_Candle / the D:\flames candle study lands in 921/1004.)
- **Wider rooms = 922, standalone**, but its bake **combines with 919** (one rebuild wave, per 922 §E).
- **Camera = 920, standalone**, sequenced **after 919** (a stable cam over open sky still shows blue).
- **UI cohesion = 1005**, **engine/content = 1001** — independent lanes.

## Build order (bake waves + sequence)
1. **BAKE WAVE 1 — geometry + size + surfaces (combine 919 + 922 + 1004 + 921-lighting):** set `Cell = 10` (922) → raise walls + ceilings + kill sky (919) → materials: no rainbow, stone shell, **Env_Candle sconce light + fog** (1004 + 921) → `BuildAll` prefabs → recompose all graphs → **rebake once**. (922 §E + 1004 + 919 all want the same single rebuild.)
2. **1000** — the same enclose/relight for the hand-coded outpost (separate builder, can run in parallel).
3. **920** — stationary camera (after wave 1 so it seats under the new ceilings).
4. **1005** — UI cohesion (Descend button + EXIT label to the kit).
5. **1001** — the deep-dungeon engine slices + the 3 themed dungeons (uses the now-clean, enclosed, wider, candle-lit pipeline).

## Numbering — reconciled, no collision
Grok's **919–922 are in the CLI MAIN-LINE range and ALREADY consumed** there — the banner head shows main-line
next-free = **923** (`900–922 CONSUMED`). No collision. (Fixed the stale banner *table row* that still read 912 —
a self-contradicting authority file is how collisions start.) Future UI dungeon WOs mint from the **1000s** block;
CLI main-line resumes at 923.

## RESULT
No RESULT for the index; each WO keeps its own. Update this index if a dungeon WO is added/merged.
