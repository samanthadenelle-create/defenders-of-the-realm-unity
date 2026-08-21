<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 405 — Complete UGUI Design System for ALL Game HUDs

**Priority:** P0
**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).
**Type:** Design system + implementation (foundation)
**Owner sign-off needed on:** the light-parchment direction (verify in a build) before the full sweep
**WO-numbering note:** 405 minted by owner 2026-06-10; slot into MASTER_PIPELINES_BACKLOG + Notion WO DB.

---

## 1. Objective

ONE cohesive, light mystical-medieval **UGUI design system** that every HUD/screen
draws from, so Town, Combat, Inventory, ATB Battle, and Dialogue all feel like one
polished game. **Code-built uGUI only** (UXML does not render in player builds —
PIPELINE_STATE §8). The single source of truth is the **`ElarionUiKit`** builder kit;
no parallel SO/prefab/reflection theming system.

North-star = owner mockups #40 (combat HUD), #42 (town HUD), #41 (inventory) + the
Grok 3-WO layout spec. The feel: **light warm parchment + thin glowing gilt/rune
borders + airy, low-opacity, dark-ink text** — premium but calm. NOT dark-glass, NOT
sci-fi.

## 2. Style Guide (apply everywhere)

- **Palette:** parchment fills `#EDE6D6`/`#F4EEDF`; dark ink text `#231910` (+ dimmer inks);
  gilt rims `#E8B923`/gold; soft gold glow; muted blue/green/violet accents kept for
  state colors (HP red, mana blue, XP violet) — they read fine on light.
- **Borders:** thin glowing **gilt/rune** lines (Outline + soft Shadow — cheap, no sprites)
  + an optional **ornamental runic border frame** (the one real sprite asset — see §6).
- **Typography:** elegant serif for titles, clean sans for numbers (existing TMP fonts).
- **Layout:** edge-anchored panels, Canvas Scaler (Scale-With-Screen-Size) + Layout Groups,
  consistent padding, mobile/safe-area aware.
- **Motion (polish layer, later):** alpha-fade + slight scale on show/hide; subtle rune glow.
- **Consistency:** SAME Tree-of-Life icon, compass, button style, frame, and bars on every screen.

## 3. Component Library (formalize as `ElarionUiKit` builder methods)

Panel/Frame (w/ runic border) · OrnateTopBar (HP/Mana) · CircularIconButton (settings/
inventory/talk/quest) · AbilityFrame (circular rune ring + cooldown overlay + level badge) ·
ItemSlot (icon + rarity frame + count) · Tabs (active-glow) · PartyPortrait (slim circular
rune-framed) · DialogueBox (parchment) + NameBanner + PortraitNiche · Bar (HP/mana/XP/ATB) ·
Scrollable list (ScrollRect + Mask). All light-palette by default, town/combat accent variant
via a param.

## 4. HUDs to cover (apply the system to each)

| Screen | File | Layout (per mockups) |
|---|---|---|
| **Town Hub** | `VillageHudController` | compass top-center; Tree+HP top-left; LEFT slim party portraits; top-right Settings+Inventory icons + Next Wave + Start Now; RIGHT = **Talk + Quest** buttons; full runic border |
| **Combat HUD** | `VillageHudController` (combat state) | same, but RIGHT = **Skills** panel (4 circular rune ability buttons + cooldown rings) |
| **Inventory/Loadout** | `HeroInventoryController` | tabs (Weapons/Armor/Accessories/Consumables); LEFT paper-doll w/ equipped slots in a runic ring; CENTER/RIGHT scrollable item grid; bottom Sort/Filter + Gold + SKR |
| **ATB Battle** | `BattleHudUgui` | party + enemy sprite rows, ATB gauge, command/skills panel — light parchment |
| **Dialogue** | `CompanionDialoguePresenter` | parchment box + portrait niche + **name banner** + options |

## 5. Art integration

- **Item icons** — DONE (`ItemIconSlicer` + `ItemIconCatalog`, sliced to `Resources/ItemIcons`).
- **HUD widget icons** (mockup sheet `Assets/Art/UI/HudIcons/hud_widgets_sheet.jpg`: Tree,
  HP/Half/Low bars, compass, Settings gear, Inventory backpack, Party frame, Talk bubble,
  Quest arrows, runic ability-circle frames) — **TODO**: slice (mirror the ItemIconSlicer
  pattern → `Resources/HudIcons`) + wire into the kit's icon/bar/ability builders.
- **Runic border frame** — the one ornamental sprite (9-sliced) for the full-screen frame; or
  a procedural approximation if no art. Fed through the kit centrally so all HUDs inherit it.

## 6. Constraints (non-negotiable)

- Code-built uGUI ONLY. No UXML. No parallel theming system — extend `ElarionUiKit`.
- **Skin only** — do NOT change any HUD's data bindings / update logic / event wiring.
- WebGL-safe (Resources for sprites; RoundedSprite fallback; BMP-only glyphs).
- Per-file brace gate before commit (CLAUDE.md §1). Light-palette = a tone inversion;
  verify ALL text stays readable on the light bg.

## 7. Current State (what's already landed vs remaining)

**DONE (committed):**
- `ElarionUiKit` shared builder kit exists.
- Light-parchment restyle: Inventory, Town/Combat HUD (`VillageHudController`), BattleHud, Dialogue (+ name banner).
- Item-icon art integration (inventory).
- Party rows → slim circular rune portraits (HUD).

**REMAINING:**
1. **Formalize the kit** — promote the per-screen local light palettes into ONE shared
   light token set in `ElarionUi`/`ElarionUiKit` (right now each screen has its own local
   inversion; consolidate so one change re-themes all).
