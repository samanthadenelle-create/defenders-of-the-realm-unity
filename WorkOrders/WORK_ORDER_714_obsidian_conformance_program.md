# WORK ORDER 714 — Obsidian conformance program: apply the pack styling across ALL screens

**Status: READY TO IMPLEMENT (phased program — run as lanes, one wave at a time)**
(owner ruling 2026-07-13: "have this styling applied across all screens so they fit").
**Lane:** UI program (View-only throughout). **Type:** deliberate holistic/leverage program
(ARCH §3 — ordered by the owner as its own program, NOT smuggled into feature work).
**Minted from banner: 714 → 715.**

## BINDING READ ORDER (per CLI's pointer, 2026-07-13 — every agent on this program reads first)
1. `docs/UI_BLINK_TEMPLATE_CANON.md` — the master-frame formula (frame, drop-zones, close).
2. `docs/HUD_OBSIDIAN_ARCHITECTURE_2026-07-03.md` — kit structure, panel composition.
3. `docs/UI_Mockups/` — the image-pair references (side-by-side verify).
4. `docs/SME/BLINK_SME.md` + `docs/MASTER_CATALOG/BLINK.md` — the pack SME dossiers.
5. `docs/UI_MVVM_BINDING_MAP.md` — VM wiring (Views stay dumb skins).
**Composition authority per screen = the OWNED pack's store-gallery reference layouts**
(Obsidian UI #206302, owner-verified; owned at `Assets/Blink/Art/UI/Obsidian_UI/`) + the
frame-zone table (`ZonesFor`). Missing sprites = BlinkUiImporter rows, never rebuilt art.

## The program shape (three phases — primitives first, then screens, then the sweep gate)

### Phase 1 — shared primitives at the FACTORY (fix once, inherit everywhere)
The defect classes from the WO-675/680/683/693/697/713 arc, promoted to kit-level:
P1 uniform TAB row builder (panel_tab/element_tab, icon+label, fit-never-truncate) ·
P2 `CurrencyChip` row = THE ONLY currency read (CompactNumber, WO-697; channel-skinned wallet;
currency-ellipsis forbidden) · P3 the detail-card builder (icon plate · spaced displayName ·
one-line effect/BESTOWS · count · actions; WO-683 grammar) · P4 slot grammar (RoleSlot plates +
rarity_1..5 rims; empty = dim plate, sparse-grid law) · P5 toast for transient feedback (no
stuck status text) · P6 footer/close-band reservation at the factory (nothing ever clips under
chips/Close) · P7 mobile font floor (WO-693) in all fit helpers · P8 `PanelOpenCloseFx`
promoted to the kit (the flagged item) · P9 sprite-first ALWAYS (RpgUiCatalog + fallback;
never gated on ff.blinkchrome) · P10 zero hex literals / zero raw itemIds player-visible.

### Phase 2 — per-screen lanes (each = capture → conform → image-pair verify)
Already covered (do NOT re-spec; land/verify them as program instances): 675+680 upgrade
panel · 693 jeweler/crafting · 713 inventory + character/gear · 676 skill tree · 697 chips ·
699/SEL-1 hero-select chips.
**Remainder inventory (the BuildObsidianPanel consumer census + known UXML risks), in
player-touched priority order:**
W1 ShopPanel + PartyShopPanelMvvm + vendor flows — **exact model APPROVED-PENDING in-chat
2026-07-13 (the merchant buy/sell mockup is the composition authority for this lane):**
  · Header: 42px round medallion (vendor-trade glyph) + vendor NAME + one flavor line.
  · Mode toggle: full-width segmented BUY | SELL (34px band, selected = lit plate + bold —
    replaces any separate buy/sell screens; VendorStockContract keeps governing stock).
  · Item rows: FIXED 48px height, 8px gaps, 10px side padding; 36px icon plate with the
    RARITY-colored border; name (13px floor) + one-line grant/effect under it; PRICE CHIP
    pinned right (icon-first, CompactNumber). Unaffordable = dimmed row + red-bordered
    "Need 🪙N" chip (text carries state). SELL mode: same rows, right chip = sell value,
    name line gains "×owned".
  · Select → detail strip (60px): icon + name + rarity chip + **the upgrade-delta line**
    ("+22 attack — replaces Squire's Blade (+12)" — computed vs equipped; the single best
    buy-decision aid) + ONE action button carrying the full price ("BUY · 🪙1.2k" — a
    truncated price is a defect).
  · Footer: gold chip left, Close right, top hairline; rows never clip under it.
  · Spacing rhythm for the WHOLE lane: 14px panel padding, 12px between sections, 8px
    between rows — no other gap values.
  · **STOCK RULE (owner 2026-07-13, reaffirms + extends VendorStockContract):** the list shows
    ONLY what THIS vendor sells — weaponsmith = weapons only, armorer = armor only; the ITEM
    STORE (general/marketplace) = crafting materials + consumables. Extend `AllowedFor()` with
    the item-store mapping; the AutoPilot stock assert keeps checking intent (the built seam).
  · **PREVIEW WINDOW right (owner 2026-07-13):** the buy/sell layout becomes two-column —
    rows LEFT, a preview pane RIGHT: weapons/armor render as a lit model preview (the WO-713
    render-rig/GearIconRenderer recipe — show the item, or on-hero for armor), consumables/
    materials show the large icon + effect text. Preview updates on row select; the detail
    strip's upgrade-delta line moves INTO the preview pane's base. Render cost only while
    open (same §B guardrails).
  · **W1b — the UPGRADE screen (exact model approved-pending in-chat 2026-07-13):** header
    "UPGRADE — <structure> · Level N → N+1" · LEFT = "WHAT CHANGES" delta rows (fixed 44px,
    same rhythm: stat glyph + name, then old → new with the new value green+text) + build-time
    row + COST chips · RIGHT = the PREVIEW pane showing the NEXT-TIER model (the
    ReskinForLevel visual — the player sees what they're buying; slow turntable, rim-lit) ·
    footer = relevant currency chips + ONE "UPGRADE · <full cost>" action + Close. Serves
    resource buildings, towers (WO-696 context routes here), and gates/walls alike — deltas
    from catalog rows, ZERO per-type layouts. Blocked states name why ("Repair first" per
    WO-696; "Requires Village Tier N" per the gate).
    **+ UNLOCKS BAND (owner 2026-07-13: "each tier unlocks perks like Warcraft that can be
    researched"):** below WHAT CHANGES, a "UNLOCKS AT LEVEL N+1 — RESEARCH" strip: the perk
    chips this tier opens (perk icon plate + name, dimmed with a small lock-open glyph +
    "research after upgrade"), sourced from the WO-432 per-building research defs
    (BuildingTierCatalog/perk rows — data-driven, no hardcoding). POST-upgrade, the success
    toast carries "New research available — <Building> Enhancements" and tapping it routes to
    the enhancement panel (the WO-675/680 surface) with the newly-opened tier band visible.
    This makes the tier buy legible as the TECH-GATE it is: stats now + research unlocked —
    the Warcraft promise on one screen. If a tier opens no research, the band hides (no empty
    header). ·
W2 EndStateView + wave damage report (defeat/victory = pack row-list grammar; REP-1 fix lane
touches the same surface — coordinate) · W3 QuestsHud/DailyQuestHud + GameGuidePanel (pack
QUESTS reference: list + parchment detail) · W4 RaidSelectionScreen + RaidDeployScreen +
TroopTrainingPanel · W5 HeroLoadoutPanelMvvm (pack SOCKETING/slot grammar) · W6 DialogueView
polish vs pack NPC card (FrameCore rebuild landed — verify only) · W7 BossHealthBar + world
HUD chrome accents · W8 Settings/Pause — the LAST UXML-bound surfaces with no code-built
fallback (MASTER_CATALOG P1 #8): rebuild code-built through the kit — this closes the
UXML-in-builds risk class for good · W9 Title/HeroSelect/PetSelect front-end chrome ·
W10 PackStore (respect the standing scene-wiring hold; panel skin only).
Rules per lane: file-disjoint silos (§9), View-only, one screen = one agent, each lane ships
its canon §7 IMAGE PAIR (runtime capture vs pack reference/template PNG) — the
UI_Mockups pairs are the sign-off artifact the owner reviews.

### Phase 3 — the sweep gate (definition of done)
- The fleet panel-capture run (13/13 + the new screens) produces the full image-pair set;
  owner reviews pairs, not prose.
- Conformance checklist per screen: frame from the table · zones only · one Close · kit
  tabs/chips/cards/slots · toasts · no truncation at phone aspect · sprite-first fallbacks
  exercised (art-absent run never blanks) · no dev controls in player builds.
- Update `docs/UI_COVERAGE_MATRIX` rows in the same breath (§15); COMPILE_GATE_OK +
  REGRESSION_OK per wave; owner felt-pass per wave, program closes when the matrix is green.

## What NOT to touch
VMs/game logic (Views only) · PanelManager arbiter semantics · the UXML ban stands (W8
rebuilds code-built, never "fixes" UXML) · PackStore scene-wiring hold · WO-702/710-CLI
founding-flow content (skin only if touched).

*Cross-refs:* the read-order docs above · WO-675/676/680/693/697/713 (instances) ·
MASTER_CATALOG P1 #8 (Settings/Pause UXML risk) · pack gallery (owner screenshots 2026-07-13)
· `BlinkUiImporter` (§5 mirror pipeline).

## PROGRAM STATUS (2026-07-13 overnight)
Waves 1+2 SHIPPED and OWNER-ACCEPTED ("i accept the edits", night review): P1 kit factory +
W1/W2/W3/W4/W5/W7/W8/W9 + WO-713 — nine lanes, gated per wave, committed per lane, pushed.
Review drop: UI_REVIEW/INDEX.html (31/32 pairs; 01_HeroTalents re-shoots next pass).
Remaining: W6 dialogue verify (capture-only) · W10 PackStore skin · PetSelect UITK conversion
(own WO) · per-screen FIX verdicts from the owner's ongoing pair walk.
