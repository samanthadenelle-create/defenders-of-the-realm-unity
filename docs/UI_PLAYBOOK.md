# UI PLAYBOOK — how to build UI in this repo

**Audience:** any seat about to add or change a screen, panel, HUD element or overlay.
**Read time:** five minutes. **Status:** practice doc, sourced from the tree 2026-08-09.

This repo has a house style that was paid for in defects. Until now it lived in code comments,
`WorkOrders/*.md` and RCA history, so every new seat rediscovered it by shipping the same bugs.
This file is that knowledge, checkable.

Two things to internalise before the list:

- **The kit is the design system.** `ElarionUiKit` (+ its partials) is the single visual language.
  You assemble screens by calling its builders, you do not re-author chrome per surface.
- **Compile-green proves nothing about a screen.** Nine defects in one recent ticket
  (`WorkOrders/WORK_ORDER_1010_build_ui_carousel_minimize.RESULT.md` §3) were found *only* by
  opening the PNGs. 132 regression suites were green the whole time. Practice 13 is the one that
  catches the rest.

Every practice below states the failure it prevents and points at code that demonstrates it.
`file:line` references were read at source; if one drifts, fix the pointer.

---

## 1. Code-built uGUI on the kit. ZERO UXML.

Build every surface in C# with `Canvas` / `Image` / `Button` / `ScrollRect` / `TextMeshProUGUI`,
via `ElarionUiKit`. Never add a `UIDocument`, never author a `.uxml`.

**Why:** UXML / UI Toolkit HUDs come up **empty in player builds**. Learned the hard way; it is
canon in `CLAUDE.md` §8 and restated in the kit's own header.

**Pointers**
- `Assets/_Modules/Core/UI/ElarionUiKit.cs:20-24` — the rule, plus the WebGL-safe rounded-sprite
  fallback that means a kit surface can never blank.
- `Assets/Editor/Regression/HudUiRegression.cs:23-27`, `:97-98`, `:197-199` (CHECK 2) — the source
  fence that fails any runtime `.cs` outside the baseline which constructs or declares a
  `UIDocument`.
- ⚠ **The fence only scans `.cs`.** `docs/reference/AUDIT_2026-08-09.md` F3/F4 found **enabled
  `UIDocument`s still shipping inside scenes** (`Dungeon_HealersCottage.unity`, `ATBBattle.unity`).
  The rule is right; the tree is not yet clean. Do not add to the pile, and do not "fix" one by
  hand-editing scene YAML (`CLAUDE.md` §3 — resave corruption).

---

## 2. Assemble from kit builders — don't re-author chrome.

Panels get `ElarionUiKit.BuildObsidianPanel` (near-black fill + gold trim + header + the ONE shared
Close). Buttons get `BuildObsidianButton`. Bars, tabs, wallet strips, scroll wells, toasts, sliders,
dropdowns, nameplates all already exist.

**Why:** before the kit, `ArenaPanel`, `HeroInventoryController`, `HeroEquipHud` and the HUD each
grew a near-identical private copy of the same recipe — so every fix had to be made four times, and
usually wasn't.

**Pointers**
- `Assets/_Modules/Core/UI/ElarionUiKit.cs:1-31` — what the kit consolidates and why it lives in
  `DeNelle.Core.UI` (the one assembly both `DeNelle.HUD` and `DeNelle.Village` reference, so a shared
  kit exists without a forbidden HUD↔Village edge, `CLAUDE.md` §5).
- `Assets/_Modules/Core/UI/ElarionUiKit.cs:174-217` — `BuildObsidianPanel` / `PanelChrome`; parent
  your content under `chrome.content` or a `FrameLayout` drop-zone.
- `Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs` — `BuildObsidianButton`, `BuildObsidianBar`,
  `MakeScrollZone`, `BuildTab`, `CurrencyChip`, `BuildToggle`, `BuildDropdown`, `BuildSlider`.
- `Assets/_Modules/Core/UI/ElarionUiKitConformance.cs:1-30` — the "fix at the FACTORY" list
  (`BuildTabRow`, `BuildWalletRow`, rarity slots, `ShowToast`, `SpacedDisplayName`). If your problem
  is on that list, it is already solved kit-side.

