# WORK ORDER 1359 — Owner-authored action bar face emblems

**Status:** CLOSED 2026-09-04 - owner felt-test PASS (validated 2026-09-04T17:22:11, build 2026.09.04.354315). PRIOR STATUS: FIXED 2026-09-03 - ON HER DEVICE. Her five emblems (BUILD / TALK / HERO / JOURNEY / MANAGE) sliced from the sheet's own alpha and keyed BY NAME, so a reordered sheet cannot swap JOURNEY and MANAGE. She regenerated them without baked words herself, which closes the WO-1341 doubling risk at the source. Her export arrived 24bpp with the transparency flattened to white and was repaired by a border-inward flood fill (never a global threshold) - verified zero holes across 3409 interior samples per emblem. `PresentAuthoredEmblem` makes the kit step back so her medallion is not double-ringed, clipped or squashed - but only when authored art answered; a miss falls back to the old look, never a bare button. Adopting a new sheet is drop-the-png plus one menu item, zero code. Gates COMPILE_GATE_OK + REGRESSION_OK 358/358. AWAITING HER FELT-VERIFY on the bar; then Owner Validation closes it.
**Date:** 2026-09-03
**Silo:** HUD / UI kit (calm dock) + Core icon resolution
**Lane:** UI presentation — file-disjoint from the live SME lanes (deck workspace, posture,
raid capability, world clock, damage states, store, board/ship tooling)

---

## What the owner gave us

> *"icons for the hud"*

Finished art for the five calm-dock faces, delivered as one sheet:

```
Assets/Resources/UI/ElarionMedieval/actionbar/actionbar-emblems.png   1983 x 793 RGBA
```

Five circular emblems in a single row, left to right: **BUILD** (hammer + wrench), **TALK**
(speech bubble over two figures), **HERO** (caped swordsman before a blue portal), **JOURNEY**
(compass + treasure map), **MANAGE** (helm on a shield). Each carries its own gold ring and four
diamond points.

**No baked words.** Her first sheet (`actionbar-icons-sheet-2026-09-03.png`) had an engraved name
plate under every emblem; she re-generated it without them specifically so the live text labels
stand. That sheet is deleted from the project.

---

## The two defects this had to be built against

1. **A baked word under a live label.** WO-1341, same day: the Hero deck cards mounted PNGs with
   the title painted in, and the device printed every label twice, in two fonts, with two
   wordings. The dock draws its caption with live TMP text, which localises, fits and is already
   styled to the kit — art must never become a second producer of that word.
2. **A face wearing another face's emblem.** The FIRST sheet already disagreed with the bar about
   order (it read BUILD/TALK/HERO/MANAGE with JOURNEY beneath; the bar draws
   BUILD/TALK/HERO/JOURNEY/MANAGE). A position-indexed slice transposes MANAGE and JOURNEY — and
   both faces still look plausible, so it ships.

---

## What was built

### Slicing — one sheet, name-keyed slices, rects derived from its own alpha
- `Assets/Editor/ActionBarEmblemSlicer.cs` — menu item **Elarion/UI/Re-slice Action Bar Emblems**.
  Decodes the PNG bytes directly (no readable-texture toggle, so the shipped import settings are
  never disturbed), segments the alpha into islands left to right, gives every island the SAME box
  (largest island + 4 px pad, centred on its own bounds) so all five render at one scale, and writes
  **normalized 0..1** rects into a manifest. Islands found: 5, at x 45-419 / 430-801 / 809-1178 /
  1184-1561 / 1568-1940, all y 190-594; shared box **386 x 411**.
- `Assets/Resources/UI/ElarionMedieval/actionbar/actionbar-emblems.json` — the derived manifest.
- **Adopting a new sheet:** drop the .png, run the menu item. Zero code edits. If a future sheet
  re-orders the emblems, the single edit is `ActionBarEmblemSlicer.FaceOrder`.
- **Why normalized rects:** they survive a `maxTextureSize` downscale or a re-export at another
  resolution untouched. A pixel rect cannot.
