# WORK ORDER 331 — Inventory / Character / Equipment screen

**Status: SPEC (design-driven).** **Lane:** 4 (UI/HUD). **Reference concept:**
`docs/design/inventory-screen-concept.png` (owner-provided 2026-06-07). Same classic-RPG
visual language as WO-307 (HUD overhaul) — build them coherently.

## Layout (from the concept)
A single full-screen modal, four regions:
1. **Inventory (left):** scrollable item grid + "Capacity X/Y" header. Slots show item icon +
   (stack count). Click → details / equip / use.
2. **Character Status (center):** hero portrait + HP (red) and mana/XP (blue) bars, ornate frame.
3. **Equipment (right):** a paper-doll with slots **Head / Chest / Hands / Legs / Weapon / Shield**
   (+ portrait), plus a **Character Attributes** list — **STR / DEX / INT / VIT** (derived from base
   hero stats + equipped gear; concept shows 45/30/50/40).
4. **Abilities (bottom) — a configurable LOADOUT, not just a display** (owner 2026-06-07):
   each **Q/W/E/R slot is assignable** — tap a slot → pick from the player's **talent-tree-unlocked
   abilities** → the slot binds that ability. Show each ability's **description** (already in
   `abilities.json`, e.g. "Meteor Strike — 160 dmg over a 9m blast") so the player knows exactly what
   it does. Bonus: descriptions directly mitigate the "same-animation / unclear-action" readability gap.
   Source the unlocked pool from the talent system (`HeroTalentPanel` / talent tree); persist the chosen
   loadout (GameState). Same Q/W/E/R mapping as the HUD skill bar. Default pool incl.
   Fireball · Frostbolt · Heal · Stun · Buff · Dash · Defense · Execute · Meteor.

## Backing systems (mostly exist — wire, don't greenfield)
- Inventory grid ← `VillageInventory` / `GameState.OwnedItemIds`. ⚠ `VillageInventory.CanCraft/TryCraft`
  are stubs (no real consume) — inventory READ is fine; item use/consume needs that plumbing (coord WO-293).
- Equipment slots + equip/unequip ← `GearLoadout` (`EquipWeaponById`/`EquipArmorById`) + `GearCatalog`
  (weapon/armor defs + stats). The ShopPanel EQUIP flow already drives these.
- Attributes (STR/AGI/INT…) ← summed from equipped `GearDef` stats (+ base hero stats).
- Character status bars ← `HeroHealth` (HP) + `HeroAbilities` (mana).
- Abilities bar ← `HeroAbilities` slots (same source as the HUD skill bar).

## Build approach
- **Code-built UI only** (project rule: UXML does NOT render in builds). Reuse the HUD's code-built
  panel/style helpers so the inventory + HUD share one look.
- **Phase 1 (functional):** clean stylized layout wired to the real systems — grid, paper-doll equip
  (drag or click-to-equip), live attributes, status bars, ability row. Playable + correct.
- **Phase 2 (art polish):** ornate gold frames / slot textures / parchment bg to match the concept
  (needs frame art assets; until then a clean flat-stylized skin).
- **Always-accessible MENU** (owner 2026-06-07): the player's "manage my hero" home base — openable
  ANYTIME via a HUD button + hotkey (e.g. `I`/`Tab`), mobile-safe touch targets. Keeps the in-world HUD
  lean (deep inventory/equip/loadout lives here, not on the play screen). One cohesive screen, not scattered panels.
- **PAUSES THE WORLD + DOUBLES AS THE IN-GAME PAUSE (Zelda/WoW/CoC style — owner decision 2026-06-07):**
  this menu IS the pause screen. `Time.timeScale=0` while open; restore on close (finally-guarded).
  **Mobile-critical:** the player must be able to pause at a moment's notice (phone interruptions) — so the
  open button is prominent/always-there and pauses INSTANTLY. Also **auto-pause on app-background**
  (`OnApplicationPause(true)` / focus loss) so an interruption never costs a wave. One button = pause + manage.
  (Single-player → pause-to-dodge exploit is a non-issue now; gate mid-combat later if ever competitive.)

## Notes
- Coheres with WO-307 (HUD overhaul) + the ShopPanel equip flow — share components, don't duplicate.
- Reference image preserved at `docs/design/inventory-screen-concept.png` (converted from owner's .avif).
- Local WO; next free 332.
