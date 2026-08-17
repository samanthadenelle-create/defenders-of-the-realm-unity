<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_504 — BATTLE VFX "WOW" (wire the owned packs into the live spine)

**Status:** READY TO IMPLEMENT · VFX/Combat lane · owner directive 2026-06-24 ("most wow, we do right not easy")
**Supersedes/absorbs:** WO-502 (weapon-VFX differentiation folds in as item #3 here).

## The finding (4-scout asset census, 2026-06-24)
The combat spine is MATURE and live: `VFXManager` (pooled, quality-gated) + `VFXType.cs` (50+ types) +
`SpellVfxFactory` (effect+element -> VFXType) + `CombatFeedbackManager` (hit-stop/combo/kill-streak/slo-mo/
shake) + swing trail + element-tinted impacts + death bursts + scorch decals + damage numbers. ALL firing in
the arena today. BUT the authored prefab packs are NOT wired into the catalog -> combat plays the PROCEDURAL
fallback (`AbilityVfxKit`) instead of the pro VFX we own.

## What we OWN (verified present)
- GIT-COMMITTED / clone-safe (SHIP ON THESE):
  - `Assets/Lana Studio/Casual RPG VFX/` — 124 prefabs (Slash, Range_attack, Fire, Shields, Fog, Orbs,
    Top_down_attack AoE telegraphs, States stun/levelup, Burst crit/flash, Regeneration heals, Loot).
  - `Assets/Spells Pack/Particles/Prefabs/` — 76 element spells (Arcane/Dark/Fire/Ice/Light/Nature/Storm) +
    HDR glow materials (Crystal 4.237, Arcane Shield 4.541, Fire Shield 4.237, FireTrail 2.996).
  - `Assets/Resources/VFX/Projectiles/` — 9 custom WebGL-safe (Projectile_/Explosion_ Fire/Ice/Storm/Arcane).
  - `Assets/Shaders/ForceFieldGate.shader` — custom fresnel-rim + dissolve + pulse (boss barrier/shield).
  - Bloom ACTIVE in `DefaultVolumeProfile.asset` (HDR > 1.0 glows automatically).
- LOCAL-ONLY / DO NOT SHIP-DEPEND:
  - `Assets/Mirza Beig/` — 261 prefabs but **GITIGNORED** (absent on fresh clone/CI -> silent procedural
    fallback). EXCLUDE from any shipped wiring. May be referenced only behind a present-check, never required.

## THE RULE (binding for this WO)
1. Ship wiring uses ONLY git-committed assets (Lana + Spells + custom Resources). Mirza Beig excluded.
2. Author the catalog by SCRIPT/RECIPE (editor generator that scans prefab folders), NEVER inspector
   drag-drop (owner canon `never-dragdrop-or-manual-playtest`).
3. Mobile URP: prefer the existing pooling + quality gate; keep drawcalls modest; reuse, don't author shaders.
4. **CURATE — "one soldier, not the brigade" (owner 2026-06-24).** Wire a MINIMAL set: the single best
   effect per `VFXType` the battle actually uses — NOT the whole 124+76 pack. Reference only the chosen few;
   Unity strips unreferenced assets from the build, so the rest stay benched on disk (no bloat). BUILD-SIZE
   GUARD: never copy/dump a whole pack into a `Resources/` folder (Resources force-ships everything). Put ONLY
   the curated prefabs in the resolve path; if the resolver is Resources-based, move just the chosen handful
   into `Resources/VFX/...`. Target ~one prefab per wired VFXType (impact/cast/death/aura/trail/telegraph).

## Slices (ranked wow-per-risk)
1. **Catalog wiring (foundation, gate-provable).** VERIFY how `VFXManager`/`VFXCatalog` resolve a `VFXType`
   to a prefab (ScriptableObject asset? Resources.Load by name? — read the code FIRST). Then SCRIPT-author the
   mapping of git-committed Lana + Spells + custom prefabs to the `VFXType` enum (Impact_*, Projectile_*,
   Cast_*, Death_*, Aura_*, Juice_*). Name-aligned 1:1 where obvious (Impact_Flame<-Lana Hit_fire, etc.).
   Outcome: combat instantly upgrades procedural -> authored. Effect PICKS are bones; owner felt-tunes.
2. **Arena bloom (multiplier, gate-provable).** Confirm the BattleArena scene actually receives bloom (global
   `DefaultVolumeProfile` vs a scene Volume). If the arena (built at far offset on `_arenaRoot`) gets none,
   add a URP `Volume` (+ tuned Bloom profile, threshold/intensity) under `_arenaRoot` so the HDR materials POP.
3. **Swing-trail color by weapon rarity (WO-502 ask).** Drive `PlayerAttackController` swing TrailRenderer
   color/width from `GearLoadout.EquippedWeapon.rarity` (steel->green->blue->violet->gold) + theme tint from
   `makersMark`. Use FireTrail.mat / Lana slash materials. Owner felt-tunes colors/intensity (inspector-exposed).
4. **Element-colored impacts.** Stamp weapon element onto `Enemy._nextImpactElement` so a fire sword hits with
   `Impact_Flame`, frost with `Impact_Ice`, etc.
5. **Telegraphed casts (the "about to smack you" tell).** On mage/enemy wind-up (rooted cast, DEF-48 telegraph)
   spawn a Lana `top_down_*_circle` ground ring + Spells decal so the player can read + interrupt.
6. **Multi-hit AoE cascade.** In `Blast()` scale the burst by hit count (>4 = bigger/secondary ring) so a
   swarm kill feels bigger than a single.

## Acceptance
- Combat plays AUTHORED prefabs (not procedural) for the wired types; gate-clean; headless markers green.
- No shipped reference to `Assets/Mirza Beig/**`. No inspector drag-drop authoring (catalog built by script).
- Trail/impact/telegraph effects driven by data (rarity/element), inspector-exposed for owner felt-tuning.
- BONES vs FINESSE: structure/wiring is CLI gate-provable; exact effect picks, colors, bloom intensity = owner.

## Do NOT touch
- The procedural `AbilityVfxKit` (keep as the fallback when a prefab is absent). The CombatFeedbackManager
  feel tuning (separate). VillageSceneBuilder. Anything gitignored as a hard dependency.
