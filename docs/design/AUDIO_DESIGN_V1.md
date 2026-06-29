# Audio & Music — V1 Design Plan

**Date:** 2026-06-28  **Owner:** Samantha (PO)  **Author seat:** CLI (assessment + design)
**Status:** DESIGN — proposed plan for owner sign-off. No code in this doc.
**Scope:** the V1 single-Knight loop (hub → overworld → isolated BattleArena → victory/defeat → home),
plus the village wave-defense loop that already ships.

> **TL;DR.** The audio *system* is mature, well-architected, and already data-driven
> (Resources-by-id, no inspector drag-drop, never-silent SFX synth fallback). V1 audio is
> **NOT an engineering project — it is a content/sourcing + one mixer-asset fix.** The single
> load-bearing code gap is that `GameAudioMixer.mixer` is a near-default **stub** (only a
> `Master` group, no exposed params), so the Settings volume/mute sliders only work via a
> source-direct fallback and the per-group mix (Music/SFX/UI/Voice balance) does nothing.
> Everything else is "drop a correctly-named clip at a path and it just works."

---

## 0. Source-of-truth cross-refs (read alongside this)

- `docs/audio-mix-spec.md` — owner-locked per-track volume/fade/transition mix (still canonical).
- `docs/AUDIO/AUDIO_CLIP_MANIFEST.md` — the living **drop-in clip list** (every path + present/missing).
- `docs/MASTER_CATALOG/audio.md` — verified file-by-file catalog of the audio module + its FLAGS.
- `WorkOrders/WORK_ORDER_571_audio_pass.md` — the most recent wiring pass (today) that made
  music/voice/ambient resolve by Resources path.
- Combat-direction canon: single-Knight north star → overworld encounter → isolated real-time
  `BattleArena` (memories `combat-pivot-single-hero-northstar`, `overworld-encounter-isolated-battle`).

This doc is the **design layer above** those: it states *what each context should sound like*, the
*coverage gaps that block V1*, and the *sequenced plan* to close them. It does not restate the
per-track volume table (that lives in `audio-mix-spec.md` and `MusicTrackRegistry`).

---

## 1. Current coverage assessment (verified from code, 2026-06-28)

### 1.1 What is BUILT and LIVE (do not reinvent)

| System | File | Role | State |
|---|---|---|---|
| **AudioService** | `Assets/_Modules/Audio/AudioService.cs` | DDOL singleton music director: A/B crossfade, 8-voice SFX pool, scene→track map, jukebox, volume/mute, WebGL gesture-unlock | LIVE |
| **AudioBootstrap** | `Assets/_Modules/Audio/AudioBootstrap.cs` | Auto-spawns the service (RuntimeInit), loads every music clip **by Resources short-name** | LIVE |
| **MusicTrackRegistry** | `Assets/_Modules/Audio/MusicTrack.cs` | Owner-locked per-track volume/loop/fade defaults | LIVE |
| **BattleMusicManager** | `Assets/_Modules/Village/Audio/BattleMusicManager.cs` | Wave-driven 4-state battle scorer (Combat / Intense / Boss / Victory-sting), own crossfading sources routed through the shared Music group, hands back to ambient on resolve | LIVE |
| **WorldMusicDirector** | `Assets/_Modules/Village/World/WorldMusicDirector.cs` | Position-read Village↔Overworld swap for the additive open world; advances the 2-theme overworld rotation per crossing | LIVE |
| **ProceduralSfx / GameSfx / EnemyCombatAudio** | `Assets/_Modules/Audio/ProceduralSfx.cs`, `Assets/_Modules/Village/Audio/GameSfx.cs`, `.../Enemies/EnemyCombatAudio.cs` | Synth-generated one-shots so **SFX are NEVER silent**; an authored `Resources/Sfx/<name>` drop-in overrides the synth per id | LIVE |
| **Jukebox** | `MusicSelectionPanel` (+ bootstrap), `AudioService.AmbientChoicesFor` | Player picks the ambient track per context (Village/Overworld); persisted in PlayerPrefs | LIVE |
| **WebGLAudioUnlock** | `Assets/_Modules/Audio/WebGLAudioUnlock.cs` | Un-suspends the mobile-browser AudioContext on first gesture | LIVE |
| **Settings volume/mute** | `MusicToggleBootstrap`, `AudioMixerBridge`, `GameState.MusicVolume/SfxVolume/Muted` | Persisted player audio prefs seeded into the service on boot | LIVE (degraded — see 1.3) |