- **Why a sheet, not Unity multi-sprite:** Unity's slicing binds regions to the importer and to
  their grid POSITION. This binds them to a NAME.

### Runtime
- `Assets/_Modules/Core/UI/SpriteSheetSlices.cs` (new) — resolves `"<sheet Resources path>#<face
  name>"` to a `Sprite.Create` cut from the sheet using the manifest. `Sprite.Create` samples on the
  GPU, so the sheet stays compressed and non-readable. Every miss (no sheet, no manifest, no such
  face, malformed rect) returns null and is `FlowTrace`d; caches hits AND misses.
- `ConceptIconResolver` — `IconRef` gains an optional `path`, tried BEFORE `role`/`name`. Also
  `ResolveAuthored(conceptId)`, which returns a sprite ONLY when it came from `path`, so a caller
  can tell owner art from pack art. A path-only row short-circuits before the catalog
  (`RpgUiCatalog.Get(null, null)` reaches `Dictionary.TryGetValue(null)` and throws).
- `UiStyle.AuthoredIcon(conceptId)` — the caller-facing half of the above.
- `concept-icons.json` (Resources + StreamingAssets) — five rows keyed `build`/`talk`/`hero`/
  `journey`/`manage`, each `path` = `UI/ElarionMedieval/actionbar/actionbar-emblems#<key>`, each
  keeping its old `role`/`name` pack address as the fallback.
- `HudKitController.BuildPeacefulDockSlot` — now takes the pack fallback ids and derives the icon
  key from the caption itself (`caption.ToLowerInvariant()`). **The caption IS the key**, so a
  slot cannot be handed another face's art. A null icon is traced and the medallion keeps its kit
  look; a missing icon is never a missing button.
- `ElarionUiKit.PresentAuthoredEmblem(slot)` (new) — her emblems ARE medallions. The kit's round
  treatment would draw a second ring around hers, clip her four diamond points at the stencil, and
  stretch 386x411 into a square. When authored art answers, the kit steps back: its face and bezel
  stop drawing, the stencil stops clipping and opens to full bounds, `preserveAspect` on, no tint.
  Nothing renamed, re-parented or destroyed — enabled/disabled and re-anchored only.
  ⛔ `slot.frame` is deliberately NOT disabled: it is the Button's `targetGraphic`, and hiding it
  would cost the face its raycast. It is already alpha 0 and stays enabled.

