# AUDIO SME Dossier — the game's audio content & pipeline

**Date:** 2026-07-12 (overnight SME research session)
**Scope:** all audio content (`Assets/Audio/`, every `Resources/` audio drop-in, Hovl bundled sounds, Mirza Beig check) + the full DeNelle.Audio consumption architecture.
**Verified from code and files on disk, not comments.** All line numbers cite the working tree at commit `c6912c9d` (branch `wip/village2-and-f8-tickets`).

---

## Table of contents

1. [Inventory — every audio pack/folder](#1-inventory)
2. [How WE consume it — the audio architecture](#2-how-we-consume-it)
3. [Coverage audit — game moment → sound](#3-coverage-audit)
4. [Web research — pack identities & licenses](#4-web-research)
5. [Opportunities + gaps](#5-opportunities--gaps)
6. [Executive summary](#6-executive-summary)

---

## 1. Inventory

### 1a. Totals at a glance

| Location | Files | Size | What |
|---|---|---|---|
| `Assets/Audio/` | 33 audio/asset files | 51.2 MB | Music (15 MP3) + combat SFX (17 WAV) + AudioMixer |
| `Assets/_Modules/Audio/Resources/Sfx/` | 16 WAV | 3.0 MB | Authored SFX drop-ins (the runtime-loaded set) |
| `Assets/Resources/Audio/` + `Assets/Resources/Sfx/` | 3 files | 4.3 MB | GameOver music, LookoutHorn, one orphan |
| `Assets/Hovl Studio/HSFiles/Sounds/` | 12 WAV | 5.2 MB | Hovl RPG VFX Bundle's bundled skill sounds |
| **Total shipped audio** | **~64 files** | **~64 MB** | |

### 1b. Music — `Assets/Audio/Resources/` (loaded by short Resources name)

All BGM is **owner-generated via Suno** (per `AudioBootstrap.cs:103,108,122` "owner Suno track 2026-06-29" and `BattleMusicManager.cs:42` "Suno-generated") — not a purchased pack. MP3 format throughout.

| File | Duration | Role (runtime) | Status |
|---|---|---|---|
| `title.mp3` | 69 s | Title/cold-open theme | wired ✔ |
| `whispering_pines.mp3` | 229 s | PRIMARY town/hub theme (Village track) | wired ✔ |
| `whispering_depths.mp3` | 158 s | Dungeon ambient ("The Whispering Depths") | wired ✔ |
| `siege_iron_bastion.mp3` | 320 s | PRIMARY battle theme | wired ✔ |
| `mainworld1_NEW.mp3` | 134 s | Overworld exploration | wired ✔ |
| `victory.mp3` | 40 s | Battle-win sting (no loop) | wired ✔ |
| `defeat.mp3` | 174 s | Battle-loss sting — immediately **overridden** by GameOver.mp3 (`AudioBootstrap.cs:116-117`) | shadowed |
| `battle.mp3` | 134 s | **ORPHAN** — nothing Resources-loads the name "battle" anymore (bootstrap loads `siege_iron_bastion` instead, `AudioBootstrap.cs:124`) | dead weight 3.0 MB |
| `Music/echo_theme.mp3` | 76 s | Arena raid BGM ("Echo's theme") | wired ✔ |
| `Music/Raid/brass-rampart.mp3` | 198 s | Offensive-raid BGM (WO-453) | **wired but UNPLAYABLE — code bug, see §2d** |
| `Music/Battle/Overworld_Battle_1.mp3` | 152 s | Wave-combat loop (BattleMusicManager Combat state) | wired ✔ |
| `Music/Battle/Overworld_Battle_2.mp3` | 214 s | High-pressure loop (5+ live enemies) | wired ✔ |
| `Music/Battle/Overworld_Boss_Fight.mp3` | 165 s | Boss/apex-wave loop | wired ✔ |
| `Music/Battle/Overworld_Victory.mp3` | 124 s | Post-wave one-shot sting | wired ✔ |

Elsewhere:

| File | Duration | Status |
|---|---|---|
| `Assets/Resources/Audio/Music/GameOver.mp3` | 113 s | Game-over music; wins over `defeat` (`AudioBootstrap.cs:117`) ✔ |
| `Assets/Resources/Audio/bellssteel-panic.mp3` | 40 s | **ORPHAN** — no code reference (GUID `59ea263f…` referenced by nothing; confirmed again this session) |
| `Assets/Audio/Victory/Victory.mp3` | 18 s | **ORPHAN** — outside any Resources folder, GUID `9cd85130…` referenced by no scene/prefab/asset |

**Files the bootstrap TRIES to load that do NOT exist anywhere in the project** (each fires a `FlowTrace.Warn` per `AudioBootstrap.cs:181-185,201-205` on every boot):
`village` (`AudioBootstrap.cs:106`), `battle_theme_NEW`, `battle_theme2_NEW`, `battle_theme3_NEW` (`:125-127`), `world_theme_NEW` (`:129`). Verified absent by whole-tree filename search. Consequence: the Battle and Overworld "rotation pools" (WO-171) each hold exactly ONE clip — the rotation feature is currently a no-op.

### 1c. Combat SFX — `Assets/Audio/SFX/Combat/` (17 WAV, ~3 MB) — SOURCE copies

Freesound-sourced, ffmpeg-processed (trim + loudnorm to −16 LUFS, 44.1 kHz) per `SOURCE_LICENSE.md` in that folder. **These files are NOT loaded at runtime** — they are the masters; runtime copies live under `Assets/_Modules/Audio/Resources/Sfx/` (§1d). Contents: `sword_clash_1..4`, `sword_draw`, `melee_swing`, `projectile_whoosh_1..3`, `cast_spell`, `enemy_cast_chant`, `enemy_death`, `enemy_death_2`, `footsteps_walk_loop`, `dragon_roar`, `building_construct`, `ui_select`.

### 1d. Runtime SFX drop-ins — `Assets/_Modules/Audio/Resources/Sfx/` (16 WAV, 3.0 MB)

These are the files the game actually loads (`Resources.Load<AudioClip>("Sfx/<Name>")`). Mirrors of §1c under the runtime naming convention:

`SwordClash.wav` + `SwordClash2/3/4` (variant pool), `SwordSwing`, `WeaponDraw`, `SpellCast`, `EnemyDeath`, `EnemyDeath2`, `EnemyCastCharge`, `HeroHit`, `FootstepsWalk`, `DragonRoar`, `BuildingUpgrade`, `TowerArrowHit`, `UiClick`. Plus `Assets/Resources/Sfx/LookoutHorn.wav` (1.0 MB, the "raid incoming" horn).

### 1e. Hovl Studio bundled sounds — `Assets/Hovl Studio/HSFiles/Sounds/` (12 WAV, 5.2 MB)

Part of the purchased **Hovl Studio RPG VFX Bundle v6.0.4** (ledger). Files: `Skill 1.wav` … `Skill 11.wav` + `Hit Skill9.wav`. Consumed **only inside Hovl's own prefabs** (e.g. `Skill 1.wav` GUID referenced by `Assets/Hovl Studio/AOE Magic spells Vol.1/Prefabs/Knives.prefab` — an AudioSource on the VFX prefab). None are referenced by our C# audio code. When a Hovl VFX prefab with an embedded AudioSource is instantiated by VFXManager, its sound plays on the **default audio output**, NOT through our AudioMixer — it ignores the player's SFX volume/mute (see §5).

### 1f. Mirza Beig — "sound effects" flag: **NO audio content**

`Assets/Mirza Beig/` (Ultimate VFX 3.5.2) contains **zero audio files** — verified by extension sweep of the imported folder AND by scanning the 886 MB `Ultimate VFX.unitypackage` in the Asset Store cache for `.wav/.mp3/.ogg/.aif` pathnames (none). Mirza Beig Ultimate VFX is particles/shaders/textures only. The owner's "Mirza Beig sound effects" flag is a mis-attribution; the pack ships no SFX.

### 1g. leohpaz "RPG Essentials Sound Effects — FREE!" — **purchased, NEVER imported**

The ledger (`docs/SME/ASSET_STORE_LEDGER_2026-07-12.md`) lists leohpaz RPG Essentials Sound Effects — FREE v1.0 (16.1 MB, purchased 2026-06-29). **It is not in the project.** Verified: no `leohpaz`/`RPG Essentials` folder anywhere under `Assets/`, and — decisive — no `leohpaz` publisher folder in the Asset Store download cache (`%APPDATA%/Unity/Asset Store-5.x/` holds Blink, Carlos Wilkes, Game Fuel, Hovl Studio, Lana Studio, Mirza Beig, polyperfect, Supercyan, Yarn Spinner, Zakhan — no leohpaz). It was purchased but never even downloaded. See §4 for what it contains and §5 for why importing it is the cheapest coverage win available.

### 1h. Mixer — `Assets/Audio/Resources/Audio/GameAudioMixer.mixer`

One AudioMixer, five groups: Master / Music / SFX / UI / Voice, exposed params `MasterVol/MusicVol/SfxVol/UiVol/VoiceVol` (`AudioService.cs:171-183`). Resources-loaded by `AudioBootstrap` at `"Audio/GameAudioMixer"` (`AudioBootstrap.cs:55`).

---

## 2. How WE consume it

### 2a. The assembly and its services

Everything lives in **`DeNelle.Audio`** (`Assets/_Modules/Audio/`), resolved cross-assembly via **`CoreServices.Audio`** (the `IAudioService` seam, `Assets/_Modules/Core/Audio/IAudioService.cs`; registered in `AudioService.Awake`, `AudioService.cs:217`).

| Class | File | Role |
|---|---|---|
| `AudioBootstrap` | `AudioBootstrap.cs` | `[RuntimeInitializeOnLoadMethod]` — auto-spawns the AudioService in every scene, Resources-loads the mixer + all music clips by name (`:102-136`). No scene wiring, no prefab (the preferred `DeNelleAudioService` prefab path at `:69` is unused — no such prefab exists). |
| `AudioService` | `AudioService.cs` | The single audio surface: `PlayMusic`, `PlaySfx/PlayUiSfx/PlayVoice`, `PlaySfxAtPosition(SfxId)`, `SetVolume/SetMuted`, scene→track map (`:823-849`), ambient jukebox (WO-162/171), 8-voice round-robin SFX pool (`:151-153`). |
| `MusicDirector` | `MusicDirector.cs` | **THE single owner of music AudioSources** (2026-07-09 MUSIC_AUTHORITY_DESIGN). One A/B crossfade pair; every music requester Pushes/Releases a `MusicLayer` (priority stack: Cutscene > Outcome > Battle > Wave > Overworld > Ambient, `LayerFor` at `:158-173`); the highest active layer sounds. Two simultaneous beds are impossible by construction. |
| `SfxId` | `SfxId.cs` | 16-value enum of named SFX events (+None). |
| `SfxClipLibrary` | `SfxClipLibrary.cs` | ScriptableObject `SfxId → AudioClip` map. **No such .asset exists in the project** (verified: only the .cs + the editor builder). |
| `ProceduralSfx` | `ProceduralSfx.cs` | Synth fallback: generates a distinct placeholder tone per SfxId (sine sweep + noise + exp decay). Checks `Resources/Sfx/Sfx_<Id>` drop-in first (`:62`). |
| `MusicTrack` / `MusicTrackRegistry` | `MusicTrack.cs` | 9-track enum + owner-locked mix registry (volume/loop/fades, ported from `docs/audio-mix-spec.md`). |
| `BattleMusicManager` | `Village/Audio/BattleMusicManager.cs` | Wave-driven 4-state battle music machine (Combat/Intense/Victory/Boss) — listens to WaveManager events, pushes clips onto MusicDirector's Battle layer. |
| `GameSfx` | `Village/Audio/GameSfx.cs` | Static one-shot library for combat/world events; `Resources.Load("Sfx/<Name>") ?? Generate<Name>()` per sound. |
| `EnemyCombatAudio` | `Village/Enemies/EnemyCombatAudio.cs` | Enemy hit / death (+death variant) / cast-charge, same drop-in-or-synth convention. |
| `AbilityAudioBridge` | `Village/Hero/AbilityAudioBridge.cs` | Per-`AbilityEffect` cast SFX (synth w/ `Sfx/<AbilityEffect>` override, `:89`), class-flavoured variants, danger sting. |
| `WebGLAudioUnlock` | `WebGLAudioUnlock.cs` | First-gesture unlock for mobile/WebGL suspended AudioContext → `AudioService.ResumeAfterUnlock()` (`AudioService.cs:407-411`). |
| `MusicSelectionPanel` | `MusicSelectionPanel.cs` | The WO-162 jukebox UI over `AudioService.AmbientChoicesFor` (`AudioService.cs:971-992`). |
| `VillageAudioResources` | `Village/Audio/VillageAudioResources.cs` | WO-571 convention-path loader for the Village controllers (Heartwood beds, Heart voice) — drop a correctly-named clip and it plays, no inspector wiring. |

### 2b. The SfxId path — and the truth about clips

`AudioService.PlaySfxAtPosition(SfxId, pos, vol)` (`AudioService.cs:617-650`) resolves in strict priority:

1. Inspector-assigned `SfxClipLibrary` — **never assigned** (no prefab).
2. `Resources.Load<SfxClipLibrary>("Audio/SfxClipLibrary")` (`:628`) — **asset does not exist** (the WO-243 builder `Assets/Editor/Audio/SfxClipLibraryBuilder.cs` can bake it + per-id WAVs, but has never been run — `Assets/_Modules/Audio/Resources/Audio/` and the `Sfx_<Id>.wav` set are absent).
3. `ProceduralSfx.For(id)` (`:646`) — which itself first checks `Resources/Sfx/Sfx_<Id>` (**none of the 16 exist**; the authored WAVs use the GameSfx naming, not the `Sfx_` prefix) and then **synthesises a tone**.

**Conclusion: every one of the 16 `SfxId` events plays a procedural synth placeholder today.** None is silent (WO-243 guaranteed that), but none is authored audio either.

Callers of the SfxId path: `VFXManager.VfxToSfx()` (`Village/Vfx/VFXManager.cs:223-262` — the full VFXType→SfxId pairing table), `WardStone` (WardLit/WardDim), `KillComboTracker` (ComboSmall/Big), `WaveCelebrationManager` (WaveClear).

### 2c. The named-clip path (the one with real audio)

`GameSfx` / `EnemyCombatAudio` / `AbilityAudioBridge` / `HeroLocomotion` load by **string name** `Resources/Sfx/<Name>` and fall back to a local synth (or, for #51 clips, to silence). Because the 16 authored WAVs in `Assets/_Modules/Audio/Resources/Sfx/` use exactly these names, **this path plays real recorded audio** for: sword clash (4 random variants when `FeatureFlags.CombatFeel` is on, `GameSfx.cs:120-145`), sword swing, weapon draw, spell cast, enemy death (+variant), enemy cast-charge, hero hit, footsteps (`HeroLocomotion.cs:689`), dragon roar, building upgrade, arrow hit, UI click, lookout horn.

`AudioService.PlayUiClick()` (`AudioService.cs:572-577`) loads `Resources/Sfx/UiClick` (authored ✔) — the IAudioService seam DeNelle.HUD uses for button clicks.

### 2d. ⚠ BUG — the Raid BGM can never play (silent raid + boot-warning)

`RaidGarrisonSpawner.cs:156` requests `CoreServices.Audio?.PlayMusic(MusicTrack.Raid)` when a raid starts. The chain is broken in TWO places:

1. **`MusicTrackRegistry.Defs` has no `Raid` entry** (`MusicTrack.cs:139-159` — the dictionary stops at Arena). `PlayMusic(Raid)` hits `MusicTrackRegistry.Get(track) == null` and returns with `"[AudioService] No mix definition for track 'Raid' — ignored."` (`AudioService.cs:364-369`).
2. Even if it got past that, **`AudioService.SetMusicClip` has no `Raid` case** (`AudioService.cs:476-491`) — so `AudioBootstrap.cs:136`'s `TryAssignClip(service, MusicTrack.Raid, "Music/Raid/brass-rampart")` loads the clip and then silently drops it (the switch falls through). `ClipFor` (`AudioService.cs:435-449`) also has no Raid case.

`MusicDirector.LayerFor(Raid)` (`MusicDirector.cs:167`) and the Core enum bridge (`MusicDirector.cs:498,515`) are ready — only the registry entry, the `ClipFor` case, a `_raidClip` field, and the `SetMusicClip` case are missing. The 198-second `brass-rampart.mp3` is shipped, paid for in build size, and unreachable. `docs/AUDIO/AUDIO_CLIP_MANIFEST.md:46` marks it "✅ wired" — **stale/wrong**.

### 2e. Motion Caster → sfx

The Motion Caster tool (`Assets/Editor/MotionCasterWindow.cs` + `MotionCastings.cs:58` `public string sfxId`) binds an optional `sfxId` **string** per animation row into `Assets/Resources/Data/Canonical/motion-castings.json`. At play time `ActionBundlePlayer.cs:143-144` fires it and `:313-325` resolves it as **`Resources.Load<AudioClip>("Sfx/" + sfxId)`** — the GameSfx string convention, NOT the `SfxId` enum (the enum is unreachable from DeNelle.Village, per `ActionBundleCatalog.cs:21-22`). A missing clip warns once via `FlowTrace.Once` ("sfx-missing:<id>") and plays nothing.

Current data: 19 rows, 18 with empty `sfxId`, one wired — the Knight `castHeal` row (owner-pick 2026-07-12) has `"sfxId": "Heal"`. **There is no clip at `Resources/Sfx/Heal`** (verified), so the Knight's heal cast is silent through this path and logs the one-time warn. Pitfall to know: naming a row `"Heal"` does NOT reach `SfxId.Heal`'s synth — the enum path would need the file named `Sfx_Heal`; the Motion Caster path needs it named `Heal`. Dropping one WAV at `Assets/_Modules/Audio/Resources/Sfx/Heal.wav` fixes the row.

### 2f. Music routing (who pushes which layer)

- Scene loads → `AudioService.OnSceneLoaded` → `TrackForScene` (`AudioService.cs:823-849`): Title/Splash/Onboarding→Title; Village + MainCastle_Hall/CastleHub/Main_Castle_Overworld→Village (via ambient-jukebox `PlayAmbientContext`); `Dungeon_*`→Dungeon; ATBBattle→Battle. Unknown scenes leave music alone.
- Wave combat → `BattleMusicManager` pushes its four Suno clips on the Battle layer, 1.5 s crossfades, Victory sting then auto-falls back to ambient.
- Raid → broken (§2d). Arena → `echo_theme` on the Battle layer.
- The old `WaveMusicController` is **superseded and deliberately inert** (header banner, `WaveMusicController.cs:4-13`) — ships with null clips so it can't double-score; flagged for removal.
- `TowerAudioController` still carries `[SerializeField]` clips that nothing assigns (drag-drop is banned) → its build-complete/upgrade chimes are structurally silent; the actually-heard upgrade sound is `GameSfx.PlayBuildingUpgrade` (authored ✔).
- `HeartwoodAmbientController` + `TowerVoiceController` resolve by convention paths (`VillageAudioResources`) — **all their clips are missing** (§3).

---

## 3. Coverage audit

Legend: **AUTHORED** = real recorded clip plays · **SYNTH** = procedural placeholder tone plays · **SILENT** = nothing plays · **BROKEN** = code path defect.

| Game moment | Mechanism (file:line) | Resolves to | Status |
|---|---|---|---|
| Melee swing (whoosh) | `GameSfx.PlaySwordSwing` ← `PlayerAttackController.cs:359` | `Sfx/SwordSwing` | **AUTHORED** |
| Melee hit / clash | `GameSfx.PlaySwordClash` ← `PlayerAttackController.cs:394` | `Sfx/SwordClash`(+2/3/4 random) | **AUTHORED** (4 variants) |
| Weapon draw (enter combat) | `GameSfx.PlayWeaponDraw` ← `PlayerAttackController.cs:601` | `Sfx/WeaponDraw` | **AUTHORED** |
| Hero spell cast | `GameSfx.PlaySpellCast` / `AbilityAudioBridge.PlayForKind` | `Sfx/SpellCast` / synth per AbilityEffect | **AUTHORED** (GameSfx) / SYNTH (bridge) |
| Knight castHeal (Motion Caster row) | `ActionBundlePlayer.cs:313` | `Sfx/Heal` — **file missing** | **SILENT** (warns once) |
| Hero takes hit | `GameSfx.PlayHeroHit` ← `HeroHealth.cs:491` | `Sfx/HeroHit` | **AUTHORED** |
| Hero footsteps | `HeroLocomotion.cs:689` | `Sfx/FootstepsWalk` | **AUTHORED** (loop) |
| Enemy takes hit | `EnemyCombatAudio.cs:53` | `Sfx/EnemyHit` — file missing | SYNTH |
| Enemy death | `EnemyCombatAudio.cs:72-79` / `GameSfx` / `SfxId.EnemyDeath` | `Sfx/EnemyDeath` + `EnemyDeath2` | **AUTHORED** (2 variants) |
| Enemy caster telegraph | `EnemyCombatAudio.cs:97` | `Sfx/EnemyCastCharge` | **AUTHORED** |
| Dragon roar (boss swoop) | `GameSfx.PlayDragonRoar` ← `DragonBoss.cs:457` | `Sfx/DragonRoar` | **AUTHORED** |
| UI button click | `AudioService.PlayUiClick` (`:572`) | `Sfx/UiClick` | **AUTHORED** |
| Build denied | `GameSfx.PlayBuildDenied` ← `BuildFeedbackToast.cs:95` | `Sfx/BuildDenied` — missing | SYNTH (double-blip) |
| Tower placed | `GameSfx.PlayTowerPlace` ← `TowerPlacementSystem.cs:353` | `Sfx/TowerPlace` — missing | SYNTH |
| Tower fires | `GameSfx.PlayTowerFire` ← `TowerCombat.cs:335` | `Sfx/TowerFire` — missing | SYNTH |
| Tower arrow impact | `GameSfx.PlayTowerArrowHit` ← `TowerCombat.cs:374` | `Sfx/TowerArrowHit` | **AUTHORED** |
| Building upgrade / build complete | `GameSfx.PlayBuildingUpgrade` ← `NPCUpgradeStation.cs:206` | `Sfx/BuildingUpgrade` | **AUTHORED** |
| Wave countdown warning | `GameSfx.PlayLookoutHorn` + `AbilityAudioBridge.PlayDangerSting` | `Sfx/LookoutHorn` ✔ / synth sting | **AUTHORED** + SYNTH |
| Wave start | `GameSfx.PlayWaveStart` ← `WaveFeedbackDirector.cs:166` | `Sfx/WaveStart` — missing | SYNTH (horn) |
| Wave clear (SFX) | `SfxId.WaveClear` via `WaveCelebrationManager` | `Sfx_WaveClear` — missing | SYNTH (chime) |
| Wave clear (music sting) | `BattleMusicManager` Victory state | `Overworld_Victory.mp3` | **AUTHORED** |
| Level up | `GameSfx.PlayLevelUp` / `SfxId.LevelUp` | `Sfx/LevelUp` — missing | SYNTH |
| Kill combo tier 1 / 2 | `SfxId.ComboSmall/ComboBig` ← `KillComboTracker` | `Sfx_Combo*` — missing | SYNTH |
| Pet aura / attack / harvest | `SfxId.Pet*` / `GameSfx.PlayPetHarvest` | missing | SYNTH |
| Fire/arcane explosion, shockwave, heal impact | `VFXManager.VfxToSfx` (`VFXManager.cs:225-230`) | `Sfx_FireExplosion` etc. — missing | SYNTH |
| Ward-stone lit / dim | `WardStone.cs` → `SfxId.WardLit/WardDim` | missing | SYNTH |
| Heart takes damage / falls | `HeartwoodAmbientController.cs:169-170` | `Audio/Sfx/Heart_Hit`, `Heart_Fall` — missing | **SILENT** (no synth for these) |
| Heart HP ambient beds (3 tiers) | `HeartwoodAmbientController.cs:166-168` | `Audio/Ambient/Heartwood_*` — missing | **SILENT** |
| Heart-failing voice line | `TowerVoiceController` | `Audio/Voice/HeartFailing(_1/2/3)` — missing | **SILENT** |
| Tower build-complete chime (controller) | `TowerAudioController` serialized clips, never assigned | n/a | **SILENT** (structurally; GameSfx covers the felt moment) |
| Title / village / dungeon / battle / overworld / arena music | `AudioBootstrap` + scene map | Suno MP3s | **AUTHORED** ✔ |
| Battle-music rotation variety | `_battlePool` / `_overworldPool` (WO-171) | pool extras missing (§1b) | DEGRADED — 1 clip each, no rotation |
| Offensive-raid BGM | `RaidGarrisonSpawner.cs:156` | `brass-rampart.mp3` | **BROKEN** (§2d) |
| Game over | `AudioBootstrap.cs:117` | `GameOver.mp3` | **AUTHORED** |
| Hovl skill VFX sounds | AudioSources inside Hovl prefabs | `HSFiles/Sounds/Skill *.wav` | AUTHORED but **bypasses our mixer** |

**Silent gaps (owner-felt):** Knight heal cast, Heart hit/fall stingers, Heartwood HP ambience, Heart voice lines. **Placeholder-tone gaps:** every explosion/impact/ward/combo/pet/level-up/wave-start/tower-fire/tower-place, enemy-hit, build-denied.

---

## 4. Web research

### 4a. leohpaz — RPG Essentials Sound Effects — FREE! (purchased 2026-06-29, NOT imported)

- **Store page:** https://assetstore.unity.com/packages/audio/sound-fx/rpg-essentials-sound-effects-free-227708 — publisher **leohpaz** (publisher id 61102), v1.0, Aug 2022, 16.1 MB, license **Extension Asset (Standard Unity Asset Store EULA)** — safe for commercial use in builds.
- **Contents:** 48 retro-RPG sound effects — a free sampler drawn from the publisher's five paid packs: *100 Retro RPG UI SFX*, *50 Retro RPG Battle Magic SFX*, *50 Retro RPG Heals and Buffs SFX*, *90 Retro RPG Player Movement SFX*, *90 Retro RPG Battle SFX*. Also distributed on itch.io (https://leohpaz.itch.io/rpg-essentials-sfx-free) and GameMaker Marketplace; itch lists an "RPG Essentials SFX Bundle" combining the paid packs.
- **Intended organization:** category folders per source pack (UI / Battle / Magic / Heals-Buffs / Movement) — which maps 1:1 onto our gap list (§5). Style note: "retro RPG" (chiptune-adjacent, inspired by classic JRPGs) — audition against our realistic Freesound combat set for tonal fit before mass-wiring.
- **Companion paid packs worth knowing:** the five source packs above; if the free 48 fit the game's tone, the Heals/Buffs and Battle Magic packs directly fill our biggest authored-audio hole (spell/heal/buff impacts currently all synth).

### 4b. Freesound provenance — ⚠ license record does not check out

`Assets/Audio/SFX/Combat/SOURCE_LICENSE.md` logs three Freesound IDs, all "TODO verify". Verified this session — **the logged IDs resolve to unrelated sounds**, so the provenance record is wrong as written:

| Logged | Claimed sound | What freesound.org/s/<id> actually is | License found |
|---|---|---|---|
| 6341 | "sword against sword" | Waldorf PPG synth brass note (author Jovica, 2005) | CC-BY 4.0 |
| 426521 | "footsteps knight walking for rpg" | metal statue falling in snow (author ShintaBoy) | CC0 |
| 98277 | "dragon shout roar" | 48 s synthesizer sequence (author reaktorplayer) | **CC-BY-NC 4.0** |

None of the three descriptions match the claimed source. Either the IDs were mis-transcribed or the sounds came from different pages. **Action needed:** re-locate the true sources of `sword_clash_1..4`, `footsteps_walk_loop`, `dragon_roar` (and the rest of the 17-WAV set, which has no IDs logged at all) and record real licenses. If any turn out CC-BY, an in-game credits line is required; CC-BY-NC would be unusable commercially. Until verified, treat the recorded combat set's license as **unknown**.

### 4c. Hovl Studio RPG VFX Bundle (v6.0.4, purchased 2026-07-10)

Ledger-identified. Its `HSFiles/Sounds` skill WAVs are licensed under the same Asset Store EULA as the VFX — fine to use, including re-routing them through our own SFX system (they'd be strong authored candidates for `Sfx_FireExplosion` / `Sfx_ArcaneExplosion` / cast sounds — see §5).

### 4d. Mirza Beig Ultimate VFX (v3.5.2, purchased 2026-05-27)

VFX-only; no audio in the imported folder or the .unitypackage (§1f). Another agent owns the VFX dossier.

### 4e. Music

All BGM is owner-generated (Suno). No third-party publisher, no store license; rights follow Suno's terms for the owner's account tier — worth a one-line confirmation before commercial launch (Suno's free tier historically did not grant commercial rights; paid tiers do).

Sources: [leohpaz store page](https://assetstore.unity.com/packages/audio/sound-fx/rpg-essentials-sound-effects-free-227708) · [leohpaz publisher](https://assetstore.unity.com/publishers/61102) · [leohpaz itch.io](https://leohpaz.itch.io/rpg-essentials-sfx-free) · [freesound 6341](https://freesound.org/s/6341/) · [freesound 98277](https://freesound.org/s/98277/) · [freesound 426521](https://freesound.org/s/426521/)

---

## 5. Opportunities + gaps

**Ranked by player-felt leverage (per ARCHITECTURE_PRINCIPLES queueing):**

1. **Fix the Raid BGM code path (bug, ~8 lines).** Add a `Raid` def to `MusicTrackRegistry.Defs`, a `_raidClip` field + `ClipFor`/`SetMusicClip` cases in `AudioService`. The clip ships already; raids are currently scored by whatever ambient was playing. Also fix the stale "✅ wired" row in `docs/AUDIO/AUDIO_CLIP_MANIFEST.md:46`.
2. **Drop one WAV at `Resources/Sfx/Heal`** — un-silences the Knight's owner-picked castHeal moment (the newest combat feel work). Candidate: a leohpaz Heals-and-Buffs sample, or Hovl `Skill *.wav`.
3. **Import the already-purchased leohpaz pack** (download via Package Manager → My Assets). 48 authored clips mapping straight onto the synth-placeholder list: UI sounds, battle magic (→ `Sfx_FireExplosion`, `Sfx_ArcaneExplosion`, `Sfx_WizardCast`), heals/buffs (→ `Sfx_Heal`, `Heal`), movement, battle. Wiring = renaming files into `Assets/_Modules/Audio/Resources/Sfx/` per the manifest names — zero code. Audition first: retro style vs our realistic Freesound set.
4. **Kill the boot-time missing-clip warnings + restore music variety.** Either source the 3 battle + 1 overworld pool variants (Suno) or delete the five dead `TryAssignClip/TryAddClip` lines (`AudioBootstrap.cs:106,125-127,129`). Today every boot logs 5 warns and the WO-171 rotation pools are single-entry no-ops.
5. **Heart audio identity (all-silent category).** 3 Heartwood ambient beds + Heart_Hit/Heart_Fall stingers + HeartFailing voice line(s) — the defend-the-Heart core loop has zero audio identity. The wiring exists (WO-571 convention paths, `docs/AUDIO/AUDIO_CLIP_MANIFEST.md` §3-4); this is purely a sourcing task. Suno can do the beds; the VO line needs TTS/ElevenLabs or a recorded read.
6. **Route Hovl prefab AudioSources through the mixer.** Hovl skill sounds bypass the SFX group — they ignore player volume/mute. Either a one-time editor pass assigning `outputAudioMixerGroup` on Hovl prefab AudioSources, or strip prefab audio and fire equivalents via `PlaySfxAtPosition`. Alternatively harvest the 12 WAVs as `Sfx_*` overrides (kills two birds: mixer routing + replacing 5 synth explosion/cast tones with pro clips).
7. **License hygiene (pre-launch blocker).** Re-verify the Freesound provenance of all 17 recorded WAVs (§4b — the logged IDs are wrong); confirm Suno commercial rights; keep the leohpaz set as the clean-licensed fallback (Asset Store EULA).
8. **Housekeeping:** delete or wire the three orphans — `battle.mp3` (3.0 MB, superseded), `Assets/Audio/Victory/Victory.mp3` (18 s, unreferenced), `bellssteel-panic.mp3` (flagged in the manifest as owner-decision; a natural fit for the StructureAttackAlert "village under attack" cue). Run `SfxClipLibraryBuilder` (or don't — the Resources/Sfx drop-in convention has made the library asset optional; decide one canonical path and note it in the manifest). Retire `WaveMusicController` per its banner. Update `AUDIO_CLIP_MANIFEST.md` — its §1 and §5 status columns are stale in both directions (says `village`/`battle_theme_NEW` ✅ though deleted; says SwordClash/SpellCast/etc ❌ synth though authored WAVs landed 2026-07-02).
9. **Missing categories worth acquiring eventually:** ambient world beds (wind/forest/town walla — nothing exists), inventory/equip foley (gear system has no sounds), NPC voice, and a victory fanfare that matches the new combat (the 18 s orphan Victory.mp3 may have been intended for this).

---

## 6. Executive summary

The game's audio stands on three legs of very different strength. **Music is the strong leg:** fifteen owner-generated Suno tracks (about 50 MB) cover title, town, dungeon, overworld, battle (a four-state wave-driven score with boss and victory cues), arena, defeat, and game-over, all flowing through a genuinely well-built single-owner music architecture — one crossfading source pair inside MusicDirector, a priority-layer stack that makes "two songs at once" structurally impossible, scene-load auto-routing, a player jukebox, and WebGL gesture-unlock. One real defect hides here: the offensive-raid theme (brass-rampart) is shipped and requested by the raid spawner but can never play, because the track registry and the clip-assignment switch were never given a Raid entry — an eight-line fix. A secondary niggle: the battle/overworld "rotation pools" reference five music files that no longer exist, so every boot logs five warnings and rotation is a no-op.

**Recorded sound effects are the middle leg.** Seventeen Freesound-sourced, loudness-normalized WAVs were processed and mirrored into the runtime Resources folder, so the moments the player feels most — sword swings, four randomized clash variants, weapon draw, spell cast, footsteps, hero hit, enemy deaths, dragon roar, UI click, lookout horn, building upgrade — all play real audio through the mixer. However, the provenance file for that set is unreliable: the three Freesound IDs it logs resolve to entirely different sounds online, so the licenses are effectively unverified; this needs a re-check before commercial release.

**The synth-placeholder leg is the weak one.** The entire SfxId event system — explosions, shockwaves, heals, ward stones, combos, pets, level-up, wave-clear, tower fire and place — plays procedurally generated tones, because no clip library asset was ever built and no per-id override files exist. Beyond placeholders, four moments are fully silent: the Knight's new heal cast (its sound key points at a file nobody dropped), the Heart's damage and destruction stingers, the Heart's health-tier ambience, and the heart-failing voice line — notable because defending the Heart is the core loop.

Two purchases resolve cleanly: Mirza Beig's Ultimate VFX contains no sound effects at all (the flag was a mis-attribution), and the leohpaz "RPG Essentials Sound Effects — FREE" pack — 48 clean-licensed clips spanning UI, battle magic, heals, movement, and battle, exactly our gap categories — was bought on 2026-06-29 but never downloaded or imported. Importing it, dropping one heal clip, and fixing the Raid registry are the three highest-leverage audio moves available, and none of them requires new spending.
