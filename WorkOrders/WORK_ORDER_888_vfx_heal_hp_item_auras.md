# WORK ORDER 888 — VFX: heal + HP-state auras + item auras (colourblind accessibility fix)

**Status:** READY TO IMPLEMENT · **Silo:** Hero/VFX/accessibility · **For:** CLAUDE CLI · **Date:** 2026-08-05
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` (Step 1–8) · `VFX_CREATIVE_PICKS_REGISTRY.md` §6a/6b/6c. Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform. **May promote before WO-886 (Death) — this fixes a real accessibility bug.**

## Scope
Three things, all reading by **rising/pulsing SHAPE + motion**, never colour (owner is red/green colourblind):
1. Heal moments (cast / contact / regen / buff).
2. HP-state world auras — the PRIMARY low-HP read (today's low-HP tell is a **red vignette only = invisible to the owner**; fix that).
3. Item-granted persistent auras (heal relic + elemental weapon glows).

## Recipes (registry §6a/6b/6c)
| Moment (VFXType) | Recipe | Family | Trigger |
|---|---|---|---|
| Cast_Heal | RisingSteam column | A loop→Stop | heal cast |
| Impact_Heal | FireFlies upward burst | B | heal contact |
| Aura_HealingInProgress | RisingSteam low | A loop | `HeroHealth.RegenTick` L1107 |
| Aura_LowHealth | SmokeEffect guttering, pulse ↑ as HP↓ | A loop | `UpdateInjuredState` L1171 (<0.30) |
| Aura_NearDeath | TinyFlames fast gutter | A loop | <0.25 (AegisAutoThreshold L76) |
| Aura_ItemHeal | RisingSteam low held | A loop | equip (GearVisualApplier L41) |
| Fire/Frost/Arcane weapon aura | TinyFlames / DustMotes cold / ElectricalSparks (faint) | A loop | equip |

## Files to touch
- Builders: RisingSteam, FireFlies, SmokeEffect, TinyFlames, DustMotesEffect, HeatDistortion → `Resources/VFX/Aura/`.
- `VFXCatalogGenerator.cs` Map rows.
- `HeroHealth.cs` — `UpdateInjuredState` (L1166) drives the LowHealth/NearDeath emitter off the severity value; `RegenTick` (L1107) drives HealingInProgress; **demote `HeroInjuredVignette` to a SECONDARY channel** (world aura is primary).
- New **`GearAura`** held-loop component (mirror `ArcaneAura.Ensure/SetAuraKey` + `Pets/AuraController`), attached in `GearVisualApplier.Apply` (L41), keyed by item element, tiered by `GearProgression`; `Stop()` on unequip.

## Acceptance criteria
**Engineering:**
- [ ] Low-HP is legible with the red vignette DISABLED — the world aura (pulse-rate + guttering shape) carries the read.
- [ ] Aura_LowHealth pulse rate scales with severity (`InverseLerp(0.30,0,Fraction)`); NearDeath swaps in below 0.25.
- [ ] HP auras are mutually exclusive (one live at a time); `Stop()` when healed above threshold.
- [ ] `GearAura` holds while equipped, `Stop()` on unequip/teardown; no orphan loop.
- [ ] All loops share the budget correctly (no leak); `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] At low HP the owner can tell they're in danger WITHOUT relying on red — fast pulse + guttering flame reads urgent.
- [ ] Healing reads as "mending" by calm rising motion + the heal number (not green).
- [ ] Equipping a heal relic shows a soft persistent restoration aura; a fire weapon faintly smolders.
- [ ] Headless low-HP + heal + equipped-item screenshots opened.

## RESULT
`WorkOrders/WORK_ORDER_888_vfx_heal_hp_item_auras.RESULT.md`.
