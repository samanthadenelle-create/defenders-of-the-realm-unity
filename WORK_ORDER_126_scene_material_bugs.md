# WORK ORDER 126 — Village Scene Material & Placement Bugs

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30
**Priority:** High — live-playtest visual defects in the Village/Elarion scene
**Lane:** Architect (World/Environment) — `VillageSceneBuilder.cs` + architect-lane rebake
**Scope:** Mixed — one editor/CLI material-fix step (no `.cs` change) + small `VillageSceneBuilder.cs` coordinate edits, then a rebake (WO-103 pattern)

---

## Context

Four visual bugs were caught in a live playtest screenshot of `Village.unity`
(the Elarion scene). Two are missing-material (URP magenta) signatures, two are
prop/building placement overlaps. All four resolve through the architect lane:
the magenta is fixed by the existing polyperfect URP material fixer (an
editor/CLI op, no code change); the placements are coordinate edits in
`VillageSceneBuilder.cs`. Every fix needs a **rebake** to appear in the scene.

### Single-touch bottleneck — coordination required

`Assets/Editor/VillageSceneBuilder.cs` is a serialization bottleneck (CLAUDE.md §9).
**WO-104** (replaces `BuildWallPerimeter()` with `BuildCastleFortification()`),
**WO-107** (climate regions / terrain), and **WO-109** (rampart-level wall towers)
ALL edit this same file. Only ONE branch touches it at a time. Coordinate the
ordering with CLI before landing — the coordinate edits here (Bug 2, Bug 3) must
be rebased onto whatever wall/fortification version is current, because
WO-104 changes the perimeter coordinates the Farm-clip math depends on.

### Hard scene rules (CLAUDE.md §3, PIPELINE_STATE.md §4)

- **NEVER hand-edit `Village.unity`** — corruption-on-resave history. All scene
  changes go through `Defenders > Week 3 > Build Village Scene`
  (`-executeMethod DeNelle.Editor.VillageSceneBuilder.BuildVillage`).
- Run the rebake only with the Unity editor **CLOSED** (project lock).
- UI does not fire batchmode — the rebake is CLI's, queued as a WO-103-style step.

---

## Bug 1 — Magenta missing-material shapes ("A A" shapes + purple blob near the tree)

**Symptom:** Bright magenta/purple shapes render near the central Elarion tree
and elsewhere — the classic Unity URP missing-material fallback (built-in/Standard
shader rendering pink in URP, or a model whose material did not import).

**Root cause:** The polyperfect Low Poly Ultimate Pack is **gitignored**
(CLAUDE.md §4) and ships with **built-in (Standard) materials**, which render
magenta in this URP project. On this machine the pack's materials have not been
converted to URP/Lit (or the pack was re-imported and the fixer was not re-run).
The "A A" shapes and purple blob are polyperfect prefabs (perimeter wall
segments, towers, building prefabs from `Assets/polyperfect/...`) showing the
URP error-shader fallback.

- Editor fix that exists: `PolyperfectUrpFix.Fix()` —
  `Assets/Editor/PolyperfectUrpFix.cs:27` — menu **`Defenders/Art/Fix Polyperfect URP Materials`**.
  It scans every material under `Assets/polyperfect`, and for any on a built-in /
  `Standard` / `InternalErrorShader` shader swaps it in-place to URP/Lit,
  carrying base color + main texture + emission (`PolyperfectUrpFix.cs:40-74`).
  In-place edit (same GUIDs), so baked buildings render correctly **without** a
  re-bake of the scene — but a rebake is still required for the placement fixes below.

**The fix:**
1. **Re-run the URP material fixer** (CLI, editor op):
   `-executeMethod DeNelle.Editor.PolyperfectUrpFix.Fix`
   (or menu `Defenders/Art/Fix Polyperfect URP Materials`).
   Confirm the log reports `converted N built-in -> URP/Lit` with N > 0.
2. If the pack is **not present** on the machine at all (a fresh clone), the
   prefabs load as `null` and the builder substitutes magenta-free placeholder
   primitives — re-import the pack first, then run the fixer. Check the build log
   for `prefab not found at 'Assets/polyperfect/...'` warnings; if present, the
   pack itself is missing, not just its materials.

**Acceptance criteria:**
- `Defenders/Art/Fix Polyperfect URP Materials` log shows `converted N` with N > 0
  (or N = 0 with 0 built-in remaining, meaning already converted).
- No magenta on any polyperfect wall segment, tower, or building in the scene.
- The "A A" shapes and the purple blob near the Elarion tree are gone.

---

## Bug 2 — Red Barn embedded inside the middle wall (placement overlap)

**Symptom:** The red "barn" building is clipping through / embedded inside a wall.

