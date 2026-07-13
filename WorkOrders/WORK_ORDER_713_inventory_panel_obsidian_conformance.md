# WORK ORDER 713 — Inventory panel: Obsidian conformance, hero render window, consumable hot-swap belt

**Status: READY TO IMPLEMENT — layout wireframe awaiting owner approval in-chat** (owner asks
2026-07-13: "clean the layout so it flows better — refer to Obsidian SME + Blink architecture" +
"a better render window" + "consumables add to hot-swap potions and mana potions").
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
