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
W1 ShopPanel + PartyShopPanelMvvm + vendor flows (pack MERCHANT reference) ·
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
