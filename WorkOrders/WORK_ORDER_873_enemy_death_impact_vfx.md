> ## RECONCILED 2026-08-08 - true status is SUPERSEDED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: superseded by WO-886; commit 29f9ac2b shipped the same artifacts (Enemy.cs:2846 SpeciesDeathVfx, plus the Death_Generic / Death_Brute / Death_Tiefling prefabs).
> The previous Status line read "READY - child of WO-872." and was wrong.

# WORK ORDER 873 — Enemy death + melee-impact VFX (highest player-felt)

**Status:** SUPERSEDED by WO-886 (reconciled 2026-08-08) — child of WO-872. **Lane:** Combat/AI VFX. **WO#:** UI-seat block; **873**.
**Origin:** owner 2026-08-04 VFX pass. Audit-backed (WO-872 §2). **Layer:** A/B.

## 1. Gaps (audit, `Enemy.cs`)
- **E1 — regular enemy death is a generic grey burst.** `VfxPool.SpawnDeathBurst` (`Enemy.cs:2547`) fires one
  species-agnostic burst; the per-species `Death_Skeleton/Wolf/Tiefling/Brute` VFX only fire if a prefab-authored
  `_deathVFXOverride`/`EnemyTypeVfxSet` exists — and the pool/factory spawn path sets NEITHER, so it never runs.
- **E2 — enemy melee hit lands with ZERO impact VFX.** Only a pre-swing ground telegraph exists
  (`Impact_ShockwaveRing`, `Enemy.cs:1507`); nothing plays when the blow actually connects (`Enemy.cs:1554`).

## 2. Fix
- Wire `Enemy.Die()` to pick the **per-species** `Death_*` key (from the enemy's catalog species/type) — reuse the
  existing `Death_Skeleton/Wolf/Tiefling/…` keys; fall back to the generic burst only when a species has none.
- Add an **on-landing melee impact** at `Enemy.cs:1554` (reuse `VfxPool.SpawnHitImpact` / `Impact_Physical` — the same
  stack the "enemy takes a hit" path E4 already uses).
- Route via `VFXManager`; owner-tags any missing species key / CLI maps verbatim; WO-753 teardown.

## 3. Acceptance
- [ ] On-device: enemies die with a species-appropriate death VFX (not one grey burst); a melee hit that connects
      shows an impact. `CompileGate` green.

## 4. Do NOT
- Author no new VFX (species `Death_*` keys exist). No raw Instantiate. WO-872 §4 rules apply.
