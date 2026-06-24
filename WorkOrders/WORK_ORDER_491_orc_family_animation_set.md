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
