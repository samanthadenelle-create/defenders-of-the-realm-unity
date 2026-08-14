# WORK ORDER 1015 — Equipment/paperdoll screen: empty preview, overprinted slots, dead space, and the GLOBAL rogue "Orient" button

**Status:** DONE — shipped `ac0667a4` *fix(ui): WO-1015*.
⚠ Caveat: this is a **presentation** fix (E1–E6); owner felt-verify of the paperdoll screen still owes a
verdict, and the E1 "Orient is global — assume more screens" sweep stays a standing check on any screen
not yet re-captured.
**Minted:** 2026-08-10 (UI seat) — provenance stack bumped 1015 → 1016 in the same edit
**Lane:** HUD/UI presentation. **No gear/stat/equip LOGIC changes** — the data is right, the screen is wrong.
**Provenance:** owner felt-test capture 2026-08-10 ("this is the weapon display screen") — the
Equipment/paperdoll panel for `Thrain the Wise`.
**Anchors:** `Assets/_Modules/Village/Hero/EquipmentPanel.cs` (registers with
`TutorialHighlightRegistry`), `ElarionUiKit` (obsidian frame + fixed-band law), the gear/paperdoll
layer (`HeroCanonNames` "cleric"/"healer" alias note), `RpgUiCatalog` item icons.
**Adjacent:** WO-1010 D1 (the same rogue "Orient" button — **now proven global, see E1**), WO-1010 D7/D18
(HUD chips bleeding into modals — same class as E6).

---

## 1. Defects (from the capture; E-numbered)

- **E1 — the rogue "Orient" button is GLOBAL, not build-mode-only.** The same oversized obsidian
  `Orient` control that WO-1010 D1 orders removed from build mode **also renders on this modal**,
  floating detached over the Shield (Off Hand) slot and clipping its text. **This escalates D1:** find
  what constructs `Orient` and remove it at the SOURCE (a shared kit/HUD path, not a per-screen strand),
  then verify every screen. Two screens is a pattern; assume more.
- **E2 — the hero PREVIEW BOX renders EMPTY.** The centre panel is a flat dark-navy rectangle: no hero
  model, no portrait, no fallback. A paperdoll screen whose entire point is showing the hero shows
  nothing. §12 applies — **instrument the preview path first** (render-texture camera? model spawn?
  layer/culling? material?) and read the trace before changing code; do not guess. Whatever the cause,
  ship a **visible fallback** (portrait sprite + name) so this can never be a blank box again.
- **E3 — ~40% of the panel is DEAD SPACE.** The frame's body starts near the top but content begins
  below the midpoint; the whole slot/preview cluster is crammed into the lower half. Re-band with the
  fixed-pixel law (measure the well → sum the fixed bands → remainder to content), the same arithmetic
  `ManageScreenPanel.BuildChrome` already proves in a `FlowTrace.Step` line.
- **E4 — every slot OVERPRINTS itself.** In each slot the label, the value and the hint are drawn on top
  of one another: `Weapon (Main Hand)` over `Emberglass Staff` over `+0% dmg`; `Amulet` over `Empty`
  over `Craft one at the Jeweler`; `Shield (Off Hand)` over a clipped `...o...`. Each slot needs its own
  fixed-height row with **three stacked, non-overlapping text bands** (label / value / hint), each
  ≥ one line box, exactly like the WO-911 queue-row banding.
- **E5 — item icons are effectively invisible** (a few dark pixels in each slot). Icons need a real
  fixed-px art band inside the slot, resolved through the existing icon-catalog path (same resolver the
  queue/cards use, so an item cannot look like one thing here and another there).
- **E6 — the `Echoes 1/6` chip bleeds through the modal** at right. Same class as WO-1010 D7/D18: HUD
  chips must be suppressed (or z-ordered under) while an exclusive modal is open.
- **E7 — proportion:** the `Close` button is enormous relative to the content it closes; slot rows are
  small by comparison. Bring `Close` to the kit's canonical CTA size and give the slots the space.
- *(Check)* `+0% dmg` on an equipped Emberglass Staff reads like a real stat bug — **verify at source**
  whether the staff's damage bonus is genuinely 0 or the panel is failing to read/format it. If the
  data is right and the display is wrong, fix here; if the data is wrong, bounce a separate ticket.

## 2. Constraints

- **Fixed-pixel bands, never fractions of parent** (the WO-841/852/905 root cause — and E3/E4 are that
  same class again).
- **MinTouchPx 112** on every tappable slot/control.
- Colorblind law: empty/locked slots say so in WORDS (they already try — E4 is why you cannot read it).
- ASCII only in TMP strings; code-built uGUI via `ElarionUiKit`; no UXML.
- **No gear/stat/equip logic changes.** Presentation only (except the E-check bounce above).
- **Instrument (§12):** `[Flow:Equip]` lines for panel-open, preview-build (success/fallback + reason),
  each slot bound (id + icon resolved?), so the next regression here starts from data.

## 3. Acceptance criteria

- [ ] No `Orient` button on ANY screen; the source construction site is removed (not per-screen patched),
      and WO-1010 D1 is closed by the same fix.
- [ ] The preview shows the hero (or a clearly-intentional portrait fallback) — never a blank box.
- [ ] No dead band: content fills the frame; the band budget is printed once in a `FlowTrace.Step` line.
- [ ] Every slot's label / value / hint are separately legible — zero overprint, at any supported aspect.
- [ ] Item icons render at a readable size through the shared icon resolver.
- [ ] No HUD chip (Echoes or otherwise) draws over the modal.
- [ ] `Close` at canonical CTA size; slots proportionate.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` — open the PNGs: equipped
      state, all-empty state, and one narrow aspect.

## 4. What NOT to touch

- Gear stats/catalog data, equip rules, the Jeweler crafting flow.
- The build-mode surfaces (WO-1010) beyond removing the shared `Orient` source.
- Hero roster/class naming.
