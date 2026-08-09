# WORK ORDER 284 — Unified Animation Routines (event → clip, for all actors)

**Status:** ⚠ **PARTIAL — hero slice SHIPPED, enemy half NEVER MIGRATED** — committed `bac3fd9`
(see `WORK_ORDER_284_unified_animation_routines.RESULT.md`, which states "partially met (heroes done)").
`AnimParams.cs`, `ActorAnimator.cs`, `IActorAnimator.cs` are all present.
**Still open:** Enemy / Pet / Dragon / DungeonHero were never migrated onto `ActorAnimator`.

> ⚠ **THE INVARIANT THIS WO BOUGHT, AND IT IS BINDING ON NEW WORK (2026-08-09):** `ActorAnimator` is
> "the ONLY place that calls SetTrigger/SetBool/SetFloat/SetInteger" (§3). Any future beat/strike
> sequencer MUST drive animation THROUGH `ActorAnimator` and never touch the `Animator` directly, or it
> breaks the single invariant this WO exists to hold. `ActionBundlePlayer.cs:122` already resolves an
> `ActorAnimator` and warns when absent — that is the correct pattern to copy.

> ⚠ **§15 STALENESS FLAG (2026-08-09).** Read `READY TO IMPLEMENT` for ~2 months with a RESULT file
> beside it; contributed to a session re-designing shipped work. Re-open the ENEMY half as a scoped
> follow-up — do not re-mint a new number for it.
**Date:** 2026-06-06
**Author:** UI (creative/architecture lane)
**Owner approval:** Samantha — greenlit. Scope: **all actors**, **unify + standardize**.
**Priority:** High — the runtime layer that actually drives the new animation library
(WO-283). Without it the clips exist but nothing routes idle/walk/hit/death/etc. to them.
**Lane:** Combat/AI + animation — **code only** (+ animator-factory states). NO
`VillageSceneBuilder.cs` (frozen, §3/§9). NO `.unity` hand-edits. NO new `System.Reflection`.
**Implemented + build-verified by:** CLI (batchmode compile-gate + animator bake).
**Depends on:** WO-283 (clip library + per-class controllers). Run **after 283**.

**Architecture reference:** `docs/CHARACTER_ARCHITECTURE.md` — "a universal **action
verb** drives animation + VFX." This WO is the concrete, pragmatic first cut of that
verb layer, scoped to animation. `docs/ANIMATION_PIPELINE.md` for the clip set.

---

## 1. Problem (why this WO exists)

Animator parameters are scattered and inconsistent across actors today — every class
re-declares its own `StringToHash` with no shared source of truth, and names disagree:

| Actor (file) | Params used today |
|---|---|
| `Village/Hero/HeroLocomotion.cs` | `Speed`, `Victory` |
| `Village/Hero/HeroAbilities.cs` | `Cast` |
| `Village/Hero/HeroImpactFeedback.cs` | `BowRecoil` |
| `Village/Enemies/Enemy.cs` | `Speed`, `Attack`, `WindUp`, `Hit`, `Dead`(bool), `HitDir`(int) |
| `Village/Enemies/EnemyBrain.cs` | `IsAlert` |
| `Village/Enemies/DragonBoss.cs` | `Speed`, `Attack`, `Dead` |
| `Village/Enemies/PlayerAttackController.cs` | `Attack` |
| `Pets/Pet.cs` | `Speed`, `Attack`, `Hit`, `Dead` |
| `Pets/PetAnimatorController.cs` | `Speed`, `Attack`, `Hit`, **`Death`** |
| `Pets/PetEmoteController.cs` | `Happy`, `Alert`, `Celebrate` |
| `Dungeons/DungeonHero.cs` | `Speed` |

**Conflict to fix:** death is `Dead` (bool) in Enemy/Pet/Dragon but `Death` (trigger) in
`PetAnimatorController`. Standardize to ONE convention everywhere.

---

## 2. Deliverable A — canonical parameter set + constants (in Core)

Create `Assets/_Modules/Core/Combat/AnimParams.cs` (`DeNelle.Core`) — the single source
of truth all controllers AND all callers use. No more local `StringToHash` per class.

