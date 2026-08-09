> ## RECONCILED 2026-08-08 - true status is NEEDS-OWNER-RULING
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: `AddComponent<EliteVFXController>` returns 0 hits in any .cs (VERIFIED at source 2026-08-08). Commit 4c1da079 promoted SpawnVfxFor / PlayDeathShake to statics called from Enemy.cs:720 and Enemy.cs:2701, delivering the tell but silently routing AROUND the owner's WIRE ruling with no reversal recorded. The aura and OnEliteAttack have never run.
> The previous Status line read "READY - child of WO-872." and was wrong.

# WORK ORDER 874 — Elite/Boss VFX: wire or kill `EliteVFXController` (+ DragonBoss spawn)

**Status:** BLOCKED - NEEDS OWNER RULING (reconciled 2026-08-09 - `AddComponent<EliteVFXController>` returns zero hits repo-wide; commit `4c1da079` delivered the tell via statics called from `Enemy.cs:720` and `Enemy.cs:2701` but routed AROUND the owner's WIRE ruling with no reversal recorded, so the aura and `OnEliteAttack` have never run)

**Status:** NEEDS-OWNER-RULING (reconciled 2026-08-08) — child of WO-872. **Lane:** Combat/AI VFX. **WO#:** UI-seat block; **874**.
**Origin:** owner 2026-08-04 VFX pass. Audit-backed (WO-872 §2, E6/E8/E9). **Layer:** B/D.
**OWNER RULING 2026-08-04: WIRE it** (not kill) — do §2's "Wire it" path: attach `EliteVFXController`, map the
`Boss_*`/`Elite_*` rows to real Mirza Beig prefabs, add the DragonBoss spawn entrance.

## 1. Gaps (audit)
- **E6 — `EliteVFXController` is DEAD.** Fully written (`Boss_Spawn`/`Elite_Spawn`/`Elite_Death`/elite aura/
  `OnEliteAttack`) but **never `AddComponent`'d on any prefab/spawner** — only read once (`Enemy.cs:2538`), never
  attached. So ALL elite/boss spawn/aura/attack/death differentiation never runs.
- **E8 — DragonBoss has no spawn entrance VFX** (no `Boss_Spawn` call).
- **E9 — all 10 `Boss_*`/`Elite_*` VFXType rows are PROC-only** (0 catalog prefabs) — generic nova/meteor bursts
  (`VFXManager.cs:1207-1237`); Mirza Beig has boss-scale nova/storm/portal prefabs to wire.

## 2. Fix (owner picks the direction — WO-872 §3.2)
- **Wire it:** `AddComponent<EliteVFXController>` in the elite/boss spawn path so its spawn/aura/attack/death fire; add
  a `Boss_Spawn` entrance to `DragonBoss` (`DragonBoss.cs`); map the `Boss_*`/`Elite_*` VFXType rows to real prefabs
  (Mirza Beig) instead of procedural bursts.
- **OR kill it:** delete `EliteVFXController` and fold what's wanted into `DragonBoss` (which already does phase VFX
  correctly), leaving no dead controller.
- Default = wire it (the owner wants VFX to work well; the library is owned).

## 3. Acceptance
- [ ] Elites/bosses show distinct spawn/aura/attack/death VFX (or `EliteVFXController` is removed with no dead code);
      DragonBoss has a spawn entrance. `CompileGate` green; verified on-device.

## 4. Do NOT
- Leave a fully-written-but-never-attached controller in the tree. Author no new VFX (Mirza Beig owned). WO-872 §4.
