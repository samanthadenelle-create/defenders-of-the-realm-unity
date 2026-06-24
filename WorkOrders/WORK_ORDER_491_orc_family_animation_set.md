# WORK_ORDER_491 — ORC FAMILY ANIMATION SET (walk + role actions + mage casting)

**Status:** SPEC / READY · Combat/Animation lane · owner-requested 2026-06-23 (felt-test in BattleArena)
**Goal:** the orc family in the BattleArena (and roaming overworld reps) read as ALIVE — they WALK
(not slide), and each role uses its full motion set: **mage CASTS SPELLS**, warriors swing, tanks
shield/taunt. This is the animated-combat centerpiece (canon: [[atb-flat-vs-overworld-animated-combat]]).

## Root cause (verified from code, 2026-06-23)
`Assets/Editor/BuildOrcHumanoidController.cs` builds the `OrcHumanoid` controller with states
**Idle / Attack / Hit / Dead** and params **InCombat / Attack / Hit / Dead** — **NO `Speed` param,
NO walk/locomotion state.** So a moving orc (NavMeshAgent) stays in Idle and **slides** ("enemies
slide in arena, no motions", owner). `ActorAnimator` (Enemy.cs:184) already tries to drive
`Speed`/`Attack`/`Hit`/`Dead`, but the controller has no `Speed` param or walk state to receive it.

## Build
1. **Locomotion (fixes the slide — all roles):** add a `Speed` float param + an Idle↔Walk(/Run)
   blend (or Locomotion blend tree) to the controller; ActorAnimator already feeds `Speed` from the
   agent velocity, so a walk state is all that's missing. Source a humanoid walk/run clip (Mixamo /
   `Assets/Action/`). Re-run `BuildOrcHumanoidController` (editor/batchmode) to rebuild the asset.
2. **Role-specific actions (the "family" feel):** give each role its motion:
   - **Mage** → a CAST state + `Cast` trigger (casting clip) wired to the spell moment.
   - **Warrior** → melee swing (the existing Attack state, maybe a better orc-swing clip).
   - **Tank** → shield-up / taunt idle + a heavier attack.
   **ALL family members SHARE ONE humanoid rig (owner 2026-06-23: "they all pull same rig")** —
   so this is clip-swap per role on the same skeleton, NOT per-character rigging (the light win,
   [[tripo-roster-knight-orcs-first]]). One controller with role-driven clip overrides (Animator
   OverrideController per role) is cleaner than N controllers. EnemyAnimatorFactory picks the
   override per id.

2b. **TELEGRAPH + ROOTED CAST (owner 2026-06-23 — combat feel):** a cast/heavy-attack must be
   READABLE — a visual wind-up tell so the player sees the smack coming and can react:
   - **Telegraph:** a wind-up anim pose + a VFX cue (e.g. a Spells Pack `Casting_*` / charge glow at
     the caster, or a ground ring) during the cast's wind-up window.
   - **Rooted while casting:** stop the NavMeshAgent for the cast duration — the caster does NOT
     slide/move while casting (commit to the cast). Resume after. (This is also the telegraph that
     makes it dodgeable.) Applies to the mage cast + any heavy/tank attack.
   - **AUDIO telegraph (owner 2026-06-23, not now):** add a sound cue on cast wind-up — an audible
     "you're being cast on" signal (via CoreServices.Audio / a charge/whoosh SfxId) so the player
     reacts even off-screen. Pairs with the visual tell; tune so a busy fight stays readable.
3. **Wire the triggers:** EnemyBrain/Enemy fires `Attack` on melee contact (exists) and a `Cast`
   trigger on the mage's ability cast (new) — so the animation matches the action.
4. **Apply to BOTH venues:** the same controllers drive the roaming overworld reps AND the arena
   family (both go through EnemyFactory/EnemyAnimatorFactory), so the slide is fixed everywhere.

## Files
- `Assets/Editor/BuildOrcHumanoidController.cs` — add Speed param + walk state (+ per-role variants).
- `Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs` — map role→controller.
- `Assets/_Modules/Village/Enemies/Enemy.cs` / `ActorAnimator` — confirm Speed feed; add Cast trigger.
- Clips from `Assets/Action/` (walk, casting, attacks) — verify humanoid rig + retarget.

## Acceptance
Orcs WALK when moving (no slide), the mage plays a CAST animation when it casts, warriors/tanks read
distinct. Verified in the BattleArena felt-test. Build the controllers (batchmode, editor closed),
then headless-smoke + owner felt-verify.

## Note
Bigger than a tweak — it's an animation pass. Do it on a CLEAN committed base (the encounter +
hero + HUD fixes from this session must be banked first).

---

## ✅ IMPLEMENTATION PLAN (Plan agent, 2026-06-23 — verified from code; BUILD-READY)