| Constant | Type | Drives |
|---|---|---|
| `Speed` | Float | Locomotion blend: idle ↔ walk ↔ run (0 → 1) |
| `InCombat` | Bool | Idle vs **combat** idle (Shared_Idle ↔ Shared_Combat_Idle) |
| `Attack` | Trigger | Primary attack / melee (per-type clip) |
| `Combo` | Int | Optional combo index (Knight has multiple) — 0 if unused |
| `Cast` | Trigger | Spell cast (Wizard set; Mage + Cleric) |
| `WindUp` | Trigger | Telegraph before attack (existing DEF-48 enemy tell) |
| `Block` | Bool | Hold block (Shared_Block) |
| `Hit` | Trigger | Hit reaction |
| `HitDir` | Int | 0=front,1=left,2=right,3=gut (Shared_Hit_Reaction + injured turns) |
| `Dead` | Bool | **Canonical death latch** (replaces the `Death` trigger) |
| `DeathDir` | Int | 0=fall,1=left,2=right (Shared_Death / Standing_Death_Left/Right) |
| `Victory` | Trigger | Shared_Victory_Pose |
| `TurnDir` | Int | -1=left, 0=none, 1=right (Shared_turn_left / Shared_Turn_Right) |
| `Emote` | Int | Pet emotes: 1=Happy, 2=Celebrate, 3=Alert |

Keep `BowRecoil` (Ranger-specific) and `IsAlert` (= drive from `InCombat`/AI) reconciled —
fold `IsAlert` into `InCombat` or keep as a documented alias; call it out in RESULT.

## 3. Deliverable B — the routine driver

`Assets/_Modules/Core/Combat/IActorAnimator.cs` + `ActorAnimator.cs` (`DeNelle.Core`,
plain MonoBehaviour, no Village/Pet deps so every assembly can add it).

```csharp
public interface IActorAnimator {
    void SetLocomotion(float speed01);     // idle/walk/run blend
    void SetCombatStance(bool inCombat);   // idle vs combat idle
    void PlayAttack(int combo = 0);
    void PlayCast();
    void PlayWindUp();
    void SetBlocking(bool on);
    void PlayHit(HitDirection dir);
    void Die(DeathDirection dir);          // latches Dead = true
    void Revive();                         // clears Dead (respawn paths)
    void PlayVictory();
    void PlayTurn(TurnDirection dir);
    void PlayEmote(EmoteType emote);       // pets; no-op if absent
}
```

`ActorAnimator` resolves the `Animator`, guards every call (`Animator` may be null /
param may be absent on a given controller — log once, no spam), and is the ONLY place
that calls `SetTrigger/SetBool/SetFloat/SetInteger` for actor animation. Enums
(`HitDirection`, `DeathDirection`, `TurnDirection`, `EmoteType`) live in Core.

## 4. Deliverable C — event → routine wiring (the "on hit / on death / on walk / on idle… for all")

Route each gameplay event to the driver, for **every** actor type:

| Event | Source today → call |
|---|---|
| **On idle** | no movement input / `Speed→0`, `InCombat` per AI/combat state → `SetLocomotion(0)` + `SetCombatStance(...)` |
| **On walk / run** | locomotion speed → `SetLocomotion(speed01)` (Hero: `HeroLocomotion`; Enemy: `Enemy`/NavAgent speed; Pet: `Pet`/`PetAnimatorController`) |
| **On attack** | `Enemy` melee, `PlayerAttackController`, `HeroAbilities`, `Pet` → `PlayAttack`/`PlayCast` (+`PlayWindUp` telegraph) |
| **On block** | block state → `SetBlocking(true/false)` |
| **On hit** | `HeroHitReaction`, `EnemyHitReaction`, `Pet` damage path → `PlayHit(dir)` (compute dir from attacker bearing — Enemy already has `HitDir`) |
| **On death** | `HeroHealth`, `HeartController`/enemy death, `DragonBoss`, `Pet` → `Die(dir)` (latch `Dead`); `Revive()` on respawn |
| **On victory** | wave-clear (`WaveCelebrationManager`/`HeroVictoryPoseBridge`) → `PlayVictory()` |
| **On turn** | sharp facing change → `PlayTurn(dir)` (optional polish; safe to stub if blend covers it) |
| **On emote** | `PetEmoteController` → `PlayEmote(...)` |