2. **HUD widget icons** — slice `hud_widgets_sheet.jpg` + wire real Tree/compass/gear/bag/
   talk/quest/ability-frame sprites into the kit (replacing code-drawn placeholders).
3. **Runic border frame** — add the full-screen ornamental frame to the kit.
4. **Talk/Quest (town) + Skills (combat) side panels** — currently deferred (need a relayout,
   not just a skin); build them per the mockups. Event hooks (`SkillsRequested`/`ShopRequested`)
   exist.
5. **ATB battle** — finish the light pass + (later) the 2D hero/enemy sprite states (#34/#35).
6. **Polish layer** — fade/scale transitions, hover/press feedback, rune glow.

## 8. Acceptance Criteria

- [ ] Every HUD (Town, Combat, Inventory, ATB, Dialogue) shares the light look + the same Tree/compass/button/frame.
- [ ] All driven by `ElarionUiKit`; one palette change re-themes everything.
- [ ] HUD widget icons + item icons render from real art (glyph/procedural fallback intact).
- [ ] Runic border frame present on the main HUDs.
- [ ] Talk/Quest + Skills side panels built + wired.
- [ ] All text readable on light; mobile/safe-area scaling holds.
- [ ] Behavior-preserving (no binding/logic regressions); compile gate clean.

## 9. Suggested implementation order (parallel-safe silos)

1. Kit foundation (light tokens + the new builder methods) — `ElarionUi`/`ElarionUiKit` (one agent).
2. HUD widget icon slice + wire — disjoint (slicer + kit icon builders).
3. Then per-HUD passes (Town/Combat, Inventory, ATB, Dialogue) — file-disjoint, run parallel,
   batch-gate once.
4. Runic border + Talk/Quest/Skills panels.
5. Polish (transitions) last.

---

## 10. SCOPING (2026-06-11) — remaining FOUNDATION that unblocks WO-411

Grounded in the current code (`ElarionUiKit` + HUD):

**DROPPED — light-token relight (was REMAINING #1).** Owner 2026-06-11: the "make it
lighter" ask was about the elements/inventory screen and was a misread of the mock; the
**global kit relight is moot — do NOT do it.** The kit keeps its current (dark-default)
palette; the new builders are **palette-parameterized** (take a color arg) so the light
HUDs pass their own light locals — no global re-theme needed.

**What EXISTS in `ElarionUiKit`:** BuildModalCanvas, Scrim, Panel, Well, Niche, Header,
Rule, Button, StyleButtonColors, Slot/SetSlot*, Card, Label, AddImage, ApplyRounded,
AddRimUnderline, AddInnerRim.

**What's MISSING (the foundation WO-411 + WO-308 need) — all ADDITIVE, parallel-safe:**

- **F1 — Layout-group cluster helpers (the backbone; NONE exist today).** Kit helpers that
  emit `HorizontalLayoutGroup`/`VerticalLayoutGroup`/`GridLayoutGroup` + `ContentSizeFitter`
  + `LayoutElement` clusters, so HUDs stop hand-anchoring (mockup #42 / WO-411 explicit req).
  This is the biggest structural shift and what makes the town HUD hold across resolutions.
- **F2 — Missing component builders:** `CircularIconButton` (TOWN ACTIONS row + top-right
  icons), `OrnateBar` (HP/Heart/resource bars), `PartyPortrait` (party/pet frames),
  `AbilityFrame` (combat ability buttons — WO-308), `RunicBorderFrame` (promote the inline
  `VillageHudController.BuildRunicBorderFrame` into the kit). All palette-parameterized.
- **F3 — Shared hub-scene source (Core) + its EditMode test (§2c gate).** ONE list of hub
  scenes (`Village2`/`MainCastle_Hall`/`CastleHub*`) read by `VillageHudController.EvaluateInVillage`
  AND `WorldSceneLoader` (kills the drift that hides the town chrome — WO-411 root cause A).
  Test asserts every hub scene is recognized. Small; Core-only.
- **F4 — HUD widget icons (§5):** wire a real **compass** sprite (today `IconCompass` = a
  *star* → deviation #8) and slice `Assets/Art/UI/HudIcons/hud_widgets_sheet.jpg` →
  `Resources/HudIcons` for Tree/gear/bag/talk/quest/ability-frame, into the kit's icon builders.
  **TRANSPARENCY (owner, 2026-06-11):** the current RpgUiCatalog icon PNGs (`Tab icons`/`Rpg icons`
  via `IconSettings`/`IconInventory`/…) have an **opaque dark background baked into the art**, so
  the HUD icon buttons can't render transparent (clearing the code seat just reveals the sprite's
  dark frame). F4 must source/slice **frameless, alpha-transparent** icons (candidate unframed sets
  in the pack: `Tech hud elements/Sprites/Icons 1` and `…/GreenUielements/Icons` — verify alpha +
  re-point RpgUiCatalog), so gear/bag/intel/talk float with no dark block. The HUD-side seat is
  already transparent (`BuildIconButton`) — only the art is left.

**Consumers (after the foundation):** WO-411 (town HUD), WO-308 (combat ability bar);
inventory/ATB/dialogue are already styled and just adopt F1/F2 incrementally.

**Order:** F1 + F2 together (one kit-foundation silo) → F3 (small, Core) → F4 (icon slice,
disjoint) → then WO-411 consumes F1–F4. **Verification:** kit builders unit-test where
possible; every HUD consumer closes with side-by-side screenshot + owner sign-off (no self-cert).

---

*Captures the north-star (task #23) + mockups #40/#41/#42 + the Grok 3-WO spec. The design
direction needs an owner build-verify before the full sweep.*

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `docs/UI_BLINK_TEMPLATE_CANON.md:1-16` — light-parchment reversed by Obsidian canon. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
