# Assets/ — Top-Level Map

**Defenders of the Realm / Echoes of Elarion (Unity 6 LTS, URP, mobile-first).** V1 = a single
controllable Knight in an overworld with real-time BattleArena combat, dungeons, and a COC-style
raid loop; base/tower-defense is V2-gated (`ff.basebuilding`). The authoritative architecture
reference is `../docs/ARCHITECTURE.md` (module boundaries, world/scene model, save). The root
`CORE_ARCHITECTURE_PLAN.md` is a historical pre-pivot plan (read for intent, not current state).

What lives in each folder. Code modules have their own READMEs under `_Modules/`.

## First-party

| Folder | Contents |
|---|---|
| `_Modules/` | **All first-party code.** See `_Modules/README.md` for the module map |
| `Editor/` | `DeNelle.Editor` asmdef. Scene builders — `VillageSceneBuilder` (partial, split across ~10 files; **serialization bottleneck, one agent at a time**), `BattleSceneBuilder`, `DungeonSceneBuilder`, `ExteriorTerrainBuilder`, `FolksGranaryBuilder`, `IntroFlowSceneBuilder`, `CastleHubBuilder` (Defenders > Scenes > Build CastleHub_MainKeep — run after blank scene; Quaternius + polyperfect _M for Central Castle Hub with 8 storefront structures, 2-level keep/home quarters, upper battlements for defenses, overworld gate marker) — plus animator setups/factories, build scripts (`AndroidBuild`, `DesktopBuild`), `CompileGate`, material fixers (`KayKitMaterials`), portrait generators |
| `_Village2/` | `Village2Generator` + `TorchFlicker` — the modular village rebuild (WO-278/279/280) |
| `Scenes/` | **Never hand-edit `.unity` — rebuild via batchmode scene builders (see `Editor/`).** Home hub = `Main_Castle_Overworld` (MergedWorld ON, one navmesh; `Village.unity` and `OuterWorld.unity` are DELETED, `MainCastle_Hall` kept as legacy). Raid targets: `Village2`, `Garrison_*` (frost_keep/hill_fort/ruined_keep/troll_outpost/village2_stronghold), `RaidBase_*` (IronBastion/fortified_garrison/mage_enclave/raider_camp_small), `Outpost1/2`, `KayKitChallengeOutpost`. Dungeons: `Dungeon`, `Dungeon_Demo`, `Dungeon_HealersCottage`, `Dungeon_FolksGranary`. Combat/flow: `ATBBattle`, `Title`, `HeroSelect`, `PetSelect`. Dev/showcase: `VfxGallery`, `BattleHUD_Mockup`, `HUD_Obsidian_Showcase` |
| `Generated/` | Build-time generated animators/materials/terrain — don't hand-edit |
| `Prefabs/` `Materials/` `Models/` `Shaders/` `Audio/` `Art/` | Standard asset folders |
| `Data/` `Resources/` `Settings/` `Localization/` `StreamingAssets/` | Config + runtime-loaded assets |
| `UI Toolkit/` | UI Toolkit assets — NOTE: UXML does NOT work in builds; UI is code-built |
| `Dialogue/` | Dialogue content |
| `AddressableAssetsData/` | Addressables config |
| `Action/` | Action/animation clips |

## Third-party packs (notes in `docs/INSTALLED_PACKS_INDEX.md` + per-pack `docs/*_NOTES.md`)

Big art packs travel as zips and several are **gitignored** — on a fresh clone run
`tools/art/verify-runtime-art.ps1` (see `tools/art/REQUIRED_PACKS.md`) to confirm the tracked
runtime fallbacks are present. Master pack list: `docs/INSTALLED_PACKS_INDEX.md`.

| Folder | Pack |
|---|---|
| `polyperfect/` | Low Poly Ultimate Pack — **gitignored**; re-import then run `Defenders/Art/Fix Polyperfect URP Materials`. Use `_M` tier prefabs only. Catalog: `docs/polyperfect-asset-catalog.md` |
| `Quaternius/` | Quaternius models — **gitignored** (re-import on fresh clone). `docs/QUATERNIUS_NOTES.md` |
| `Blink/` | Obsidian UI kit — the source art for the code-built ElarionUiKit chrome (`BlinkChrome`). `docs/SME/BLINK_SME.md` |
| `Hovl Studio/` | Combat VFX (towers / sword-shield / spellcasting). `docs/HOVL_STUDIO_SME.md`, `docs/vfx/HovlStudio_Inventory.md` |
| `Mirza Beig/` | VFX pack (`docs/MIRZABEIG_VFX_NOTES.md`) |
| `Spells Pack/` | Spell VFX (`docs/SPELLS_PACK_NOTES.md`) |
| `Lana Studio/` | RPG VFX (`docs/LANA_RPG_VFX_NOTES.md`) |
| `Leohpaz/` `Supercyan/` | Audio (Leohpaz) + character (Supercyan) packs |
| `Black Dragon/` `Dragon/` | Dragon models |
| `Plugins/` `TextMesh Pro/` | Standard plugins |

KayKit assets are referenced via catalogs: `docs/kaykit-asset-catalog.md`.
`Medieval Village/` was removed; if a doc still references it, treat it as stale.

> Maintenance: update when folders are added/removed or conventions change.
