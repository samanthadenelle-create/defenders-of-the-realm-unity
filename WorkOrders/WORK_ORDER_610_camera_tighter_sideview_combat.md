# WORK ORDER 610 — Combat Camera: Tighter Cinematic Side / Angled Third-Person + Slower Battle Pacing

**Status:** READY TO IMPLEMENT — **owner-ratified 2026-07-05** (extend-SMC · 3/4-angled start · pacing default-ON ~0.8). **WO number 610 is PROVISIONAL** (authority is `MASTER_PIPELINES_BACKLOG`, not the filesystem; confirm on mint).
**Lane:** Combat / Camera + Combat-feel (pacing)
**Minted:** 2026-07-05
**Flags:** new `ff.combatcam` (default OFF — camera seat opt-in) · new `ff.combatpacing` (**default ON at 0.8 — owner ratified**; flip OFF restores today's 1.0 tempo) · reconciles with existing `ff.lockon` (WO-512, default OFF)
**Reuse law:** REUSE-FIRST. No new camera rig, no Cinemachine adoption for combat. Extend the existing, owner-validated `SmartMobileCamera`. Felt-sensitive — the fight already reads well; the camera seat is flag-gated so `ff.combatcam` OFF == today's exact path.
**Style reference:** `WorkOrders/WORK_ORDER_609_battle_hud_prefab_layout.md`.

---

## Goal

Move combat from the current **near-first-person, close over-the-shoulder** seat to a **tighter, more cinematic side / angled third-person** framing (Elden Ring / Skyrim duel feel) that reads as visceral and legible on mobile — AND slow the battle **tempo** via game logic (movement / attack-cadence / cooldown scalars), with **animations left at full playback speed** (NO `Time.timeScale`, NO `Animator.speed` change). Both behind feature flags, both reconciled against the real code below.

---

## Current-state map (files + constants — the REAL seams)

### The combat camera IS `SmartMobileCamera` (SMC) — hand-rolled, NOT Cinemachine
`Assets/_Modules/Village/Hero/SmartMobileCamera.cs` — runtime singleton (`Instance`, L320), `[RequireComponent(typeof(Camera))]`, drives the transform every `LateUpdate` (L566) on `Time.unscaledDeltaTime` (L585). This is the authoritative gameplay + battle camera — `HeroControlEnsurer` attaches/wires it (`HeroControlEnsurer.cs` L188-247, L330-341); the legacy `VillageCamera` is disabled by SMC (L360-361, L1015-1020).

Drivers / current constants:
- **Follow seat (the "close OTS" the owner wants to replace):** `_followOffset = (0, 2.6, -4.5)` (L67), backed by `DefaultFollowOffset = (0, 2.6, -4.5)` (L79). `_lookAtHeight = 2.5` (L83). `_forceCameraFix = true` (L165) **re-forces this seat every Play** (Awake L378-394) regardless of the baked scene value — so the seat is authored in CODE here, not the scene.
- **Look-at lead:** `_leadDistance = 3.5` (L88), clamped to `1.5` when `_forceCameraFix` (L392-393); `_leadSmoothTime = 0.3` (L91).
- **Follow smoothing:** `_smoothTime = 0.10` (L95), `_posVelocity` SmoothDamp (L656).
- **Combat zoom (proximity):** `_combatScanRadius = 12` (L99), `_combatZoomOut = 2.5` added to `offset.z` in combat (L105, L600), `_combatFovBoost = 4` deg (L108, L662), `_combatZoomSpeed = 2.5` (L111); `_combatBlend` 0..1 (L597).
- **Auto-framing:** `_framingBias = 0.2` (L124); lock-on variant `_lockFramingBias = 0.32` capped 0.45 (L142). Framing lerps `leadTarget` toward `Lerp(heroBase, enemyPos, bias)` via the `_leadPoint` SmoothDamp (L688-699).
- **Orbit / player yaw:** player-authoritative `_panYaw` (never velocity-driven) rotates the offset (L608-641); pitch clamp `_panPitchMin=-10` / `_panPitchMax=35` (L180-183).
- **FOV:** base FOV read from the `Camera` (`_baseFov`, L352); only combat boost writes it (L662). No explicit combat FOV target.
- **Wall handling:** occluder spherecast + fade-to-ShadowsOnly (`ApplyCollision` L849), plus screen shake (`Shake` L782), teleport snap (`OnHeroTeleported` L554).
- **SOLE-CAMERA GUARD (critical):** `EnforceSoleCamera()` runs **every LateUpdate** (L568, L1000-1030) and disables every other enabled screen `Camera` — including any Cinemachine vcam/brain. **This is why a new Cinemachine combat rig would be fought to death.**

### The town-vs-combat camera CHOICE
`Assets/_Modules/Village/Hero/CameraModeController.cs` (WO-338) post-processes SMC's seat (`[DefaultExecutionOrder(100)]`, L69). `EvaluateContext` (L355-379) gates **TOWN bird's-eye strictly to active base-build**; **all combat / wave / exploration = `BattleExploration`, which is SMC untouched** (L370-371). So "combat camera" == SMC's seat. Bootstrapped by `CameraModeControllerBootstrap.cs`.

### Cinemachine IS in the project (3.1.6) — but not on the combat path
`Packages/manifest.json` L7: `"com.unity.cinemachine": "3.1.6"` (3.x, not 2.x). Present usages:
- `HeroCinemachineRig.cs` — a CM3 `ThirdPersonFollow` OTS rig (shoulder `(0.4,1.6,0)`, `CameraDistance=7`, `Damping=0.25`, Deoccluder). **Legacy/alternate — SMC's sole-camera guard disables it; not wired into the combat path.**
- `CinemachineCameraController.cs` (WO-87) — `vcVillage/vcCombat/vcWaveClear` + `CinemachineImpulseSource` shake. Overhead/village helper.
- `DungeonCameraRig.cs` — CM3 top-down iso (`FollowOffset (0,13,-9)`, pitch 52, FOV 40). Dungeon-only.

### Battle pacing — current tempo knobs
- **Hero:** `HeroLocomotion.cs` `_moveSpeed` serialized 4 but **forced to 6 in Awake** (L391); `TownMoveSpeedMax = 3.5` (L76). The combat/town split already exists: `engaged = IsWaveInCombat()` (L711) -> `moveSpeedCap = engaged ? _moveSpeed : TownMoveSpeedMax` (L712). **This `moveSpeedCap` is the clean combat-speed knob to extend.** Precedent for a non-animation-slowing scalar already in the same line: `HeroHealth.MoveSpeedMultiplier` (injured-stance slow, L718).
- **Enemies:** `Enemies/Enemy.cs` `_moveSpeed` (L70, set from def L501/L536), `_attackInterval` / `_attackCooldown` (L172, L1259-1262), existing global multipliers `EnemyAttackIntervalScale = 1.12` (L165) and `speedMult` (L605) — precedent hooks. Per-enemy stats authored in `BattleArena.cs BuildEncounterDef` (L1203-1233): `MoveSpeed` 2.2-3.2, `AttackInterval` 1.2-1.8s, threat scalar `t` (L1205).
- **`Time.timeScale` is used ONLY by `ArenaDeathCam`** kill slow-mo (`KillSlowMoScale = 0.55`, L44, L126-155) — a deliberate ~3.6s cinematic death beat. That path DOES stretch animation and is intentional; general pacing must NOT reuse it.

### Lock-on (WO-512) — already the "framed duel" seam
`WorkOrders/WORK_ORDER_512_lockon_camera.md`, flag `ff.lockon` (default OFF). SMC already has `SetLockTarget/ClearLockTarget` (L756-772) + capped `_lockFramingBias` (L142); `HeroLocomotion` has `SetLockFace/ClearLockFace` + strafe-facing (L113-152, L764-822). Owner = `HeroTargetIndicator`. **The side-view combat camera is a SEAT/profile change; lock-on is the TARGET-framing behavior. They COMPOSE — side seat + lock framing = the cinematic duel. Do NOT add a second framing path or a new rig.**

---

## Recommendation (owner decision — HP-B2B "easy vs right" lens)

**Recommendation: EXTEND `SmartMobileCamera` with a flag-gated "combat camera profile" (a second seat: offset/pitch/yaw/FOV/lead) that SMC blends to when a battle is engaged. Do NOT adopt a Cinemachine combat rig.**

Why this is both the *easy* and the *right* call here (not a shortcut):
- **"Easy":** a handful of serialized fields + one blend already fits SMC's existing `_combatBlend` pattern (L597). ~1 file of runtime change, fully reversible via `ff.combatcam`.
- **"Right":** SMC is the owner-validated felt-baseline and already owns lock-on framing, occluder-fade, teleport-snap, shake, town-blend handover (`CameraModeController`), and the sole-camera contract. A Cinemachine `StateDrivenCamera` would require **ripping out `EnforceSoleCamera`** and **re-implementing** lock-on framing, occluder fade, teleport snap, and the town/build-mode handover on the CM stack — a large, high-risk rewrite of a felt-sensitive system for no gameplay gain.
- **The honest tradeoff (name it for the owner):** the *textbook* "right" long-term answer for a AAA-style cinematic combat camera is Cinemachine 3.x (StateDrivenCamera + ThirdPersonFollow + free-look + native deoccluder + designer-tunable in-Editor with no rebuild). We are **deliberately not** taking that path now because the migration cost + regression risk against a validated rig outweighs it. **If the owner wants the Cinemachine foundation, that is a separate, larger WO (a rig migration) and an explicit package/architecture decision — flag it, don't smuggle it in here.** This WO gets ~90% of the cinematic feel at ~10% of the risk.

**Owner decisions — RATIFIED 2026-07-05:**
1. **EXTEND SMC — approved.** No Cinemachine combat rig. A CM migration, if ever wanted, is a separate future WO (explicit package/architecture decision), never smuggled here.
2. **Framing: 3/4 ANGLED to start** — yaw ~35-55 deg, pitch ~12-18 deg, pulled back + slightly higher (the mobile-legible "Elden Ring lock" feel). Dial toward side-on live from there; pure side-on is the risk-flagged extreme, not the starting target.
3. **Pacing: DEFAULT-ON at ~0.8** — `ff.combatpacing` ships ON so the slower combat tempo is the default experience; the flag stays for instant revert to 1.0 and live scalar tuning. (Owner accepted the risk that 0.8 is pre-felt-tune; dial live.)

---

## Highest-leverage changes (the 3-5 that matter)

1. **SMC combat camera profile + blend** — add a second seat (`_combatFollowOffset`, `_combatYaw`, `_combatPitch`, `_combatFov`, `_combatLookAtHeight`, `_combatLead`) and blend the existing `_followOffset`/FOV/look-at toward it on `_combatBlend` when engaged. Reuse the **existing** `_combatBlend` (L597) and `_leadPoint`/`_posVelocity` SmoothDamps — never a transform snap. Gate the whole delta on `ff.combatcam`; OFF = today's `(0,2.6,-4.5)` seat byte-for-byte. Feed the angled seat through the existing `Quaternion.Euler(_panPitch,_panYaw,...)` offset rotation (L641) so player pan still composes. Files: `SmartMobileCamera.cs`.
2. **"Engaged" signal for the camera** — SMC currently derives combat only from its own proximity scan (`_enemyInRange`, L807). Reuse the canonical combat signals already used elsewhere: `BattleArena.AnyBattleInProgress` and `BattleLock.IsInBattle()` (see `HeroLocomotion.IsWaveInCombat` L599-613) so the cinematic seat engages for the ACTUAL battle, not just any nearby mob. Files: `SmartMobileCamera.cs`.
3. **Combat pacing scalar (game-logic, animations untouched)** — new `DeNelle.Core.Combat.CombatPacing` static holding `MoveScalar` / `EnemyMoveScalar` / `EnemyCadenceScalar` / `CooldownScalar` (**ratified default 0.8 with `ff.combatpacing` ON**; flag OFF => 1.0 == today). Apply: hero `moveSpeedCap * CombatPacing.MoveScalar` when `engaged` (`HeroLocomotion.cs` L712, next to the existing `HeroHealth.MoveSpeedMultiplier`); enemy `_moveSpeed`/`_attackInterval` (`Enemy.cs` L536/L1262, mirroring `EnemyAttackIntervalScale` L165 & `speedMult` L605); ability cooldowns (`HeroAbilities` cooldown timers). Gate on `ff.combatpacing`. **Explicitly forbid `Time.timeScale` and `Animator.speed`** — reducing move speed only shifts the locomotion blend (walk vs run) while each clip still plays at 1.0, which is exactly "slower tempo, full-speed animation."
4. **Reconcile with lock-on, do NOT duplicate** — when `ff.lockon` is engaged, the cinematic side seat should read as the "duel" seat and the existing `_lockFramingBias` path (L682-691) keeps the locked enemy framed. Confirm the combat-profile yaw does not fight lock-face strafe (`HeroLocomotion` L764-822). No new framing code.
5. **HUD/VFX re-tune hooks (flag, don't fix here — see dependencies)** — the tighter/angled seat changes safe zones; surface the new engaged-seat state so the HUD/FCT layer can react.

---

## Battle-pacing approach (explicit)

- Single source of truth: `CombatPacing` static scalars, resolved from `ff.combatpacing` (+ optional per-scalar PlayerPrefs for live tuning). **Ratified default = 0.8 (flag ON)**; flipping `ff.combatpacing` OFF restores 1.0 (today's tempo).
- Multiplicative, combat-scoped only (guarded by the existing `engaged` / `AnyBattleInProgress` checks) so town/explore tempo is unchanged.
- Animations: **untouched.** No `Time.timeScale`, no `Animator.speed`, no clip retime. The hero blendtree just settles on a calmer walk band; enemy anim speed is velocity-damped already (`Enemy.cs` L860) so it tracks the slower move for free.
- Leave `ArenaDeathCam`'s deliberate kill slow-mo (`Time.timeScale`, L126-155) alone — it is the cinematic death beat, not general pacing.

## Mobile / perf considerations (60 fps mid-range)

- Zero new per-frame allocations; reuse SMC's existing SmoothDamps, scan buffers, and unscaled-dt path.
- No new camera, no second `Camera`/CinemachineBrain (avoids the extra render + the sole-camera fight).
- Blend math is a few lerps/frame — negligible.
- **Mobile legibility risk:** a true side-on seat + portrait FOV can bury depth cues and telegraphs; start 3/4-angled, validate FCT/telegraph readability on device before pushing toward side-on. Occluder-fade (L849) already prevents a side seat clipping into arena geometry.

## Toggle / backward-compat plan

- `ff.combatcam` (default OFF): OFF => SMC seat identical to today `(0,2.6,-4.5)`; every combat-profile field is skipped.
- `ff.combatpacing` (**default ON, 0.8**): flip OFF => all scalars 1.0, hero/enemy/cooldown code paths byte-identical to today (the instant-revert path).
- Both reversible via PlayerPrefs (`ff.combatcam` / `ff.combatpacing`) + a `Defenders/Debug` editor menu (mirror the `ff.lockon` menu, `FeatureFlags.cs` L577-593).
- Free-look / town / build-mode / lock-on invariants preserved (guard every new branch on the flag + `engaged`).

---

## Phased acceptance criteria

**Phase 0 — flags + no-op scaffolding**
- [ ] `FeatureFlags.CombatCam` + `FeatureFlags.CombatPacing` + `CombatPacing` static added; editor menu toggles present. OFF == today (headless + owner felt: no visible change).

**Phase 1 — combat camera profile (camera only)**
- [ ] With `ff.combatcam` ON, engaging a `BattleArena`/wave fight blends SMC to the angled/side cinematic seat over ~`_combatZoomSpeed`; disengaging eases back — **no snap** (reuses `_leadPoint`/`_posVelocity`).
- [ ] Player pan (`AddYaw`/`AddPitch`) still composes; wall occluder-fade + shake + teleport-snap still work; `EnforceSoleCamera` untouched.
- [ ] OFF == byte-identical to today. Owner felt-call: cinematic + readable on device.

**Phase 2 — battle pacing (game logic)**
- [ ] With `ff.combatpacing` ON, hero + enemy movement and enemy attack cadence + ability cooldowns run slower by the scalar; **animation playback speed unchanged** (verify hero clip length / enemy anim not retimed; `Time.timeScale == 1`).
- [ ] Town/explore tempo unchanged (scalar scoped to `engaged`). OFF == today.

**Phase 3 — lock-on reconciliation**
- [ ] With `ff.combatcam` + `ff.lockon` both ON: locked enemy stays framed via the **existing** `_lockFramingBias` path; strafe-facing unaffected; no duplicate framing/rig. Release => free-look with the combat seat.
- [ ] `COMPILE_GATE_OK`; brace balance on every edited `.cs`.

---

## HUD / readability dependencies (FLAG ONLY — do not fix in this WO)

A tighter / angled combat seat forces the HUD + combat-VFX layer to re-tune (owned by WO-609 HudKit + `hud-areas.json` `hostile(activebattle)`):
- The `hostile(activebattle)` layout assumes an **empty center** for the 3D fight (WO-609 layout). A side/angled seat pushes the action **off-center** — verify the fight does not collide with the TL player plate / TC enemy plate.
- **Floating combat text + enemy world-space health bars / nameplates** are world-to-screen anchored; a new seat/FOV shifts their safe zones and can overlap the top plates. Re-tune in `BattleArenaHud` / `BattleHud9Zone`.
- Enemy health-bar scale + reticle (`HeroTargetIndicator`) apparent size change with the pulled-back seat.
- Telegraph / VFX readability (ground decals, cast tells) must be re-checked at the new angle for mobile.
These are **downstream re-tune tickets**, not part of this camera/pacing WO.

## Files (provisional)

| File | Change |
|---|---|
| `Assets/_Modules/Core/FeatureFlags.cs` | `CombatCam`, `CombatPacing` flags + editor menu (mirror L577-593) |
| `Assets/_Modules/Core/Combat/CombatPacing.cs` | **new** — static scalar holder |
| `Assets/_Modules/Village/Hero/SmartMobileCamera.cs` | combat-profile seat + blend; engaged-signal reuse |
| `Assets/_Modules/Village/Hero/HeroLocomotion.cs` | apply `CombatPacing.MoveScalar` at L712 |
| `Assets/_Modules/Village/Enemies/Enemy.cs` | apply enemy move/cadence scalars (L536, L1262) |
| `Assets/_Modules/Village/Hero/HeroAbilities*` | apply cooldown scalar (verify file/knob) |

## Do NOT touch
- SMC `_posVelocity`/`_smoothTime`/wall-collision/occluder-fade/`EnforceSoleCamera`; the lock-on framing path.
- `HeroLocomotion` move-vector / NavMesh Move / seam-crossing / ground-snap.
- `ArenaDeathCam` slow-mo (deliberate death beat).
- Cinemachine rigs (`HeroCinemachineRig`, `CinemachineCameraController`, `DungeonCameraRig`) — out of scope unless a separate migration WO is minted.
- `.unity` hand-edits (seat is code-forced via `_forceCameraFix`).
