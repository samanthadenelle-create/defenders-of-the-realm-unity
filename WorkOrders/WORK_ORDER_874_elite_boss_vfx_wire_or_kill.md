> ## RECONCILED 2026-08-08 - true status is NEEDS-OWNER-RULING
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: `AddComponent<EliteVFXController>` returns 0 hits in any .cs (VERIFIED at source 2026-08-08). Commit 4c1da079 promoted SpawnVfxFor / PlayDeathShake to statics called from Enemy.cs:720 and Enemy.cs:2701, delivering the tell but silently routing AROUND the owner's WIRE ruling with no reversal recorded. The aura and OnEliteAttack have never run.
> The previous Status line read "READY - child of WO-872." and was wrong.

# WORK ORDER 874 — Elite/Boss VFX: wire or kill `EliteVFXController` (+ DragonBoss spawn)

**Status:** IMPLEMENTED 2026-08-22 - EliteVFXController is genuinely AddComponent-ed on the elite/boss spawn path (Enemy.EnsureEliteVfx from Configure); aura + OnEliteAttack + DragonBoss spawn entrance now actually run. A source-lint pins the AddComponent so the 4c1da079 static-shortcut shape cannot return. 3 keys still need an owner VFX tag (Boss_AttackImpact / Boss_PhaseTransition / Boss_Telegraph) - hooks live, art unmapped by design.

> *(superseded status, kept for the record: BLOCKED - NEEDS OWNER RULING, reconciled 2026-08-09.)*

**Status:** BLOCKED - the three boss VFX keys are OWNER-OWED art tags, confirmed 2026-08-24 (`FOUNDATIONAL_RULINGS.md` §4 - no prefab names itself the answer, so it is a substitution and hers). Everything else implemented 2026-08-22. *(Prior line:)* NEEDS-OWNER-RULING (reconciled 2026-08-08) — child of WO-872. **Lane:** Combat/AI VFX. **WO#:** UI-seat block; **874**.
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


---

# OWNER RULING RECONFIRMED 2026-08-21 - **WIRE IT. THE 2026-08-04 RULING STANDS.**

Owner verbatim: *"874 wire ruling stands"*. This closes the BLOCKED status. Execute §2's "Wire it"
path exactly as originally ruled - this is not a new decision, it is the same one, restated because
a later commit routed AROUND it with no reversal recorded.

⛔ **THE FAILURE MODE TO AVOID IS THE ONE THAT ALREADY HAPPENED HERE.** Commit `4c1da079` delivered
the "tell" via statics called from `Enemy.cs:720` / `Enemy.cs:2701` INSTEAD of attaching the
controller - so the aura and `OnEliteAttack` have still never run, and the ticket read as progressed
while the ruled behaviour did not exist. Do not repeat that shape: `EliteVFXController` must be
genuinely `AddComponent`'d on the elite/boss spawn path so its spawn / aura / attack / death fire.
A source-lint asserting a real `AddComponent<EliteVFXController>` is part of the work.

---

# IMPLEMENTED 2026-08-22 — what landed, and what is still an owner call

## The attach (the ruling itself)
- `Enemy.EnsureEliteVfx()` — `Assets/_Modules/Village/Enemies/Enemy.cs`. Called from the
  **end of `Configure`**, which is the ONE place every spawn path sets the stat block and is
  also the pooled-reuse entry point. It does a real
  `gameObject.AddComponent<EliteVFXController>()` when `_def` reads boss or elite, then calls
  `ArmForTier(boss, elite)`. Nothing is attached to a plain-tier enemy.
- `EliteVFXController.ArmForTier(bool, bool)` — new public **instance** entry point, idempotent
  and re-armable, because a pooled body's `Start()` runs once per POOL, not once per enemy
  life. `Start()` now stands down when `ArmForTier` already ran, so the hand-placed-prefab path
  and the code path cannot double-fire. `OnDisable` stops the routines and restores the aura
  light, so a body reused 100 times carries one aura coroutine, not 100.
