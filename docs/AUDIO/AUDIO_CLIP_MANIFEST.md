# Audio Clip Manifest (WO-571)

**Date:** 2026-06-28  **Owner:** Samantha  **Status:** living drop-in reference

This is the single list of every audio clip the game resolves **by Resources path**.
Drop a correctly-named file at the listed path and it "just works" — **no inspector
drag-drop** (canon bans it, memory `never-dragdrop`), no code change. This mirrors the
intro-image drop-in pattern: the wiring is done; the owner sources/generates + drops.

## How resolution works (the wiring)

- **Two Resources roots merge** into the same namespace: `Assets/Audio/Resources/...`
  and `Assets/Resources/...`. A path like `Music/echo_theme` resolves from either.
- A path here has **no `Resources/` prefix and no file extension** (`.mp3`/`.wav`/`.ogg`).
  Example: manifest path `Audio/Voice/HeartFailing` → file
  `Assets/Audio/Resources/Audio/Voice/HeartFailing.mp3` **or**
  `Assets/Resources/Audio/Voice/HeartFailing.wav`.
- **SFX** also accept a per-id override at `Sfx/Sfx_<SfxId>` (e.g. `Sfx/Sfx_FireExplosion`),
  and when no clip is found fall back to a **procedural synth tone** so they are never
  silent (`ProceduralSfx.cs`, `GameSfx.cs`, `EnemyCombatAudio.cs`).
- **Music / voice / ambient** have **no synth fallback** (you can't synth a song or
  speech) — a missing clip plays **silent** and is logged via `FlowTrace.Warn` so a run
  self-reports which cue has no audio.

Status legend:  ✅ present in repo · ❌ missing (silent, or synth placeholder for SFX)

---

## 1. Music — `MusicTrack` (resolved by `AudioBootstrap.cs`)

Files live under `Assets/Audio/Resources/` (and `Assets/Resources/Audio/` for GameOver).

| Resources path | Track | Status | Description |
|---|---|---|---|
| `title` | Title | ✅ | Title screen + cold-open theme |
| `village` | Village | ✅ | Town / hub exploration loop |
| `victory` | Victory | ✅ | Battle-win sting (no loop) |
| `defeat` | Defeat | ✅ | Battle-loss sting (no loop) |
| `Audio/Music/GameOver` | Defeat (override) | ✅ | Game-over screen music (wins over `defeat`) |
| `battle_theme_NEW` | Battle (pool 1) | ✅ | ATB / combat loop, rotation entry 1 |
| `battle_theme2_NEW` | Battle (pool 2) | ✅ | Combat loop, rotation entry 2 |
| `battle_theme3_NEW` | Battle (pool 3) | ✅ | Combat loop, rotation entry 3 |
| `mainworld1_NEW` | Overworld (pool 1) | ✅ | Open-world exploration loop 1 |
| `world_theme_NEW` | Overworld (pool 2) | ✅ | Open-world exploration loop 2 |
| `Music/echo_theme` | Arena | ✅ | "Echo's theme" — Arena raid BGM (soft, loops) |
| `Music/Raid/brass-rampart` | Raid | ✅ | Offensive-raid brass BGM (loops) |
| `dungeon` | Dungeon | ❌ **NEEDED** | Dungeon ambient loop ("echoes-beneath-elarion"). The only missing music track — Dungeon scenes play silent until dropped. |

## 2. Wave / battle-state music — `BattleMusicManager.cs`

Files under `Assets/Audio/Resources/Music/Battle/`. (Spaced-name variants are also tried.)

| Resources path | State | Status | Description |
|---|---|---|---|
| `Music/Battle/Overworld_Battle_1` | Combat | ✅ | General wave-combat loop |
| `Music/Battle/Overworld_Battle_2` | Intense | ✅ | High-pressure loop (5+ live enemies) |
| `Music/Battle/Overworld_Victory` | Victory | ✅ | Post-wave one-shot sting → returns to ambient |
| `Music/Battle/Overworld_Boss_Fight` | Boss | ✅ | Boss / apex-wave loop |