**Design principle already enforced (keep it):** *no inspector drag-drop of clips*. Everything resolves
by Resources path or synth fallback (canon: memory `never-dragdrop-or-manual-playtest`). The owner
sources/generates a clip and drops it at the named path — zero code change.

### 1.2 What is WIRED-but-EMPTY (path ready, clip missing)

These play **silent** today (music/voice/ambient have no synth fallback — you can't synth a song or
speech). Each logs `FlowTrace.Warn` so a run self-reports the gap.

- **`dungeon`** music — the **only missing MUSIC track**. Dungeon scenes play silent.
- **Heartwood ambient beds** (Healthy / Strained / Critical) + **stingers** (Heart_Hit / Heart_Fall).
- **Heart voice line(s)** — the low-HP "The Heart is failing!" VO (Voice mixer group).

### 1.3 The one real CODE gap (blocks the full mix) — **GameAudioMixer is a stub**

`Assets/Audio/Resources/Audio/GameAudioMixer.mixer` ships as a **near-default asset**: a single
`Master` group, **no** Music/SFX/UI/Voice child groups, and `m_ExposedParameters: []`. But the entire
code + docs contract assumes 5 groups with exposed params `MasterVol / MusicVol / SfxVol / UiVol /
VoiceVol` (+ `ReverbSend`). Consequences, verified:

- `_mixer.SetFloat("MusicVol", …)` etc. **silently fail** — the params don't exist.
- `FindMatchingGroups("Music"/"SFX"/…)` return null → sources keep no group routing.
- Only the **source-direct fallback** in `SetVolume`/`SetMuted` actually changes what you hear, and it
  only covers Master + Music (SFX/UI/Voice group balance is unreachable).
- `ApplyMobilePlatformRules` (the -4 dB phone-speaker SFX trim + reverb kill) is a no-op.

**This is the single highest-leverage fix in the whole audio plan.** Until the mixer asset is rebuilt
with the documented 5 groups + exposed params, there is no real per-bus mix — music can drown SFX/voice
and the owner can't tune the balance. It is an **asset/editor task**, not a gameplay-code change.

### 1.4 Enum drift (note, not currently broken)

Two `MusicTrack` enums exist — `DeNelle.Core.Audio.MusicTrack` (explicit indices, save-stable) and
`DeNelle.Audio.MusicTrack` (declaration-order). The explicit `IAudioService.PlayMusic` switch maps by
name so playback is correct, **but the persisted jukebox PlayerPrefs int is the Audio-side ordinal** —
if that enum is ever reordered, saved picks shift. Leave both append-only; never reorder.

---

## 2. Music design — track per context

V1 has two parallel "scorers" and that is correct architecture, not duplication:

- **AudioService** owns **context/scene ambient + emotional stings** (Title / Village / Overworld /
  Dungeon / Arena / Raid / Victory / Defeat) and the player jukebox.
- **BattleMusicManager** owns the **dynamic battle state machine** (Combat ↔ Intense ↔ Boss → Victory
  sting), routed through AudioService's Music group, handing back to ambient on resolve.

### 2.1 Context → track map (the V1 single-Knight loop)