---

## 3. Fixed-pixel bands, not fraction-of-screen.

Position with fraction anchors if you like, then **stamp a fixed pixel size**. Slice a column into
bands of explicit pixel heights — never `1f/n` fraction slices.

**Why:** a wide landscape canvas stretches fraction-of-width anchors into **long thin bars**
(owner felt-test 2026-07-15, verbatim: *"long thin rectangles in horizontal mode"*). And a fraction
band that resolves under the touch floor gets grown by `ClampMinTouch` **past its own slice**, so it
overlaps its neighbour — a two-defect chain from one fraction.

**Pointers**
- `ElarionUiKit.CanonCtaWidth = 360f` / `CanonCtaHeight = 132f`
  (`Assets/_Modules/Core/UI/ElarionUiKit.cs:309-312`) — the canonical Continue/Close box, and the
  height every other control should source rather than inventing a new floor.
- `ElarionUiKit.PinCanonicalCtaSize(Button)` (`:900-911`) — collapses stretch anchors to the anchor
  rect's centre and stamps the canonical box. `SeatSharedCloseInside` (`:925-936`) does the same but
  grows **upward** from the band's lower edge so a fixed box never sinks through an ornate border.
- The local variant: a private `PinSize(button, w, h)` that collapses a kit button's
  fraction-of-parent anchors to a POINT at the anchor rect's centre and stamps a fixed pixel box —
  `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:1103-1115` and
  `Assets/_Modules/Village/BuildMode/BuildTabRow.cs:115-127` (called at `BuildTabRow.cs:84`).
- ⚠ **`PinSize` no longer exists in `BuildHudController.cs`** (WO-1010 D10/D14, 2026-08-09). It went
  with the last kit word-button in that file — the pinned 200x132 `X Done` — which D10 replaced with a
  hand-built corner plate. The `IntentBtnW` / `PlaceBtnW` / `ExitBtnW` named widths were deleted in the
  same pass. That file now authors **every** rect at fixed reference px directly, which is this rule one
  step earlier: the named sizes are `BuildHudController.cs:73-162` (chip/pill, rail band, corner Done,
  strip, first-run hint), and the note recording the deletion is at `:996-1001`.
- Fixed pixel inset, not a fraction, so it cannot scale away on a short canvas:
  `BuildPaletteUI.cs:135` (`TrayBottomInsetPx = 28f`), applied at `:350` — and the dock itself now
  seats at `BuildHudController.ResourceStripReservedPx` (WO-1010 D19, `BuildPaletteUI.cs` dock
  build), consuming the published reserved band instead of anchoring flush to 0.
- The gates: `Assets/Editor/Regression/SkillsPanelLayoutRegression.cs:508-528` and
  `EchoCardLayoutRegression.cs:193-196` fail the re-introduction of a `1/n` fraction slice.

---

## 4. Touch floor is 112 reference px — reached with an INVISIBLE HIT PAD.

`ElarionUiKit.MinTouchPx = 112f` (`Assets/_Modules/Core/UI/ElarionUiKit.cs:314-318`) — the shortest
resolved side of any tappable thing, in 1080×1920 reference px (~50 dp on the Seeker; ≈7.1 mm at
400 dpi, which is the Apple 44 pt / Google 48 dp band).

Three ways to satisfy it, in order of preference:

1. **Author it at/above the floor.** Best. Nothing to fight.
2. **Invisible hit pad** when the visual must stay small, or when an external layout pass owns the
   visual's size. A transparent, raycast-target `Image` at `MinTouchPx`, with the small visible art
   inside it. uGUI bubbles the pointer up to the parent `Button`.
3. **`ElarionUiKit.ClampMinTouch(Button)`** (`:948-996`) — the post-layout safety net. It grows the
   rect **symmetrically about its centre**. Treat this as a backstop, not a plan: that growth is
   itself a documented defect source (a sub-floor control grows past its band and overlaps).

**A small chip in a large hit box — never a slab.** Growing the *visual* to 112 puts opaque
rectangles all over the play field and destroys the thing you were trying to build.

