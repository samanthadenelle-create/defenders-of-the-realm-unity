# WORK ORDER 577 — In-Game Seating / Offset Editor (Offset Forge slice 2)

**Status:** FIXED 2026-06-28 (`a09877248`) — awaiting owner felt-verify. *(Status audit 2026-08-24: BUCKET CORRECTION — the prior line predated the commit and still advertised gates/commit as owed; verified at source in `git log`, `a09877248` (2026-06-28) landed this work. Body unchanged. Prior line: IMPLEMENTED (this branch worktree) — pending CLI batch-gate + commit)*
**Date:** 2026-06-28
**Lane:** Combat/AI + UI (Hero gear seating) — file-disjoint from world/scene lanes
**Supersedes nothing.** Extends WO-490 (AttachmentOffsetRegistry) and the editor-only Offset Forge.

---

## 1. Owner ask

> "Implement [the offset tool] in-game like it is in the build menu."

There is an editor-only `Tools > Offset Forge` window. The owner wants the SAME capability
IN THE RUNNING GAME — a live, on-screen seating editor (felt parity = the Build Menu's
**Orient** live-adjust) to dial in weapon/shield attachment offsets BY EYE on the actual
equipped hero, and PERSIST them so item attachments are always correct. Motivation: the owner
F8'd "this is how the weapon looks" — current seating is off and they want to fix it live.

---

## 2. RCA — parity target + the offset apply path (file:line)

### 2a. Build-menu "Orient" UX (the parity blueprint)
- `BuildPaletteUI` raises `OnOrientRequested`; `BuildModeController.OpenOrientEditorForArmed`
  (`BuildModeController.cs:1757`) resolves the armed entry's prefab and calls
  `TowerPlacementRotateMenu.OpenDevOrient(id, prefab, name)` (`:1783`).
- **`TowerPlacementRotateMenu`** (`Assets/_Modules/Village/UI/TowerPlacementRotateMenu.cs`) is a
  **code-built UIToolkit** modal (NO UXML) that **adopts a scene UIDocument's PanelSettings**
  to render in builds (`AdoptPanelSettings` `:1002`). It exposes X/Y/Z euler sliders + numeric
  fields + per-axis scale + reset + a live `TowerPreviewCamera` RenderTexture (`BuildAxisRow`
  `:384`, `OnConfirmClicked` `:848`). Dev-orient mode applies to the CatalogEntry live + logs an
  `[OrientRecipe]` (`ApplyOrientToCatalog` `:884`).
- **Key difference for seating:** that editor previews a SEPARATE prefab in a RenderTexture. The
  seating editor must drive the **LIVE equipped weapon on the real hero** instead. The new
  overlay mirrors the *interaction model* (bottom-bar Orient/Done, sliders + steppers, code-built
  UIToolkit, PanelSettings adoption, black+gold theme) but targets the live `EquipmentController`.

### 2b. Runtime seating + offset apply path
- `EquipmentController.AttachLoadedProp` (`EquipmentController.cs:681`) seats the main weapon:
  - **Geometry-first**: `NormalizeInto` (`:1693`) orients **longest axis → +Y** (= the owner's
    "100% vertical" baseline, **confirmed**), narrowest → +X, bounds-centre at origin, scaled to
    `heldLength`. For melee, `SeatByHandle` (`:939`) re-seats the inferred handle to origin.
  - The **grip root** (`_gripRoot`) is parented under the hand bone; its `localRotation` =
    `ComputeMeleeGripRotation` (`:1431`, rig-hand-axis basis) for melee.
  - The **Offset Forge offset composes as a NUDGE on top** (`:804`): `localPosition = gripPos+pos`,
    `_baseGripRot = _baseGripRot * Euler(rot)`, `localScale *= scale`. Key = mesh name (`sword_A`)
    then weapon id. An all-zero entry == pure geometry.
  - `fullOverride` (`:718`) was an opt-in raw-pivot (`SeatNative`) bypass — **0 entries used it.**
- `AttachmentOffsetRegistry` (`AttachmentOffsetRegistry.cs`) loads `Assets/OffsetForge/offsets.json`
  (`{id, rot, pos, scale, fullOverride}`) once, cached; `TryGetOffset(key)` (`:153`).
- Off-hand (`AttachOffHandProp` `:1298`) did **not** read the registry — shield offset was baked
  into the `Shield()` preset (`:157`). (Now extended — see §3.)
- Dev-tools host = **`AdminOverlay`** (`Assets/_Modules/HUD/AdminOverlay.cs`) — the live
  "Settings > Dev Tools" panel the owner uses (NOT the deprecated F10). It opens cross-assembly
  tools by reflection (`OpenOrientMenu` `:332`, `OnVfxParade` `:473`).

---

## 3. Design — the in-game seating editor

### Access
- New button **"Seating Editor (gear)"** on `AdminOverlay` → `OnSeatingEditor()` reflection-launches
  `DeNelle.Village.UI.SeatingEditorOverlay.Launch()` (HUD can't reference Village; same idiom as
  the orient menu). DEV-gated by the existing AdminOverlay reachability (owner dev tools only).

### Controls (build-menu Orient parity)
- Side panel (code-built UIToolkit, black+gold Obsidian chrome, PanelSettings adopted). Transparent
  full-screen root with `pickingMode=Ignore` so the **hero stays visible + camera works behind it**.
- Target toggle: **Main Weapon / Off-hand**.
- **Rotation X/Y/Z**, **Position X/Y/Z**, **uniform Scale** — each with `−−/−` + slider + value +
  `+/++` steppers (rotation ±1/±15°, position ±0.005/±0.02 m, scale ±0.05/±0.25×).