| Context / moment | Track key | Driver | Clip(s) | Status | Intent |
|---|---|---|---|---|---|
| Studio bumper / cold-open / Title | `Title` | scene map (`TrackForScene`) | `title` | ✅ | Moderate (0.6); supports the cold-open narrative, doesn't compete |
| Hub — MainCastle_Hall / CastleHub / Village | `Village` | scene map → `PlayAmbientContext(Village)` | `village` | ✅ | Soft (0.4) anti-fatigue town bed; jukebox-overridable |
| Open world — OuterWorld regions | `Overworld` | `WorldMusicDirector` position read (boundary crossing) | `mainworld1_NEW`, `world_theme_NEW` (2-pool rotation) | ✅ | Soft (0.4) wander bed; rotates per village→world crossing |
| Overworld encounter chase (pre-arena) | `Overworld` (continues) | encounter rep leash | (uses overworld bed) | ✅ system | V1: no dedicated chase stinger — see §2.3 candidate |
| **Isolated BattleArena** (the V1 fight) | `Arena` ("Echo's theme") | `BattleArena` StageRoutine | `Music/echo_theme` | ✅ | Soft background (0.4) under the kite-fight SFX |
| Village wave-defense — general combat | (battle SM) Combat | `BattleMusicManager` ← `WaveManager.OnWaveStarted` | `Music/Battle/Overworld_Battle_1` | ✅ | Featured loop, drives tension |
| Village wave-defense — high pressure (5+ live) | (battle SM) Intense | `BattleMusicManager` enemy-count poll | `Music/Battle/Overworld_Battle_2` | ✅ | Crossfades up under pressure |
| Boss / apex wave | (battle SM) Boss | `BattleMusicManager` ← `OnApexBossSpawned` | `Music/Battle/Overworld_Boss_Fight` | ✅ | Boss loop |
| Wave cleared (in-loop) | (battle SM) Victory sting | `BattleMusicManager` ← `OnWaveCleared` | `Music/Battle/Overworld_Victory` | ✅ | One-shot → `ReturnToAmbient()` |
| Battle WIN (arena/encounter result) | `Victory` | combat result | `victory` | ✅ | Hard-cut celebratory sting (0.7, no loop) → return to ambient |
| Battle LOSS / Game Over | `Defeat` | combat result / `GameOverScreen` | `defeat` → `Audio/Music/GameOver` override | ✅ | Slow grief beat (0.5, no loop); "the loss should sink in, not slap" |
| **Dungeon** (chunk-composer dungeons, V1.x) | `Dungeon` | scene map (`Dungeon_*`) | `dungeon` | ❌ **MISSING** | Very soft (0.25) ambient bed; footsteps/VO sit above it |
| Offensive raid on enemy stronghold (Village2) | `Raid` | raid controller | `Music/Raid/brass-rampart` | ✅ | Driving brass for marching an army |
| Heart of Elarion — proximity ambient | (3 HP-tier beds) | `HeartwoodAmbientController` | `Audio/Ambient/Heartwood_{Healthy,Strained,Critical}` | ❌ **MISSING** | World-tree life-state bed; ties to the life-force economy |

### 2.2 Transitions (already implemented per `audio-mix-spec.md` §3)

Crossfade durations are owner-locked in `MusicTrackRegistry` and honored by the A/B crossfade:
Title→Village 1.2 s, Village/Dungeon→Battle 0.6 s (fast — combat is a moment), Battle→Victory hard-cut +
0.2 s fade-in (celebratory beat must land), Battle→Defeat 1.5 s slow grief, returns 1.0–1.5 s. **No new
transition work for V1** — the table is built; it just needs the missing clips to have something to fade.

### 2.3 V1 music gaps to close (priority order)

