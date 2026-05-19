# Unity Port — Decisions Log

Every non-trivial architectural call made during the v2 Unity port, per Part 6 of
`docs/v2-unity-port-spec.md`. The owner reads this to stay oriented after stretches
of background work. Newest rows at the bottom of each week.

## Week 1 — Project skeleton + Core module

| Date | Decision | Alternative considered | Reason chosen | Reversible? |
|------|----------|------------------------|---------------|-------------|
| 2026-05-18 | Agent creates the Unity project shell headlessly via Unity CLI `-batchmode -createProject`, then configures URP and every package by editing `Packages/manifest.json` and generating URP pipeline assets (Task #3). | Owner creates the project in Unity Hub with the URP template (the initial plan, logged earlier the same day, then superseded). | Owner directed the stream to run autonomously and not act as a bottleneck ("get started with autonomy"). Batchmode creation keeps Week 1 unblocked; the Hub URP baseline is reproduced via `manifest.json` + a generated URP pipeline asset. | Yes — the shell can be recreated in Hub if the manifest-driven URP setup proves incomplete. |
| 2026-05-18 | Canonical `data/*.json` is maintained as a **tracked copy** inside the Unity repo at `Assets/Data/Canonical/`, refreshed by a sync script on the Part 8 Monday cadence. | Symlink `data/` from the React project into the Unity repo. | The React project has no `data/` folder yet (game data still lives as TypeScript modules); Windows git symlinks need `core.symlinks` and are fragile; the two repos are deliberately history-isolated. A tracked copy + sync script is exactly the Part 8 protocol. | Yes — a symlink can replace the copy later with no loader change (same path). |
| 2026-05-18 | The agent **authors** the canonical `data/*.json` files (extracted from the React TS modules) directly in the Unity repo; these become the de-facto source of truth until the React stream creates its own `data/`. | Wait for the React stream to produce `data/*.json` first. | Part 10 forbids the agent writing to the React codebase, and Week 1 requires `canon-strings.json` + `locale/en.json` to exist. Authoring them Unity-side unblocks the port; the Monday sync reconciles when React catches up. | Yes — React-side `data/` supersedes on first sync if it diverges. |
| 2026-05-18 | Audio-mix canon = `docs/audio-mix-spec.md` (the file exists in this repo). | Fall back to `docs/sound-design.md` + `docs/intro-audio-timing-reference.md` per Appendix A. | Part 2 states: "if `docs/audio-mix-spec.md` exists by Week 6, supersede with that." It already exists, so the Appendix A fallback does not trigger. | n/a — resolves a spec ambiguity. |
| 2026-05-18 | Dungeon encounter/checkpoint canon = `docs/dungeon-3d-healers-cottage-design.md` + `docs/dungeons-system-design.md`. | `docs/dungeon-encounters-and-checkpoints-spec.md` (the spec's primary citation). | That file exists in neither repo; Appendix A names this exact fallback pair. Logged now though first consumed in Weeks 5–6. | n/a — substitution per Appendix A. |
| 2026-05-18 | Brand-asset source paths corrected: `heart-wing.jpg` → `public/assets/landing/`, `studio-bumper.mp4` → `public/assets/`, intro images → `public/assets/intro/`. | The Part 7 paths (`public/heart-wing.jpg`, `public/studio-bumper.mp4`, `public/intro/`). | The Part 7 top-level paths do not exist; the assets sit one folder deeper under `public/assets/`. Verified by directory search. | n/a — path correction only; asset bytes are what the spec intends. |
| 2026-05-18 | Canonical naming follows the **`docs/narrative-bible.md` layer**, not the `src/content/story.ts` layer, wherever the two drift ("Hollow Ones" not "Hollowed", "the Keeper" not "Guardian", "Tend the Heart" not "Tend the Lantern"). `canon-strings.json`/`en.json` retain both under distinct keys, but the bible layer is authoritative for shipped text. | Treat `story.ts` (the v1 React copy) as authoritative, or defer the choice to the owner. | Spec Part 1 explicitly enumerates "Hollow Ones" and "the Keeper" as canon, and the spec's own sign-off line is "Tend the Heart." — the bible layer is what the contract names. The `story.ts` drift is a v1 artifact. | Yes — a key remap if the owner later prefers the `story.ts` layer. |
| 2026-05-18 | Week-1 package install covers **10 packages**: URP 17.4.0, Cinemachine 3.1.6, Input System 1.19.0, Localization 1.5.8, Addressables 2.9.1, Timeline 1.8.12, Test Framework 1.6.0, Newtonsoft.Json 3.2.2, uGUI 2.0.0, UniTask 2.5.10 (OpenUPM scoped registry). The **Solana Unity SDK install is deferred to Week 7**. | Install every package including the Solana SDK in Week 1, as Part 2 / Week 1 lists. | The Solana SDK is a git-URL package, unused until Week 7's `WalletService`, and spec §2 itself calls its install method volatile ("fetch current install instructions at spinup"). Deferring it keeps the Week-1 resolve low-risk and lets Week 7 verify against the then-current README. Versions for the 8 Unity packages are editor-validated for 6000.4.7f1; Cinemachine 3.1.6 and UniTask 2.5.10 were verified against the live registries. | Yes — adding the git-URL line in Week 7 is a one-line manifest change. |

| 2026-05-18 | Week 2 (ATB combat-engine port) is started **in parallel** with Week 1, not strictly after it. | Strict sequential week-by-week pacing per spec Part 5 ("slow burn… does not compress the schedule"). | Owner explicitly directed autonomous, parallel execution ("get started with autonomy", "feel free to use many agents"). The ATB engine is pure, self-contained C# with no Week-1 dependency, so parallelizing it is low-risk. Week boundaries are still honored as integration/review checkpoints. | Yes — parallel work merges at the normal integration points; nothing ships out of order. |
| 2026-05-18 | Canonical data JSON lives at `Assets/StreamingAssets/Data/Canonical/`, not the spec's `Assets/Data/Canonical/` (Part 4). | The spec's literal `Assets/Data/Canonical/` path. | `Assets/Data/Canonical/` is not readable at runtime without `Resources.Load` (spec Part 3 forbids it) or a full Addressables group (heavier than Week 1 needs). `StreamingAssets/` is Unity's standard location for runtime-read config JSON and keeps the files as plain editable JSON for the Part-8 Monday sync. `Theme.cs` reads via `Application.streamingAssetsPath`. | Yes — the files can move to an Addressables group later with only a loader-path change. (Android note: StreamingAssets needs a `UnityWebRequest` read; to be added with the Week-7/8 Seeker build.) |

### Flags raised (not decisions — awaiting owner)

- **2026-05-18 — Missing dungeon audio.** `public/audio/` contains only `battle/defeat/title/victory/village.mp3`. The spec references `echoes-beneath-elarion.mp3` (Week 5 dungeon BGM) and `lantern-flicker.mp3` (Week 6 low-oil SFX) — neither exists. Per Part 10 the agent does not substitute music; the affected dungeon audio will be paused and flagged until the owner supplies the tracks. **Not a Week 1 blocker.**
- **2026-05-18 — Unsourced canon names.** Spec Part 1 lists **Bryn, Mara, Tovin, Eira, Aelf, Mira** as canon, but none appear in `docs/narrative-bible.md` or `src/content/story.ts`. The extracted `canon-strings.json` carries them as flagged placeholders. Bryn (the Wanderer) is likely defined in `docs/dungeon-3d-healers-cottage-design.md`; the others may surface in dungeon/questline docs. These must be sourced before any Week 6 dungeon text ships. **Not a Week 1 blocker.** The tagline "By lantern. By oath. By Heart." and "DeNelle Studios" were also absent from source files but are supplied verbatim by spec Part 1 — authorized, no action needed.
- **2026-05-18 — Unity licensing handshake warning.** Batchmode runs log `[Licensing::Module] Failed to handshake to channel … LicensingClient has failed validation; ignoring`. Unity proceeds normally afterward (project create, package resolve, compile, and test runs all succeed), so the editor is operating on a cached/offline license. Watch item: if the license fully lapses, batchmode builds/CI would start failing. **Not currently blocking.**
- **2026-05-19 — Realm Map region names lack canon authority.** `realm-map.json` defines 5 regions (`thornwood`, `mirewood`, `hollowfrost`, `emberwastes`, `starfall-reach`). The React design doc (`map-content-dungeons-design.md`) and the live React code disagree on several names (doc: Wintermere / Sunken Causeway / Hollow Deep — code: Mirewood / Hollowfrost / Starfall Reach). The extraction followed the **React code catalog**, because the `RegionId`s are persisted save-ledger keys and cross-stream save compatibility depends on them. None of the region names appear in `docs/narrative-bible.md`, and the region `description` prose was authored fresh (no canon source). Owner must ratify the canonical region names before any Realm Map UI ships. **Not a Week 1 blocker** (Realm Map is deferred / v1.1).

## Week 3 — Avalon village layout

Per `docs/avalon-village-layout-spec.md`; creative decisions logged per spec §13.

| Date | Decision | Alternative considered | Reason chosen | Reversible? |
|------|----------|------------------------|---------------|-------------|
| 2026-05-18 | Castle Keep ("Keeper's Keep") placed adjacent to Elarion at the village centre | Elarion alone at the centre | Two anchors frame the central plaza better; the Keeper's home was implied but unspecced in the React scene | Yes — the Keep can be relocated or removed |
| 2026-05-18 | Curtain wall is a shaped rectangle (~30×24 hex, wider E–W) with a south bow-out, not a tight square | A tight defensive square (the old `segments.ts` layout) | Owner-directed creative latitude (spec §2); `WallLayout.cs` reworked from the square port to the rectangle | Yes — the wall can be re-shaped freely |
| 2026-05-18 | Residential cluster grouped in the SW quadrant | Houses distributed evenly around the town | A "village quarter" reads more lived-in than scattered homes | Yes |
| 2026-05-18 | Forest Nature pack tree used for the Elarion centerpiece | A Hexagon-pack tree | The Forest pack has larger, more visually anchoring tree meshes | Yes — falls back to the Hexagon-pack tree if the Forest pack is absent |
| 2026-05-19 | Exterior is a hybrid — hex tiles inside + approach lanes, Unity Terrain beyond, with a 55u smoothstep seam plateau holding Y=0 under the walls | All-hex exterior, or all-Terrain | Spec §9.1 mandates the hybrid; the plateau keeps the wall seam flush (§9.8) | Yes |
| 2026-05-19 | Exterior Terrain offset −22u so its heightmap straddles village Y=0 | Terrain at Y=0 with the village raised | Lets the heightmap dip (south) and rise (north) around the village's neutral baseline | Yes |
| 2026-05-19 | Biomes blended by directional weight (smooth diagonals), not hard borders | Hard biome borders at the 45° lines | Avoids a visible seam where biomes meet; reads as one continuous realm | Yes |
| 2026-05-19 | Exterior Terrain uses flat-tinted, noised TerrainLayers (no external texture assets) | Import/author real ground textures | No suitable texture assets are imported; tinted layers are adequate for Week 3 and avoid blocking | Yes — swap in real textures later |

### Flags raised — Week 3

- **2026-05-19 — Per-direction fog gradient deferred.** Spec §9.5 wants denser fog toward the south (the Wound). Unity's built-in `RenderSettings` fog is uniform and cannot express a directional density gradient; a volumetric / URP fog pass is needed. The exterior ships with uniform exponential-squared dawn fog; the gradient is deferred and flagged. **Not a Week 3 blocker.**
- **2026-05-19 — Gate yaw corrected.** Owner observed the village gates sat ~90° off. The KayKit `wall_straight_gate` model needs the same `WallStraightYawFix` (90°) the `wall_straight` ring pieces already get; `BuildGates` now applies it to both the gate model and the violet `ForceFieldShimmer` stand-in quad, so the gate fills its wall opening flush. Verified in `screenshot-village-week3.png`. **Resolved.**
- **2026-05-19 — Elarion/Keep centerpiece white-render — OPEN finetune item.** The Heart (Elarion) + Keeper's Keep centerpiece renders as a white mass in the batchmode review screenshots. Extensively investigated and NOT cracked headlessly — logging the full state for the interactive-Editor pass:
  - The FBX importer remaps are verified correct (all 404 `fbx(unity)` models → a valid `hexagons_medieval_URP.mat` with `_BaseMap` set). The KayKit textured models (walls, buildings, crops, plaza) render correctly — so the atlas pipeline and batchmode rendering both work.
  - A renderer diagnostic confirms every object under Elarion has a URP/Lit material assigned, and a `Village.unity` grep confirms their `_BaseColor` values are correct in the serialized scene (tree green `{0.24,0.42,0.22}`, stones `{0.62,0.6,0.66}`, mound, veins, etc.). **The scene data is correct.**
  - Code hardening shipped anyway: `InstantiateModel` force-assigns the shared atlas material (`ForceHexMaterial`); `MakeFlatMaterial` now COPIES a known-good imported KayKit `.mat` (inheriting URP keyword setup) rather than building a bare `new Material(shader)`; `ApplyColorAll` tints all child renderers; the Keep is flat-tinted.
  - Result: the centerpiece is **immune to every material change** across ~12 rebuilds — strong evidence the white is NOT a material-assignment bug. Likely a batchmode shader-variant / lighting quirk on these specific meshes, or the meshes themselves. Needs the interactive Unity Editor (inspect the live renderer, not headless) to finish. **Deferred — owner-flagged "finetune later"; the village layout, walls, gates, buildings, crops all render correctly and the Week-3 deliverable stands.**
- **2026-05-19 — Exterior wilderness still rough (Task #14 open).** The exterior review shot shows a black (unlit) Terrain, an orange-void sky (no skybox), and a few strays floating off the Terrain. Exterior polish is owner-flagged "finetune later" and Task #14 is still in progress; the interior village is the Week-3 deliverable that is complete. **Tracked, not blocking.**
- **2026-05-19 — `ForceFieldShimmer` over-bright.** The gate force-field stand-in uses emissive intensity 1.4, which washes to white in the review shot rather than reading violet. Cosmetic; the real shimmer shader lands Week 4. **Finetune item.**

## Week 4 — Village systems

Built in parallel by three scoped agents (buildings / waves / hero-pets-gate); the
slice-level detail lives in `docs/port-notes/week4-*.md`. Key calls:

| Date | Decision | Reason | Reversible? |
|------|----------|--------|-------------|
| 2026-05-19 | Week-4 gameplay systems landed as compiling C# modules first; scene wiring (NavMesh bake, `VillageController` hookup, prefabs, HUD `UIDocument`, layers) is a separate integration pass | The C# compiles clean (0 errors) and is a reviewable unit; scene assembly is editor-scripting work that does not block the code review | Yes |
| 2026-05-19 | Enemy stats / wave countdowns sourced verbatim from React v1 (`enemyArchetypes.ts`, `waveConfig.ts`); Mage abilities + all pet data sourced from `mage.ts` / `petData.ts` | Part 4 — React is the canonical data source where it exists | n/a — extraction |
| 2026-05-19 | Per-building HP + crystal cost AUTHORED (React v1 has no such table — its 5 buildings are fixed map placements, not player-built) | Week 4 needs buildable structures with HP; values are JSON-tunable | Yes — rebalance via `buildings.json` |
| 2026-05-19 | Waves 2-3 AUTHORED (React generates waves procedurally — no literal table); Knight/Ranger ability sets are placeholders (v1 ships Mage only) | A concrete 3-wave table is needed for Week-4; v1 parity is Mage-only | Yes |
| 2026-05-19 | Cross-module pet/ability→enemy combat goes through a new `IDamageable` interface in `DeNelle.Core` (+ an `EnemyDamageable` adapter), not a direct type reference | Spec Part 2 forbids a module asmdef referencing another gameplay module's asmdef | Yes |

### Flags raised — Week 4

- **2026-05-19 — Village NavMesh bake required.** `Enemy` uses `NavMeshAgent`; only the legacy `com.unity.modules.ai` is in the manifest, so the village scene needs a NavMesh baked (legacy Navigation panel) before enemies path. Enemies hold position + log one warning until then. **Integration item, not a code defect.**
- **2026-05-19 — Week-4 scene wiring outstanding.** WaveManager / HeroAbilities / PetDeployer / BuildMenu `UIDocument` / the `ForceFieldGate` material / enemy + building prefabs / enemy layer mask must be added to `Village.unity` (or its builder) for Wave 1 to play. The C# is in place and compiles; see the `week4-*.md` notes for the precise wiring checklist. **Resolved 2026-05-19** by the Week-4 scene-integration pass (`VillageSceneBuilder.BuildGameplaySystems` + `BakeVillageNavMesh`).

## Weeks 5-7 — Dungeon foundation, dungeon systems, wallet

Built in parallel by scoped agents; slice detail in `docs/port-notes/week5-*.md`, `week6-*.md`, `week7-*.md`. All Week 5-7 C# compiles clean (0 errors).

| Date | Decision | Reason | Reversible? |
|------|----------|--------|-------------|
| 2026-05-19 | **Solana Unity SDK deferred — Wallet ships on the stub provider for now.** The `magicblock-labs` SDK was added to `manifest.json` as a git-URL and DID resolve, but it bundles its own copy of UniTask, producing dozens of GUID conflicts with the project's existing UniTask package. The manifest entry was reverted. | The bundled-UniTask collision actively breaks the build. The Wallet code is fully written behind `#if SOLANA_SDK` guards, so with the package absent it compiles clean on `StubWalletProvider` (full UI/devnet-flow testing still works). Real-SDK integration is a follow-up that must de-dupe UniTask — likely via the OpenUPM `com.solana.unity-sdk` (resolves deps separately) and pinned to a tag (security finding SEC-001). | Yes — re-add via OpenUPM once the UniTask collision is resolved. |
| 2026-05-19 | `DungeonCameraRig` uses a fixed-tilt **perspective** Cinemachine rig; the WorldSpace binding-mode override and the orthographic option were dropped. | `LensSettings.Orthographic` is read-only and the `TargetTracking.BindingMode` enum did not resolve in Cinemachine 3.1.6 — rather than guess version-specific API, the rig was simplified to the stable subset (a perspective follow rig is exactly right for a top-down dungeon). | Yes — both are CM-API finetune items. |

### Flags raised — Weeks 5-7

- **2026-05-19 — Dungeon scene wiring outstanding.** Week 5/6 C# (DungeonController, DungeonHero, DungeonCameraRig, Lantern, Bryn, LoreStone, EncounterTrigger, Checkpoint) compiles but is not yet wired into `Dungeon_HealersCottage.unity` — the dungeon-scene integration pass is the next step. See the `week5/6` port-notes for the checklist.
- **2026-05-19 — Breach→ATB return-scene bug (BUG-008 / CODE-001).** `BattleController` hard-codes the post-battle return to the Village; a dungeon ATB encounter must return to the dungeon. `WaveManager` also always restarts at the start wave on return. To be fixed in the integration pass.
- **2026-05-19 — Dungeon audio missing.** `echoes-beneath-elarion.mp3` and `lantern-flicker.mp3` are not in the project; the AudioSources are wired and guarded, silent until the owner supplies the tracks.

## Content additions (owner-supplied assets)

| Date | Decision | Reason | Reversible? |
|------|----------|--------|-------------|
| 2026-05-19 | The owner-supplied Black Dragon (a `.unitypackage`) is added as an apex flying boss — `DragonBoss.cs` + `Boss_Dragon.prefab` + `Dragon.controller`. Owner-ratified name: **"Syndrath the Devourer"** (overrides the agent's placeholder "Vael, the Ash-Wing"). Placement = a special apex village wave-boss above the Necromancer. | The owner pulled the asset in and wants a flying dragon boss; the enemy codex flagged the bestiary as having no true monster. The dragon's `.blend` sibling (BGE Dragon) was rejected — its export carried a 34k-unit environment plane and no animations. | Yes — the name is one display string; the boss is self-contained. |

## Grant-polish feature pass — 2026-05-19

Four scoped agents built, in parallel, the owner-confirmed feature set for the
Solana Foundation grant submission (`docs/qa/owner-acceptance-checklist.md` —
"owner decision 2026-05-19"): **A** intro flow + difficulty, **B** wall-repair +
countdown, **C** dungeon crafting + oil HUD, **D** townsfolk + camera. File
ownership was partitioned across the agents so they did not collide; all four
workstreams compile clean together (0 errors) and the five scene builders
(village / wall-repair / intro / dungeon / battle) re-ran via `GrantPolishBuilder`.

| Date | Decision | Reason | Reversible? |
|------|----------|--------|-------------|
| 2026-05-19 | Difficulty (Easy/Normal/Hard) scales the between-wave countdown by a multiplier (2.0× / 1.0× / 0.6×) over `WaveDef.CountdownSeconds`; reference base 300 s → Easy 10 / Normal 5 / Hard 3 min | Owner spec; a multiplier keeps per-wave values authored once with difficulty a single setting | Yes — tune in `DifficultyTuning` |
| 2026-05-19 | Hero presentation data lives in C# (`HeroCatalog`), not a `heroes.json` | Heroes are a code enum (`HeroClass`) with no content file; only display copy is externalised to en.json | Yes |
| 2026-05-19 | `PetSelectController` writes `GameState.StarterPetId` directly + `Save()` (no `ChooseStarterPet` mutator — hero has `ChooseHero`, pet had none) | Functionally equivalent; optional follow-up = add a mutator that raises `PetsChanged` | Yes |
| 2026-05-19 | Hero/pet-select screens self-skip to the Village when the save already records that choice | A returning player is never re-shown the intro flow | Yes |
| 2026-05-19 | The wall-repair confirm prompt is **modal** (taps route only to Confirm/Cancel while open) rather than plain tap-to-confirm | UI-Toolkit-vs-`Update` event ordering is non-deterministic; a modal is deterministic | Yes |
| 2026-05-19 | The wall-repair → HUD link uses a reflection bridge (`WallRepairHudBridge`) | The `RepairPromptInfo` struct payload is not a valid persistent-UnityEvent arg; matches the project's established reflection-based cross-module wiring | Yes |
| 2026-05-19 | `TownsfolkBubble` is a NEW class — a verbatim behaviour copy of `WandererBubble` (which lives in `DeNelle.Dungeons`) | Module isolation forbids `DeNelle.Village` referencing `DeNelle.Dungeons` | Yes — fold into a shared Core util later |
| 2026-05-19 | The dungeon crafting pedestal sits in the Hidden Vault (where the placeholder primitive was), not the spec's Crypt light-puzzle vault; ingredients auto-collect on walk-over | Keeps all other dungeon content in place; auto-collect aids legibility (the pedestal still needs an explicit interact) | Yes |
| 2026-05-19 | Village camera retuned to pos (0,51,-37) / pitch 55° / FOV 48; battle camera to (0,4.1,-6.6) / pitch 22° / FOV 46 | Tighter, better-composed framing for the grant demo | Yes — a values pass |

### Flags raised — grant-polish pass

- **2026-05-19 — Six UXML header comments had invalid XML (`--` dash rows) — FIXED.** The build agents wrote decorative `------` divider rows inside `<!-- … -->` header comments; XML forbids `--` inside a comment, so Unity rejected all six `.uxml` files on import (the C# still compiled). The dash rows were replaced with `=` rows and the import re-verified clean. Lesson for future agent UI work: no `--` inside XML/UXML comments.
- **2026-05-19 — Localization strings pending consolidation.** Workstreams B (wall-repair) and C (crafting) kept new user-facing strings as `// LOCALIZE:`-marked constants / in `crafting-recipes.json` rather than editing the shared `en.json` (avoiding a parallel-edit collision). They display correctly in English; a consolidation pass should fold them into `en.json` for multi-language. Keys are in `WallRepairStrings.cs` and `crafting-recipes.json`. **Follow-up, not a defect.**
- **2026-05-19 — `DungeonSceneBuilder` mirrors `crafting-recipes.json` ingredient placements as a literal editor table** (the Editor asmdef cannot read the typed runtime JSON shape — the same constraint the lore-stone / checkpoint builders work under). The JSON stays the runtime source of truth; the builder table only sets editor placement and must be kept in sync.
- **2026-05-19 — "Player" tag absent.** The townsfolk NPC hero-fallback is name-based ("Hero…"), not `FindGameObjectWithTag("Player")` (which would throw — the tag is undefined in `TagManager.asset`). Primary path is the explicit `SetHero` wire from the builder.
- **2026-05-19 — Grant-polish features built + compile-verified, not yet play-tested.** All four workstreams compile and the scenes build; runtime QA (the checklist's per-feature QA steps) is the next pass.