**Pointers**
- The pattern, stated: `BuildHudController.cs:78-84` — `ChipVisualPx = 52f`,
  `ChipHitPx = ElarionUiKit.MinTouchPx`. The implementation is `MakeVerb` at `:592-674`: the
  transparent parent `Image` is the raycast target at `ChipHitPx` (`:605-610`) and the visible disc is
  a child at `visualPx` (`:621-643`). Its doc comment names the failure: *"Growing the visual instead
  would put slabs down the right edge and undo the point of the redesign."*
- Same helper, same pad, smaller art: the D10 corner **Done** is a 76 px visual inside the full 112 px
  hit box — `BuildHudController.cs:313-346` (`DoneVisualPx = 76f` at `:124`).
- The kit version: `ElarionUiKit.EnsureTouchFloorArea(slot)` (`:3397-3429`) — a `TouchFloor` child
  at fixed `MinTouchPx`, `SetAsFirstSibling` so it never draws over the icon. Its comment carries the
  measurement (arc medallions resolve to **93.7 px**, 18.3 under floor) *and* the reason the rect
  itself is not grown: the owning layout rewrites `sizeDelta` every re-layout, so growth is wiped.
- **That wipe is a live bug class.** `docs/reference/AUDIT_2026-08-09.md` F15: action-bar faces are
  **78.66 px** because `ApplyActionBar` re-zeroes `offsetMin/offsetMax` every repack, erasing the
  `ClampMinTouch` guard. If another pass owns your rect, the pad is the *only* option.
- `img.sprite` must be non-null for uGUI to raycast the full rect — `ElarionUiKit.cs:3426` uses
  `SolidSprite` for exactly this.

---

## 5. Meaning is NEVER carried by colour alone.

The project owner is **red/green colourblind**. Every state, validity and affordability signal must
carry a **WORD** or a **SHAPE**. Colour is allowed only as a redundant second cue.

**Why:** the canonical failure — an unaffordable build card differed from an affordable one by
`ElarionUi.Danger` vs `ElarionUi.Affordable` and **nothing else**. The cost string was byte-identical.
The owner could not tell them apart. The file's own comment two lines above already cited the rule
for the freebie case; the unaffordable branch just never got it.

**Pointers**
- `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:889-897` — now reads `NEED 80W 30I`.
  *"'NEED' leads so the state is read before the numbers; colour stays a redundant second cue."*
- `BuildPaletteUI.cs:866-879` — a placed singleton shows a **`Built` chip**: word plus a rounded
  shape plate, not a grey-out.
- `BuildPaletteUI.cs:909-931` — tower targeting reads `Land` / `Air` / `Land+Air` as text.
- `BuildTabRow.cs:86-99` — the active category tab carries a **gilt underline**: position and
  shape, never colour alone (the palette just routes state to it, `BuildPaletteUI.cs:1082-1087`).
- A blocked placement **says why in words**: `BuildHudController.cs:953-987`. Note the split — the
  chip carries only the compact state (the ASCII fallback flips `OK`/`No`; the D17 check-mark sprite
  dims + disables, a brightness change, not a hue), while the 620 px pill carries the sentence
  (`Arcane Spire - Not enough Wood`). Putting the sentence on the chip wrapped it to four lines and
  covered its neighbours.
- Other live examples: `ArenaPanel.cs:309` (`NEED MORE SKR`), `HubRepairAffordance.cs:247`.
- ⚠ Gate coverage is thin here. `docs/reference/AUDIT_2026-08-09.md` lists *"Colour, contrast,
  legibility"* as an explicit **non-goal** of the regression suite. This one is on you and on the
  capture.

---

## 6. Anything over the 3D field must carry its OWN edge.

`ElarionUiKit.ObsidianFill = (0.02, 0.02, 0.025, 0.98)` — effectively **black**
(`Assets/_Modules/Core/UI/ElarionUiKit.cs:187`). A black plate on a dark terrain is invisible; on
pale sand it is fine. You do not get to choose what is behind it.

**Rule:** a floating element gets an accent **edge** (or a dark backing plate under text drawn over
art). It may not borrow contrast from the world.