### Import settings
`ElarionMedievalUiImporter` — `/actionbar/` gets `maxTextureSize 2048`; everything else keeps 4096.
2048 is the first power of two that does not resample a 1983-wide sheet. A face medallion is ~240
reference px (~300 device px on the owner's 2670-wide screen), so a 386 px source emblem is the
right side of sharp and a 1024 cap would halve it and soften the most-tapped art in the game.
Inherited from the existing kit contract: **Sprite / Single, alphaIsTransparency on, mipmaps OFF,
npotScale None, CompressedHQ, no 9-slice border** (the folder is not in `BorderFor`).

---

## Constraints honoured

- **Colourblind-safe.** Greyscale check done on the sliced faces: all five read distinctly by
  SHAPE — crossed hammer/wrench, two figures + bubble, standing figure in a ring, scroll +
  compass, helm on shield. Hue carries nothing; the live word carries the rest.
- **Touch targets.** `ClampMinTouch` untouched; nothing shrinks a target to fit art. Pinned by 8a.
- **No rect renamed or re-parented** — FTUE highlights (`hud.build_button`, `hud.hero_button`)
  resolve by name and are untouched.
- **`ButtonCount` 7, `MaxVisibleFaces` unchanged, no `ActionBarButtonId` renumbered.**
  ⚠ Note for canon: `MaxVisibleFaces` is **4** in `HudActionBarModel.cs:121` and
  `HudLabelFitRegression` Case 0 fails if it is not — CLAUDE.md §7 says 6. Not touched here; flagged.
- **Her art is untouched** — sliced and mounted, never recoloured, restyled or effected.
- ASCII-only strings; no `.unity` scene edited; no UXML.

---

## The oracle — `HudLabelFitRegression` Case 8 `[bar-face-icons]`

Extended the suite that already owns the WO-1341 baked-word precedent (registered; marker
`HUD_LABEL_FIT_OK`). Source/data lints, because `DeNelle.EditorRegression` cannot reference
`DeNelle.HUD`:

- **8a** the icon key is derived from the caption; the slot resolves its own art; authored emblems
  go through `PresentAuthoredEmblem`; the touch-floor clamp survives.
- **8b** five faces, right captions, right order, and no icon handed in at the call site.
- **8c** `slot.SetCaption(caption)` still runs — the word stays LIVE text.
- **8d** the sheet and its manifest exist; every face has a row in BOTH copies of
  `concept-icons.json`; the two copies agree; each row's `path` is `<sheet>#<its own key>`.
- **8e** no face points at art on the baked-word denylist.
- **8f** the named slice exists in the manifest and its rect is normalized inside the texture.
- **8g** no denylisted word-bearing art is referenced by the HUD or the table.

### RED proof (offline mutation)
The lints were re-implemented verbatim in Python and run against the live tree and five mutations
(`scratchpad/oracle_mutation.py`). Live tree **GREEN**; every mutation **RED**:

| mutation | fired |
|---|---|
| JOURNEY and MANAGE paths swapped | 8d x4 (`row 'journey' points at ...#manage`, + copy disagreement) |
| BUILD re-pointed at the baked-word sheet | 8e `face BUILD points at baked-word art` |
| `UiStyle.Icon("journey")` hand-paired at slot 4 | 8b `slot 4 (MANAGE) is handed an icon at the call site` |
| manifest face `hero` renamed | 8f `manifest has no face 'hero'` |
| sheet deleted | 8d `the emblem sheet is gone` |

⚠ Not yet run under Unity — this seat is edit-only. The lead's gate run is the first in-engine
execution.

---

## Files touched

| file | change |
|---|---|
| `Assets/_Modules/Core/UI/SpriteSheetSlices.cs` | NEW — name-keyed sheet slicing |
| `Assets/Editor/ActionBarEmblemSlicer.cs` | NEW — alpha-derived manifest generator |
| `Assets/Resources/UI/ElarionMedieval/actionbar/actionbar-emblems.json` | NEW — derived manifest |
| `Assets/_Modules/Core/UI/ConceptIconResolver.cs` | `path` field, `ResolveAuthored`, authored cache |
| `Assets/_Modules/Core/UI/UiStyle.cs` | `AuthoredIcon` |
| `Assets/_Modules/Core/UI/ElarionUiKit.cs` | `PresentAuthoredEmblem` + `HideChildGraphic` |
| `Assets/_Modules/HUD/Kit/HudKitController.cs` | caption-keyed dock icons; authored presentation |
| `Assets/Editor/ElarionMedievalUiImporter.cs` | `/actionbar/` max size 2048 |
| `Assets/Editor/Regression/HudLabelFitRegression.cs` | Case 8 `[bar-face-icons]` |
| `Assets/Resources/Data/Canonical/concept-icons.json` | five face rows |
| `Assets/StreamingAssets/Data/Canonical/concept-icons.json` | same |

Brace/NUL check: BALANCED + clean on all eight `.cs`.

---

## What the owner hands over next time

**Easiest for us: one sheet, emblems in a single row, transparent background, no words.** Name it
`actionbar-emblems.png`, drop it in `Assets/Resources/UI/ElarionMedieval/actionbar/`, and the lead
runs **Elarion/UI/Re-slice Action Bar Emblems**. Emblems must not touch each other — the slicer
finds them by alpha gaps and refuses (loudly) if it does not find exactly five islands. If the
left-to-right order changes, one line moves: `ActionBarEmblemSlicer.FaceOrder`.

Export note: her exporter flattened the alpha to near-white on both deliveries and it had to be
repaired by hand. **Exporting with real transparency preserved would remove that step entirely.**
