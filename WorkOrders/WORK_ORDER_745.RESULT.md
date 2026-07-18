# WORK ORDER 745 — RESULT (+ Room Forge program 740–743 close)

**Status:** DONE — gate-green, committed on `wip/village2-and-f8-tickets`.
**Date:** 2026-07-18

## Room Forge program (WO-740 → 743) + WO-745 — all landed on mainline

| WO | What | State | Commit |
|----|------|-------|--------|
| 740 | Merge Room Forge (feat) into wip + CompileGate | DONE | `070f955f` + meta `a87cdee2` |
| 741 | Default room prefab library (17) + KayKit materials | DONE | `a4fb5cf0` |
| 742 | Bake demo compose layout, soft-lock-free scene | DONE | `82fa4d12` |
| 743 | Canon / README / RESULT close | DONE | this file |
| 745 | Regression oracle + FlowTrace + baker contract fixes | DONE | `5eb5a7fa` |

## WO-745 — the three deliverables

**1. Root cause of the demo 0/2 mate failure (DATA, not logic).** The sample layouts referenced
long-form socket ids (`north_door_01`) that no shipped prefab carries — `DefaultDungeonRoomsBuilder`
emits `n_door_01`. Every connection hit `missing-socket` → `matesFail == connections`,
`sealed == all sockets`. Also some branch-turn yaws pointed the same direction instead of opposing
(alignment fail). Fixed both sample layouts (socket ids + cells + yaws; both Resources +
StreamingAssets copies byte-identical) + added the `"version"` field the DATAWEB oracle requires.

**2. Two baker contract fixes** (`DungeonBaker` + shared `DungeonBakerChecks`):
- FIX-1 hard gate: on ANY mate failure the bake now saves NO scene and touches NO Build Settings
  (optional `_FAILED_<id>.unity` behind a default-off editor pref). Previously it saved then logged.
- FIX-2: post-pass re-verify (a later nudge can drift an earlier mate → `drift` failure) + AABB
  room-overlap check (`overlap` failure).
- Plus the `ToFilesystemPath` fix for a doubled-path crash (`Replace("Assets/", …)` also mangled
  `StreamingAssets/`) — this is what unblocked WO-742.

**3. `[Flow:DungeonBake]` / `[Flow:RoomForge]` instrumentation** per INSTRUMENTATION_STANDARD, with a
per-mate fail-reason enum (missing-instance / missing-socket / type-mismatch / distance / alignment /
drift / overlap) and a machine-parseable `SUMMARY id= rooms= matesOk= matesFail= sealed= saved=` line.

**4. `RoomForgeRegression` (10 cases, `ROOMFORGE_REGRESSION_OK`)** wired into `DataRegression.RunAll`
as `[room-forge]` — the same ratchet pattern as `[ui-mvvm]`. Throwaway in-memory scenes only; never
opens/saves a shipping scene; passes WITH and WITHOUT the KayKit pack present.

## Proving evidence (§12 — captured lines, not claims)

- CompileGate: `COMPILE_GATE_OK`.
- Oracle: `[room-forge] ROOM-FORGE OK — 10/10 cases pass (17-room catalog, dual-copy, mate/seal/drift/
  overlap contract, spine+demo green sealed=1)`.
- DataRegression: `REGRESSION_FAIL: 8` = the exact known baseline (arena ground, B2 dual-wallet,
  pet-slot, core-save Tribes/Wards/Arena, fountain L2/L3, DATAWEB drift, HUDUI, orc-raider) — the
  dungeon-layout `version-missing` findings are cleared; NO new red.
- Demo bake (§7.5): `SUMMARY id=d4_sunken_crypt rooms=3 matesOk=2 matesFail=0 sealed=1 saved=True
  path[EntryHall->RewardVault]=PathComplete`.
- Hard-gate proof (§2 fix-1): the oracle's negative case emits `[Flow:DungeonBake] mate FAIL … reason=
  type-mismatch` and the bake reports `saved=False … ABORT: not saving scene, not touching Build
  Settings (WO-745 fix 1)`.

## Notes / follow-ups (not blocking)
- `DungeonBakerChecks` lives in the runtime `DeNelle.Dungeons` assembly (not `Editor/`) to avoid an
  editor asmdef reference cycle; `DeNelle.EditorRegression.asmdef` gained `DeNelle.Dungeons` +
  `Unity.AI.Navigation`. Single source of truth for the mate logic preserved.
- NavMesh case 8 is lenient headless (`PathPartial` = note, not fail) so a flaky `-nographics` bake
  can't false-fail; only `PathInvalid`/walkable-sample-fail is a hard fail.
- KayKit dungeon atlas texture stays machine-local per the big-art-out-of-git policy; the prefabs
  carry sockets/meta regardless, so the oracle + bake pass on a fresh clone.
- Banner: 745 recorded, next-free = 746 (`CLI_LANES_WO_NUMBERS.md`).
