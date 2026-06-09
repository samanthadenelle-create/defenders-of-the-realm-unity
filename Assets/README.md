# Assets/ — Top-Level Map

**Professional structure for Defenders of the Realm (Unity 6 LTS mobile TD + dungeon + Solana).** See `../CORE_ARCHITECTURE_PLAN.md` for the canonical recommended folders, module boundaries, and feature mapping.

What lives in each folder. Code modules have their own READMEs under `_Modules/`.

## First-party

| Folder | Contents |
|---|---|
| `_Modules/` | **All first-party code.** See `_Modules/README.md` for the module map |
| `Editor/` | `DeNelle.Editor` asmdef. Scene builders — `VillageSceneBuilder` (partial, split across ~10 files; **serialization bottleneck, one agent at a time**), `BattleSceneBuilder`, `DungeonSceneBuilder`, `ExteriorTerrainBuilder`, `FolksGranaryBuilder`, `IntroFlowSceneBuilder`, `CastleHubBuilder` (Defenders > Scenes > Build CastleHub_MainKeep — run after blank scene; Quaternius + polyperfect _M for Central Castle Hub with 8 storefront structures, 2-level keep/home quarters, upper battlements for defenses, OuterWorld gate marker) — plus animator setups/factories, build scripts (`AndroidBuild`, `DesktopBuild`), `CompileGate`, material fixers (`KayKitMaterials`), portrait generators |
| `_Village2/` | `Village2Generator` + `TorchFlicker` — the modular village rebuild (WO-278/279/280) |
| `Scenes/` | `Village.unity` (NEVER hand-edit — rebuild via `Defenders > Week 3 > Build Village Scene`), `Village2`, `Village2Test`, `OuterWorld`, `ATBBattle`, `PatriciaLightMode`, `Title`, `HeroSelect`, `PetSelect`, `Dungeon_HealersCottage`, `Dungeon_FolksGranary` |
| `Generated/` | Build-time generated animators/materials/terrain — don't hand-edit |
| `Prefabs/` `Materials/` `Models/` `Shaders/` `Audio/` `Art/` | Standard asset folders |
| `Data/` `Resources/` `Settings/` `Localization/` `StreamingAssets/` | Config + runtime-loaded assets |
| `UI Toolkit/` | UI Toolkit assets — NOTE: UXML does NOT work in builds; UI is code-built |
| `Dialogue/` | Dialogue content |
| `AddressableAssetsData/` | Addressables config |
| `Action/` | Action/animation clips |

## Third-party packs (notes in `docs/INSTALLED_PACKS_INDEX.md` + per-pack `docs/*_NOTES.md`)

| Folder | Pack |
|---|---|
| `polyperfect/` | Low Poly Ultimate Pack — **gitignored**; re-import then run `Defenders/Art/Fix Polyperfect URP Materials`. Use `_M` tier prefabs only. Catalog: `docs/polyperfect-asset-catalog.md` |
| `Medieval Village/` | Medieval village assets |
| `Quaternius/` | Quaternius models (`docs/QUATERNIUS_NOTES.md`) |
| `Mirza Beig/` | VFX pack (`docs/MIRZABEIG_VFX_NOTES.md`) |
| `Spells Pack/` | Spell VFX (`docs/SPELLS_PACK_NOTES.md`) |
| `Lana Studio/` | RPG VFX (`docs/LANA_RPG_VFX_NOTES.md`) |
| `Black Dragon/` | Dragon model |
| `Plugins/` `TextMesh Pro/` | Standard plugins |

KayKit assets are referenced via catalogs: `docs/kaykit-asset-catalog.md`.

> Maintenance: update when folders are added/removed or conventions change.
