# WORK ORDER 285 — 3D Real-Time Fighting Uses the Animation Library (responsive event → clip)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
(see `WORK_ORDER_285_3d_combat_uses_animation_library.RESULT.md`).
This is the WO that already built the Knight's 3-swing escalating combo plus hit/death/block routed
through `ActorAnimator` — i.e. combo sequencing is NOT greenfield; extend this spine.

> ⚠ **§15 STALENESS FLAG (2026-08-09).** Read `READY TO IMPLEMENT` for ~2 months with a RESULT file
> beside it.
**Date:** 2026-06-06
**Author:** UI (creative/architecture lane)
**Owner approval:** Samantha — greenlit. Goal: the **3D real-time combat** actually
plays the new animation clips, responsively, for every fighter.
**Priority:** High — WO-283 imported the clips and WO-284 built the routing layer; this
is the payoff WO that makes melee/cast/hit/death visible in actual 3D fighting.
**Lane:** Combat/AI — **code only.** NO `VillageSceneBuilder.cs` (frozen, §3/§9), NO
`.unity` hand-edits. NO new `System.Reflection`.
**Implemented + build-verified by:** CLI (batchmode compile-gate + play smoke test).
**Depends on:** WO-283 (clip library + controllers) ✅ and WO-284 (`ActorAnimator` /
`AnimParams` driver) ✅ — both landed, so this is ready to assign now.

**Scope = real-time 3D combat only** (village defend + open-world / "3D fighting").
The ATB turn-based battle is a separate path — NOT this WO.

---

## 1. What exists today (don't rebuild — connect)

The in-world combat already triggers animation, using the now-unified params:
- **Enemy** (`Enemies/Enemy.cs`): WindUp telegraph → Attack → Hit (+HitDir) → Dead,
  every call param-guarded (the DEF-48 telegraph + DEF-46 directional hits).
- **Hero abilities** (`Hero/HeroAbilities.cs`): fires `Cast` (guarded, re-scans after
  `HeroBodySwapper` swaps the controller).
- **Hero melee** (`Enemies/PlayerAttackController.cs`): swing damage + perfect-hit window
  + whoosh/trail — but the **body attack animation is thin** (timing/feedback focus, DEF-47).
- **Locomotion** (`Hero/HeroLocomotion.cs`): Speed + Victory.

WO-284 routed all of these through `ActorAnimator`/`AnimParams` (Core). This WO makes
the combat path **drive the full clip set** and **feel connected**.

## 2. Deliverables

### A. Hero melee → real attack clips (the main gap)
`PlayerAttackController` swing should call `ActorAnimator.PlayAttack(combo)` so the body
plays the class clip from the new library:
- **Knight** → sword-and-shield set (`Assets/Action/Knight/`) — cycle a small combo index
  (e.g. 3 swings) on consecutive hits; reset on idle.
- **Ranger** → aim/shoot (`Ranger/` + existing `BowRecoil`).
- **Mage / Cleric** → `Cast` (Wizard set) — reconcile with `HeroAbilities` so a basic
  attack and an ability cast don't double-fire.

### B. Hit reactions + death on the hero (currently enemy-only)
- On hero taking damage (`Hero/HeroHealth.cs` / `HeroHitReaction.cs`) →
  `ActorAnimator.PlayHit(dir)` using `Shared_Hit_Reaction` (+ direction from attacker bearing).
- On hero 0 HP (`HeroHealth` death path, DEF-102) → `ActorAnimator.Die(dir)`
  (Shared_Death / Standing_Death_Left/Right); `Revive()` on respawn.

### C. Enemy uses the injured/attack set
- Enemy attack/hit/death already trigger — ensure they resolve against the new clips
  (`Shared/` + `Enemies/` injured set, per the WO-283 enemy factory). No T-pose; missing
  clip = safe no-op via `ActorAnimator` guards.

### D. Block (new)
- Where a hero/enemy can block (shield classes), `SetBlocking(true/false)` → `Shared_Block`.

### E. Responsiveness — make hits *connect*
This is the "responsive" half — animations must line up with gameplay, not lag it:
- **Damage on the impact frame:** drive the perfect-hit window / damage application off the
  attack clip's contact moment (AnimationEvent on the clip, or the existing
  `_perfectHitWindowStart/End` timing in `PlayerAttackController`) so the swing visibly
  connects when damage lands.
- **Attack while moving:** use the WO-218 upper-body layer so an attack/cast doesn't freeze
  locomotion — hero stays responsive (no root lock during a swing in open-world).
- **Cancel/interrupt:** a new attack input or a hit reaction interrupts the current swing
  cleanly (no stuck pose); telegraph→attack→recovery timing per WO-217.
- **No input latency:** trigger on input, not on a delayed coroutine; keep transition
  durations short for combat states.

## 3. Acceptance criteria

- [ ] In real 3D fighting, each hero class plays its **class attack clip** on swing
      (Knight combo cycles; Ranger shoots; Mage/Cleric cast) — not a static pose.
- [ ] Hero plays a **hit reaction** when damaged and a **death** anim at 0 HP; respawn clears it.
- [ ] Enemies play windup → attack → directional hit → death from the new clip set; no T-pose.
- [ ] Block plays for shield-capable fighters.
- [ ] Damage lands on the swing's impact frame (visible connection), and the hero can
      attack while moving (upper-body layer) without locomotion freezing.
- [ ] Attacks/hits interrupt cleanly — no stuck or sliding poses during combat.
- [ ] All driving goes through `ActorAnimator`/`AnimParams` (WO-284) — no reintroduced
      local `StringToHash` or raw `SetTrigger` in combat code.
- [ ] **Brace balance check** on every `.cs` edited (CLAUDE.md §1); `?.` on cross-module
      calls (§10). Compile-gate + play smoke test green (CLI).
- [ ] Tested in BOTH village-defend and open-world combat, all 4 heroes + a basic enemy.

## 4. Do NOT touch

- ATB turn-based battle (`DeNelle.BattleATB`) — separate WO if wanted.
- The clip FBX / import settings (WO-283) or `AnimParams`/`ActorAnimator` core design
  (WO-284) — consume them, don't redesign.
- `VillageSceneBuilder.cs` / `.unity` files.

## 5. Notes for CLI / owner

- This is integration + timing, not new systems: the triggers mostly exist; the work is
  routing them to the new clips and aligning impact frames.
- Related: WO-259 (in-world combat core), WO-269/DEF-269 (open-world hero combat range/
  facing), WO-217/218 (anim feel + upper-body layer), DEF-102 (hero death path).
- Knight full combo trees remain a separate follow-up (WO-283 note) — this WO wires a
  small responsive combo, not the entire 99-clip set.
- Linear note: workspace is at its free-issue limit, so this couldn't be filed as a DEF
  ticket yet — assign from the WO file, or clear a Linear slot and I'll create it.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
