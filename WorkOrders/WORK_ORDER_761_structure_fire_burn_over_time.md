# WORK ORDER 761 — Fire leaves a lingering BURN on structures until repaired

**Status:** SPEC — READY (owner-requested 2026-07-24, design idea)
**Lane:** Combat/AI + Structures + Repair loop (pairs with WO-757 dragon breath cone)
**Owner intent (verbatim):** "the fire damages would be nice to continue burn over time till repaired."

---

## 0. The idea (owner-refined 2026-07-24)

When fire brings a structure to **50% damage (≤50% HP)**, it **catches fire and keeps taking damage over time until it is REPAIRED (extinguished) or DESTROYED.** Not every fire hit burns forever — the lingering burn only kicks in once a structure is **critically damaged**, which is cleaner + more balanced. This turns fire into a lingering threat on wounded structures and wires the dragon's attack directly into the repair loop: a burning tower keeps dropping until you rush to save it (fight the dragon, or repair your towers).

**Trigger:** structure crosses to ≤50% HP from fire damage → ignite. **Ends:** repair (extinguish) OR destroy. Burn does NOT self-expire — only repair or death stops it (per owner "till repaired or destroyed").

## 1. Current state (code-verified 2026-07-24)

- The dragon's fire attack (`DragonBoss.FireAtTowerCore:826`, `DealStrike:1224`) applies **instant `ApplyContactDamage`** + **tries `StatusEffect.Burn`** — but the burn **NO-OPS on towers**: `CombatStatusTracker` / `StatusEffect.Burn` operate on `IDamageable`, while towers are `IDamageableStructure`. So structures never actually burn over time. (RCA flag: "TryApplyBurn guarded - no-op for towers today.")
- Repair exists: `WallRepairController` / the hub repair affordance + `IDamageableStructure` repair path.
- Fire VFX routes through `VFXManager` (one pool).

## 2. The mechanic to build

1. **Structure burn state:** a `StructureBurn` capability/component (or extend the structure damage model) that, once ignited, **ticks damage** to the structure on an interval (e.g. N dmg/sec) for as long as it's burning. One owner per structure (pool the fire VFX; the two-VFX-stack scar).
2. **Ignite:** a fire source (dragon breath, WO-757 cone, fire weapon on a structure) sets the structure burning. Stacking rule = owner ruling (refresh duration vs stack intensity; recommend REFRESH, capped).
3. **Extinguish = REPAIR:** repairing the structure puts the fire out (clears the burn state + stops the tick + stops the fire VFX). Optionally: burn also self-expires after T seconds if the structure survives (owner ruling — "till repaired" suggests it does NOT self-expire, only repair stops it; confirm).
4. **Visual:** a looping fire VFX on the burning structure (via `VFXManager`, handle-managed, stopped on extinguish/destroy) so the player SEES which structures are on fire — colorblind-safe (flame shape/motion, not hue alone) + a HUD/worldspace "burning" tell.
5. **Death:** if burn damage destroys the structure, the `Destructible` lifecycle (WO-753: no-rebuild + full-cost + VFX cleanup) fires; the fire VFX tears down with it.

## 3. Seams to reuse (don't greenfield)
- `IDamageableStructure` (the structure damage seam) — extend to carry/tick a burn, OR a sibling `StructureBurn` component that calls `ApplyContactDamage` on a tick.
- `StatusEffect.Burn` / `CombatStatusTracker` — the existing burn model (currently `IDamageable`-only); either bridge structures into it or mirror its tick for structures.
- `VFXManager.PlayKey` loop handle for the on-structure fire (one pool).
- The repair path (`WallRepairController` / repair affordance) — hook "on repaired -> extinguish".
- `Destructible` (WO-753) — death cleanup.

## 4. Acceptance criteria
- [ ] A fire hit on a tower/structure sets it BURNING; it ticks damage over time.
- [ ] Burning is VISIBLE (looping fire VFX on the structure, colorblind-safe tell).
- [ ] **Repairing the structure EXTINGUISHES the fire** (burn cleared, tick stopped, VFX stopped).
- [ ] Burn damage can destroy the structure -> `Destructible` cleanup fires, fire VFX torn down (no leak, no orphaned loop — the loop-cap starvation class).
- [ ] Fire VFX pooled, ONE owner (no second VFX stack).
- [ ] Non-fire damage unaffected; structures with no fire behave as before.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`; a regression asserts ignite->tick->repair-extinguish.

## 5. Owner decisions to confirm
- Burn tick rate + total (balance).
- Does burn self-expire, or ONLY repair stops it? (owner's "till repaired" implies repair-only — confirm.)
- Stacking: refresh vs intensity.
- Which fire sources ignite structures: dragon breath only, or also the fire weapon / any fire? (recommend: any fire damage to a structure.)
- Pairs with WO-757 (fire-does-hit breath cone) — build together or burn-DoT first?

## 6. Notes
- This is a FOLLOW to the current test build (a mechanic, not a tweak) — not deployed in the 2026-07-24 test build.
- Natural pairing: WO-757 (the breath cone that does the hit) + WO-761 (the fire it leaves behind) = "fire is a real, lingering weapon."