**Why:** the first build of the ghost chips used a plain `ObsidianFill` circle. The capture showed
black-on-black with only bare labels floating over the field — *worse than the word-buttons they
replaced*.

**Pointers**
- `BuildHudController.cs:612-643` — the accent-edge-around-near-black-fill recipe, with the RCA in
  the comment. Note the bonus: the edge gives each verb a second non-textual identity
  (gold confirm / parchment rotate / red cancel) *without* meaning ever resting on colour, because
  the label already says which is which. It still applies **inside** the D14 rail: the band is the
  same near-black, so a plate without its edge would be black-on-black again.
- `BuildHudController.cs:373-396` — same treatment on the name/cost pill, and `:261-285` on the D19
  bottom resource strip (gold `Gilt` edge, `ObsidianFill` inset by `ChipEdgePx`); the WO-1010 P3
  first-run hint line reuses the same recipe.
- Text over art gets its own dark backing: `BuildPaletteUI.cs:925-928` (`alpha 0.62` plate under the
  targeting caption).
- World-space readouts do it too: `GhostPreview.cs:326-330` — *"Dark pill so the text reads over any
  terrain colour."*

---

## 7. Screen-space, not world-space billboards.

For UI anchored to a world object, use a **screen-space overlay canvas** and project the world point
(`Camera.WorldToScreenPoint` → `RectTransformUtility.ScreenPointToLocalPointInRectangle`).

**Why:** a world-space billboard **shrinks with camera zoom**, so it drops under the 112 px touch
floor exactly when the player needs it — placing a small wall piece while zoomed out.

**Pointers**
- The ruling: `WorkOrders/WORK_ORDER_1010_build_ui_carousel_minimize.md:95-96` — *"screen-space UI
  anchored to the ghost's projected position, clamped to the safe area — never world-space billboards
  that can shrink with zoom."*
- The implementation: `BuildHudController.cs:351-360` (the doc comment restates the rule, and now
  states the split — after WO-1010 D14 the **pill is the only thing that follows**; the verb rail is
  fixed at the right edge and follows nothing), `:225-238` (screen-space overlay, 1920×1080 landscape
  reference, match 0.5), `:917-921` (the projection — and note the trap: **overlay canvases take a
  NULL camera** in `ScreenPointToLocalPointInRectangle`; passing one silently offsets everything).
- Historical precedent: `Assets/_Modules/Village/Waves/WaveCountdownUI.cs:1-8` replaced a
  world-space gate number with a screen-space singleton.
- World-space canvases remain correct for **non-interactive readouts** attached to a specific object
  — `UnderConstructionVisual.cs:240` (*"NO GraphicRaycaster: it is a readout"*), `FloatingHealthBar`,
  `ThreatSkullPlate`. The rule is about **tappable** things, and about anything whose legibility must
  survive zoom.

---

## 8. Clamp against the RESERVED BANDS, not just the screen.

"Fully on-screen" was never the real requirement. A floating element must also stay clear of every
**fixed band another surface owns** — otherwise two separately-correct clamps produce one unreadable
result: at a screen corner the ghost pill and the verb chips each satisfied "fully on-screen" and
landed **on top of each other**, with the chips covering the cost text.

Two ways to satisfy it, and the cheaper one is now the house answer:

1. **Take the follow away.** WO-1010 D14 (2026-08-09) retired the cluster that followed the ghost and
   flipped sides near an edge. Fixed chrome cannot land on the piece, cannot walk off-screen, and
   cannot need a clamp. If a control does not have to follow, do not make it follow.
2. **For whatever genuinely must follow, publish the bands and subtract them.** Reserved widths are
   `public const` so a neighbouring lane seats clear of them without reaching into your file.

**Pointers**
- `BuildHudController.cs:906-988` (`LayoutGhostControlsNow`) — the worked example, now down to the
  **pill alone**:
  - `:926-936` — the clamp limits subtract `RailReservedWidthPx` on the right and
    `ResourceStripReservedPx` at the bottom on top of the pill's own half-size and `SafePadPx = 24f`
    (`:90`), so the pill can never slide *under* the rail or the strip. The two degenerate guards
    below them collapse the range instead of inverting it on an absurdly narrow canvas.
  - `:938-945` — prefers **above** the ghost anchor, drops **below** only when there is no room up
    there, so the pill labels the piece without covering it.
  - `:910-914` — the comment stating that the rail is deliberately **not** laid out here.
