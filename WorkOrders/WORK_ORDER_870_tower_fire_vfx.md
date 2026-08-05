# WORK ORDER 870 — Tower VFX: cast → projectile → impact, by TYPE × TIER (+ fix Aether/Fire mismatch)

**Status:** READY TO IMPLEMENT — child of WO-872 (VFX pass master).
**Author:** UI/QA triage + audit (read-only, §13) — Claude UI
**Lane:** Buildings/VFX. **WO#:** UI-seat block; **870**=this.
**Origin:** owner 2026-08-04 — *"the fire from the towers is horrible … from casting to projectiles from towers by
type (archer / arcane / ballista) and level (L1 → L2 → L3)."* Audit-backed (WO-872 §2, T1–T11).
**Layer:** B (Hovl string-key `HovlVfxCatalog.asset`, the active path) + C (`ProjectileVFXCatalog`). Browse the owned
library FIRST: `docs/asset-inventory/04_vfx_spells_audio.md`.

---

## 1. RCA (from the audit — `DefenseTower.cs` / `ArcaneTower.cs`)
Towers already play type-keyed cast/impact, but **the tier dimension is inconsistent and the projectile bodies are
placeholders:**
- **T1 muzzle/cast** — WIRED by type (`MuzzleVfxFor`/`CastKeyFor`, `DefenseTower.cs:982-1024`) but **NO tier** (L1=L2=L3).
- **T2 projectile body** — **PRIMITIVE** (code sphere/cylinder+cube/orb, `DefenseTower.cs:858-899`); tier keys wired
  ONLY for ground-archer-None (`ArcherTowerLevel1/2/3_Projectile`) — every other type/element ignores tier.
- **T3 impact** — WIRED by element (`ImpactVfxFor`/`ImpactKeyFor`) but **NO tier**.
- **T4–T6 ArcaneTower** — cast/projectile/AoE-impact are wired WITH tier (`VfxScale` 1.0/1.3/1.7 + L2/L3 key swaps) —
  the good example to mirror.
- **T7 element MISMATCH** — ArcaneTower deals **Aether** damage but renders **Fire** everywhere
  (`BoltVisualElement = Flame` hardcoded, `ArcaneTower.cs:67`; damage `StructureFactory.cs:717`).
- **T8** — no true cast **windup**: the cast flash is simultaneous with the shot, no pre-fire charge.
**The owner already TAGGED the per-type/per-tier Hovl keys** (`ArcherTowerLevel1/2/3`, `ArcherTower-Fire/Ice`,
`ArcaneTower-Baselevel`, `RangerTowerBase/Upgraded/level2`, `FireballTower_Projectile`,
`FireFromTower-ArcaneTowerLevel3_Aura`) — **the gap is the call sites wire only a subset.**

## 2. The fix
- **Wire tier + type consistently across cast, projectile, AND impact** — extend `MuzzleVfxFor` / `ProjectileKeyFor` /
  `ImpactKeyFor` (`DefenseTower.cs`) to read the tower TIER (from the catalog/BuildTimerService tier the ArcaneTower
  path already uses) and pick the owner-tagged per-type-per-tier key. Archer / Arcane / Ballista(wizard) each escalate
  L1 → L2 → L3.
- **Replace the PRIMITIVE projectile bodies** (sphere/cube/orb) with the real projectile prefabs via
  `ProjectileVFXCatalog.SpawnFlying` / the tagged Hovl body keys — no code-mesh placeholders on a shipped tower.
- **Fix the Aether/Fire mismatch (T7) — RENDER AETHER (owner 2026-08-04).** Change `BoltVisualElement` off the
  hardcoded `Flame` so ArcaneTower's cast/projectile/impact read as **Aether** (match its damage). It is NOT
  thematically fire. Do NOT ship "deals Aether, looks Fire."
- **Add a cast windup (T8)** — a short pre-fire charge VFX at the muzzle before the shot (mirror the enemy caster's
  `SpawnCastWindup`), so a tower telegraphs.
- **System B is DEAD legacy (owner 2026-08-04) — do NOT touch** (`Tower`/`TowerCombat`/`PooledProjectile`, T9–T11 out
  of scope). System A (`DefenseTower`/`ArcaneTower`) is the only live tower path.

## 3. Rules (WO-872 §4)
Reuse the owned/owner-tagged keys (author none); route via `VFXManager.PlayKey` / `ProjectileVFXCatalog`;
owner-tags-key / CLI-maps-verbatim; WO-753 teardown; keep the URP shader-proof path.

## 4. Acceptance
- [ ] Archer / Arcane / Ballista towers each show DISTINCT cast + projectile + impact VFX that **escalate L1 → L2 → L3**
      (on-device) — no primitive sphere/cube projectiles.
- [ ] ArcaneTower no longer "deals Aether but looks Fire" (element matches, per the owner ruling).
- [ ] A cast windup telegraphs before each shot.
- [ ] All keys resolve from the owner-tagged catalog; `CompileGate` green; verified on the Seeker.

## 5. Do NOT
- Do NOT author new VFX (owner-tagged keys already exist). Do NOT keep primitive projectile bodies. Do NOT raw-
  Instantiate (use `VFXManager`/`ProjectileVFXCatalog`). Do NOT touch legacy System B without owner confirm.