**Root cause:** The "barn" is the **Farm** building. `VillageSceneBuilder.cs:1154`
places it at **`X = -15f, Z = 20f`** (primary `Farm_House.prefab`, with a
secondary `Windmill_Medieval.prefab` offset `+3 m X` -> world `(-12, 0, 20)`,
`VillageSceneBuilder.cs:1311`). The **inner KayKit gameplay wall ring**
(`BuildWallRing`, driven by `WallLayout`) has half-extent **`WallHalfZ = 21f`**
(`VillageSceneBuilder.cs:125`), so the north gameplay wall runs at **z ≈ 21**.
A building sitting at **z = 20** (normalized to ~7 m, so its footprint spans
≈ z = 16.5…23.5) **overlaps that z ≈ 21 wall line** — the Farm is literally
inside the middle (north) wall segment. (The outer polyperfect perimeter wall is
far out at z = 33, so this is the inner ring, not the perimeter.) DEF-101 moved
the Farm off the *north gate* (0,+25 -> -15,+20) but pushed it onto the wall line.

**The fix (VillageSceneBuilder.cs:1154-1160):** Pull the Farm inward so its
footprint clears the z ≈ 21 inner wall by the same 8 m margin the gate-clearance
guard uses. Change the Farm placement Z from `20f` to **`14f`** (interior side of
the wall, north-west quadrant, still clear of all gates):

```csharp
new BuildingPlacement { Type = 4, Id = "farm", Label = "Farm",
    X = -15f, Z = 14f, YawDeg = 270f, Fbx = "building_windmill",
    ...
```

`X = -15` already clears the west gameplay wall (`WallHalfX = 28`) and all
cardinal gates. Lowering Z to 14 moves the Farm + its +3 m Windmill fully inside
the ring. **Verify against the current wall version** — if WO-104's
`BuildCastleFortification` has already changed `WallHalfZ` or the inner ring, recompute
the clearance: target = (inner north wall z) − (Farm footprint half-depth ≈ 3.5 m) − 3 m margin.

**Acceptance criteria:**
- Farm (`Farm_House` + `Windmill_Medieval`) sits fully inside the wall ring with
  visible clearance — no mesh interpenetration with any wall segment.
- Farm still clears all four cardinal gates by ≥ 8 m (no
  `DEF-101 gate-clearance violation` error in the build log).

---

## Bug 3 — Blue prop clipping under the round spire/tower (placement)

**Symptom:** A blue prop ("wizard-hat / cauldron / blue thing") is sticking out
from under the round spire/tower.

**Root cause:** The round spire/tower is the **Arcane Tower** —
`Tower_Medieval_Big.prefab` (round, crenellated; catalog §1) at
**`X = -20f, Z = -10f`** (`VillageSceneBuilder.cs:1138`). The nearest blue prop
is the **Crystal Mine**, at **`X = -20f, Z = 10f`** (`VillageSceneBuilder.cs:1122`),
whose placeholder color is blue (`new Color(0.38f, 0.65f, 0.98f)`, line 1128) and
whose primary prefab is `House_Medieval_Small.prefab` with a secondary `Well.prefab`
at `+3 m X` -> `(-17, 0, 10)`. Two contributing causes:

1. **Placeholder blue.** If `House_Medieval_Small.prefab` fails to load (pack
   missing / not imported — see Bug 1), the builder falls back to a blue
   placeholder cube (`VillageSceneBuilder.cs:1224-1228`, color line 1128). That
   blue cube is the "blue thing." Fixing Bug 1 (re-import pack + run URP fixer)
   removes the placeholder entirely.
2. **Overlap geometry.** Crystal Mine (z=10) and Arcane Tower (z=−10) are 20 m
   apart center-to-center, but both normalize to ~7 m and the round tower is the
   tallest building (`Tower_Medieval_Big`). If the playtest shows the blue prop
   *under the tower base*, it is the placeholder cube reading through, not a true
   coordinate overlap — Bug 1's pack/material fix resolves it. If a real prop
   (e.g. the `Well` secondary) is clipping the tower foot after the pack loads,
   nudge the Crystal Mine secondary offset: change the `+3 m X` secondary offset
   at `VillageSceneBuilder.cs:1311` for this plot, or move Crystal Mine to
   `X = -22f, Z = 12f` for extra clearance from the Arcane Tower silhouette.

**The fix:**
1. Apply **Bug 1** first (re-import pack + run `PolyperfectUrpFix.Fix`). Re-test:
   if the blue prop disappears, it was the placeholder — done.
2. If a real prop still clips the tower base after the pack loads, move the
   Crystal Mine plot to **`X = -22f, Z = 12f`** (`VillageSceneBuilder.cs:1122`)
   to separate it from the Arcane Tower's round footprint.

**Acceptance criteria:**
- No blue placeholder cube anywhere in the scene (pack loaded + URP fixer ran).
- No prop interpenetrating the Arcane Tower (`Tower_Medieval_Big`) base.
- Crystal Mine still clears all gates by ≥ 8 m (no clearance-violation log error).

---

## Bug 4 — Gates are the wrong color

**Symptom:** The gates render the wrong color.

**Root cause:** Two distinct gate systems exist in the builder, and the likely
culprit is the same missing-material issue as Bug 1:

