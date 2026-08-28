# WORK ORDER 1261 — Pursuit arena: failed warp-in + battle-lock still held after retreat

**Status:** FIXED — COMPILE + FOCUSED + FULL REGRESSION PASS; DEVICE RETREAT OWED
**Minted:** 2026-08-28 (CLI, F8 device triage seq 3629 + 3630)
**Silo:** Combat/Arena
**Evidence (captured):** device `SM02G4061955851`, `Main_Castle_Overworld`, 2026-08-27 ~20:37Z,
two linked lines ~100s apart in one session:
- seq 3629: `[Flow:BattleArena] WATCHDOG: hero OUT of arena 2.7s (pos=(4852.67,0.08,4841.69)
  centre=(5000,0,5000)) — failed warp-in / orphaned battle; force-resolving to ResumeAll()`
  (stack: `Arena.<WatchToResolution>`)
- seq 3630: `[Flow:Quiescence] BATTLE_QUIESCENCE_FAIL (retreat) — battle-lock: still HELD after the
  battle ended. Combat input stays suppressed and the HUD cannot return to its town context.
  HOLDER(S): PursuitBattleProbe.Probe (of 3 registered: PursuitBattleProbe.Probe,
  BattleArena.<Awake>b__84_0, WaveManager.<OnEnable>b__106_0).`
Captures: `logs/f8-inbox/capture-device-20260828-131839-seq3629.md`, `...131840-seq3630.md`.

## RCA (from the data — the holder is NAMED)
Two defects, likely one lifecycle:
1. **Warp-in failed** — the hero was ~150 units from arena centre 2.7s after battle start. The
   watchdog resolved it, but the warp path has a failure mode (instrument the warp step to capture
   WHY: navmesh placement fail? position set before scene-side arena ready?).
2. **`PursuitBattleProbe.Probe` never releases the battle-lock on the RETREAT path.** The
   quiescence checker names it as the sole surviving holder — the other two registrants released.
   Player-felt: input suppressed, HUD stuck in combat context after retreating.
Instrument the probe's release path on retreat (vs victory/defeat) and prove where it dies before
editing. Fix the probe's release; keep the watchdog and quiescence checker as nets.

## Acceptance
1. Retreat from a pursuit battle: battle-lock fully released, HUD returns to town context,
   quiescence reports all invariants restored (no BATTLE_QUIESCENCE_FAIL).
2. Warp-in either succeeds or its failure cause is captured with a FlowTrace.Fail naming the step.
3. Regression: retreat-path quiescence added to the battle regression suite.

## Reconciliation, implementation, validation — 2026-08-28

The two captured symptoms crossed an already-landed lifecycle fix and one remaining gap:

- **Lock holder:** WO-1233 (commit `b303c4fbf`, after the published device build) already corrected
  this exact owner. Both `Resolve` outcomes, including `Flee -> Resolve(false)`, call the one
  `BattleSessionEnd.Release` lifecycle seam; it clears `PostureSignals` pursuit pulses before the
  quiescence gate evaluates. The existing behavioural suite explicitly reproduces a lock held by
  the pursuit probe and drives `retreat`, asserting that the lock clears. This ticket does not add
  another per-button release.
- **Warp:** `StageRoutine` previously treated the return from `WarpHero` as proof of arrival and
  spawned the enemy family immediately. The only check ran later in `WatchToResolution`, after an
  unwinnable remote fight already existed. It now waits one frame under the black transition,
  confirms actual pose/arena membership, retries once, waits and confirms again. A second failure
  aborts through the existing clean loss teardown **before enemy spawn**. Both failures name wanted
  and actual pose, drift, scene, NavMeshAgent state and CharacterController state.

Evidence:

- `COMPILE_GATE_OK` — `Builds/wo1261-compile.log`
- `BATTLE_QUIESCENCE_SUITE_OK` — focused retreat/session lifecycle, `Builds/wo1261-focused.log`
- `REGRESSION_OK` — 315/315, `Builds/wo1261-regression.log`
- Remaining honest boundary: a played Seeker/device retreat + forced-warp-failure capture is owed;
  no felt-device claim is made here.
