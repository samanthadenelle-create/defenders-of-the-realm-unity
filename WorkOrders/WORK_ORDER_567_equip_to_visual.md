# WORK ORDER 567 — Equip → Visual (weapon + shield flair + armor-tint)

**Status:** IMPLEMENTED (edit-only worktree; NOT gated/committed — orchestrator batch-gates)
**Date:** 2026-06-28
**Silo:** Combat/AI + UI (Equip → Hero visual)
**Canon:** combat-pivot single-hero north star — ONE static Tripo hero model, **static armor**,
the visible flair is **weapon + shield**. Blink armor mesh-swap is **JUNKED**. Armor "shows" via a
**material TINT/accent** on the static model, NOT a mesh swap.

---

## RCA — current equip → visual pipeline (verified from code)

| Slot | Path | Status |
|---|---|---|
| **Weapon** | `GearLoadout.EquippedWeapon` → `OnGearChanged` → `EquipmentController.EquipBestForHero` → `Equip(def)` → Resources/Addressable load → `AttachLoadedProp` (NormalizeInto + SeatByHandle + rig-axis grip) | **WORKS (WO-551).** Equipping a different weapon shows its real KayKit mesh on the RightHand. |
| **Shield / off-hand** | `GearLoadout.EquippedOffHand` → `EquipOffHand(def)` → `AttachOffHandProp` (LeftHand, native/normalize seat) | **WORKS.** Equipping a shield shows its mesh on the LeftHand. |
| **Armor** | `GearLoadout.ApplyStats` → `PushArmorTierToBody` → `EquipmentController.SetArmorTier(tier)` | **WAS DEAD** — `SetArmorTier` (EquipmentController.cs ~:1458) only stored `_armorTier`, body unchanged. This is the gap "equipping armor never changes the hero look". |
| **Armor rim glow** | `GearLoadout.ApplyStats` → `HeroArmorRimLight.Refresh` → `ArmorVfxMap.Resolve` → MPB `_EmissionColor` | Already works for **rare+** rarity (common = off). Emission GLOW only; no albedo read for low tiers. |

So: **weapon + shield flair already fully works** (the expected outcome). Only the **armor body read** was dead.

**Gear Preview (WO-543 / WO-434):** `EquipmentPanel` → `HeroPreviewViewer` clones the body + adds its own
`EquipmentController`, but only mirrored the **weapon** (`RefreshWeapon`) — shield + armor were NOT shown in the preview.

---

## What was implemented

### 1. Armor = static-model TINT (no mesh swap, no Blink) — `EquipmentController.cs`
- `SetArmorTier(int)` now applies a **per-tier base-color accent** to the hero BODY's
  `SkinnedMeshRenderer`s via a **`MaterialPropertyBlock`** (`ApplyArmorTint` / `ResolveBodyRenderers`).
- **Leak-free:** MPB never instances a material (same technique as `HeroArmorRimLight`).
- **No clobber:** uses the `GetPropertyBlock`-merge pattern, so it COEXISTS with `HeroArmorRimLight`'s
  `_EmissionColor` set — base-color tint + rarity rim glow stack.
- **Multiply, not wipe:** captures each renderer's authored `_BaseColor` once and multiplies by the
  tier accent, so **tier 0 restores the original exactly** (won't blow out a baked tint).
- **Body only:** targets `SkinnedMeshRenderer` (the character) — weapon/shield `MeshRenderer` props are never tinted.
- **Timing-robust:** if the body isn't built when an early `SetArmorTier` fires, it stays `_armorTintDirty`
  and re-applies in `Update` once the renderers come online.
- Tier→accent table (`ArmorTintByTier`) = **owner-tunable BONES** (steel → blue → violet → gold, tracking ArmorVfxMap bands).
- Added `EquipOffHand(string)` convenience overload (resolves the shield def from `GearCatalog`) for the preview.

### 2. Gear Preview mirrors the full look — `HeroPreviewViewer.cs` + `EquipmentPanel.cs`
- `HeroPreviewViewer.Begin/Retarget/AttachWeaponDriver` extended with `offHandId` + `armorTier`;
  new `RefreshGear(weaponId, offHandId, armorTier)` drives weapon + shield + armor tint then repaints once.
- `EquipmentPanel`: new `ActiveOffHandId()` / `ActiveArmorTier()` helpers; `BeginOrRetargetPreview` +
  `RefreshPreviewWeapon` now feed the full gear so the showcase reflects weapon + shield + armor tint.

### 3. One tier mapping — `GearLoadout.cs`
- `ArmorVisualTier(ArmorDef)` made **public static** so the preview uses the SAME rarity→tier map the world hero uses (no divergence).

### 4. Canon — `docs/MASTER_CATALOG/village-hero.md`
- Updated the `SetArmorTier` entry (was "STUB, no-op") to the WO-567 tint behaviour; noted `EquipOffHand`.

---

## Guarantees / acceptance
- **NO mesh swap, NO Blink revival** — `HeroArmorVisual` (the junked Blink swap) is untouched and stays inert (`ff.blinkarmor` OFF).
- **No material leak** — MPB only; zero `new Material(...)` added.
- **WO-551 weapon seating** untouched (no edits to the seat/grip path).
- **WO-543 Gear Preview / rim light** preserved — tint composes with the rim glow via MPB merge.

## Files modified (for reconcile)
- `Assets/_Modules/Village/Hero/EquipmentController.cs`
- `Assets/_Modules/Village/Hero/HeroPreviewViewer.cs`
- `Assets/_Modules/Village/Hero/EquipmentPanel.cs`
- `Assets/_Modules/Village/Hero/GearLoadout.cs`
- `docs/MASTER_CATALOG/village-hero.md`

## Brace checks
All balanced: EquipmentController 300/300, GearLoadout 105/105, HeroPreviewViewer 27/27, EquipmentPanel 82/82.

## OWNER-DECISION FLAGS
1. **Tint mapping (`ArmorTintByTier`)** — the per-tier accent colors/strengths are BONES; felt-tune (currently gentle so skin/face don't discolor hard). Confirm the hues read as "armor sheen" vs the rim glow.
2. **Tint vs rim overlap** — armor now reads via BOTH an albedo tint (all tiers ≥1) and the existing emission rim (rare+). If that's too much for low tiers, drop the tier-1/2 tint strengths toward identity.