- The published seams: `BuildHudController.RailReservedWidthPx` (`:109-115`) and
  `BuildHudController.ResourceStripReservedPx` (`:139-145`).
- The defect this class produces when the bands are *not* published: `BuildHudController.cs:414-430`.
  The rail's first build centred vertically and the palette lane's D15 quick-tabs occupied the top of
  the same column, so "Defenses (3)" drew directly on the OK confirm verb. **The two canvases are
  captured separately, so no single PNG showed it** — it only fell out of comparing their rects.

---

## 9. ASCII only inside TMP string literals.

Comments may contain anything. **String literals may not.** Use `--` for a dash, straight quotes, no
arrows, no box-drawing, no emoji, no shape glyphs.

**Why:** the shipped TMP font atlas renders non-ASCII as **tofu** (□). It shipped to a device
screenshot on 2026-07-12 and cost a whole audit lane.

**Pointers**
- `Assets/Editor/Regression/HudUiRegression.cs:1-11` — the tofu oracle; it scans first-party runtime
  source under `Assets/_Modules/**`.
- Per-panel enforcement: `GlossaryRegression.cs:280-298`, `EchoCardLayoutRegression.cs:215-223`,
  `BuildMenuLayoutRegression.cs:432`, `DailyQuestEmptyStateRegression.cs:399-407`.
- The real cost: `BuildPaletteUI.cs:915-916` — *"WO-683: the old leading shape glyphs rendered as
  tofu boxes on the shipped TMP font."*
- Same scan class also rejects NUL bytes (`CLAUDE.md` §1, WO-434).
- **The rule beats the icon pass, and the gap gets reported rather than papered over.** WO-1010 D17
  (2026-08-09) moved the build rail to sprite icons *where real sprite art exists* — and later the
  same day the two missing sprites (`element`/`check`, `element`/`rotate`) landed beside
  `element`/`cross`, closing the gap this bullet used to record. The DISCIPLINE is unchanged and is
  the point: while the art was absent, confirm and rotate shipped their ASCII words (`OK` / `Rot`)
  rather than a typed `⟳` that would render as tofu, and the absence was a logged `FlowTrace.Warn`,
  not a silent square. `BuildHudController.cs:455-476` resolves all three and logs which resolved as
  sprites; `MakeVerb` (`:592`) is sprite-or-null, so the ASCII words remain the live fallback if a
  sprite ever fails to load (`RpgUiCatalog.cs:171-172` holds the names). Do not invent art, and do
  not type the glyph.

---

## 10. Panels stay near-black. Do NOT lighten.

Obsidian canon (WO-562, owner 2026-06-28): **black panel + gold trim**, never brown, never lifted.

**Why:** the panel language was warm stone/wood (`#2c2115`) and got unified to near-black obsidian so
every surface reads as one designed game. "It looks a bit dark on my monitor" is not a reason.

**Pointers**
- `Assets/_Modules/Core/UI/ElarionUi.cs:43-58` — the canon note and the tokens
  (`PanelStone`, `PanelStoneDark`, `Scrim`, `Gold`, `Gilt`).
- `ElarionUiKit.cs:174-191` — one chrome, one trim colour (`ObsidianTrim`), `ObsidianTrimPx = 3f`,
  and **no per-panel `X` button** — one consistent Close.
