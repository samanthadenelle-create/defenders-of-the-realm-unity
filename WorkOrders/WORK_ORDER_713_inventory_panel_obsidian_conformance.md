**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 713 — Inventory panel: Obsidian conformance, hero render window, consumable hot-swap belt

**Status: READY TO IMPLEMENT — WIREFRAME APPROVED (owner 2026-07-13: "much better — relay to
CLI to follow this").** The approved wireframe is the layout authority: uniform icon-tab row →
left column (live render alcove w/ rim light + name/class/LV badge + HP/MP bars w/ values + XP
strip + VIEW GEAR + the 2-slot BELT card beneath) → centered slot grid w/ A/B badges → detail
card (icon · spaced name · one-line effect · count · USE + ON BELT·A/B state chip) → footer
chips + one Close. Implement to THIS composition; canon §7 screenshot-verify against it.
(Owner asks 2026-07-13: "clean the layout so it flows better — refer to Obsidian SME + Blink
architecture" + "a better render window" + "consumables add to hot-swap potions and mana potions").
**Lane:** UI (View-only + one small render rig). **File:** `Assets/_Modules/Village/Hero/
InventoryUIBuilder.cs` (+ a portrait-camera rig prefab/injector for §B). **Canon:**
`docs/UI_BLINK_TEMPLATE_CANON.md` (BINDING) · `docs/MASTER_CATALOG/BLINK.md` (Obsidian SME) ·
WO-675/683 shared grammar. **Minted at 713 (banner bumped 714); the earlier 710 file is a stub.**

> **CLI design intent (standing):** nothing is functionally broken — the failure is flow +
> conformance. Kit over hand-rolled, remove over shrink, verify vs the template PNG (canon §7).

## A. Obsidian conformance (defect → SME answer)

1. **Wrong chrome** (grey frame, white-doodle sack medallion) → `BuildObsidianPanel(frameName:
   RpgUiCatalog.FrameInventory)` — `frame_inventory` is mirrored; medallion = mirrored
   `icon_inventory`; ONE shared Close in the frame's close band (the floating Close dies).
2. **Tab row** (mixed sizes, truncating "Access…"/"Consu…") → uniform kit tab chips
   (`panel_tab`/`element_tab`, sliced), icon + label one size, labels that FIT by renaming:
   Weapons · Armor · Trinkets · Potions · Skills. Selected = lit plate + bold (never color-only).
3. **Left column** (flat mustard slab; black-on-black hero; three identical bars) → portrait
   card on a kit plate; HP and MP as kit bars WITH values; the LVL bar becomes `badge_level` +
   a thin XP strip; VIEW GEAR stays the column's one action.
4. **Grid adrift** (5×5 huddled left, dead right third) → kit `Slot()` plates with rarity rims,
   centered + sized to the body zone (WO-675 cell-width pattern); empty slots dim.
5. **Detail band** ("BoneFragment/Used BoneFragment." mustard strip) → the WO-683 detail card:
   icon plate + spaced displayName ("Bone Fragment") + one-line effect + count + actions (§C).
   "Used X" feedback = toast. **Id-leak audit:** any raw itemId reaching a label is a defect.
6. **Footer chips off-canon** (hand-rolled gold/purple/teal; π wallet glyph on the WINDOWS
   build) → standard `CurrencyChip` row; the wallet chip is CHANNEL-SKINNED (π only under the
   Pi skin; store builds hide it) — never hardcoded.
7. **Dev leak:** the Orient checkbox gates to `UNITY_EDITOR || DEVELOPMENT_BUILD` only.
8. **Backdrop bleed** (wave banner reads through the panel top) → scrim + sort must own the
   screen while open (PanelManager modal conventions — verify registration + canvas order).

## B. The hero render window (owner: "a better render window")

Replace the static black portrait box with a LIVE paperdoll:
- A dedicated **portrait camera rig**: offscreen camera → RenderTexture → RawImage in the
  portrait card. Rig frames the REAL hero rig (equipped gear included — it already wears
  GearLoadout state) in a lit three-quarter pose against a dark backdrop plate with a soft rim
  light so the silhouette reads on obsidian (the current failure = unlit black on black).
- Idle-breathe animation only (no turntable spin in V1; a drag-to-rotate is a nice-to-have pin).
- Budget guardrails: the rig renders ONLY while the panel is open (camera disabled on close),
  RT sized modestly (~512), no post; pooled/single instance (§2b.1 one-owner).
- Reuse first: `GearIconRenderer` already photographs prefabs — mirror its lighting recipe;
  check for any existing paperdoll rig before building (reuse-not-reinvent gate).

## C. Consumable hot-swap belt (owner: "add to hot swap potions and mana potions")

- **The belt = two quick-slots (A/B)** surfaced on the combat HUD's existing consumable
  affordance (the v8 HUD already renders a potion tile — extend that seam to two assignable
  tiles), default expectation: A = health potion, B = mana potion, but ANY consumable may sit
  in either (data-driven, no hardcoded potion types).
- **Assign flow (in this panel):** selecting a consumable adds an "ASSIGN TO BELT" action next
  to Use on the detail card → tap A or B chip → assigned; the grid tile gains a small A/B badge;
  re-assign replaces with a toast. One action = one button per state (Use / Assign / Unassign).
- **Combat use:** belt tiles show icon + remaining count, consume through the SAME use path as
  the panel's Use (one owner for consume/cooldown; per-item cooldown chip on the tile).
- **Persistence:** two itemId fields, additive save-schema bump + default-empty on read (the
  vNN precedent); survives reload; migrated saves start unassigned.
- Cross-links: potion art `potion_health`/`potion_mana` are mirrored; WO-709's panel + WO-681
  card stay untouched; the skill-tree quick-swap (1-4 abilities) is a SEPARATE system — do not
  merge them, the belt is consumables-only.

## D. The VIEW GEAR pop-out (EquipmentPanel — owner 2026-07-13: "not clear, needs a better
## visual to match"; wireframe shown in-chat, pending her approval)

**⛔ OBSIDIAN COMPLIANCE IS BINDING (owner 2026-07-13: "MUST comply with Obsidian UI design —
reference UI skills from Blink architecture").** No hand-rolled chrome anywhere on this panel.
The exact Obsidian/Blink seams to use (per `docs/MASTER_CATALOG/BLINK.md` + UI_BLINK_TEMPLATE_CANON):

- **Frame: `BuildObsidianPanel(frameName: RpgUiCatalog.FrameCharacter)`** — the Stats_Panel
  frame is DESIGNED for this screen: measured `ZonesFor(FrameCharacter)` already carves the
  medallion socket, header band, the PORTRAIT ARCH region, body well, and footer (ElarionUiKit
  zone table). The render window seats in the portrait-arch zone — never a hand-placed rect.
- **Slot cards:** `RpgUiCatalog.Get(RoleSlot, …)` sprites — `slot_armor` / `slot_armor_2`
  (armor+shield), `slot_item` (weapon), `slot_socket`/enchant plates (amulet/ring), rarity
  rims `rarity_1..5` by item rarity. Sliced via the kit (`Slot()`/`Card()` builders —
  BLINK.md: "RpgUiCatalog + ElarionUiKit ARE the helper; don't duplicate").
- **Empty paperdoll fallback:** the mirrored `sil_male` silhouette sprite behind the render —
  if the RT rig ever fails, the Obsidian silhouette shows (null-safe-by-construction law;
  a screen can NEVER blank).
- **Type + trim:** kit fonts (font_title/body/stamp from Fonts_Obsidian mirrors) + ElarionUi
  color constants only — zero hex literals in the View.
- **Sprite-first, always:** every lookup through RpgUiCatalog with the procedural fallback;
  never conditioned on ff.blinkchrome (§5 canon — the flag hides OUR chrome, never withholds
  Obsidian art). Canon §7 screenshot-verify against `Stats_Panel.png` + the approved wireframe.

**PACK REFERENCE LAYOUT (owner-supplied 2026-07-13 — the Obsidian pack's own CHARACTER screen
from its store gallery is the composition authority for this panel, over my wide-card wireframe):**
- **Compact paperdoll:** the hero render/silhouette CENTER with TIGHT square slot cells flanking
  in two columns (armor column left, weapon/accessory column right, weapon row at the base) —
  the pack's slot-square grammar, NOT wide labeled cards. Slot names/grants appear in a detail
  strip on select (WO-683 grammar), not inline labels — that's how the pack keeps it clean.
- **Rarity = the pack's ornate colored slot FRAMES** (`rarity_1..5` mirrors) around filled cells.
- **Header = the pack's title-bar grammar:** round medallion glyph left + panel name; our ONE
  shared Close stays per master-frame canon (the pack's top-right X is superseded by our
  close-band convention — canon wins on close placement).
- **Action bar:** the pack's single bottom action button grammar (their green CONFIRM band) maps
  to our gold kit button — one action, bottom band.
- The owner's gallery screenshots also confirm the pack grammar for stat grants (green gains /
  red costs in Enchanting) — the slot detail strip uses that read (glyph + text, colorblind law).

**Cross-WO note (same gallery):** the pack's TALENT TREE reference (icon-only nodes, orange
live connectors, x/y rank pips, rarity frames, bottom CONFIRM) independently confirms the
WO-676 skill-tree redesign; its CRAFTING reference confirms the WO-683/FrameCrafting grammar.
CLI: treat the pack gallery as the per-panel composition reference library.

**SOURCE VERIFIED (owner 2026-07-13):** the pack IS Obsidian UI, Asset Store id **206302**
(https://assetstore.unity.com/packages/2d/gui/obsidian-ui-rpg-mmorpg-arpg-206302), owned
locally at `Assets/Blink/Art/UI/Obsidian_UI/` — this resolves BLINK.md's "store id
unverifiable" flag (CLI: update that line in BLINK.md in the same breath, §15). Implication:
every gallery composition above exists as OWNED sprites on disk — any piece this WO needs
that isn't yet mirrored (socketing plates, loot rows, merchant rows, the Character screen's
slot squares) is one `BlinkUiImporter` BuildTable row + re-run away (gitignored source →
committed `Resources/RpgUi` mirror, the §5 pipeline). Never rebuild what the pack ships.

Same conformance family, SAME shared assets — one lane, two consumers:

- **Layout = the classic paperdoll:** BIG lit render window CENTER (the §B rig, same instance,
  larger RT framing — one owner, two consumers per §2b.1); equipment slot CARDS flanking —
  left: Armor (full set) + Shield; right: Weapon + Amulet + Ring. Every card is a kit slot
  plate (slot_armor/slot_item mirrors) with its label INSIDE the card (the current floating
  labels collide: "Shield (Off Hand)" overlaps "Ironward Plate", "Amulet" overlaps
  "Squire's Blade" — the §1.14 fit-never-spill class).
- **Each filled card shows the VALUE:** item icon + name + its one-line grant ("+25% block",
  "+35 HP · +7% defense" — from the defs, the WO-683 BESTOWS read; gear must state why it
  matters). EMPTY slots = dashed plate + a pointer, not a bare "Empty": "Empty — craft at the
  Jeweler" (routes the player to the system that fills it).
- **Footer stat strip:** the hero's effective totals (HP / DEF / ATK / BLOCK) — the panel's
  payoff line; values from the same modifier sums combat uses (one source).
- Header carries name + class/LV badge (title zone); ONE shared Close; Orient dev-gated;
  the dead upper half of the current layout dies — the frame body hugs the content.
- "Drag to turn" on the big render = the §B nice-to-have promoted HERE (this is the panel
  where admiring the Knight is the point); still idle-breathe only if drag slips the slice.
- File: `Assets/_Modules/Village/Hero/EquipmentPanel.cs` (View-only; equip logic untouched).

## Acceptance
- [ ] Panel matches the approved wireframe + `Inventory_Panel.png` (canon §7 screenshot verify).
- [ ] Five uniform tabs, no truncation at desktop + phone aspect; selected state reads sans color.
- [ ] LIVE hero render visible + lit, reflects equipped gear, costs nothing while the panel is
      closed (profile line in the RESULT).
- [ ] Assign a health potion to A + mana potion to B → both usable in combat with counts;
      consume + cooldown correct; survives reload; unassign works.
- [ ] Footer = standard chips, channel-skinned wallet; no Orient in player builds; no banner
      bleed; "Used X" is a toast; zero raw itemIds player-visible.
- [ ] COMPILE_GATE_OK + fleet panel probes green + save round-trip regression (new fields) +
      owner felt-pass on exe (PO closes).

## What NOT to touch
Item/equip logic beyond the two belt fields · GearLoadout · the ability quick-swap (1-4) ·
PanelManager semantics · other panels (grammar spreads on-touch).

*Cross-refs:* `docs/UI_BLINK_TEMPLATE_CANON.md` · `docs/MASTER_CATALOG/BLINK.md` ·
WO-675/683 (grammar) · WO-697 (CompactNumber) · combat HUD v8 potion tile seam ·
`GearIconRenderer` (lighting recipe) · skin service (wallet chip).

## Owner ruling appended (2026-07-13 late): NO Pi symbol on the inventory currency
Verbatim: "we need to remove the Pi symbol on inventory screen for SKR, maybe leave generic as
wallet." The inventory's currency row must NOT render the Pi glyph/skin — present a GENERIC
WALLET (icon + plain amount) instead. Mechanism exists: CurrencySkinResolver.Active drives the
symbol (InventoryUIBuilder.cs:134-136 reads the active skin, never hardcoded) — add/select a
generic "wallet" skin for V1 (canon: V1 ships ZERO crypto; Pi/SKR skins return when that arc
does). Same colorblind/ASCII laws as everything.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