**Good news — most wiring already exists; only the controller + `Injured` are missing:**
- `ActorAnimator.SetLocomotion` already feeds `Speed`; `Enemy.DriveAnimator` calls it — but the legacy
  path is gated by `_hasSpeedParam`=false because the controller has NO `Speed` param. Add the param +
  Walk state and the slide is fixed for everyone. (Enemy.cs:698-709, ActorAnimator.cs:94-99.)
- `AnimParams` ALREADY has `Cast`/`CastVariant`/`WindUp` (+hashes); `ActorAnimator.PlayCast`/`PlayWindUp`
  exist + guarded. **Do NOT re-add.** `Injured` is the ONLY new param.
- Mage cast moment fires via `Enemy.RangedAttack` ← `EnemyBrain.TriggerAttack` (EnemyBrain.cs:269) — the
  seam to fire `Cast` + root the agent. Both venues route through `EnemyAnimatorFactory.Apply`.

**Architecture:** ONE base `OrcHumanoid` controller (full state machine + all params) + per-role
`AnimatorOverrideController` assets (`OrcHumanoid_Mage/_Warrior/_Tank`) swapping clips on shared states
(the owner's "all pull same rig"). Follow `HeroAnimatorFactory.cs:216-238` blend-tree pattern:
`BlendTree Simple1D blendParameter="Speed" useAutomaticThresholds=false` children idle@0/walk@6/run@9
(the `=false` is load-bearing — auto-thresholds skips walk).

**Clips (all Humanoid animationType:3, retargetable onto the shared avatar):**
- Loco: Idle `Assets/Action/Orc Idle.fbx` · Walk `Assets/Action/standing walk forward.fbx` · Run `Assets/Action/standing run forward.fbx`
- Mage cast: `Assets/Action/Spell Cast.fbx` (+ `Standing 2H Magic Attack 01/03.fbx`, `Standing 2H Magic Area Attack 01.fbx` for variants/wind-up)
- Warrior: `Assets/Action/Knight/standing melee combo attack ver. 1.fbx` / `Assets/Action/Sword And Shield Attack.fbx`
- Tank: idle `Assets/Action/Knight/sword and shield idle.fbx` · taunt `standing taunt battlecry.fbx` · heavy `standing melee attack downward.fbx`
- Hit `Assets/Action/Shared/Shared_Hit_Reaction.fbx` · Death `Assets/Action/Shared/Shared_Death.fbx`
- Injured: `Assets/Action/Enemies/injured idle.fbx` · `injured walk.fbx` · `injured run.fbx`

**File changes (all .cs via CLI; ASCII logs; brace gate; flag-gated; §12):**
1. `Core/Combat/AnimParams.cs` — add `Injured` const + hash (Cast/WindUp already there).
2. `Core/Combat/ActorAnimator.cs` (+ IActorAnimator) — add `SetInjured(bool)` guarded by `Has(InjuredHash)`.
3. `Assets/Editor/BuildOrcHumanoidController.cs` — REBUILD: add params Speed/Cast/CastVariant/WindUp/Injured
   (+HitDir/DeathDir parity); replace bare Idle with a Speed Locomotion blend tree; add Cast state
   (AnyState→Cast on trigger, →Loco on exit) + WindUp telegraph state; add Injured loco sub-tree
   (injured idle/walk/run) entered when Injured==true; then create the 3 AnimatorOverrideController
   assets under `Assets/Resources/Enemies/`. Log `ORC_CTRL_OK ...` (ASCII).
4. `Village/Enemies/EnemyAnimatorFactory.cs` — role→override: Orc_Mage/_Warrior/_Tank → the override name
   (default Orc_* → base); `Resources.Load<RuntimeAnimatorController>` (override IS a RuntimeAnimatorController).
5. `Village/Enemies/Enemy.cs` — in `RangedAttack`: `PlayWindUp()`→`PlayCast()` + `_agent.isStopped=true` for
   the cast window then resume (ROOTED cast, reuse `TelegraphThenAttack` shape); in `DriveAnimator` drive
   `SetInjured(HpFraction < ~0.3)`; flag-gate.
6. `Village/Enemies/EnemyCombatAudio.cs` — add `PlayCastCharge()` via `CoreServices.Audio` (the audio tell).

**Build/gate (editor CLOSED, batchmode):** CLI writes .cs → brace gate → CompileGate → build controllers
(`run-unity-method.ps1 -Method DeNelle.Editor.BuildOrcHumanoidController.Run`) confirm `ORC_CTRL_OK` +
4 .controller assets + walk/cast resolved (no idle-fallback) → fleet smoke (nonzero Speed, no param-MISSING,
mage rooted on cast, low-HP flips Injured) → owner felt-verify in the arena.
Reference: `Assets/Editor/HeroAnimatorFactory.cs` (the proven blend-tree/Cast/override pattern).
