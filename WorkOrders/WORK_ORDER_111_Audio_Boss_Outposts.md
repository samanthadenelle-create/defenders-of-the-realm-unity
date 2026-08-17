<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **NUMBER COLLISION — this document does not own WO-111; `WORK_ORDER_111_world_resource_collection_pillar.md` does.**
> Referred to hereafter as **WO-111-B (audio / boss outposts)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK_ORDER_111 — Audio Depth + Epic Boss Battles + Enemy Outposts (Chunk 10)

**Status: READY TO IMPLEMENT**

**Context:** Builds on prior (WO-108 castle/outposts/camps with ClaimableCamp/Outpost/CampSystem, WO-109 NPC dialogue/equip, VFX pooling, DragonBoss phases, Economy for rewards, builder for world, CoreServices.Audio).

**Prioritize mobile:** Pooling (VfxPool exists), limited concurrent audio, small/additive scenes, _M or Quaternius low-poly.

## Review Summary (done first)
- **Audio:** DeNelle.Audio/AudioService (implements IAudioService: PlaySfx(AudioClip), PlayMusic, PlayUiClick; owns mixer groups Master/Music/SFX/UI/Voice, crossfade). Core/IAudioService. Village/Audio/GameSfx (generated procedural clips for tower fire/place, wave start, lookout horn — routed via CoreServices.Audio?.PlaySfx; CC0 Resources/Sfx/ override path). Village has TowerAudioController, WaveMusicController, HeartwoodAmbient, etc. audio-mix-spec.md exists. CoreServices.Audio for cross (?.).
- **VFX/Spells:** Village/Vfx/VFXManager (pooling via VfxPool, VFXCatalog, VFXType enum with Impact/Cast/Death/Env/Aura; Play/PlayCasting/PlayImpact/PlayDeath/PlayAura/PlayEnvironment; quality tiers for mobile). CombatFeedbackManager, HitStopManager, DecalSpawner, PetAuraVFX, EnvironmentVFX. Spells Pack, Mirza Beig, Lana RPG VFX referenced in catalogs. EliteVFXController for boss/elite auras/death (hooks Enemy.Die/Attack).
- **Bosses:** Enemies/DragonBoss (phases: Circling 100-60%, Stooping 60-25%, Last Wing 25-0%; VFX via Elite, animator params Speed/Attack/Dead; IDamageable; flight kinematic; rewards on death). Ties to PatriciaLight apex wave.
- **Outposts/Camps:** Village/World/Camps/ (ClaimableCamp: clear via guards/kills -> claim -> build Outpost with trickle + defense wave; CampSystem for spawning; CampGuards, CampDefenseWave, Outpost, CampVisual/PromptUI). Previous: OutpostHub for player build grids + troops. World/ has RegionMobSpawner etc. Quaternius for buildings (medieval/enemy-themed via catalog; URP native, _M not required but low-poly). OuterWorldBuilder for region anchors. Economy for rewards (Grant on clear/secure).
- **Other:** EconomyService for loot/rewards. Builder (OuterWorldBuilder, VillageSceneBuilder) for world/castle. VFX pooling good for perf.

Gaps: Limited SFX coverage (extend GameSfx), boss phases basic (enhance with audio/VFX), outposts more camp-like than full "clear + boss + claim outpost scene" (extend for enemy-themed additive scenes with Quaternius).

## Proposed Architecture
**Audio:**
- Extend GameSfx (or new CombatSfxBridge.cs in Village/Audio or Vfx) with procedural/generated clips for new events (sword clash = noise burst, spell cast = whoosh + tone, tower arrow = twang + hit, harvest = chop, upgrade = build thunk, death = groan + impact, hit reaction = grunt).
- Tie-in: 
  - VFXManager: After PlayVFX(type, pos), if combat type play matching Sfx (e.g. on Impact_Flame play "spell_hit").
  - Enemy.cs: OnHit (sword clash), Die (death sfx + VFX).
  - PlayerAttackController / HeroAbilities: On swing/cast (weapon/spell sfx).
  - TowerCombat / DefenseTower: On fire (arrow/magic pew).
  - NPCUpgradeStation / HarvestSite: On upgrade/harvest (build/harvest sfx).
  - Use CoreServices.Audio?.PlaySfx(clip, volume) — routes to SFX mixer group.
