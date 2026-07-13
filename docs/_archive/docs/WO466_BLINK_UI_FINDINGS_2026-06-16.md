# WO-466 "Blink + UI" — Findings & Execution Plan (2026-06-16)

Distilled from a full read of the docs + Blink pack on disk + the store/inventory UI code,
before writing any code (per the research-first directive). Feeds WO-466.

## TL;DR (the reframe)
**The store/inventory UI is NOT broken.** Its icons resolve from **8 committed icon sheets**
(`Assets/Resources/ItemIcons/*.jpg`) and every missing case falls back to the committed
`Resources/RpgUi/*` slice, then a procedural glyph. There is **no systemic blank-white-square
bug** — null icons are either hidden or fall back. So "make the store show real icons" is
**largely already true** for weapons/armor/potions via the sheets.

**Blink's real value is NOT flat store icons.** Blink ships:
- **400 weapon PREFABS** (`Assets/Blink/Art/Weapons/.../_Prefabs_MWP1/`, 16 types ×25) — 3D models
- **292 armor/character PREFABS** (`Assets/Blink/Art/Characters/Stylized|LowPoly/`) — armor as
  **togglable child meshes** (`ClothSet*_Chest/Helmet/...`) on a shared Humanoid skeleton
- **43 Humanoid animation FBX** (`Art/Animations/{Combat,Movement,Gathering}`)
- **623 flat PNG icons** — but these are **class/ability art** (Rogue1–20, Pyromancer1–20,
  emblems, slot frames), **not** depictions of "a longsword / iron helm." A poor match for gear
  item icons; better suited to ability/spell-slot art if used at all.

So the high-value "Blink + UI" work = **equip-on-hero (attach the 3D weapon/armor)** and
**animation**, both per WO-466 — NOT swapping the (already-working) flat store icons.

## Hard constraints found
1. **Blink is gitignored and NOT under any `Resources/` folder** → runtime `Resources.Load`
   cannot see it. Any Blink asset used at runtime must have its **used slice copied into a
   `Resources/` folder and committed** (per asset policy). Nothing references Blink in code today.
2. **The referenced audit docs do not exist yet**: `ASSET_PACK_CATALOG_2026-06-16.md`,
   `ASSET_USAGE_AUDIT_2026-06-16.md`, `CLEAN_BASELINE_AND_ASSET_HYGIENE.md`. The "used closure"
   audit WO-466 depends on has **not** been done.
3. **Most of WO-466 needs the Unity editor** (import the slice, build animators, render gear
   previews, verify socket attach). It could not run this session (editor was importing — license lock).

## Code map (verified, file:line)
- `Assets/_Modules/Village/Hero/ItemIconCatalog.cs` — `ForWeapon`/`ForArmor`/`ForConsumable`
  (~L57–92) resolve id/keyword+tier → sprite from `Resources/ItemIcons/<sheet>`; null → caller glyph.
- `GearCatalog.cs` — `WeaponDef.icon` / `ArmorDef.icon` string fields exist but are **UNUSED**
  (verified: not read in ItemIconCatalog or ShopPanel). **This is the clean routing hook.**
- `ShopPanel.cs` — list rows fall back `ItemIconCatalog → RpgUiCatalog` (≈L632–650). **Detail pane
  (≈L413–423) sets `sprite=icon; enabled=icon!=null` → it HIDES on null instead of falling back
  like the rows do.** Minor inconsistency; candidate micro-fix.
- Tech-hud `Resources.Load("Tech hud elements/…")` (InventoryGrid/PaperDoll/UIBuilder + ElarionUiKit)
  — 8 sites, all already fall back to `RpgUiCatalog` (commit `e8cea2a`). **No clean-build null risk.**
- `EquipmentController.cs` — already attaches **KayKit** weapon meshes to the hero rig from
  `Resources/Heroes/Props/Weapons/<id>.fbx`. **This is the existing socket-attach path to extend for Blink.**

## Side-bug surfaced (data-only, not UI)
`weapons.json` aegis weapons have **no `setId`** (only `armor.aegis_plate` does) →
`GearLoadout.AegisSetActive` can never be true → the Oathweld full-set ward/perk is unreachable.
Fix = add `"setId":"aegis"` to the 4 aegis weapons in **both** canonical copies. Confirm with owner.

## Execution plan (3 tracks)
### Track A — Animation (TOP priority; code already landed in `923e390`)
- The hero-animator "beast" is committed; it needs **`Defenders → Animation → Build Hero Animators
  (Mixamo)`** run in-editor + a recompile, then playtest Q/W/E/R per class.
- Optionally fold in Blink's 43 clips later (they're Humanoid; retarget like Action/Mixamo).
- **Blocked only on: Unity editor free.** No new code needed first.

### Track B — Equip-on-hero (the real "Blink + UI" win) — needs Unity + a decision
1. Pick the **used weapon slice** (e.g., one prefab per gear archetype the shop sells) → copy into
   `Assets/Resources/Heroes/Props/Weapons/` (the path `EquipmentController` already loads) → commit slice.
2. Extend `GearLoadout.EquipWeaponById` to attach via the existing `EquipmentController` socket +
   `WeaponOrientHelper` (grip). Armor = toggle `ClothSet*` children if we adopt the Blink character body.
3. Verify grip/orientation in editor.

### Track C — Store icons (LOW priority; mostly already works)
- Leave the committed `ItemIcons` sheets as the source. **Do not** force Blink class-icons in as gear icons.
- Optional polish: route the unused `GearCatalog.icon` field as an explicit override
  (`ItemIconCatalog` checks `def.icon` first → a small `Resources/` sprite → else existing keyword logic).
  Purely additive, backward-compatible, WebGL-safe. Only worth it if we author specific overrides.
- Optional micro-fix: make `ShopPanel` detail pane fall back to `RpgUiCatalog` like the rows (no empty preview).

## The one decision needed from owner
For Track B: **which weapon/armor archetypes** should map to **which Blink prefabs** (the used slice)?
That's a creative/curation call (owner owns creative). Once chosen, the rest is mechanical.

## What was explicitly NOT done (and why)
- No code written: everything material needs the editor to compile-gate + verify, which wasn't
  available. Per "don't patch & claim fixed," I won't ship ungated edits.
- No Blink slice committed: the used-closure audit isn't done; committing now would violate asset policy.