Migrate the §1 callers to go through `ActorAnimator` + `AnimParams`. Delete the now-dead
local hashes. Hero, Enemy, DragonBoss, Pet all get an `ActorAnimator` on their prefab/root
(or resolve-or-add at runtime in their existing `Awake`).

Animation **events** (footstep SFX, attack-connect VFX/damage frame, hit flash) reuse the
existing systems — `HeroFootstepController`, `HeroImpactFeedback`, `AbilityAudioBridge`,
`VFXManager`. Add clip AnimationEvents (via importer) or `StateMachineBehaviour`s that call
into those; do NOT duplicate their logic here.

## 5. Deliverable D — controllers expose the canonical set (coordinate with WO-283)

`HeroAnimatorFactory` (+ the enemy/pet animator factories) must build controllers that
declare **exactly** the `AnimParams` set and contain states/transitions for: Locomotion
blend (Speed), Combat-idle (InCombat), Attack/Cast (upper-body layer already exists, WO-217/218),
WindUp, Block, Hit (+HitDir), Death (Dead/DeathDir), Victory, Turn. Where a clip is absent
for a given type (e.g. Ranger has no block), the state is omitted and `ActorAnimator`'s
null-guard makes the call a safe no-op.

> If WO-283 and WO-284 both edit `HeroAnimatorFactory`, 284 runs second and is authoritative
> on the parameter set. Keep 283's clip-loading; 284 finalizes params/states.

---

## 6. Acceptance criteria

- [ ] `AnimParams.cs` is the only definition of actor animator param hashes; no actor
      class declares its own `StringToHash("Speed"/"Hit"/...)` anymore (grep proves it).
- [ ] Death uses the single canonical `Dead`(bool)+`DeathDir` everywhere — the `Death`
      trigger in `PetAnimatorController` is gone.
- [ ] `ActorAnimator`/`IActorAnimator` is the sole caller of actor `SetTrigger/SetBool/
      SetFloat/SetInteger`; every call null-guarded.
- [ ] All four heroes, a basic enemy, the dragon boss, and a pet each correctly play:
      idle, walk, run, attack/cast, hit reaction, death, victory (play smoke test).
- [ ] Death latches (no flicker back to idle); respawn `Revive()` clears it.
- [ ] Controllers (from the factories) expose the full `AnimParams` set; missing-clip
      states degrade to safe no-ops, no errors in console.
- [ ] **Brace balance check passes on every `.cs` edited** (CLAUDE.md §1) —
      `using DeNelle.Core.Combat;` present where the interface/params are used.
- [ ] Null-conditional (`?.`) on cross-module service calls (CLAUDE.md §10).
- [ ] Batchmode compile-gate + build-verify green (CLI). RESULT lists every migrated file
      and how `IsAlert`/`BowRecoil` were reconciled.

## 7. Do NOT touch

- `VillageSceneBuilder.cs` (frozen) or `.unity` files by hand.
- The clip FBX or import settings (WO-283 owns those).
- Asset locations / Addressables (WO-282 owns those).
- VFX/SFX trigger logic — reuse existing systems via animation events, don't reimplement.

## 8. Notes for CLI

- This is mostly **unification + routing**, not greenfield (per CHARACTER_REFACTOR_PLAN
  philosophy). The driver is small; the work is migrating ~10 call sites cleanly.
- Assembly placement: constants + interface + enums + `ActorAnimator` in `DeNelle.Core`
  so Village, Pets, and Dungeons can all use them (all reference Core).
- Turn routine is optional polish — if the locomotion blend already reads fine, stub
  `PlayTurn` and note it; don't block the WO on it.
- Sequencing: **283 → 284 → 282** (build clips → standardize/route → relocate to
  Addressables). See `OVERNIGHT_QUEUE_2026-06-06.md`.
