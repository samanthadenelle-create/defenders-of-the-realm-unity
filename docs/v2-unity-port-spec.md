# v2 Unity Port — Spec & Agent Contract

**Status:** Authoritative contract for the parallel Unity port stream. The Claude Code session that opens this file reads it as its **only required context** before beginning Week 1 work. Every architectural call already made in this doc is binding; every architectural call NOT made is delegated to the agent (subject to the decisions-log discipline in Part 6).

**Owner:** Samantha Denelle / DeNelle Studios.
**v1 stream:** the React + Vite + Three.js client at `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\`. Continues unchanged. Ships independently.
**v2 stream:** the Unity 6 LTS port at `C:\Users\Kayden-Laptop\Documents\defenders-unity\`. Slow burn, weeks–months, separate git repo, separate Claude Code session.

**Canon names (non-negotiable, copy verbatim everywhere):**
Town — **Avalon**. World-tree — **Elarion** (also called "the Heart"). Mage player hero — **Blaise**. Antagonist — **Alduin the Mournful**. Brand dragon — **The Heart-Wing**. Tagline — **"By lantern. By oath. By Heart."** Publisher — **DeNelle Studios**. Also canon: **Hollow Ones**, **Wardens**, **Bryn**, **Mara**, **Tovin**, **Eira**, **Aelf**, **Mira**, **the Wound**, **the Withering**, **First Light**, **Aether Sprite / Flame Pup / Ice Wolf**, **the Keeper**. Lift any user-facing string from `docs/narrative-bible.md` — never paraphrase.

---

## Part 1 — The mental model

The big move: **engine-agnostic design preserved; engine-specific implementation rewritten.** Roughly 70% of the project's accumulated value — the specs in `docs/`, the lore in `docs/narrative-bible.md`, the audio in `public/audio/`, the KayKit models in `public/kaykit/`, the brand assets, the canonical strings, the questline narratives, the pack ladder, the wallet architecture — carries over verbatim. Roughly 30% — the React component tree, the R3F scene mounts, the Zustand stores, the Vite build, the localStorage persistence — gets rewritten in C# and Unity.

This document is the **agent's contract**. The Unity Claude Code session opens it once at the start of Week 1, treats every locked decision as binding, and operates without owner attention for stretches of days at a time. When the agent has to make a call that this spec does not cover, it makes a defensible call and writes a row into the decisions log (Part 6). It does not block waiting for owner clarification — the parallel stream's value is that it runs in the background while v1 ships.

The agent does **not** push to the React codebase, ever. There is no shared git history between v1 and v2. The Unity project lives in its own folder, its own git repo, its own Vercel-less universe. The only thing that flows between the two streams is the **data layer** described in Part 4 (the canonical `data/*.json` files), plus the asset files which both projects copy from a shared source-of-truth folder (Part 7).

Acceptance for the contract: the agent ships a **v2-foundation** that PRODUCES a playable Unity build by the end of Week 8. "Playable" means: village scene loads, Wave 1 fires, an enemy breaches, the camera cuts to an ATB scene, the player wins or loses, control returns to the village; or alternately, the player walks from the village into the Healer's Cottage dungeon scene, has the Bryn/Wanderer encounter, triggers an ATB battle, and returns. Polish is not the goal; **viability** is the goal. A Unity contractor (if hired) should be able to pick up the project at Week 9 and continue productively without owner ramp-up time.

The Unity stream's pace is **deliberately slower** than the v1 React stream. The Unity stream is research; the React stream is shipping. If the React stream evolves a spec mid-Unity-week (a canon name moves, a system gets cut, a pack price changes), the Unity stream absorbs the change on the following Monday (Part 8's sync protocol) and does not race to keep up.

This document is the only context required to begin. If the agent finds a referenced doc that doesn't exist (some referenced specs are aspirational or may have been renamed — `docs/audio-mix-spec.md` and `docs/dungeon-encounters-and-checkpoints-spec.md` for example), it falls back to the nearest existing doc by topic (e.g. `docs/sound-design.md` and `docs/intro-audio-timing-reference.md` for audio; `docs/dungeon-3d-healers-cottage-design.md` and `docs/dungeons-system-design.md` for dungeon mechanics) and records the substitution in the decisions log.

---

## Part 2 — Unity project setup

### Engine + packages

- **Unity version:** Unity 6.0 LTS. At agent spinup, install the latest minor revision of Unity 6 LTS available via Unity Hub. Do not use Unity 2022 LTS; the Solana Unity SDK and several mobile packages target 6+.
- **Render pipeline:** Universal Render Pipeline (URP). The project is mobile-first; HDRP is out of scope.
- **Camera:** Cinemachine 3.0+ for village free-look + dungeon follow-cam + ATB battle framing.
- **Input:** the new **Unity Input System** package (not the legacy `Input.GetKey` API). Map a `PlayerInput` action map for WASD/joystick movement, ability hotkeys (Q/W/E/R), tap-to-move, and pinch-to-zoom on mobile.
- **UI — HUD:** **UI Toolkit** (UXML + USS). Mirror the React HUD's structure (resource bar, hero portrait, ability hotbar, wave countdown, repair prompts). UI Toolkit scales cleanly across phone/tablet/desktop and is Unity's long-term direction.
- **UI — in-world:** **UGUI world-space canvases** for speech bubbles, name plates, building damage flashes, and tap-target indicators. UGUI's world-space mode handles depth and occlusion that UI Toolkit can't.
- **Localization:** the official **Unity Localization** package. The string table draws from `data/locale/en.json` (Part 4) so both engines read the same canonical strings.
- **Audio:** Unity's built-in **Audio Mixer**, with mixer groups matching the React project's audio layout (Master / Music / SFX / UI / Voice). Mixer default volumes match `docs/sound-design.md` and `docs/intro-audio-timing-reference.md` (the v1 audio is the canonical mix; if `docs/audio-mix-spec.md` exists by Week 6, supersede with that). All music tracks load as `Streaming`; one-shot SFX load as `DecompressOnLoad`.
- **Animation:** **Animator state machines** for hero / pets / enemies. **Timeline** for the studio bumper and the title-screen cinematic. The hero animator mirrors the React project's animation states (idle / walk / cast-Q / cast-W / cast-E / cast-R / hit / down).
- **Physics:** Unity built-in 3D physics for village + dungeon. No 2D needed in v2 foundation. CharacterController for the hero (not Rigidbody — the village's tap-to-move logic is kinematic by design).
- **Addressables:** yes. Mandatory for KayKit asset loading (211 GLTFs in the dungeon pack would blow up a Resources folder) and for streaming dungeon content per-scene. Configure two profiles: `Local` (everything bundled, for development) and `Remote` (CDN-streamed, for v1.1+ — placeholder for now).
- **Solana SDK:** the official **Solana Unity SDK** from `solana-mobile/solana-unity-sdk`. At agent spinup, fetch the current install instructions from the package README — package URL may have evolved. The SDK provides Mobile Wallet Adapter (MWA) for Android/Seeker; on iOS and desktop, fall back to deep-link wallet flows. Wallet operations in v2 foundation are **devnet only**; mainnet integration is gated by the owner.
- **Other useful packages:** `UniTask` (better async than `await Task` in Unity), `DOTween Pro` (tweening — owner-licensed if needed; otherwise stick to LeanTween free or write small lerp helpers), `Newtonsoft.Json` (more robust than `JsonUtility` for the data layer).

### Project structure (`Assets/` layout — mirrors `src/modules/`)

```
Assets/
  _Modules/                        # Feature modules, one folder + one .asmdef each
    Core/                          # App shell, scene loader, save system, settings
    Village/                       # Village scene, walls, gates, towers, heart
    Dungeons/                      # Dungeon scenes, lantern, lore-stones, encounters
    BattleATB/                     # ATB combat — pure C# engine + Unity scene
    Onboarding/                    # Title, studio bumper, splash, story intro
    Wallet/                        # Solana SDK wrapper, pack purchase, payouts UI
    HUD/                           # UI Toolkit documents, ResourceBar, AbilityBar
    Pets/                          # Pet rendering, AI, bond progression
  Data/                            # ScriptableObjects: enemies, towers, packs, pets
  Scenes/
    Title.unity
    Village.unity
    Dungeon_HealersCottage.unity
    ATBBattle.unity
  Prefabs/                         # Reusable composed prefabs (Tower, Wall, Enemy, Pet)
  Materials/                       # URP materials, including the Heart's violet emissive
  Shaders/                         # Custom shaders (force-field gate, withering vignette)
  Audio/                           # Imported from public/audio/ — see Part 7
  Models/                          # KayKit imports — see Part 7
  Localization/                    # en.json + LocalizationSettings asset
  Editor/                          # Editor scripts, asset import settings, custom inspectors
ProjectSettings/                   # Quality, Player, Input, etc.
Packages/                          # manifest.json — pinned versions
```

Each `_Modules/<Name>/` folder contains its own **Assembly Definition** (`.asmdef`). The asmdef enforces module isolation the same way `OWNERSHIP.md` enforces it in the React project: a module's asmdef references `Core`, `Data`, `Localization`, and shared util asmdefs, but never another module's asmdef. Cross-module communication flows through ScriptableObject events (Part 3) or `Core/Services/`.

### Project settings to lock

- **Color space:** Linear (URP requirement).
- **Texture import:** mobile-aggressive defaults — Max Size 1024 for character/prop atlases, 256 for small props, **ASTC 6×6** compression for Android, ASTC 6×6 also for iOS (same format, smaller file size than ETC2). Generate mipmaps off for UI sprites, on for 3D textures.
- **Audio import:** Vorbis compression. Music tracks (`Streaming` load type, `Compressed In Memory`). One-shot SFX (`DecompressOnLoad`, `Decompress On Load` — instant playback, small files).
- **Quality settings:** three named levels —
  - **Seeker_Low** (Seeker fallback): shadows soft only, no real-time shadows on dynamic objects, MSAA off, render scale 0.85, anisotropic textures off, target 30 FPS.
  - **Seeker_High** (default Seeker target): soft shadows on, MSAA 2x, render scale 1.0, target 60 FPS with stretch to 90 FPS on Seeker's 120Hz display.
  - **Desktop** (Vercel parity): full shadows, MSAA 4x, render scale 1.0, target 60 FPS.
  The player can switch quality from settings; Seeker auto-detects `SystemInfo.deviceModel` and defaults to `Seeker_High`.
- **Player settings (Android):** target SDK = current Solana Mobile dApp Store minimum (verify at submission; as of 2026-05 = Android 13 / API 33). Package name: `studios.denelle.defendersoftherealm`. Bundle ID matches the iOS bundle for cross-platform parity.
- **Player settings (iOS):** capabilities — none beyond network and storage for v2 foundation. No push, no IAP (Stripe handles purchases via web view).
- **Scripting backend:** IL2CPP (release builds). Mono is fine for editor + development builds.
- **API compatibility level:** .NET Standard 2.1 (the Solana Unity SDK's minimum).

The Unity project is created at `C:\Users\Kayden-Laptop\Documents\defenders-unity\`. Git repo initialized at root (`.gitignore` from Unity's GitHub template). First commit message: `chore: scaffold Unity 6 LTS project per v2-unity-port-spec.md Week 1`.

---

## Part 3 — The C# port table

The agent's primary map. Every TypeScript module from the v1 codebase has a Unity equivalent here. Difficulty is `Easy` (1-2 days), `Medium` (3-5 days), `Hard` (1-2 weeks).

| TypeScript source | Unity port | Pattern | Difficulty | Order (week) |
| --- | --- | --- | --- | --- |
| `src/lib/atb/types.ts` | `_Modules/BattleATB/Engine/Types.cs` | Plain C# structs + enums; mirror unions as enums | Easy | 2 |
| `src/lib/atb/rng.ts` | `_Modules/BattleATB/Engine/Rng.cs` | Seedable PRNG (xorshift); same seed → same sequence as TS | Easy | 2 |
| `src/lib/atb/targeting.ts` | `_Modules/BattleATB/Engine/Targeting.cs` | Static class, pure functions, no MonoBehaviour | Easy | 2 |
| `src/lib/atb/actions.ts` | `_Modules/BattleATB/Engine/Actions.cs` | Static class, pure functions | Easy | 2 |
| `src/lib/atb/ai.ts` | `_Modules/BattleATB/Engine/Ai.cs` | Static class, pure functions, takes battle state and returns chosen action | Medium | 2 |
| `src/lib/atb/combat.ts` | `_Modules/BattleATB/Engine/Combat.cs` | Static class — damage formulas, status ticks, defense math | Medium | 2 |
| `src/lib/atb/turn.ts` | `_Modules/BattleATB/Engine/Turn.cs` | Static class — turn step, ATB bar fill, ready detection | Medium | 2 |
| `src/lib/atb/state.ts` | `_Modules/BattleATB/Engine/BattleState.cs` | Plain C# class with serialized fields; held by the runtime ScriptableObject | Medium | 2 |
| `src/lib/atb/defs.ts` | `_Modules/BattleATB/Engine/Defs.cs` + matching ScriptableObjects in `Data/Combatants/` | Mirror TS literal defs as `[CreateAssetMenu]` ScriptableObjects per combatant | Medium | 2 |
| `src/store/gameStore.ts` | `_Modules/Core/State/GameState.cs` (ScriptableObject) + `GameStateService.cs` | ScriptableObject holds persisted state; service raises `UnityEvent`s on mutation; serialized to `PlayerPrefs` as JSON | Hard | 1 |
| `src/store/atbStore.ts` | `_Modules/BattleATB/State/ATBRuntimeState.cs` (ScriptableObject) | Runtime-only (not persisted); UnityEvent on action submit / turn resolve / outcome | Medium | 2 |
| `src/store/dungeonRuntimeStore.ts` | `_Modules/Dungeons/State/DungeonRuntimeState.cs` (ScriptableObject) | Runtime-only; tracks current room, checkpoints reached, lore-stones read | Medium | 5 |
| `src/store/clanStore.ts` | `_Modules/Core/State/ClanState.cs` | Defer to v2.1 — clan API stub returns mock data in v2 foundation | Easy | 7 |
| `src/store/towerSimStore.ts` | (deferred) | Tower-Sim is a v1.1 feature in React too; Unity skips for v2 foundation | — | — |
| `src/store/saveSchema.ts` | `_Modules/Core/State/SaveSchema.cs` + `SaveMigrator.cs` | Versioned JSON shape with migration steps; PlayerPrefs as the storage layer (analogous to localStorage) | Hard | 1 |
| `src/App.tsx` (router) | `_Modules/Core/SceneRouter.cs` | Static class with `LoadScene(string)` + `LoadSceneWithFade()`. Scenes are Title / Village / Dungeon_X / ATBBattle | Easy | 1 |
| `src/modules/onboarding/LandingPage.tsx` | `Scenes/Title.unity` + `_Modules/Onboarding/TitleController.cs` | Scene with Heart-Wing banner, "By lantern. By oath. By Heart." tagline, Connect Wallet + Start buttons | Medium | 1 |
| `src/modules/onboarding/SplashLoading.tsx` | `_Modules/Onboarding/SplashLoading.cs` + Timeline | Studio bumper (DeNelle Studios MP4 played via VideoPlayer) → fade → title | Medium | 1 |
| `src/modules/onboarding/StoryIntro.tsx` | `_Modules/Onboarding/StoryIntroPlayable.cs` + Timeline | Three-line cold open from `narrative-bible.md` §7.1, ~5s, auto-plays on first launch | Medium | 1 |
| `src/modules/village/Village3D.tsx` | `Scenes/Village.unity` + `_Modules/Village/VillageController.cs` + sub-MonoBehaviours per system | One scene; controller orchestrates wave manager, build menu, hero rig, camera. Sub-systems each have their own MonoBehaviour | Hard | 3-4 |
| `src/modules/village/heart/` | `_Modules/Village/Heart/HeartController.cs` + `HeartCrystalEmissive.shader` | Crystal-veined tree with violet emissive that responds to threat state (serene → vigilant → warning → danger → critical) | Hard | 3 |
| `src/modules/village/walls/segments.ts` | `_Modules/Village/Walls/WallLayout.cs` | Square perimeter generator: 4 sides × 4 sections per side with centered gate gap. Port the math verbatim from `segments.ts` | Medium | 3 |
| `src/modules/village/walls/KayWalls.tsx` | `_Modules/Village/Walls/WallSegment.cs` (MonoBehaviour) + `WardedWall.prefab` | One prefab per wall section, instantiated by VillageController against WallLayout's output | Medium | 3 |
| `src/modules/village/walls/Gate.tsx` | `_Modules/Village/Walls/Gate.cs` + `ForceFieldGate.shader` | Cardinal force-field gate with shimmer shader; takes damage; force-field collider toggles on/off | Medium | 4 |
| `src/modules/village/buildings/` | `_Modules/Village/Buildings/Building.cs` + per-type prefabs (CrystalMine, PetHouse, ArcaneTower, Farm, Workshop) | One Building MonoBehaviour, configured by a BuildingDef ScriptableObject | Medium | 3 |
| `src/modules/village/hero/` | `_Modules/Village/Hero/HeroController.cs` + AnimatorController | Three classes (Mage / Knight / Ranger); v2 foundation ships **Mage / Blaise** only | Hard | 4 |
| `src/modules/village/waves/` | `_Modules/Village/Waves/WaveManager.cs` + `WaveDef` ScriptableObjects | Per-wave spawn pattern; reads `data/waves.json` | Medium | 4 |
| `src/modules/village/enemies/` | `_Modules/Village/Enemies/Enemy.cs` + `EnemyAi.cs` | Base Enemy MonoBehaviour drives nav, HP, on-hit. AI variants: Walker (default), Charger, Skirmisher | Medium | 4 |
| `src/modules/village/hud/` | `_Modules/HUD/HUDDocument.uxml` + `HUDController.cs` | UI Toolkit document; resource bar, hero portrait, ability hotbar, wave countdown, build menu | Hard | 3-4 |
| `src/modules/village/dev/` | `_Modules/Village/Dev/DevPanel.cs` (Editor-only or env-gated) | Spawn wave, set HP, jump to dungeon — only loaded in development builds | Easy | 4 |
| `src/modules/dungeons/` | `_Modules/Dungeons/DungeonController.cs` + one scene per dungeon | v2 foundation ships **Healer's Cottage only** | Hard | 5-6 |
| `src/modules/dungeons/encounters/` | `_Modules/Dungeons/EncounterTrigger.cs` + `RandomEncounterTable.cs` | Random encounter logic from `docs/dungeon-3d-healers-cottage-design.md`; checkpoints heal + save | Medium | 6 |
| `src/modules/dungeons/lantern/` | `_Modules/Dungeons/Lantern.cs` + `LanternLight.prefab` (Point Light) | Hero-attached PointLight; intensity falls when lantern oil drops; gates audio (`lantern-flicker.mp3`) | Easy | 6 |
| `src/modules/dungeons/wanderer/` | `_Modules/Dungeons/Wanderer/Bryn.cs` + `WandererDialogue.cs` | Bryn (the Wanderer) at the Healer's Cottage entrance; world-space speech bubble | Medium | 6 |
| `src/modules/dungeons/lore-stones/` | `_Modules/Dungeons/LoreStone.cs` + `LoreStoneModal.uxml` | Tap-to-read; lore copy from `data/lore-fragments.json` (preserves narrative-bible voice) | Easy | 6 |
| `src/modules/battle-atb/BattleATB.tsx` | `Scenes/ATBBattle.unity` + `_Modules/BattleATB/BattleController.cs` | Wires the pure-C# engine to scene combatants + UI; loads via `SceneRouter.LoadScene("ATBBattle", BattleParams)` | Hard | 2 |
| `src/modules/pets/` | `_Modules/Pets/Pet.cs` + per-species prefabs | Aether Sprite, Flame Pup, Ice Wolf — bond progression + AI mode (aggressive/defensive/balanced) | Medium | 4 |
| `src/modules/wallet/` | `_Modules/Wallet/WalletService.cs` + `WalletConnectDialog.cs` | Wraps Solana Unity SDK; exposes `Connect()`, `GetBalance()`, `Pay(pack)`. Devnet only in v2 foundation | Hard | 7 |
| `src/modules/store/` | `_Modules/Wallet/PackStore.cs` + `PackStore.uxml` | Renders the five packs (Hearth Spark → Founder's Vow) from `data/packs.json`; pack purchase flow via WalletService | Medium | 7 |
| `src/modules/chat/`, `src/modules/clans/` | (stubbed) | Mock data only in v2 foundation; real integration is v2.1 | Easy | 7 |
| `src/services/chat.ts`, `src/services/clan.ts` | `_Modules/Core/Services/ApiClient.cs` | Wraps `UnityWebRequest`; hits the same `api/*` endpoints as the React client; v2 foundation stubs these | Easy | 7 |
| `src/lib/themes.ts` | `_Modules/Core/Theme/Theme.cs` | Static class exposing colors / font; UI Toolkit pulls from a Theme.uss USS variables file | Easy | 1 |
| `src/lib/constants.ts` | `_Modules/Core/Constants.cs` | Plain static class | Easy | 1 |
| `src/content/tooltips.ts` | `Localization/StringTable.en` (Localization package) + `data/tooltips.json` | All tooltip copy lives in localized strings; ports verbatim from React | Easy | 4 |
| `src/content/story.ts` | `data/canon-strings.json` + `Localization/StringTable.en` | Cold open, wave warnings, Heart's voice state changes, defeat lines — all from `narrative-bible.md` §7 | Easy | 1 |
| `src/assets/enemyRegistry.ts` | `Data/Enemies/*.asset` (ScriptableObjects) | One ScriptableObject per enemy type; loaded into an Addressables group `enemies` | Medium | 4 |
| `src/data/gameDesign.ts` | `Data/GameDesign.asset` (ScriptableObject) | Master tuning constants — wave HP scaling, building HP, ability mana costs | Easy | 1 |

### Patterns the agent must reuse, not reinvent

- **ScriptableObject + UnityEvent for state.** Mirrors Zustand's "state + subscribe." A `GameState` ScriptableObject holds the data; a `GameStateChanged` UnityEvent fires on mutation; HUD elements subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- **Pure C# for engine math.** Mirrors `src/lib/*` — anything that is just math, no rendering, no MonoBehaviour. Easy to unit-test (Unity Test Framework). The ATB engine is the canonical example.
- **MonoBehaviour for scene-bound runtime.** When something needs to read transforms, raycast, instantiate prefabs, it's a MonoBehaviour.
- **UI Toolkit documents for HUD.** Each HUD surface is one `.uxml` + one `.uss` + one C# controller that binds `VisualElement` queries to state.
- **Addressables for asset loading.** Never `Resources.Load`. Every art asset goes through Addressables.
- **`async UniTask` for async flows.** Never `async void`. Wallet calls, scene loads, Addressables loads all return `UniTask`.

---

## Part 4 — Data-extraction protocol

The single most important pattern in this whole spec. **Both engines share game data via canonical JSON files.** This makes the React stream and the Unity stream cross-compatible: when a designer tunes an enemy stat block in `data/enemies.json`, both engines pick up the change next launch.

The canonical source lives in the **React project** under `data/` (the React project already imports many of these as TypeScript modules — they will be ported to JSON files during the Unity buildout). The Unity project either symlinks `data/` from the React project, or **the agent maintains a copy** in `defenders-unity/Assets/Data/Canonical/` and syncs it via a script on Monday mornings (Part 8).

In Unity, each JSON file is read at scene-init by a typed loader (`Newtonsoft.Json` → C# record), and the loader hydrates the relevant ScriptableObjects in memory. ScriptableObjects in `Assets/Data/` are kept as caches of the JSON; the JSON is the source of truth.

### The canonical data files

| File | Schema (abbreviated) | Used by |
| --- | --- | --- |
| `data/canon-strings.json` | `{ avalon, elarion, blaise, alduin, heartWing, tagline, publisher, ... }` — all canon names, in one place, never paraphrase | Both engines, every UI surface |
| `data/locale/en.json` | All localizable strings — tooltips, wave warnings, Heart's voice lines, defeat lines, victory lines, pet captions | Both engines (React via i18n; Unity via Localization package) |
| `data/enemies.json` | `[{ id, name, tier, element, hp, damage, speed, ai, dropTable, sprite }]` — all 400 enemy stat blocks | Both engines |
| `data/enemy-roles.json` | Role definitions — `walker`, `charger`, `skirmisher`, `boss` — with AI parameters | Both engines |
| `data/towers.json` | `[{ id, name, cost, damage, range, fireRate, projectile, ability }]` | Both engines |
| `data/buildings.json` | The five buildings (Crystal Mine, Pet House, Arcane Tower, Workshop, Farm) — HP, cost, upgrade costs, flavor text | Both engines |
| `data/walls.json` | Per-tier wall HP, segment count per side, gate gap config | Both engines |
| `data/heart.json` | Heart HP tiers, threat-state thresholds (serene/vigilant/warning/danger/critical), emissive color per state | Both engines |
| `data/waves.json` | Per-wave spawn patterns — `{ waveId, enemies: [{ type, count, lane, delay }], boss? }` | Both engines |
| `data/packs.json` | The five packs (Hearth Spark / Lanternlight / Folk's Thanks / Patron of Elarion / Founder's Vow) — USD, USDC, SOL, SKR amounts, contents, theme | Both engines |
| `data/pets.json` | `[{ id, species, name, element, bondRanks: [{ rank, xpThreshold, perk }] }]` — Aether Sprite, Flame Pup, Ice Wolf | Both engines |
| `data/abilities.json` | Hero ability defs — Q/W/E/R per class (Mage / Knight / Ranger). v2 foundation reads Mage only | Both engines |
| `data/dungeons/healers-cottage.json` | Dungeon room layout, encounter table, checkpoints, lore-stone IDs, Bryn dialogue triggers | Both engines |
| `data/dungeons/<other>.json` | One per dungeon — Apothecary's Vault, Wolfwarden's Vigil, Folk Who Forgot, Cold-Wandered's Pack, Last Keeper's Walk, At the Edge | React (v1.1); Unity ports as content arrives |
| `data/questlines.json` | Six questline definitions (Healer's Garden, Folk Who Forgot, Wolfwarden's Vigil, Cold-Wandered's Pack, Last Keeper's Walk, At the Edge) | Both engines |
| `data/lore-fragments.json` | Lore-stone text, Alduin's journal fragments, Mira's letters — all canon-voice prose | Both engines |
| `data/wallets.json` | `{ publisher, rewardsDistributor }` — public Solana addresses ONLY. Never private keys. From `docs/wallets-of-record.md` §1, §2 | Both engines (read-only display) |
| `data/audio-mix.json` | Mixer group default volumes — `{ master, music, sfx, ui, voice, ambient }` per `docs/sound-design.md` | Both engines |
| `data/gameDesign.json` | Top-level tuning constants — XP curves, currency rates, wall scaling | Both engines |

### Rules

- **Never duplicate canon strings across engines.** Every "Elarion", "Blaise", "By lantern. By oath. By Heart." comes from `data/canon-strings.json` or `data/locale/en.json`. The Unity agent never types these inline.
- **JSON wins over ScriptableObject.** If a ScriptableObject in `Assets/Data/Enemies/Goblin.asset` disagrees with `data/enemies.json`, the JSON is correct; regenerate the ScriptableObject from the JSON.
- **Schemas live in both places.** React uses `zod`; Unity uses `Newtonsoft.Json` with strongly-typed records. The agent writes a `SchemaTests.cs` test per data file that fails if the JSON shape no longer matches the C# record — early detection of cross-stream drift.
- **No private keys in `data/wallets.json`, ever.** Only the public addresses from `docs/wallets-of-record.md`. Signer keypairs do not enter the Unity repo.

---

## Part 5 — 8-week build order

Slow burn. 1–2 hours of agent work per day. The agent does not try to compress the schedule; week boundaries are also natural review points where the owner can intervene if the parallel stream has drifted.

### Week 1 — Project skeleton + Core module

**Goal:** the Unity project opens, builds, and shows a black title screen with the Heart-Wing banner.

- Install Unity 6 LTS.
- Create the project at `C:\Users\Kayden-Laptop\Documents\defenders-unity\` with the URP mobile template.
- Install packages per Part 2 (Cinemachine, Input System, Localization, Addressables, UniTask, Newtonsoft.Json, Solana Unity SDK).
- Create the folder structure from Part 2.
- Initialize git, write `.gitignore`, first commit.
- Stand up `_Modules/Core/`: `GameState` ScriptableObject, `SaveSchema.cs` + `SaveMigrator.cs`, `SceneRouter.cs`, `Theme.cs`, `Constants.cs`.
- Create the four scenes (Title / Village / Dungeon_HealersCottage / ATBBattle) as empty scenes; SceneRouter loads them.
- `Title.unity`: black background, Heart-Wing banner image (copied from `public/heart-wing.jpg`), tagline `"By lantern. By oath. By Heart."` in the canonical font. Connect Wallet button (stub). Start button (loads Village).
- Studio bumper scene timeline: DeNelle Studios MP4 plays via VideoPlayer for ~3 seconds, fades to Title.
- `data/canon-strings.json` and `data/locale/en.json` populated; Localization package wired.
- Save/load round-trip works: launch → click "New Game" → quit → relaunch → save state restored.

**Deliverable:** Title scene + studio bumper + save/load. No gameplay yet.

### Week 2 — Combat engine port

**Goal:** ATB battle engine runs end-to-end as a pure C# library, plus a placeholder scene proves it.

- Port `src/lib/atb/` to `_Modules/BattleATB/Engine/`. Module-by-module: `types`, `rng`, `targeting`, `actions`, `ai`, `combat`, `turn`, `state`, `defs`.
- The port is **line-by-line equivalent**. Same function names, same parameter order, same return shapes. Same RNG seeding behavior (so a battle started with seed=42 produces the same outcome in both engines — this is also an anti-cheat-relevant property per `docs/anti-cheat-spec.md`).
- Write unit tests (Unity Test Framework, EditMode): one test per engine module mirrors the corresponding TypeScript tests if any exist; otherwise the agent authors 5+ tests per module.
- `_Modules/BattleATB/State/ATBRuntimeState.cs` — runtime ScriptableObject.
- `ATBBattle.unity` scene: placeholder combatants (capsule meshes), one hero, one enemy, ATB bars in UI Toolkit, "Attack" button submits an action, engine resolves, log scrolls.
- `data/abilities.json` and combatant ScriptableObjects in `Assets/Data/Combatants/`.

**Deliverable:** ATB scene plays a placeholder battle to completion. Pure-C# engine is tested.

### Week 3 — Village skeleton

**Goal:** the village scene loads, the wall ring stands, the Heart glows, the player walks around.

- Import **KayKit Medieval** assets (Part 7).
- `_Modules/Village/Walls/WallLayout.cs`: port `segments.ts` math. Output: 16 wall section transforms + 4 gate gaps (4 sides × 4 sections each, with centered gate gap).
- `WardedWall.prefab`: KayKit wall mesh with warm-palette retune material (matches `docs/four-cardinal-gates-spec.md` "Warded Wall" tone).
- `_Modules/Village/Heart/HeartController.cs`: KayKit tree mesh (or a custom mesh if KayKit lacks one) at world origin, scaled up 2x. Crystal-veined emissive shader; default state `serene` = violet (#7C3AED-ish — verify against `src/lib/themes.ts`).
- `Village.unity`: walls + 4 cardinal gates (placeholder cubes) + Heart + ground plane.
- Camera: Cinemachine FreeLook rig orbits the Heart at ~12 unit radius.
- Hero rig (Blaise — Mage class): KayKit mage character mesh, CharacterController, WASD/joystick movement via Input System. No abilities yet.
- HUD shell (UI Toolkit): resource bar at top (crystals / food / coins — read from GameState), Heart HP bar.

**Deliverable:** village scene with a walking hero, walls, Heart, four gate placeholders, basic HUD.

### Week 4 — Village systems

**Goal:** the village plays Wave 1 end-to-end. An enemy can breach; on breach, the scene transitions to ATB.

- `_Modules/Village/Buildings/`: Crystal Mine, Pet House, Arcane Tower, Workshop, Farm. KayKit medieval buildings; one prefab each; HP from `data/buildings.json`.
- Build menu (UI Toolkit): floating menu near the build cursor; tap-to-place at valid tiles. Costs in crystals.
- `_Modules/Village/Hero/HeroAbilities.cs`: Q (bolt), W (frost nova), E (beacon), R (meteor). Read from `data/abilities.json`. VFX placeholders (Unity built-in particles).
- `_Modules/Village/Waves/WaveManager.cs`: countdown timer between waves, then spawn per `data/waves.json`. Wave 1 = 8 Hollow Walkers from the north gate.
- `_Modules/Village/Enemies/Enemy.cs`: KayKit skeleton mesh, NavMeshAgent, walks toward the Heart, attacks buildings/walls on contact, dies on HP zero.
- `_Modules/Village/Gates/Gate.cs` + `ForceFieldGate.shader`: violet shimmer; gate takes damage, force-field collapses below 25% HP, enemies pour through.
- Breach detection: when an enemy crosses the inner wall ring, trigger `SceneRouter.LoadScene("ATBBattle", new BattleParams { hero, pets, enemies = breachingEnemies })`. After ATB resolves, return to Village with damage applied.
- Pets: deploy the three starter pets (Aether Sprite / Flame Pup / Ice Wolf) at slots near the Heart. Each pet attacks the nearest enemy in range.

**Deliverable:** playable Wave 1. Build a tower, place a pet, wave fires, enemy approaches, you win or breach into ATB.

### Week 5 — Dungeon foundation

**Goal:** the Healer's Cottage scene exists, the hero walks in it, walls collide correctly.

- Import **KayKit Dungeon Remastered** (Part 7) — 211 GLTFs, texture atlas. Configure Addressables group `dungeons-healers-cottage`.
- `Dungeon_HealersCottage.unity` scene: layout per `docs/dungeon-3d-healers-cottage-design.md` — square rooms, corridors, entrance, end-room. Square geometry mirrors the village's square wall layout philosophy.
- `_Modules/Dungeons/DungeonController.cs`: scene manager that loads room layout, places hero at spawn, manages camera (Cinemachine follow rig, top-down isometric tilt).
- Hero collision: CharacterController + wall mesh colliders. Verify no walk-through bug. Smooth tap-to-move on touch; WASD on desktop.
- Ambient audio: `echoes-beneath-elarion.mp3` loops as dungeon BGM at the mix-spec volume.

**Deliverable:** hero spawns in the Cottage entrance, walks the rooms, can't clip through walls.

### Week 6 — Dungeon systems

**Goal:** the Healer's Cottage plays end-to-end: enter, meet Bryn, light a lantern, read lore-stones, fight an ATB encounter, return.

- `_Modules/Dungeons/Lantern.cs` + `LanternLight.prefab`: PointLight attached to hero, intensity falls over time (oil mechanic per `docs/dungeon-3d-healers-cottage-design.md`). Refill at oil stones. Audio: `lantern-flicker.mp3` at low oil.
- `_Modules/Dungeons/Wanderer/Bryn.cs`: NPC at the entrance. World-space speech bubble (UGUI). Dialogue from `data/lore-fragments.json#bryn-cottage-entry` — canonical voice from narrative-bible.
- `_Modules/Dungeons/LoreStone.cs`: tap-to-read interactable. Modal (UI Toolkit) shows the lore text. Each lore-stone ID maps to a fragment in `data/lore-fragments.json`.
- `_Modules/Dungeons/EncounterTrigger.cs`: scripted encounter zones + a random encounter table. Trigger an ATB battle via `SceneRouter.LoadScene("ATBBattle", ...)`. After resolution, return to dungeon scene with hero HP/mana state preserved.
- Checkpoints: certain rooms heal hero + save progress. Implemented as `Checkpoint.cs` MonoBehaviour at fixed world positions.

**Deliverable:** Healer's Cottage plays from entrance to end-room, including at least one scripted ATB encounter and one lore-stone read.

### Week 7 — Wallet + economy stubs

**Goal:** the wallet connects on devnet, the store renders the five packs, a pack "purchase" runs through a devnet transaction.

- `_Modules/Wallet/WalletService.cs`: wraps Solana Unity SDK. `Connect()` opens MWA (Android/Seeker) or deep-link (iOS/desktop fallback). `GetBalance()` returns SOL / USDC / SKR. `Pay(PackDef pack, CurrencyKind currency)` builds and sends the transfer transaction on **devnet**.
- `_Modules/Wallet/PackStore.cs` + `PackStore.uxml`: renders packs from `data/packs.json`. Each pack shows USD reference + per-currency amounts (SOL / USDC / SKR) per the monetization spec. On purchase, calls WalletService, awaits tx confirmation, applies pack contents to GameState.
- `Rewards Distributor display`: title scene or settings shows the public Rewards Distributor address from `data/wallets.json` for transparency (matches the v1 React "treasury transparency" pattern).
- **Mainnet is gated.** A `WalletNetwork` enum (Devnet / Mainnet) defaults to Devnet. Switching to Mainnet requires changing a single static constant — but the agent does not do this without owner approval (Part 10).

**Deliverable:** connect a Seeker phone (or Phantom on desktop) on devnet, "buy" a Hearth Spark pack with 25 devnet SKR, see the pack contents land in the game.

### Week 8 — Acceptance gate

**Goal:** end-to-end playable build, decisions log clean, documentation index ready, contractor-ready state.

- Acceptance playthrough (Part 9) runs clean on Seeker emulator (or a real Seeker if available) for 5 minutes without crashes.
- 60 FPS held during village wave 1 + dungeon walking. Frame-time spikes ≤ 33ms.
- Memory ceiling ≤ 400 MB during the playthrough.
- Save state survives an app restart.
- All canon names verified on-screen against `data/canon-strings.json` — Avalon, Elarion, Blaise, Alduin the Mournful, the Heart-Wing, "By lantern. By oath. By Heart.", DeNelle Studios.
- Decisions log `docs/unity-decisions.md` is current with every architectural call made during Weeks 1–8.
- A `docs/README.md` in the Unity repo's `docs/` folder indexes every spec the agent has produced or referenced (this spec, the decisions log, any Week-N retros).
- Build outputs an `.apk` for Android (Seeker target) and a Windows `.exe` for desktop testing.

**Deliverable:** an APK + decisions log + docs/. Owner reviews. Decision: continue to v2.1 (more dungeons, more wallet polish, Tower-Sim), pause for v1 launch, or hire a Unity contractor to take it forward.

---

## Part 6 — Decisions log template

The agent maintains `docs/unity-decisions.md` in the Unity repo. Every non-trivial architectural call gets a row. This is how the owner stays oriented after weeks of background work.

```markdown
# Unity Port — Decisions Log

| Date | Decision | Alternative considered | Reason chosen | Reversible? |
|------|----------|------------------------|---------------|-------------|
| 2026-05-20 | UI Toolkit over UGUI for HUD | UGUI (mature; familiar) | UI Toolkit is Unity's long-term direction; better for responsive scaling across phone/tablet/desktop; USS variables enable theming parallel to React `theme.ts` | Mostly yes — UGUI can be added per-screen if a specific HUD surface needs it (e.g. world-space speech bubbles use UGUI) |
| 2026-05-22 | Newtonsoft.Json over JsonUtility for data layer | JsonUtility (built-in, no dep) | JsonUtility doesn't handle nested generic dictionaries or polymorphic types the data files use | Easy — could replace later with System.Text.Json (.NET) if Newtonsoft becomes painful |
| 2026-05-25 | ScriptableObject + UnityEvent over a custom event bus | MessagePipe (Cysharp event bus) | ScriptableObject events are inspector-visible, designer-friendly, well-supported by Unity tutorials — lowest learning-curve pattern for a future contributor | Yes — extracting to MessagePipe is mechanical |
```

### What counts as a "decision worth logging"

- Anything that changes how 3+ scripts interact (asmdef topology, event flow, scene-loading pattern).
- Anything that the owner might disagree with on review (UI Toolkit vs UGUI; Cinemachine 3.0 vs 2.x; IL2CPP vs Mono).
- Any deviation from this spec (a step in the build order skipped or reordered).
- Any substitution made because a referenced doc didn't exist (e.g. falling back from `docs/audio-mix-spec.md` to `docs/sound-design.md`).
- Any package selected from a field of alternatives (DOTween vs LeanTween vs hand-rolled lerps).
- Any time the agent runs out of clear direction and makes a judgment call (e.g. "the Tower-Sim is out of v2 scope so I'm not porting `towerSimStore`").

### What does NOT need to be logged

- Naming a variable.
- Picking an obvious choice with no real alternative ("I used CharacterController for hero movement" — that's just what Unity wants for kinematic movement; no decision involved).
- Tuning numeric constants where the JSON or spec specifies the value.

### When to stop and ask the owner

If the agent encounters a decision that's **irreversible** AND **affects user-visible canon** (a canon name, a wallet address, a pack price, a music track), it pauses, writes the row with `Reversible? No — awaiting owner review`, and waits for the next Monday absorption (Part 8). It does not guess.

---

## Part 7 — Asset import order

Both KayKit packs were licensed by the owner and live in the React project's `public/kaykit/`. The Unity agent copies them; nothing needs to be re-licensed.

### Source paths

- `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\public\kaykit\` — KayKit Medieval.
- `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\public\kaykit\dungeon\` — KayKit Dungeon Remastered (211 GLTFs + texture atlas).
- `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\public\audio\` — all music + SFX, including `echoes-beneath-elarion.mp3`.
- `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\public\heart-wing.jpg` — the LandingPage banner.
- `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\public\studio-bumper.mp4` — DeNelle Studios opening bumper.
- `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\public\portraits\` — hero portraits.
- `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\public\intro\` — story-intro images.

### Import order

1. **KayKit Medieval — Week 3.** Import in this order: walls + floors → ground tiles → buildings (mine, house, tower, farm, workshop) → characters (hero, Bryn, generic Folk) → props (barrels, lanterns, fences). Each category becomes a Prefab Variant tree under `Assets/Prefabs/Village/`.
2. **KayKit Dungeon Remastered — Week 5.** 211 GLTFs is large; configure an Addressables group `dungeons-healers-cottage` and import only the meshes the Cottage uses. The rest of the pack waits for v2.1.
3. **Audio — Week 1 (BGM only) + Week 4 (SFX) + Week 6 (dungeon ambient).** All MP3s from `public/audio/`. Verbatim copy; no re-encoding.
4. **Brand assets — Week 1.** Heart-Wing banner, studio bumper MP4, tagline font. Keep JPGs/PNGs at original resolution.

### Per-category import settings

- **Models (GLTF):** `Optimize Mesh` ON, `Read/Write Enabled` OFF (saves memory) unless a specific mesh needs runtime modification, `Generate Lightmap UVs` ON for static meshes. Compression: `Medium`. Animation type: `Generic` for KayKit characters (Humanoid retargeting is overkill for this art style).
- **Textures (props, atlases):** Max Size 1024 for character/building atlases, 256 for small props. Compression: ASTC 6×6 (Android + iOS). `sRGB` ON for albedo; OFF for normal/mask maps.
- **Audio music:** Vorbis, Quality 70, Streaming load type, `Compressed In Memory`.
- **Audio SFX:** Vorbis, Quality 80, DecompressOnLoad.
- **Video (studio-bumper.mp4):** VideoClip asset with `Transcode` ON, codec H.264, quality `Medium`. Plays via VideoPlayer in a RenderTexture or directly on the title-scene UI.

The agent runs Unity's Editor scripts (`Assets/Editor/AssetImportPostprocessor.cs`) to apply these settings automatically on import. This avoids hand-tweaking 211 dungeon GLTFs.

---

## Part 8 — Cross-stream sync protocol

The risk: the v1 React stream's specs evolve while the Unity stream is mid-build. A canon name moves. A pack price changes. A questline gets cut. The Unity stream needs to absorb these without rebuilding what it already shipped.

### The cadence

- **Friday rollup (v1 stream):** every Friday, the React/Vite stream's agent emits a `docs/spec-changes-week-N.md` file in the React project summarizing every spec change that landed that week. This file lives in the React project's `docs/` folder.
- **Monday absorb (v2 stream):** the Unity agent's first task every Monday morning is to read the latest `docs/spec-changes-week-N.md` from the React project. The agent updates its plan, the decisions log, and any affected ScriptableObjects.
- **Major scope changes** (canon name change, system cut, monetization compromise reversal, wallet rotation) **pause the Unity stream until owner approval.** The agent flags in the decisions log: `paused awaiting owner: <reason>`. It does not proceed. It does not guess.
- **Minor scope changes** (one enemy stat tweak, one tooltip rewording) are absorbed in the same Monday session. Update the JSON; regenerate the ScriptableObject; commit; continue.

### The pace asymmetry

The Unity stream's pace is intentionally **slower** than the React stream's spec evolution. If the React stream emits two weeks of spec changes in one week, the Unity stream takes two weeks to absorb them. This is fine. The Unity stream is research; the React stream is shipping. The Unity stream does not race.

### What the v2 agent watches for in v1 changes

- `docs/narrative-bible.md` — any canon-name change is high-priority absorb (re-sync `data/canon-strings.json`).
- `docs/monetization-v2-spec.md` — any pack-price change is high-priority absorb (re-sync `data/packs.json`).
- `docs/wallets-of-record.md` — any wallet rotation is critical (re-sync `data/wallets.json`).
- `docs/dungeons-storyline.md` and `docs/dungeons-system-design.md` — questline + dungeon-mechanic changes feed the dungeon module.
- Any new spec file in `docs/` referenced by this Unity port spec.

### What the v2 agent doesn't track

- React build/perf reports.
- Vercel deployment changes.
- React-specific lint/typecheck output.
- React module-internal refactors.

---

## Part 9 — Acceptance gates

"v2 Unity foundation viable" means **all** of the following hold at end of Week 8:

1. **End-to-end playable on the target hardware.** A 5-minute playthrough on Seeker (or Seeker emulator) covers: studio bumper → title → start → village → place a tower → trigger Wave 1 → fight the wave → win or breach → if breach, ATB battle → return to village → exit to title. No crashes. No softlocks.
2. **Dungeon playable end-to-end.** Alternate path: village → walk to dungeon portal → Healer's Cottage scene → encounter Bryn at entrance → walk the rooms → read at least one lore-stone → trigger one scripted ATB encounter → win → return to village. No walk-through-walls bugs. Lantern PointLight works.
3. **60 FPS held** during village wave + dungeon walking on Seeker_High quality. Frame-time spikes ≤ 33ms during the playthrough. (The acceptance run is recorded; the agent inspects the frame graph from the Unity Profiler.)
4. **Wallet connect works** on devnet via the Solana Unity SDK. Mock pack purchase completes — a devnet SKR transaction goes through, the pack contents land in GameState.
5. **Audio plays** per `docs/sound-design.md` mix levels. Music crossfades at scene transitions (title → village; village → dungeon; village → ATB).
6. **Save state persists.** Quit the app mid-playthrough; relaunch; the save resumes with the same hero HP, pet bond, resources, wave number. `SaveSchema.Version` is set and bumpable.
7. **Canon names correct.** Search the Unity project for any of these strings and find zero rogue inline uses: "Avalon", "Elarion", "Blaise", "Alduin", "Heart-Wing", "By lantern. By oath. By Heart.", "DeNelle Studios". All occurrences flow through `data/canon-strings.json` or `data/locale/en.json`.
8. **Decisions log current.** Every decision row has a date, decision, alternative, reason, reversibility flag. The log is committed.
9. **Data layer populated.** Every JSON file in Part 4 exists in the Unity repo. Each is consumed by at least one C# loader. Schema tests pass.
10. **Contractor-ready.** A Unity contractor (or a different Claude Code session) can clone the repo, open the project in Unity 6 LTS, build the APK, and start adding features without owner ramp-up. The `docs/README.md` index is the only onboarding doc they need to read.

If any of these fail at Week 8, the agent writes a `Week-8-retro.md` describing the gap and what would close it. The owner decides whether to extend, descope, or pause.

### Stretch — if Week 8 goes faster than expected

- 90 FPS hold on Seeker (uses the phone's 120Hz display).
- A second dungeon (Apothecary's Vault) imported and walkable.
- Tower-Sim breach mode skeleton (placeholder; full system is v1.1 / v2.1).
- Mainnet wallet integration **stubbed and disabled** — feature-flagged off; an owner can flip the flag for a private test.

---

## Part 10 — What the agent does NOT do

Hard scope limits. Anything outside these requires owner approval — agent flags in the decisions log and pauses.

- **Does not touch the React codebase.** Not for bug fixes, not for spec edits, not for git operations. The React project at `C:\Users\Kayden-Laptop\Documents\defenders-of-the-realm\` is read-only from the Unity agent's perspective. Read freely; never write.
- **Does not deploy to production.** No Vercel writes. No App Store / Play Store / Solana Mobile dApp Store submission. No real distribution. Local build artifacts (APK, EXE) for owner testing only.
- **Does not push real-mainnet Solana transactions.** All wallet operations are devnet-only. The `WalletNetwork` static constant ships as `Devnet`. Flipping it to `Mainnet` requires explicit owner approval and the owner doing it themselves or signing off in writing in the decisions log.
- **Does not change canon names or lore copy.** Avalon, Elarion, Blaise, Alduin the Mournful, the Heart-Wing, "By lantern. By oath. By Heart.", DeNelle Studios — and every line of lore from `narrative-bible.md` — are inviolate. If a canon item appears wrong to the agent, the agent flags it in the decisions log and waits for owner clarification.
- **Does not add new gameplay systems** beyond what is specced in `docs/`. If the agent has an idea for an unspecced mechanic — a new ability, a new building, a new pet — it writes the idea into `docs/unity-ideas.md` as a parking lot and continues with the spec'd work.
- **Does not make economic decisions.** Pack pricing, SKR yield distribution, currency conversion, region-specific pricing — all owner-locked in `docs/monetization-v2-spec.md`. The Unity port reads those values; it does not change them.
- **Does not ship music it did not get from the owner.** All music transfers verbatim from `public/audio/`. No AI-generated music. No royalty-free substitutes. If a track is missing, the agent flags and pauses that scene's audio rather than improvising.
- **Does not author dungeon content beyond the six questlines.** The six dungeons are canonical (Healer's Cottage, Apothecary's Vault, Wolfwarden's Vigil, Folk Who Forgot, Cold-Wandered's Pack, Last Keeper's Walk, At the Edge — note the last is the endgame conversation, not a combat dungeon). The Unity agent ports them as content arrives in `data/dungeons/*.json` from the React stream. It does not invent a seventh.
- **Does not commit private keys, seed phrases, or signer keypairs** to any git history under any circumstance. Wallet addresses in `data/wallets.json` are public only. If the agent needs to sign on the owner's behalf for a test, it does so locally and never commits the key.
- **Does not change the cozy covenant.** The seven monetization constraints (one provisionally bent — see `docs/monetization-v2-spec.md` §2) hold in Unity exactly as in React. No loot boxes. No gacha. No randomized purchases. No FOMO. No energy systems. No combat-stat sales. Convenience power only.

---

## Appendix A — File-existence fallbacks

This spec references some docs that may not yet exist under exactly the cited filename. If the agent finds a referenced file missing, it falls back per this table and logs the substitution.

| Referenced (in this spec) | Fallback (use this if missing) | Reason |
| --- | --- | --- |
| `docs/audio-mix-spec.md` | `docs/sound-design.md` + `docs/intro-audio-timing-reference.md` | Audio mix canon spec was queued; sound-design is the closest existing source |
| `docs/dungeon-encounters-and-checkpoints-spec.md` | `docs/dungeon-3d-healers-cottage-design.md` + `docs/dungeons-system-design.md` | Encounter spec may be inside the dungeon-3d design rather than a standalone file |
| `docs/anti-cheat-spec.md` | (exists; cite directly) | — |
| `docs/cyber-audit-end-to-end-spec.md` | (exists; cite directly) | — |
| `docs/wallets-of-record.md` | (exists; cite directly) | — |
| `docs/dungeons-storyline.md` | (exists; cite directly) | — |
| `docs/refactor-feature-modules-spec.md` | (exists; cite directly) | — |
| `docs/narrative-bible.md` | (exists; cite directly) | — |
| `docs/monetization-v2-spec.md` | (exists; cite directly) | — |
| `docs/whitepaper.md` | (exists; cite directly) | — |

---

## Appendix B — Document version history

| Version | Date | Notes |
| --- | --- | --- |
| 1.0 | 2026-05-18 | Initial publication. Agent contract for the parallel Unity port stream. Locks Unity 6 LTS, URP, the 8-week build order, the decisions-log discipline, the data-extraction protocol, and the cross-stream sync cadence. |

---

_Tend the Heart. Hold the dark. — and in C# this time._
