> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Blink RPG Art Bundle — Quick Notes

The largest **gear** source we own — fantasy weapons + full-body character outfits.
This is the primary feed for the **weapon/gear collection** (`docs/ITEM_MODEL.md`).
Root: `Assets/Blink/`. **Gitignored** (like KayKit/polyperfect) — treat the on-disk
packs as the warehouse; they are NOT in `Resources` and NOT committed.

> Counts verified from the on-disk folder tree (2026-06-18). Stat/lore are NOT on the
> assets — the gear catalog authors those (rarity-templated + human), see ITEM_MODEL §5.

## What's in it (the gear)

### Weapons — `Assets/Blink/Art/Weapons/LowPoly/MegaWeaponPack1/`
**400 prefabs = 16 categories × 25 each** (+ ~405 source FBX in `Meshes_MWP1/`):
Axe1h, Axe2h, Sword1h, Sword2h, Dagger1h, Bow2h, Crossbow2h, Shield1h, Mace1h,
Polearm2h, Scythe2h, Hammer2h, Staff2h, Wand1h, SpellBook1h, Claws1h.
Example: `Meshes_MWP1/Sword1h_01.fbx … Sword1h_25.fbx`.

### Armor / outfits — `Assets/Blink/Art/Characters/` (Stylized + LowPoly)
**~290 prefabs** as **full-body outfit SETS** (each has a HumanMale + HumanFemale
variant) → in the item model these are **`Gear` entries with `slot = Body`** (full-body,
not per-slot — owner decision 2026-06-18). Cloth / Leather / Plate / Basic tiers + themed
named sets (Wolf, Stag, Centurion, LionGuard, PantherKnight, DragonHunter, DemonHunter,
Minotaur, Hydra, Dragonic, Bear, Boar, Savage, Engineer…). Example path:
`Stylized/Humans/ArmorPack1/Prefabs_ArmorPack1/Cloth1_1_1_HumanMale.prefab`.

## How to use from code (the gating fact)

- **NOT Resources-loadable.** Blink lives outside `Assets/Resources/**`, so a catalog
  `prefabPath` can't `Resources.Load` it and a fresh clone / WebGL build won't have it.
- **The right path is Addressables** (NOT a Resources mirror — that bloats the WebGL build
  we fight, WO-191/408). Addressables config already exists: `Assets/AddressableAssetsData/`.
  The gear catalog generator (`Assets/Editor/Catalog/GearCatalogGenerator.cs`, WO-Item-2)
  has an `IGearSource` seam: add a Blink/KayKit Addressables-backed source, regen → the
  collection fills with the real bundle, gated by the `DataRegression` item-model invariants.
- This is the **Addressables gear enabler** WO (ties to WO-470 Heroes→Addressables).

## Gotchas
- Gitignored → re-acquire on a fresh clone (owner-purchased; not redistributed in the repo).
- Material/shader: verify URP on import (low-poly atlas style, same class as KayKit/polyperfect
  — white if no `_BaseMap`, magenta if Built-in Standard). Confirm before mass-cataloguing.

## Sources
- On-disk: `Assets/Blink/Art/Weapons/LowPoly/MegaWeaponPack1/`, `Assets/Blink/Art/Characters/`
- `docs/ITEM_MODEL.md` (the gear/weapon model this feeds), `docs/GEAR_GENERATOR_COVERAGE.md`
- Owner-confirmed owned 2026-06-18 (with KayKit Complete + Quaternius MegaKit).
