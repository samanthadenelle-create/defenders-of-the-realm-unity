> ⚠ **UNRESOLVED NUMBER COLLISION — WO-586 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_586_fleet_save_probe_isolation.md`, `WORK_ORDER_586_battle_animation_posture_directional_death.md`
> The two tests **disagree**: `WORK_ORDER_586_fleet_save_probe_isolation.md` is first-on-disk (2026-06-29 00:20 vs 2026-07-05 15:25), but the *shipped* reference belongs to the other file — commit `38c7fd4b9` reads "WO-586: battle posture, directional death, orc cadence". First-on-disk-**and**-referenced is satisfied by neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 586 — Battle Animation Posture, Directional Death, Orc Cadence

**Status:** CLOSED 2026-09-03 - owner felt-test PASS. PRIOR STATUS: FIXED — CONTROLLER REBUILT + REGRESSION PASS; DEVICE DEATH FELT-VERIFY OWED
**Lane:** Combat/Animation (code + editor controller bake) — no scene files
**Serves:** Senior animation assessment (2026-07-04/05) — high-ROI battle feel before WO-585 ability expansion
**Related:** WO-491 (orc locomotion base), WO-585 (animation-driven actives — separate, design-only)

---

## Goal

Make V1 animated combat read as a **survival gate**, not a slide-show:

1. Hero **braces into combat stance** during arena, wave, and in-place `BattleLock` fights (not only `WaveManager` phases).
2. Hero **draws weapon on standing engage** (unsheathe) before braced locomotion when `ff.mocaploco` + KnightV3 are active.
3. Hero **dies toward the killer** (front/back/left/right buckets) instead of always playing the generic fall clip.
4. Orc family **walk/run cadence** matches agent travel speed so feet do not skate.

---

## What was implemented (this commit)

### Runtime — directional death

| File | Change |
|---|---|
| `Assets/_Modules/Core/Combat/CombatDeathDirection.cs` | **NEW** — `Resolve(victimPos, victimFwd, attackerPos?)` maps attacker world position to `DeathDirection` (Front/Back/Left/Right/Fall) via dot/cross on XZ plane. |
| `Assets/_Modules/Village/Hero/HeroHealth.cs` | `_lastDamageSourceWorld` field; `NoteDamageSource(Vector3)`; contact-damage tick sets source from primary attacker (`_attackerBuf[0]`); `PlayDeathAnim()` calls `CombatDeathDirection.Resolve` → `ActorAnimator.Die(dir)` with `FlowTrace.Step`; `ClearDeathAnim()` clears source on revive. |
| `Assets/_Modules/Village/Enemies/Enemy.cs` | `NoteHeroDamageSource(IDamageableStructure)` — feeds hero position before damage in melee `ExecuteContactAttack`, instant `RangedAttack`, and `RootedCast` land callback. |

### Runtime — combat posture signal

| File | Change |
|---|---|
| `Assets/_Modules/Village/Hero/HeroLocomotion.cs` | `IsWaveInCombat()` now also returns true when `BattleLock.IsInBattle()` — covers in-place dungeon/outpost fights via `HeroCombatEngagement` that do not stage `BattleArena` or `WaveManager`. Drives `InCombat` → `CombatLocomotion` (braced idle) on KnightMocap. |

### Editor — KnightMocap controller factory

| File | Change |
|---|---|
| `Assets/Editor/HeroAnimatorFactory.cs` | `HeroSpec` extended: `unsheatheClipOverride`, `deathFrontAnimPath`, `deathBackAnimPath`. `BuildCombatLocomotion` split from `WireCombatStanceTransitions` (standing engage → Unsheathe → CombatLocomotion; moving engage skips to braced gait). Death states extended: `DeathFront` (dir 3), `DeathBack` (dir 4). `BuildKnightMocapController` sets: unsheathe `"draw sword 1"`, front `Signature_Death_Forward.anim`, back `Signature_Standing_Death_Backward_01.anim`. |

### Editor — orc locomotion cadence

| File | Change |
|---|---|
| `Assets/Editor/BuildOrcHumanoidController.cs` | `ApplyOrcLocomotionCadence` — walk child `timeScale 1.35`, run `1.75` on healthy + injured blend trees (mirrors hero cadence bake). |

---

## Post-commit bake (CLI — editor CLOSED)

Controller assets on disk are **not** updated until these batchmode calls succeed.

**Canonical invocation (CLI_GATEKEEPER_PLAYBOOK / HANDOVER_NEXT_CLI):** call
`run-unity-method.ps1` **directly** from repo root — no `cmd /c` wrap, no `if/else`
wrap (those silently no-op or break PowerShell parse). Judge success by the **log
marker**, not the wrapper exit code (Unity forks on launch).

```powershell
# from repo root, Unity editor CLOSED:
.\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate.log
# expect log line: COMPILE_GATE_OK