- Restated at every call site that touches a band: `BuildHudController.cs:48` (*"panels near-black
  (WO-562 — do NOT lighten)"*), and at the two bands it builds — `:236-241` (the D19 strip) and
  `:530-537` (each rail verb's plate).
- Tune the language in ONE place: the tokens route through `UiStyle.Theme`
  (`ElarionUiKit.cs:60-76`), so swapping the active `UiTheme` reskins every kit screen at once. If
  you feel the urge to hand-pick a colour on your panel, that urge belongs in the theme.

---

## 11. Respect the device safe area.

Insets are eaten by gesture bars, notches and rounded corners — and the **first** thing they eat is
whatever you anchored flush to `0` or `1`.

**Pointers**
- `Assets/_Modules/Core/UI/SafeAreaInset.cs` — `EdgeMarginPx = 44f`, `Left/Right/Top/BottomInset`,
  `ApplyTopRight`, `TopRightAnchoredPosition`.
- The cheap version when you only need breathing room: a fixed pixel inset —
  `BuildPaletteUI.cs:342-350`. The card tray ran flush to the canvas bottom with the **cost line**
  sitting on the edge; on any device with a gesture bar the price is the first thing clipped.
- ⚠ `docs/reference/AUDIT_2026-08-09.md` F74: **all nine HUD zones ignore the safe area** —
  `SafeAreaInset` has exactly one caller in the entire codebase. Do not assume the HUD you are
  parenting into has handled it.

---

## 12. Layout that only runs in `Update`/`LateUpdate` must ALSO be callable directly.

Extract the pass into a public method. Have the tick call it. Have the capture call it.

**Why:** **MonoBehaviour ticks do not run in edit mode.** Without a direct entry point the headless
capture photographs your elements parked wherever they were constructed — and reports green. A
screenshot of an unlaid-out screen proves nothing about the layout rule it exists to verify.

**Pointers**
- `BuildHudController.cs:888-905` — `LateUpdate()` is a one-line delegate to the public
  `LayoutGhostControlsNow()`. The doc comment states exactly why, and adds the corollary: the method
  stays **directly callable by contract** even after D14 shrank its work to the pill alone, because
  the `OK`/`No` verdict it also computes is state the capture needs.
- `Assets/Editor/UICaptureLaunch.cs:2277-2281` and `:2314-2328` — the capture calls
  `TrackGhost(...)` then `LayoutGhostControlsNow()` for each of four states. *"without that call this
  would photograph the chips parked at the canvas centre in all four shots and prove nothing at all."*

---

## 13. VERIFY BY CAPTURE. ALWAYS. And know how a capture lies.

**Compile-green never proves a panel looks right.** Neither does a green regression count.

### Run it

```
powershell -File .\run-unity-method.ps1 `
  -Method DeNelle.Editor.UICaptureLaunch.RunCaptureHeadless -LogName ui-capture.log
```

- Output PNGs: **`Builds/ui-capture/<PanelName>_<w>x<h>.png`**
  (`Assets/Editor/UICaptureLaunch.cs:109`).
- Three **distinct** markers, all of which must be green (`UICaptureLaunch.cs:31-38`):
  - `UI_CAPTURE_OK <count>` — non-blank frames written
  - `UI_CAPTURE_FIDELITY_OK <n> builds` — every panel was BUILT at the size it was SHOT at
  - `UI_GEOMETRY_OK <n> canvases` — numeric layout assertions passed
- Do **not** use `RunCapture()` (the legacy Play-mode drive). In
  `-batchmode -quit -executeMethod` it returns immediately and Unity quits before Play ticks —
  **zero PNGs** (`UICaptureLaunch.cs:5-13`).
- Never judge by exit code. `run-unity-method.ps1` exits 0 on refusals and FAILs — check the marker,
  the log freshness and the log size.

### THEN OPEN THE PNGs

Counting them is not looking at them. The WO-1010 RESULT says it plainly: *"and the PNGs were opened,
not just counted"* — and all nine of its defects were found that way, none by any gate.

### The ways a capture lies

1. **It photographs a stale / leaked canvas.** `ElarionUiKit.BuildModalCanvas` parents the canvas at
   the **scene root**, not under your host — so destroying the host **leaks the canvas**. Canvases
   accumulated across three target sizes and a name-scan returned target 1's stale, already-collapsed
   canvas. The 2340 and 2670 "open" shots were byte-identical to their own *collapsed* shots, and the
   run was green throughout. **The tell — identical file sizes — was visible in the directory listing
   before the wrong picture was.** Destroy the root canvas explicitly, and take the **newest** match
   if you must scan. `UICaptureLaunch.cs:2455-2466`, `:2493-2496`.
2. **It renders an empty panel because runtime data never loaded.** `CatalogRegistry` is populated by
   a **runtime bootstrap that never runs in edit mode**. The first palette capture shot a dock reading
   *"No buildables registered"* and reported green. Hydrate from the canonical file first —
   `HydrateCatalogForCapture()` at `UICaptureLaunch.cs:2381-2428`, called at `:2444`. Note it reads
   through `CanonicalJson` (the same source the game and the economy oracle use), is idempotent, and
   registers only absent ids so it cannot stomp another case's fixture.
   **A capture that cannot draw the thing it captures is worse than no capture** — it launders an
   unverified screen as verified.
3. **It labels a geometry it did not build at.** Panels resolve zone geometry **at build time** from
   `Screen.*`, which reads 640×480 in `-batchmode` no matter what the game view says. Every PNG once
   shared one geometry while the filenames claimed three. Fixed by building per target size
   (`ForEachTarget`) and moving the kit's **injectable surface** before the build
   (`ElarionUiKit.SetSurfaceOverride`, editor-only). This is what `UI_CAPTURE_FIDELITY_OK` measures —
   `UICaptureLaunch.cs:40-84`.
4. **A `-nographics` run writes convincing black rectangles.** On 2026-08-04 an AutoPilot fleet in
   default mode overwrote **35 real review shots** with flat black at exactly 33150 bytes each, and
   the review tooling badged them *"PAIR COMPLETE"*. Two guards now stand between headless and a
   review full of black — do not delete either. `Assets/Editor/Regression/UiCaptureCoverageRegression.cs:7-33`.

### Add a capture case for what you built

A new screen with no capture case is unverifiable by anyone but the owner. Register it so
`UiCaptureCoverageRegression` can see it, or name it in `KnownUncapturable` **with a reason** — an
unexplained exemption is indistinguishable from an oversight
(`UiCaptureCoverageRegression.cs:35-57`, `:82-87`).

⚠ Scope check: `UI_CAPTURE_OK 44` covered **15 panels** and **zero** HUD-kit captures — roughly 100%
of actual screen time is uncaptured (`docs/reference/AUDIT_2026-08-09.md` F76). A green marker is a
liveness signal, not a correctness one.

---

## 14. Never hang art or behaviour off a player-facing label.

Resolve assets by **id**, never by a slug derived from a display name.

**Why:** `workshop` owns no portrait; its card art resolved purely through the display-name slug
`"Forge"` → `forge.jpg`. Renaming the label to **Weaponsmith** would have silently turned a
fully-illustrated card into a letter glyph — a creative text change deleting art.

Corollary, and the thing that misled the author of that ticket: **display names ≠ ids.** The card
reading "Armorer" is id `forge`; the card reading "Forge" is id `workshop`.

**Pointers**
- `WorkOrders/WORK_ORDER_1010_build_ui_carousel_minimize.RESULT.md:77-79` and §5.
- `Assets/Editor/Regression/BuildCardArtRegression.cs` — the permanent guard. It resolves through
  `BuildPaletteUI.ResolveEntryArtPublic` (**the resolver the game uses**, not a filename guess),
  reads the canonical file rather than the shared static registry so its verdict cannot depend on
  suite order, and its pass line refuses to flatter:
  *"OK (WITH RECORDED DEBT) — 24 of 29 … This is NOT a clean shop."*

---

## 15. Instrument the surface, and never fail silently.

UI code obeys `CLAUDE.md` §12 like everything else. Put `FlowTrace.Step` at each meaningful state
change; wrap risky construction and per-item list building in `Guard.Try` / `Guard.TryEach` so one
bad row logs and is skipped rather than blanking the screen.

**Why:** it splits *"the panel shows nothing"* into **data-empty** vs **built-but-invisible** vs
**threw-and-skipped** *before* you touch code. A `catch` that swallows without logging is forbidden.

**Pointers**
- `BuildHudController.cs:244-247`, `:307-310`, `:500-503`, `:883` — one `[Flow:BuildHud]` line per
  real state transition, each stating the design intent, not just the event. `:464-472` is the
  pattern for a *missing asset*: `FlowTrace.Warn` naming the fallback that was taken, so an absent
  sprite is a reported gap rather than a silent square.
- `GhostPreview.cs:300-305` — `FlowTrace.Warn` + hide, never a silent dead label.
- Helpers: `Assets/_Modules/Core/Diagnostics/` (`FlowTrace.cs`, `Guard.cs`).
  Method doc: `docs/INSTRUMENTATION_STANDARD.md`.

---

## Before you say it's done

Work top to bottom. Every line is answerable YES or NO — "probably" is NO.

**Build**
- [ ] Zero UXML / `UIDocument` added. Everything is code-built uGUI on `ElarionUiKit`.
- [ ] I used existing kit builders; I did not re-author panel chrome, a tab row, a wallet strip, a
      scroll well or a Close button.
- [ ] Cross-assembly calls go through `CoreServices.*` with `?.` (`CLAUDE.md` §5/§10).

**Layout**
- [ ] Every control has a **fixed pixel** size or an explicit pixel band. No `1f/n` fraction slicing.
- [ ] Every tappable thing resolves ≥ `ElarionUiKit.MinTouchPx` (112) on its shortest side —
      authored at the floor, or given an invisible hit pad. No visual was grown into a slab.
- [ ] If an external layout pass owns my rect, I used a hit pad (it would wipe `ClampMinTouch`).
- [ ] Anything anchored to a world object is **screen-space**, not a world-space billboard.
- [ ] Anything that follows a moving anchor clamps against the **reserved bands** of the fixed chrome
      (not just "on-screen"), and I checked a screen **corner**. Better: it does not follow at all.
- [ ] Any band my surface owns is published as a `public const` reserved size for the neighbouring
      lane, and I checked my rects against the *other* canvases — no single PNG shows an overlap.
- [ ] Nothing is flush to a screen edge that a gesture bar or notch would eat.

**Legibility**
- [ ] Every state / validity / affordability signal carries a **word or a shape**. I re-read it
      imagining red and green are the same colour.
- [ ] Anything floating over the 3D field carries its **own edge** or its own dark backing plate.
- [ ] Panels are near-black. I lightened nothing (WO-562).
- [ ] Every TMP string literal is **pure ASCII**. No `—`, no arrows, no glyphs, no emoji.
- [ ] Text that can be long either wraps inside its plate or auto-sizes down — it does not escape its
      own background.

**Proof**
- [ ] Any layout pass that lives in `Update`/`LateUpdate` is also a **public callable method**.
- [ ] There is a capture case for what I built (or a named, reasoned exemption).
- [ ] `RunCaptureHeadless` run: `UI_CAPTURE_OK` **and** `UI_CAPTURE_FIDELITY_OK` **and**
      `UI_GEOMETRY_OK` — read from the marker lines, never from the exit code.
- [ ] **I OPENED THE PNGs.** All of them, at every target size.
- [ ] File sizes differ between states that should look different (the stale-canvas tell).
- [ ] The panel actually drew its **data** — not an empty shell that reported green.
- [ ] No asset or behaviour resolves through a display name.
- [ ] `FlowTrace` lines exist for the state changes; no `catch` swallows without logging.

**Gates** (`CLAUDE.md` §8 — read the counts off the markers, never restate them)
- [ ] Brace balance on every `.cs` touched.
- [ ] `COMPILE_GATE_OK`
- [ ] `REGRESSION_OK <n>/<n> suites`
- [ ] `UI_CAPTURE_OK <n>` — with the PNGs opened.

---

*Sourced from `ElarionUiKit.cs`, `ElarionUiKitObsidian.cs`, `ElarionUiKitConformance.cs`,
`ElarionUi.cs`, `SafeAreaInset.cs`, `BuildHudController.cs`, `BuildPaletteUI.cs`,
`GhostPreview.cs`, `UICaptureLaunch.cs`, `UiCaptureCoverageRegression.cs`, `HudUiRegression.cs`,
the `*LayoutRegression.cs` family, `WORK_ORDER_1010_*` (+ RESULT), `docs/reference/AUDIT_2026-08-09.md`
and `CLAUDE.md` §5/§8/§12. Keep it current in the same breath as the change (`CLAUDE.md` §15).*