- **Spawn tell has ONE owner per tier.** `EnsureEliteVfx` clears `_spawnTellPending` for
  boss/elite, so the component's `DramaticSpawnRoutine` owns the arrival for those two tiers
  (same `VFXType` — both sides go through `SpawnVfxFor` — plus the same tier shake, after the
  authored dramatic delay). `Enemy.FireSpawnTell` is now the STANDARD tier's tell only.
- **Attack tell wired:** `Enemy.ExecuteContactAttack` calls `_eliteVfx?.OnEliteAttack(hitPos)`.
  This and the aura are the two behaviours the `4c1da079` static shortcut could not deliver.
- **Death path needed no change** — `Enemy.Die()` has always done
  `GetComponent<EliteVFXController>()`; it now returns non-null, so `OnEliteDeath` is live.

## E8 — DragonBoss entrance
`DragonBoss.PlaySpawnEntrance()`, fired from `OnEnable` beside the loop-budget declaration
(same reasoning that line gives: OnEnable is the boss's own lifecycle and survives a
re-enable). New serialized fields `_spawnVfx = VFXType.Boss_Spawn`, `_spawnShakeIntensity`
0.5 / `_spawnShakeSeconds` 0.5 — matched to the boss tier's spawn shake in
`EliteVFXController` on purpose. DragonBoss is not an `Enemy`, so the attach seam never
reaches it; the entrance has to be its own call.

## E9 — STALE as written, and the remainder is an OWNER ART TAG
E9 recorded "all 10 `Boss_*`/`Elite_*` rows are PROC-only (0 catalog prefabs)". **Measured
2026-08-22 against `Assets/Editor/VFXCatalogGenerator.cs`: 8 of the 11 ladder rows now have a
Map entry** (`Elite_Spawn`, `Elite_Death`, `Boss_Spawn`, `Boss_Death`, `Boss_Aura_Phase1/2/3`,
`Boss_FireBreath`) — WO-886/893 closed most of it.

⛔ **THREE ROWS STILL HAVE NO PREFAB AND NEED AN OWNER TAG. No prefab was chosen for them** —
standing rule (memory `vfx-map-owner-tags-no-creative-pick`): the owner tags the key, the CLI
maps it verbatim and never picks or substitutes art. The hooks are wired and live; each falls
through to `VFXManager`'s procedural nova/meteor burst until tagged:

| Key needing an owner tag | Where it already fires |
|---|---|
| `Boss_AttackImpact` | `EliteVFXController.OnEliteAttack` (boss branch) + `DragonBoss` strike/breath impact |
| `Boss_PhaseTransition` | `DragonBoss` HP-threshold enrage burst |
| `Boss_Telegraph` | `DragonBoss` swoop/fire wind-up tell |

The `elite-vfx-wire` suite REPORTS this list in its pass reason every run so the gap stays
visible; it deliberately never fails on it, because a red gate here would be pressure on an
engineer to pick a prefab.

## Oracle
`Assets/Editor/Regression/EliteVfxWiringRegression.cs` — `[elite-vfx-wire]`, registered once in
`DataRegression.RunAll`. Measures: (1) a literal `AddComponent<EliteVFXController>` exists in
the Village source (the exact grep the 2026-08-08 audit ran when it caught the shortcut);
(2) `OnEliteAttack(` has a caller outside its declaring file; (3) `ArmForTier` and
`OnEliteAttack` are public INSTANCE members, by reflection on the compiled type, not by grep;
(4) `DragonBoss.cs` references `VFXType.Boss_Spawn`; (5) ladder catalog coverage, reported.

## Not done here
- No gate, no build, no commit (edit-only lane).
- Acceptance still needs `CompileGate` green + the PO's on-device felt-verify.

---

## ⭐ OWNER RULING 2026-08-24 — batch 2, ruling 3: **the three boss keys stay HERS.**

The VFX authority split is now **canon in `FOUNDATIONAL_RULINGS.md` §4** — read it there; ⛔ not
restated here, per that file's no-paraphrase rule.

⛔ **`Boss_AttackImpact` / `Boss_PhaseTransition` / `Boss_Telegraph` are the worked example of the
OWNER column, not an exception to it.** No existing prefab names itself the answer, so choosing one is
a substitution — a creative pick — and it comes to her. The rest of the ticket is implemented; this
residue is owner-owed art tagging, nothing more.
