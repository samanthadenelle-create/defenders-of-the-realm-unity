> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: TroopController.cs has zero matches for VFXManager, VfxPool, or ProjectileVFXCatalog.
> The previous Status line read "READY - child of WO-872." and was wrong.

# WORK ORDER 876 — Troop combat VFX: on-hit impact, death, + ranged archer projectile

**Status:** NOT STARTED (reconciled 2026-08-08) — child of WO-872. **Lane:** Combat/AI VFX. **WO#:** UI-seat block; **876**.
**Origin:** owner 2026-08-04 — *"on hit with troops."* Audit-backed (WO-872 §2, Tr1–Tr4). **Layer:** B/C.

## 1. Gaps (audit, `TroopController.cs`)
Troops share NONE of the tower/enemy VFX stack:
- **Tr1 — melee troop lands a hit: NO impact VFX** (`:501-508`). **Tr3 — takes a hit: NONE** (`:519-526`).
  **Tr4 — death: NONE** (`:536-543`). Animator triggers (`Attack/Hit/Dead`) exist, but no VFX.
- **Tr2 — Archer troop "fires" with NO projectile and NO muzzle** — instant damage at range 14 (`:455-466`).

## 2. Fix
- Add **on-hit impact** (Tr1) + **take-hit** (Tr3) + **death** (Tr4) VFX by reusing the SAME stack the towers/enemies
  use — `VFXManager.Play(Impact_*)` / `VfxPool.SpawnHitImpact` / a death burst — at the troop combat hooks.
- Give the **Archer troop a real projectile** (Tr2): spawn a flying arrow via `ProjectileVFXCatalog.SpawnFlying`
  (the same body the hero Ranger + towers use) + a muzzle, resolving on arrival instead of instant damage.
- Route via `VFXManager`/`ProjectileVFXCatalog`; owner-tags any missing key / CLI maps verbatim; WO-753 teardown.

## 3. Acceptance
- [ ] On-device: melee troops show an impact when they connect + a hit/death VFX; the Archer troop fires a visible
      arrow (muzzle → flight → impact), not instant damage. `CompileGate` green.

## 4. Do NOT
- Author no new VFX (the impact/projectile stack exists). No raw Instantiate. WO-872 §4 rules.