- Mixer: AudioService already owns it; expose via IAudioService or Settings. Mobile: Volume ramp, one-shot limit (e.g. max 8 concurrent via simple counter or mixer ducking). Pool? Use PlayOneShot on 2-4 shared AudioSources.
- New SfxId entries if expanding library (but clip overload fine for Village).
- Extend audio-mix-spec if needed, but keep simple.

**Epic Boss Battles:**
- New or extend: BossEncounter.cs (base for DragonBoss + future). 
  - Phases: HP % gates (e.g. 100/60/30/0). On phase change: VFXManager.PlayAura/Env, audio (roar + music sting via AudioService), mechanic swap (e.g. adds adds, area attack with VFX + sound).
  - Mechanics: Unique (breath cone with VFX + damage over time, summon minions, enrage with speed VFX).
  - VFX + Sound: Big scale (EliteVFX + custom), AudioService.PlaySfx for roars/hits, music crossfade to boss track.
  - Rewards: On death Economy.Grant (rare crystals, gear via inventory), XP, unlock.
- Hook: EnemyFactory or WaveManager spawns with Boss flag -> attach EliteVFX + BossEncounter.
- Memorable: Camera shake (existing bridge), slow-mo on phase, unique VFX (Spells Pack + custom).

**Enemy Outposts:**
- Extend Camps/: New EnemyOutpost.cs (or subclass ClaimableCamp) — "clear camp" variant.
  - Lifecycle: Spawn guards (CampGuards style, Quaternius enemy buildings/props for camp: use Quaternius "medieval" or enemy packs for huts/tents/forges — low poly).
  - Boss: On clear guards -> spawn Boss (DragonBoss or new with outpost theme), phases as above.
  - Claim/Rewards: On boss death -> Economy.Grant (higher yield based on distance from castle/0,0,0 or danger tier), loot items (via ItemInventory), "claim" flag for progression (unlocks deeper regions or better castle upgrades).
  - Scene: Additive (SceneManager.LoadSceneAdditive("EnemyOutpost_01"); use small optimized scenes or runtime spawn in OuterWorld with builder-like code for perf). Unload on clear/leave.
  - Loader: New EnemyOutpostSceneLoader.cs or extend OuterWorldBuilder/WorldSceneLoader. Trigger from castle gates or world travel. Progression: Tiered (near castle weak, far strong enemies + better loot via Economy multiplier).
- Assets: Quaternius Medieval Village MegaKit / enemies for buildings (prefabs in Modules/Prefabs; enemy-themed via props + spawn logic). Poly _M if available for consistency.
- Mobile: Small scenes, pooling (VfxPool for combat, audio limit), distance culling.
- Tie: Rewards feed Castle Economy (passive or direct). Outposts as "regional bosses" for progression.

**Mobile Perf:**
- Audio: Shared sources or PlayOneShot, volume by distance/group.
- VFX/Projectiles: VfxPool + ProjectilePool already exist — use.
- Scenes: Additive + unload; Quaternius low-poly.
- General: Reuse VFXManager quality tiers.

**Files (see impl):**
- Audio: Extend Village/Audio/GameSfx.cs, VFXManager.cs (tie calls), Enemy.cs, TowerCombat, etc.
- Boss: New BossEncounter.cs or edit DragonBoss/EliteVFXController.
- Outposts: Extend Village/World/Camps/ClaimableCamp.cs or new EnemyOutpost.cs, CampSystem; new EnemyOutpostSceneLoader; edit OuterWorldBuilder for spawns.
- Builder: If new outpost visuals.
- Economy: For rewards (existing Grant).
- Update READMEs (Audio, Village, HUD if needed), indices, catalogs for Quaternius refs.
- New: Perhaps AudioBridge.cs, EnemyOutpost.cs.

This makes battles immersive (sound everywhere), epic (phased bosses with VFX/audio), and expands world (outposts as content/progression hubs) while mobile-first and reusing systems.

WO created. Owner: final on exact SFX params, boss mechanics, outpost tiers/locations, Quaternius prefab names (check catalog).

Implement after proposal.