> NOTE: the stale FLAG at the bottom of `BattleMusicManager.cs` (clips "not under
> Resources") is now FALSE — the four clips are present under `Resources/Music/Battle/`.

## 3. Heartwood ambient + stingers — `HeartwoodAmbientController.cs` (WO-571 wiring)

All ❌ **NEEDED** — no clips exist yet. The controller now (a) self-attaches to the
Heart GameObject at runtime (was dead code, never attached) and (b) loads these by path.

| Resources path | Status | Description |
|---|---|---|
| `Audio/Ambient/Heartwood_Healthy` | ❌ | 100–75% HP bed: warm hum, leaves, occasional chime (loops) |
| `Audio/Ambient/Heartwood_Strained` | ❌ | 74–40% HP bed: deeper hum, occasional groan (loops) |
| `Audio/Ambient/Heartwood_Critical` | ❌ | 39–0% HP bed: dissonant undertone, bark cracking, wind (loops) |
| `Audio/Sfx/Heart_Hit` | ❌ | One-shot deep resonant bell-struck impact when the Heart takes damage |
| `Audio/Sfx/Heart_Fall` | ❌ | One-shot long descending tone + crack when the Heart is destroyed |

## 4. Voice — `TowerVoiceController.cs` (WO-571 wiring)

All ❌ **NEEDED**. Fires once when the Heart drops below 30% HP. Routed to the Voice
mixer group. Drop one OR several (one is chosen at random; all that exist rotate).

| Resources path | Status | Description |
|---|---|---|
| `Audio/Voice/HeartFailing`   | ❌ | "The Heart is failing!" low-HP VO line |
| `Audio/Voice/HeartFailing_1` | ❌ | Alternate low-HP VO line (optional) |
| `Audio/Voice/HeartFailing_2` | ❌ | Alternate low-HP VO line (optional) |
| `Audio/Voice/HeartFailing_3` | ❌ | Alternate low-HP VO line (optional) |

## 5. SFX — authored drop-in overrides (procedural synth plays until dropped)

Every SFX below currently plays a **procedural synth placeholder** (functional, not
final sound design). Drop an authored CC0/recorded clip at the path to replace it — no
code change. Files go under a `Resources/Sfx/` folder (e.g. `Assets/Resources/Sfx/`).

### 5a. `SfxId` events (`PlaySfxAtPosition`) — override at `Sfx/Sfx_<SfxId>`

| Resources path | Status | Description |
|---|---|---|
| `Sfx/Sfx_FireExplosion` | ❌ synth | Fire / meteor impact boom |
| `Sfx/Sfx_ArcaneExplosion` | ❌ synth | Arcane detonation ring |
| `Sfx/Sfx_Shockwave` | ❌ synth | Expanding ground-slam ring |
| `Sfx/Sfx_Heal` | ❌ synth | Heal contact chime |
| `Sfx/Sfx_WizardCast` | ❌ synth | Caster wind-up |
| `Sfx/Sfx_FlameArrowLaunch` | ❌ synth | Flame-arrow whoosh |
| `Sfx/Sfx_TowerShot` | ❌ synth | Tower shot |
| `Sfx/Sfx_EnemyDeath` | ❌ synth | Enemy death pop |
| `Sfx/Sfx_WaveClear` | ❌ synth | Wave-clear fanfare |
| `Sfx/Sfx_LevelUp` | ❌ synth | Level-up chime |
| `Sfx/Sfx_ComboSmall` | ❌ synth | Combo tier-1 sting |
| `Sfx/Sfx_ComboBig` | ❌ synth | Combo tier-2 fanfare |
| `Sfx/Sfx_PetFireAura` | ❌ synth | Pet fire-aura loop |
| `Sfx/Sfx_PetAttack` | ❌ synth | Pet attack impact |
| `Sfx/Sfx_WardLit` | ❌ synth | Ward-stone relit |
| `Sfx/Sfx_WardDim` | ❌ synth | Ward-stone goes cold |

### 5b. Named one-shots (`GameSfx`, `EnemyCombatAudio`, `AudioService`)

| Resources path | Status | Description |
|---|---|---|
| `Sfx/UiClick` | ❌ synth | Shared UI button click |
| `Sfx/TowerFire` | ❌ synth | Tower fire "pew" |
| `Sfx/TowerPlace` | ❌ synth | Tower placement "thunk" |
| `Sfx/WaveStart` | ❌ synth | Wave-start battle horn |
| `Sfx/LookoutHorn` | ✅ | Lookout "raid incoming" horn (authored .wav present) |
| `Sfx/SwordClash` | ❌ synth | Melee clash |
| `Sfx/SpellCast` | ❌ synth | Hero spell cast |
| `Sfx/TowerArrowHit` | ❌ synth | Arrow impact |
| `Sfx/PetHarvest` | ❌ synth | Echo/pet harvest |
| `Sfx/BuildingUpgrade` | ❌ synth | Building upgrade confirm |
| `Sfx/LevelUp` | ❌ synth | Level-up (GameSfx variant) |
| `Sfx/EnemyDeath` | ❌ synth | Enemy death (GameSfx/EnemyCombatAudio) |
| `Sfx/HeroHit` | ❌ synth | Hero takes a hit |
| `Sfx/BuildDenied` | ❌ synth | Build-rejected buzz |
| `Sfx/EnemyHit` | ❌ synth | Enemy hit fallback |
| `Sfx/EnemyCastCharge` | ❌ synth | Enemy cast-charge telegraph |

### 5c. Ability-effect SFX (`AbilityAudioBridge` / `ProceduralSfx.ForKind`)

Override at `Sfx/<AbilityEffect>` (one per `AbilityEffect` enum value). All ❌ synth.

---

## Orphan / housekeeping

- `Assets/Resources/Audio/bellssteel-panic.mp3` — present but **no code reference**
  (orphaned). Owner decision: wire it (to what cue?) or delete during the asset purge.

## Owner-decision flags

1. **Dungeon music** (`dungeon`) — the one missing MUSIC track. Source/generate a
   dungeon ambient loop and drop at `Assets/Audio/Resources/dungeon.mp3`.
2. **Heartwood ambient (3 beds + 2 stingers)** and **Heart voice line(s)** — none exist;
   sourcing them is a content task (§3, §4 above).
3. **SFX sound design** — all SFX are placeholder synth. Decide which to replace with
   authored CC0/recorded clips (§5).
4. **`WaveMusicController` retirement** — superseded by `BattleMusicManager`; it ships
   silent and inert. Remove its attach from `WaveSystemBridgeBootstrap` once
   `BattleMusicManager` is felt-verified.