1. **`dungeon.mp3`** — the only missing core music track. Blocks dungeon ambience the moment the
   chunk-composer dungeon (Task #46) ships. Source a soft (0.25) ambient loop, drop at
   `Assets/Audio/Resources/dungeon.mp3`.
2. **Heartwood beds ×3** — ties directly to the life-force/tree-growth economy (the emotional core of
   the pivot). High narrative value. `Audio/Ambient/Heartwood_{Healthy,Strained,Critical}`.
3. *(Optional V1.x)* **Encounter-chase stinger** — a short tension layer when an overworld rep aggros
   and chases (currently the overworld bed just continues). Candidate new `MusicTrack.Chase` or a
   volume-nudge on Overworld (§4 of the mix-spec already specifies the nudge mechanism, unbuilt). Park
   as a polish item, not a V1 blocker.

---

## 3. SFX design — needs by category

SFX are **already never-silent**: every `SfxId` and every named one-shot resolves
`Resources/Sfx/<name>` → else a procedural synth tone. So the V1 SFX task is **"which synths to replace
with authored CC0/recorded clips,"** ranked by how often the player hears them and how much the synth
placeholder hurts the feel.

### 3.1 Combat (highest player-facing frequency → replace first)

| Cue | Resolver | Path | Now |
|---|---|---|---|
| Hero melee/sword clash | `GameSfx.PlaySwordClash` | `Sfx/SwordClash` | synth |
| Hero spell cast | `GameSfx.PlaySpellCast` / `AbilityAudioBridge` | `Sfx/SpellCast`, `Sfx/<AbilityEffect>` | synth |
| Hero takes a hit | `GameSfx.PlayHeroHit` | `Sfx/HeroHit` | synth |
| Enemy death | `GameSfx` / `EnemyCombatAudio` / `SfxId.EnemyDeath` | `Sfx/EnemyDeath`, `Sfx/Sfx_EnemyDeath` | synth |
| Enemy hit | `EnemyCombatAudio` | `Sfx/EnemyHit` | synth |
| Enemy cast-charge telegraph | `EnemyCombatAudio` | `Sfx/EnemyCastCharge` | synth |
| Fire / arcane / shockwave impacts | `SfxId.*` via `PlaySfxAtPosition` | `Sfx/Sfx_{FireExplosion,ArcaneExplosion,Shockwave}` | synth |
| Heal contact | `SfxId.Heal` | `Sfx/Sfx_Heal` | synth |
| Flame-arrow / tower shot / arrow hit | `SfxId.*` / `GameSfx` | `Sfx/Sfx_{FlameArrowLaunch,TowerShot}`, `Sfx/TowerArrowHit` | synth |

> **Note (Task #44):** the arena spell-cast **VFX** (blocky purple cubes) is a separate visual ticket,
> but its audio (`Sfx/SpellCast`) is the same drop-in path — replace both together for a coherent
> "cast" beat.

### 3.2 UI (constant, low-frequency-per-event but always present)

| Cue | Resolver | Path | Now |
|---|---|---|---|
| Button click (shared, all panels) | `IAudioService.PlayUiClick` (HUD seam) | `Sfx/UiClick` | synth tick |
| Build denied / rejected placement | `GameSfx.PlayBuildDenied` | `Sfx/BuildDenied` | synth |
| (Recommended add) panel open/close, tab switch, equip-confirm, error/deny | — **NOT YET wired** | propose `Sfx/UiOpen`, `Sfx/UiClose`, `Sfx/UiEquip`, `Sfx/UiError` | none |

UI coverage is the **thinnest category** — only a single shared click + build-denied exist. For the
Obsidian-panel UI polish phase, a small UI SFX set (open/close/confirm/error) markedly lifts perceived
quality. This is the one place a *tiny* code add (new named one-shots in the HUD seam) is warranted; keep
it on the `Resources/Sfx/` drop-in convention so it stays asset-driven.

### 3.3 Economy / world (the life-force loop — the pivot's heartbeat)

| Cue | Resolver | Path | Now |
|---|---|---|---|
| Echo/pet harvest tick | `GameSfx.PlayPetHarvest` | `Sfx/PetHarvest` | synth |
| Building upgrade confirm | `GameSfx.PlayBuildingUpgrade` | `Sfx/BuildingUpgrade` | synth |
| Tower place "thunk" / tower fire "pew" | `GameSfx` | `Sfx/TowerPlace`, `Sfx/TowerFire` | synth |
| Ward-stone lit / dimmed | `SfxId.WardLit/WardDim` | `Sfx/Sfx_{WardLit,WardDim}` | synth |
| Heart takes damage / Heart falls | `HeartwoodAmbientController` | `Audio/Sfx/Heart_{Hit,Fall}` | ❌ missing |
| (Recommended add) resource-gain pickup (wood/iron/grain), workforce-assigned confirm | — partly via harvest | propose `Sfx/Resource_{Wood,Iron,Grain}`, `Sfx/EchoAssigned` | none/synth |

### 3.4 Rewards / progression (the dopamine beats — worth authored clips)

| Cue | Resolver | Path | Now |
|---|---|---|---|
| Level up | `GameSfx.PlayLevelUp` / `SfxId.LevelUp` | `Sfx/LevelUp`, `Sfx/Sfx_LevelUp` | synth chime |
| Wave clear fanfare | `SfxId.WaveClear` | `Sfx/Sfx_WaveClear` | synth chime |
| Kill-combo tiers (small/big) | `SfxId.ComboSmall/ComboBig` | `Sfx/Sfx_{ComboSmall,ComboBig}` | synth |
| Lookout "raid incoming" horn | `GameSfx.PlayLookoutHorn` | `Sfx/LookoutHorn` | ✅ **authored** |
| Wave-start battle horn | `GameSfx.PlayWaveStart` | `Sfx/WaveStart` | synth |
| (Recommended add) Victory **crown-tier** reward sting (Task #41 star-row), quest-complete | — | propose `Sfx/CrownTier`, `Sfx/QuestComplete` | none |

### 3.5 Voice (V1: one beat only)

- **Low-HP Heart VO** — `Audio/Voice/HeartFailing(_1/_2/_3)`, Voice mixer group, fires once below 30% HP.
  All ❌ missing. One line is enough for V1; extras rotate if dropped. Bible voice: "short sentences with
  weight; quiet stakes; old bones, modern English."

---

## 4. Implementation & sourcing plan (sequenced)

The architecture is done. Work splits into **one code/asset fix**, **clip sourcing** (owner/content),
and **a small optional UI-SFX wiring add**. All clip work is drop-in: name the file, drop it at the
path, it plays — verify headless.

### Phase A — Fix the mix bus (code/asset, CLI) — **highest leverage, do first**

1. **Rebuild `GameAudioMixer.mixer`** with the documented 5 groups (Master → Music/SFX/UI/Voice) and
   exposed params `MasterVol / MusicVol / SfxVol / UiVol / VoiceVol` (+ `ReverbSend` on a send bus).
   This is an **editor/asset task** (an `.asset` author or an editor-script builder — *not* a hand-edit
   of the YAML, given the corruption-on-resave history with scene/asset files). Once present, the
   existing `SetVolume`/`SetMuted`/`ApplyMobilePlatformRules` code lights up unchanged — they already
   target these exact param names.
2. **Verify** the source-direct fallback still covers the no-mixer case (regression: a fresh clone with
   a missing mixer must still play + respond to the ♪ toggle).
3. Update `docs/MASTER_CATALOG/audio.md` FLAG #1 + `docs/port-notes/audio-system.md` (mark the stub
   resolved) in the **same commit** (canon §15).

### Phase B — Source the missing MUSIC/AMBIENT/VOICE (owner/content) — unblocks silent contexts

Priority order (drop at the exact `AUDIO_CLIP_MANIFEST.md` path, no code change):

1. `Assets/Audio/Resources/dungeon.mp3` — dungeon ambient loop (0.25). Unblocks dungeon context.
2. `Audio/Ambient/Heartwood_{Healthy,Strained,Critical}` — 3 HP-tier beds (life-force core).
3. `Audio/Sfx/Heart_{Hit,Fall}` — Heart damage/destroy stingers.
4. `Audio/Voice/HeartFailing` — low-HP VO (one line min).

### Phase C — Replace placeholder SFX synths with authored clips (owner/content) — feel polish

Replace in player-frequency order; each is a `Resources/Sfx/<name>` drop-in over the synth:

1. **Combat first** (heard every fight): SwordClash, SpellCast, HeroHit, EnemyDeath, EnemyHit,
   the impact set (FireExplosion/ArcaneExplosion/Shockwave/Heal).
2. **Rewards** (dopamine): LevelUp, WaveClear, ComboSmall/Big, WaveStart, CrownTier (new).
3. **Economy/world**: PetHarvest, BuildingUpgrade, TowerPlace/Fire, Ward lit/dim, resource pickups.
4. **UI** (see Phase D for the new ones): UiClick, BuildDenied.

CC0 sourcing lanes (licensing-safe): Kenney UI/impact packs, Sonniss GDC libraries, freesound.org CC0,
or Suno-generated stingers (already used for the battle/world music). Keep a license note per source.

### Phase D — Small UI-SFX wiring add (code, CLI) — optional but high perceived-quality

Add named one-shots through the existing HUD `IAudioService` seam for **panel open / close / equip-confirm
/ error-deny**, resolving `Resources/Sfx/Ui{Open,Close,Equip,Error}` with a synth fallback (same pattern
as `PlayUiClick`). This is the only *new code* the plan proposes, and it stays on the asset-driven
convention. Gate it so it ships silent-safe if no clips are dropped.

### Phase E — Housekeeping (CLI, low priority)

- Decide the orphan `Resources/Audio/bellssteel-panic.mp3` — wire to a cue (Heart_Hit candidate?) or
  delete in the asset purge (memory `asset-purge-deferred-to-polish-end`).
- Retire `WaveMusicController` from `WaveSystemBridgeBootstrap` once `BattleMusicManager` is
  felt-verified (it currently ships silent/inert; double-scoring risk if re-wired).
- Build a real `SfxClipLibrary.asset` only if/when authored SFX want per-id volume control beyond the
  synth path — **not required for V1** (the `Resources/Sfx/Sfx_<id>` drop-in already overrides synths).

### Verification (every phase)

- `DeNelle.Editor.CompileGate.Run` (brace/NUL gate) for any code touch.
- `DataRegression` already guards the silent-track class (e.g. asserts `dungeon` presence) — run after
  each clip drop.
- Headless AutoPilot + Editor.log: the `[Flow:Audio]` warns self-report any track/clip that resolved
  null, so a run names exactly which cue is still silent (canon §12 — instrument, don't guess).
- PO felt-verifies the mix balance after Phase A (headless can't judge "music drowns SFX").

---

## 5. V1 "done" definition (audio)

1. The 5-group mixer asset exists; Settings Music/SFX sliders + mute audibly change the **right** bus,
   and SFX/voice are not buried under music (Phase A).
2. No core context plays silent: Title, Hub, Overworld, **Dungeon**, Arena, Raid, Victory, Defeat all
   have a clip (Phase B closes `dungeon`).
3. The Heart life-state is audible — 3 ambient beds + damage/fall stingers + one low-HP VO line (Phase B).
4. Combat **feels** authored, not synthy: the combat SFX set (§3.1) is replaced with real clips (Phase C.1).
5. Reward beats land: level-up, wave-clear, combo, victory crown-tier are authored (Phase C.2).
6. UI has a coherent small SFX set (click/open/close/confirm/error) (Phase D).
7. Every cue still degrades gracefully (synth or silent + `FlowTrace.Warn`) on a fresh clone with no
   assets — the never-block-the-game guarantee is preserved.

---

## 6. What NOT to do (guardrails)

- **Do not** add `[SerializeField] AudioClip` drag-drop fields — canon bans it; use Resources-by-id.
- **Do not** reinvent a music director, mixer routing, crossfade, or volume model — `AudioService` +
  `BattleMusicManager` already own these. New cues route **through** them.
- **Do not** reorder either `MusicTrack` enum — append-only (jukebox PlayerPrefs + save indices).
- **Do not** drive `AudioService.PlayMusic(Battle)` from wave code — that's `BattleMusicManager`'s job
  (it would collapse the 4 battle states and fight the scene-load short-circuit).
- **Do not** hand-edit `GameAudioMixer.mixer` YAML — author it via the editor / a builder script
  (asset-resave corruption risk, §3 scene-file rules apply to fragile assets too).
- **Do not** claim an audio cue "works" without a headless `[Flow:Audio]` capture or PO felt-check —
  a wired path with a missing clip is silent-but-green to static reading (canon §12).
</content>
</invoke>