1. **Polyperfect perimeter gates** — `Gate_Medieval_Medium.prefab` (south) and
   `Gate_Medieval_Small.prefab` (east/west), placed in `BuildWallPerimeter`
   (`VillageSceneBuilder.cs:2682, 2690, 2698`). These carry **polyperfect atlas
   materials** that render **magenta in URP until `PolyperfectUrpFix.Fix` runs**.
   "Wrong color" on the perimeter gates = the same built-in-shader fallback as Bug 1.
2. **KayKit inner gates** — `wall_straight_gate.fbx` in `BuildGates`
   (`VillageSceneBuilder.cs:583`). These already get a `TripoMaterialFixer` with a
   **stone-grey fallback tint** `new Color(0.52f, 0.50f, 0.46f)`
   (`VillageSceneBuilder.cs:645-656`) to kill the known "purple frame on gate"
   magenta arch submesh. If the inner gates look wrong, the `TripoMaterialFixer`
   tint is the lever — but this was already addressed 2026-05-20.

**The fix:** The gate color is the **polyperfect URP material problem** — it is
fixed by **the same step as Bug 1**: re-run
`Defenders/Art/Fix Polyperfect URP Materials`
(`-executeMethod DeNelle.Editor.PolyperfectUrpFix.Fix`). No coordinate or prefab
change needed. Do **not** swap the gate prefabs — they are correct per catalog §1;
only their materials need URP conversion. (If, after the fixer, the KayKit inner
gates still read purple, adjust the `TripoMaterialFixer` fallback tint at
`VillageSceneBuilder.cs:655` — but verify post-fixer first.)

**Acceptance criteria:**
- All gates (`Gate_Medieval_Medium`, `Gate_Medieval_Small`, KayKit
  `wall_straight_gate`) render in correct stone/wood colors — no magenta.
- Confirmed in the player build, not just the editor (PIPELINE_STATE.md §8: UXML/
  material issues differ in builds).

---

## Implementation order (CLI)

1. **Material fix (no rebake yet):** run `PolyperfectUrpFix.Fix`
   (`Defenders/Art/Fix Polyperfect URP Materials`). Confirm magenta gone in editor.
   This alone fixes Bug 1 and Bug 4.
2. **Coordinate edits in `VillageSceneBuilder.cs`** (Bug 2 Farm Z 20 -> 14; Bug 3
   Crystal Mine only if a real prop clip survives step 1). Run the C# brace-balance
   gate (CLAUDE.md §1) on the file. **Coordinate with WO-104/107/109** — rebase onto
   the current wall version first.
3. **Rebake** (editor CLOSED), WO-103 pattern:
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 `
       -Method DeNelle.Editor.VillageSceneBuilder.BuildVillage `
       -LogName village-rebake-wo126.log
   ```
4. **Verify** the build log: 0 magenta, 0 placeholder primitives for polyperfect
   buildings, 0 `gate-clearance violation`, Farm clear of wall, gates correct color.

**Is a URP-material-fix + rebake needed?** YES to both. The material fixer
(`PolyperfectUrpFix.Fix`) resolves Bug 1 + Bug 4 with no rebake. The placement
edits (Bug 2, Bug 3) require a rebake to land in `Village.unity`.

---

## Do NOT touch

- **`Village.unity`** — never hand-edit; rebuild via the builder only (CLAUDE.md §3).
- Any `.cs` file other than `VillageSceneBuilder.cs` (and only the Building[]
  coordinate lines noted above). No new `System.Reflection` in bridge scripts.
- `PolyperfectUrpFix.cs` — the fixer is correct; only **run** it, do not edit it.
- Gate **prefabs** (`Gate_Medieval_*`) — correct per catalog §1; materials only.
- `BuildWallPerimeter` / wall coordinates if WO-104 is mid-flight — rebase, do not
  fork. VillageSceneBuilder.cs is single-touch (CLAUDE.md §9).
- Do NOT fire batchmode while the Unity editor is open (project lock).

---

## Files referenced

| File | Lines | Role |
|---|---|---|
| `Assets/Editor/PolyperfectUrpFix.cs` | 27-78 | URP material fixer (Bug 1, Bug 4) — run, don't edit |
| `Assets/Editor/VillageSceneBuilder.cs` | 1154-1160 | Farm placement (Bug 2) — Z 20 -> 14 |
| `Assets/Editor/VillageSceneBuilder.cs` | 1122-1128 | Crystal Mine placement + blue placeholder (Bug 3) |
| `Assets/Editor/VillageSceneBuilder.cs` | 1138-1143 | Arcane Tower (round spire) placement (Bug 3) |
| `Assets/Editor/VillageSceneBuilder.cs` | 125 | `WallHalfZ = 21` inner wall line (Bug 2 math) |
| `Assets/Editor/VillageSceneBuilder.cs` | 2621-2717 | `BuildWallPerimeter` — polyperfect gates/walls (Bug 1, Bug 4) |
| `Assets/Editor/VillageSceneBuilder.cs` | 645-656 | KayKit gate `TripoMaterialFixer` tint (Bug 4 fallback) |
| `docs/polyperfect-asset-catalog.md` | 28-71 | Wall/tower/gate prefab names |