- Mode toggle **VERTICAL+delta** (default, owner workflow) vs **NUDGE on geometry** (legacy WO-551).
  Off-hand is **vertical-locked** (a nudge wouldn't reproduce — its grip is a baked preset).
- Buttons: **Reset to Vertical**, **Re-equip (verify)**, **Export JSON**, **Clear offset**,
  **Save Offset** (gold CTA) + **Done** (the build-bar Save/Done pairing).

### Live preview (what-you-see-is-what-you-save)
- `EquipmentController` new API drives the **live grip root**, mirroring the attach math:
  `BeginSeatingEdit`, `ApplySeatingPreview`, `SaveSeating`, `ReapplySeatingFromRegistry`,
  `EndSeatingEdit` (+ `HasSeatingTarget`, `SeatingEditInfo`). Auto idle/combat hold is suspended
  while editing so the previewed pose isn't stomped (`Update()` guard).
- **Owner conventions baked in:**
  - **100% vertical baseline** = `NormalizeInto` longest→+Y (confirmed).
  - **Hilt on the LOWER HALF, blade up** → new `SeatHiltLowerHalf` (`EquipmentController.cs`):
    grip = lower-half by rule (~18% up from the bottom), width-spike only refines the grip Y within
    the lower half, **never flips**. Used by the vertical-authoring / `fullOverride` path + the
    editor preview. The default WO-551 `SeatByHandle` path is untouched (see §5 flag).
  - Saved offset = the **delta from vertical** (rotation/position/scale).

### Persistence + how saved offsets reach runtime
- `AttachmentOffsetRegistry.SaveOffset(id, pos, euler, scale, fullOverride, out devPath, out snippet)`:
  - ALWAYS writes a **writable dev file** `Application.persistentDataPath/offsets-dev.json` (a built
    player can't write `Assets/`).
  - In the **Editor** also writes the repo `Assets/OffsetForge/offsets.json` directly.
  - `Reload()`s the cache; logs a **copy-pasteable JSON snippet** so the owner can bake a build's
    edit back into the repo `offsets.json`.
- Registry **read now merges**: base (`Assets/OffsetForge/offsets.json` → Resources fallback) +
  **dev overlay** (`offsets-dev.json`, **wins per id**). So a saved offset applies immediately
  (next equip / `Re-equip` button) AND survives a reload + the next launch of the same build.
- `fullOverride` redefined to **geometry-VERTICAL + delta** (NormalizeInto + hilt-lower-half +
  authored rot/pos/scale, bypassing the rig-aware grip) — the in-game editor authors this. It
  replaces the old raw-pivot `SeatNative` meaning, which the WO-551 notes had flagged as the
  backwards approach that "never reproduced in-game". **No entry used `fullOverride` → no regression.**

---

## 4. Files changed / added (for reconcile — explicit paths)

**Modified**
- `Assets/_Modules/Village/Hero/EquipmentController.cs` — editor-capture fields; `fullOverride` →
  geometry-vertical + hilt-lower-half (main + off-hand); `SeatHiltLowerHalf`; public seating-editor
  API; `Update()` hold-suspend guard. *(Default `fullOverride=false` paths byte-for-byte unchanged.)*
- `Assets/_Modules/Village/Hero/AttachmentOffsetRegistry.cs` — dev-overlay merge read;
  `SaveOffset`/`RemoveOffset`/`DevPath` writers; `BuildSnippet`.
- `Assets/OffsetForge/Runtime/OffsetTable.cs` — additive `OffsetEntry.fullOverride` (+ Upsert copy).
- `Assets/_Modules/HUD/AdminOverlay.cs` — "Seating Editor (gear)" button + `OnSeatingEditor()`.

**Added**
- `Assets/_Modules/Village/UI/SeatingEditorOverlay.cs` (+ `.cs.meta`, guid
  `64dc64354a944de0b199bbb27c846fca`).

---

## 5. Validation + owner-decision flags

- Brace balance: all 5 `.cs` files **OK** (337/337, 77/77, 23/23, 85/85, 121/121). No NUL bytes.
  `offsets.json` valid JSON. (CLI to run the full CompileGate before commit.)
- Regression guard: every behavioural change is gated behind `fo.fullOverride`, which **no current
  `offsets.json` entry sets** → WO-551 seating, WO-567 equip-visual, and the build menu are unaffected.

**Owner-decision flags:**
1. **Persistence path** — saves go to `persistentDataPath/offsets-dev.json` (writable in builds) +
   the repo file in the Editor + a logged JSON snippet to bake back. Confirm this is the desired
   build→repo flow (vs. e.g. auto-PR).
2. **Default seat unchanged** — the hilt-lower-half rule is applied only to the new vertical/
   `fullOverride` path, NOT the default `SeatByHandle`. Rolling the lower-half rule into the DEFAULT
   seat would re-pose every currently-seated weapon (a felt change) — proposed as a follow-up the
   owner approves after felt-testing, per "don't regress / what is right not easy".
3. **Hold pose** — the editor previews the clean READY (base) pose; in-game idle still adds the
   existing lowered tilt (unchanged hold behavior). Authoring matches the ready pose (as shield_A was).
4. **Drag** — steppers + sliders implemented; pointer-drag-in-viewport deferred (optional).
5. **Off-hand** — vertical-only (runtime honors only `fullOverride` for the shield to avoid
   double-rotating the baked preset). Confirm acceptable.

---

## 6. Verify (CLI, headless or felt)
- Dev Tools → "Seating Editor (gear)" with a hero equipped → panel renders (right side), hero visible.
- Dial Rot/Pos/Scale → weapon updates live. "Reset to Vertical" → stands straight up, hilt low.
- "Save Offset" → `offsets-dev.json` written + `[Seating] SAVE ...` JSON snippet in Console.
- "Re-equip (verify)" → re-attaches from the saved file and matches the preview (proves persistence).