.\run-unity-method.ps1 -Method DeNelle.Editor.HeroAnimatorFactory.BuildKnightMocapController -LogName knight-mocap-controller.log

.\run-unity-method.ps1 -Method DeNelle.Editor.BuildOrcHumanoidController.Run -LogName orc-controller.log
# expect log tail: ORC_CTRL_OK
```

Alt form (same script): `powershell -File run-unity-method.ps1 -Method <fqmethod> -LogName <x.log>`

Or from Unity menu (editor open): **Defenders → Animation → Build Knight Mocap Locomotion Controller** + `BuildOrcHumanoidController.Run` via executeMethod.

**Regenerated assets (expected dirty after bake):**
- `Assets/Resources/Heroes/KnightMocap.controller`
- `Assets/Resources/Enemies/OrcHumanoid*.controller` (+ role overrides if rebuilt)

---

## Acceptance criteria

### Headless (CLI)
- [ ] `CompileGate.Run` → `COMPILE_GATE_OK`
- [ ] Brace balance on all touched `.cs` files
- [ ] `BuildKnightMocapController` completes without missing-clip warnings for unsheathe / front-death / back-death paths
- [ ] `BuildOrcHumanoidController.Run` logs `ORC_CTRL_OK`

### PO felt-verify (requires `ff.knightv3` + `ff.mocaploco` ON)
- [ ] Enter BattleArena (or in-place `BattleLock` fight): hero transitions to **braced combat idle** (`CombatLocomotion`), not calm town idle
- [ ] **Standing engage**: draw-sword unsheathe plays once before braced gait; **moving engage** skips unsheathe
- [ ] Hero death from **front / back / left / right** attacker positions plays distinct clips (not always generic fall)
- [ ] Orc enemies: feet track ground at walk/run — no obvious skating in arena

### Regression guard
- [ ] Stock `Knight.controller` (non-mocap) **unchanged** — only `KnightMocap.controller` output path touched
- [ ] Hero respawn clears death latch (`Revive` / `ClearDeathAnim`) — no stuck death pose

---

## What NOT to touch

- `Village.unity` / `Village2.unity` scene files (hand-edit forbidden)
- `Knight.controller` output path (mocap twin only)
- WO-585 scope (new active abilities, talent-tree actives, timed-buff handler)
- ATB flat combat path (`ff.atbdungeon` ON) — this WO targets animated overworld battle

---

## Instrumentation

- `HeroHealth.PlayDeathAnim` — `FlowTrace.Step` logs `DeathDir` + source position (captured in Player.log; F8 on felt failure)
- No new feature flags — behaviour gated by existing `ff.mocaploco` / `ff.knightv3` + controller asset presence

---

## Files touched (commit scope)

```
Assets/_Modules/Core/Combat/CombatDeathDirection.cs          (new)
Assets/_Modules/Core/Combat/CombatDeathDirection.cs.meta     (new)
Assets/_Modules/Village/Hero/HeroHealth.cs
Assets/_Modules/Village/Hero/HeroLocomotion.cs
Assets/_Modules/Village/Enemies/Enemy.cs
Assets/Editor/HeroAnimatorFactory.cs
Assets/Editor/BuildOrcHumanoidController.cs
Assets/Resources/Heroes/KnightMocap.controller               (after bake)
Assets/Resources/Enemies/OrcHumanoid*.controller             (after bake)
```

---

## Out of scope (follow-ups)

- Left/right **package** death clips (only front/back added; L/R still use Shared Mixamo clips)
- Assassinate death bucket (`DeathDirection.Assassinate = 5`)
- WO-585: wire unused attack clips to ability bar slots
- Enemy directional death (hero-only in this WO)
- Mocap capture / new clip authoring

---

🤖 Implemented from battle-animation assessment ROI list. Attach this WO to the check-in; PO closes after felt-verify.

## 2026-08-28 bounce RCA + validation

The generated controller authored the unconditional AnyState -> `Death` fallback before all four
directional transitions. Unity evaluates eligible AnyState transitions in order, so `Dead=true`
always selected the generic state before `DeathDir` could matter. The factory now authors the four
directional transitions first and the safe generic fallback last; `KnightMocap.controller` was
rebuilt. `KnightDirectionalDeathRegression` loads the real built controller and proves all four
states own clips and precede the fallback.

- `COMPILE_GATE_OK`: `Builds/ready-integrated-compile.log`
- `KNIGHT_DIRECTIONAL_DEATH_OK` and `REGRESSION_OK` (316/316):
  `Builds/ready-integrated-regression-retry.log`
- Device front/back/left/right felt verification remains owed; no device claim is made here.
