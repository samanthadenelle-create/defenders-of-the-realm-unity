# WORK ORDER 1257 — Enemy_Models (and Controllers/Textures) still one PackTogether Local blob: match Enemy_Art per-family packing so Lens pulls one family

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-28 (retargeted from incorrectly-minted PROD-018; COS process fix). CLI main line. Banner bumped 1257 → 1258 in the same edit. Number is 1257 because WO-1256 was already the launch-day Welcome Pack (FIRSTWATCH) on disk. **PROD-018 slot restored unused** — this is feature work matching Enemy_Art, not an emergency critical live defect. Owner rule: NEW FUNCTIONALITY → WO; PRO/PROD → emergency critical fixes only.
**Priority:** HIGH — a hollow wave still pays for every family; the packing law already shipped for Enemy_Art and was never applied to the groups that actually hold the meshes.
**Silo:** Addressables / content delivery. **Lane:** `Assets/Editor/ContentPackingSetup.cs` + `AddressableAssetsData` Enemy_Models / Enemy_Controllers / Enemy_Textures + `EnemyContentWarmer` / `UpcomingWaveWarmPlanner`. No scenes.
**Provenance:** Chief of Staff board capture 2026-08-28 from the packing audit. Owner packing ruling 2026-08-20 still binds: *"this means I want this broken down to each family of enemy"* / *"one family not every family"*. Goal: **Lens pulls smallest — per enemy family only.**
**Cross-refs:** **PROD-009** (Enemy_Art per-family packing — CLOSED/superseded as a first-run UX, but the packing mechanism SHIPPED in `8e072153c` anyway). **PROD-010** (opt-in whole-set download — does NOT kill this ticket; see §0). **PROD-011** (retry/timeout — Enemy_Art has `m_RetryCount: 2`; these three groups are still `0`). Do not re-open PROD-009's first-run streaming argument.

---

## 0. This is NOT a re-open of PROD-009

PROD-010's 2026-08-19 ruling (*"PROD 10 kills 10 and 09"*) retired **first-run on-demand streaming** as the player-facing download UX. That ruling stands.

What actually landed anyway, recorded on PROD-009 itself: **per-family Enemy_Art packing** (`ContentPackingSetup`, pinned by `ContentPackingRegression`) and **roster lookahead** (`UpcomingWaveWarmPlanner`). Those exist in the tree. This ticket is the **remainder those two commits did not cover**:

- `ContentPackingSetup` / `ContentPackingRegression` name **only** `Enemy_Art` and `Structure_Art`.
- The groups that hold the **meshes / controllers / textures** were left on the old packing.

PROD-010's opt-in whole-set pull still works if the player chooses it. Family packing is what makes a session that never opts in — and a Lens/warm that only needs hollows — **not** pay for trolls, orcs, and the dragon. PROD-010 §5 already listed "useful subset instead of all-or-nothing" as **NOT BUILT**. This is that subset, on the groups that still blob.

---

## 1. The defect, verified at source 2026-08-28

### 1a. Three groups are still PackTogether + Local

| Group | Schema | `m_BundleMode` | Build/Load path | Retry |
|---|---|---|---|---|
| `Enemy_Art` (already shipped) | `Enemy_Art_BundledAssetGroupSchema.asset` | **2 = PackTogetherByLabel** | Remote.BuildPath / Remote.LoadPath (R2) | **2** |
| `Enemy_Models` | `Enemy_Models_BundledAssetGroupSchema.asset` | **0 = PackTogether** | Local.BuildPath / Local.LoadPath | **0** |
| `Enemy_Controllers` | `Enemy_Controllers_BundledAssetGroupSchema.asset` | **0 = PackTogether** | Local.BuildPath / Local.LoadPath | **0** |
| `Enemy_Textures` | `Enemy_Textures_BundledAssetGroupSchema.asset` | **0 = PackTogether** | Local.BuildPath / Local.LoadPath | **0** |

Path ids, from `AddressableAssetSettings.asset` Default profile:

- Local.BuildPath `0cf521a4ee5de1f4eb470a8e10a12000` = `[UnityEngine.AddressableAssets.Addressables.BuildPath]`
- Local.LoadPath `38c0a648f7e4a184181a93361c8e0132` = `{UnityEngine.AddressableAssets.Addressables.RuntimePath}`
- Remote.BuildPath `ad0e68328bd7fd54ea79f0a9ab1dd9b1` = `ServerData/[BuildTarget]`
- Remote.LoadPath `cf151d4962873af43b9302d323a9d707` = `https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/[BuildTarget]`

So Models / Controllers / Textures bake into the **player** and load from **StreamingAssets**, as **one bundle per group**. They are not on R2. A PackTogether Models group is the ~65 MB blob the audit named.

### 1b. Labels exist and are unused

`AddressableAssetSettings.asset` `m_LabelTable` already carries `enemyfam-orc`, `enemyfam-hollow`, `enemyfam-shared`, `enemyfam-troll`, `enemyfam-bosses`.

`Enemy_Models.asset`: **24** addresses, **22** carry one `enemyfam-*` label, **2 unlabeled**:

- `Enemies/Hollow_Walker` — `m_SerializedLabels: []`
- `Enemies/Cellar_Hollow` — `m_SerializedLabels: []`

Controllers: 10/10 labelled. Textures: 1/1 labelled (`Enemies/skeleton_texture_A`).

**PackTogether ignores labels.** The labels on Models are dead until BundleMode is 2.

### 1c. Runtime warm uses FamilyOf(slug), not enemies.json `family`

`EnemyContentWarmer.FamilyOf` splits the address/slug on the **first underscore**:

- `Enemies/Hollow_Walker` → `"Hollow"`
- `Enemies/Cellar_Hollow` → `"Cellar"`
- `Enemies/Skeleton_Golem` → `"Skeleton"`

`enemies.json` `family` for those rows is **`hollow`**. Labels are `enemyfam-hollow`.

`WarmFamily` then downloads by scanning discovered addresses whose `FamilyOf` matches that token (`DownloadDependenciesAsync(keys, Union)`), **not** by label `enemyfam-{family}`. `UpcomingWaveWarmPlanner.AppendFamilyOf` does the same: catalog `modelKey` → `EnemyContentWarmer.FamilyOf(model)`. Its own comment (`:144-147`) treats `"Hollow"` as "the real family" — that is the slug heuristic, not the data family. A hollow wave therefore cannot ask for `enemyfam-hollow`.

### 1d. Duplicate addresses (watch-out, do not "fix" by deleting)

These three addresses are registered in **both** `Enemy_Art` and `Enemy_Models`:

- `Enemies/Orc_Berserker`
- `Enemies/Orc_Necromancer`
- `Enemies/Orc_Shaman`

Do **not** change, rename, or delete addresses to tidy this. Addresses are the loader contract (ContentPackingSetup header; WizardTower_1 / WO-1124 class). Record the collision in the RESULT. After re-pack, confirm which bundle(s) a resolve of those three actually pulls.

---

## 2. THE APPROACH — match Enemy_Art. One change.

Copy the packing law that already exists for `Enemy_Art`. Do not invent a second scheme.

### Part 1 — packing (editor tool + schemas)

Extend `ContentPackingSetup` so it also owns:

- `Enemy_Models`
- `Enemy_Controllers`
- `Enemy_Textures`

On those three groups, the tool must:

1. Set `BundleMode = PackTogetherByLabel` (enum **2**). **Do NOT PackSeparately** — that split a body from its maps on Enemy_Art and was rejected in the ContentPackingSetup header.
2. Point BuildPath / LoadPath at **Remote.BuildPath / Remote.LoadPath** (the same profile ids Enemy_Art uses). These groups go to R2, not into the APK.
3. Derive **exactly one** `enemyfam-{family}` label per entry from `enemies.json`'s `family` field (same `FamilyMap()` / address classifier already in this file). Unclassifiable entries → `enemyfam-shared`. Strip stale family labels before applying (regression already fails multi-label sets).
4. Label the two hollows: `Enemies/Hollow_Walker` and `Enemies/Cellar_Hollow` → `enemyfam-hollow`. They have no row-driven label today because the tool never walks this group.
5. Match Enemy_Art's retry/timeout (currently `m_RetryCount: 2` on Art, `0` on these three). A Remote group with retry 0 is the PROD-011 miss: one dropped request is a permanent miss for the session.

`ContentPackingRegression` must pin **all three groups** the way it pins `Enemy_Art` today: BundleMode, exactly-one family label, more-than-one bucket, PredicateSelfTest still able to reject PackTogether. Extend `PrefixOf` so the bundle report groups `enemy_models` / `enemy_controllers` / `enemy_textures` instead of collapsing them.

Use the existing capture/dump: `DumpAddressesBefore/After` must prove **addresses unchanged**.

### Part 2 — runtime warm from the DATA family

Change `EnemyContentWarmer.WarmFamily` so a family token means the **label** `enemyfam-{family}` (lowercase, the enemies.json value), via `DownloadDependenciesAsync("enemyfam-{family}")` — not a `FamilyOf` scan of addresses.

`UpcomingWaveWarmPlanner.AppendFamilyOf` must take the family from the catalog/`enemies.json` **`family` field** (the same field `ContentPackingSetup.FamilyMap` already reads), not `EnemyContentWarmer.FamilyOf(model)`. Update the `:144-147` comment; it is now wrong.

Keep `FamilyOf` only if something still needs a slug heuristic for diagnostics — do not let it drive which bundle is fetched.

**No `WaitForCompletion` anywhere on this path.** `EnemyLoadBoundedRegression` already fails the build if one is added. `EnemyWarmOrderRegression` case 4 source-scans the planner. Do not weaken either.

### Part 3 — rebuild + upload R2

Re-packing rehashes bundles. Already-installed players re-download once (content-hashed names). Unavoidable and one-time — same already-installed-APK hazard as PROD-010 §2.

1. Content-build for the **ship target** (WO-1124: do not build content for the wrong target).
2. Push `ServerData/<BuildTarget>` with `tools/r2_sync.py` the way the ship chain already does (WO-1130). Do not flatten to bucket root (PROD-011).
3. Confirm built names, not settings:

   `enemy_models_assets_enemyfam-hollow_*.bundle`

   Absence of `enemy_models_assets_all_*.bundle` is the packing proof. Same pattern for controllers/textures if they split (controllers may mostly land in `enemyfam-shared` — measure, do not assume).

---

## 3. Files to edit

| File | Why |
|---|---|
| `Assets/Editor/ContentPackingSetup.cs` | Own Models / Controllers / Textures; Remote paths; labels from `enemies.json` |
| `Assets/Editor/Regression/ContentPackingRegression.cs` | Pin those groups; fail PackTogether / unlabeled / Local path |
| `Assets/AddressableAssetsData/AssetGroups/Schemas/Enemy_Models_BundledAssetGroupSchema.asset` | mode 2 + Remote + retry |
| `Assets/AddressableAssetsData/AssetGroups/Schemas/Enemy_Controllers_BundledAssetGroupSchema.asset` | same |
| `Assets/AddressableAssetsData/AssetGroups/Schemas/Enemy_Textures_BundledAssetGroupSchema.asset` | same |
| `Assets/AddressableAssetsData/AssetGroups/Enemy_Models.asset` | label the two hollows (tool should do this; do not hand-author as the standing process) |
| `Assets/_Modules/Core/Addressables/EnemyContentWarmer.cs` | WarmFamily by `enemyfam-{family}` label |
| `Assets/_Modules/Village/Waves/UpcomingWaveWarmPlanner.cs` | family from catalog data, not FamilyOf(slug) |
| `Assets/Editor/Regression/EnemyWarmOrderRegression.cs` | only if the planner contract changes enough that case 3/4 assertions need the data family |
| `docs/addressables-implementation-plan.md` | record that Models/Controllers/Textures now share the Enemy_Art packing law |

Generated Addressables catalog/bundle outputs under `ServerData/` are the **build product**, not a hand edit.

---

## 4. Acceptance criteria

1. `Enemy_Models`, `Enemy_Controllers`, and `Enemy_Textures` are `PackTogetherByLabel` (mode 2) and use Remote.BuildPath / Remote.LoadPath. Proven in the schema assets **and** by the content-build file registry, not by inspection of the Groups window.
2. Every entry in those three groups carries **exactly one** `enemyfam-*` label, derived from `enemies.json` `family` (or `enemyfam-shared`). `Enemies/Hollow_Walker` and `Enemies/Cellar_Hollow` are `enemyfam-hollow`.
3. Addresses are unchanged: `addresses-before.txt` / `addresses-after.txt` diff is empty of address moves. No PackSeparately on these groups.
4. Built bundle names include `enemy_models_assets_enemyfam-hollow_*.bundle`. There is **no** `enemy_models_assets_all_*.bundle`.
5. **A hollow wave only pulls `enemyfam-hollow` (+ `enemyfam-shared` if the layout report says those assets are actually shared).** Proved by a measured download / Lens / bundle list, not by reading labels. Troll / orc / bosses bundles stay unfetched.
6. `WarmFamily` / the planner key off the **data family** (`hollow`, not `"Hollow"` / `"Cellar"` / `"Skeleton"`).
7. No `WaitForCompletion` on the enemy spawn or warm path. `CONTENT_PACKING_OK` covers the three groups. `EnemyLoadBoundedRegression` still green.
8. New bundles are uploaded to R2 for the ship target. PROD-011's catalog-vs-bucket gate still passes.

## 5. What NOT to touch

- **Do NOT PackSeparately** on Models / Controllers / Textures.
- **Do not change addresses.** Do not merge, delete, or rename the duplicate `Enemy_Art` ∩ `Enemy_Models` orc addresses.
- **No `WaitForCompletion`.** No bounded sync wait. There isn't one in Addressables 2.9.1.
- `Enemy_Art` packing (already mode 2 / Remote). `Structure_Art` packing (PackSeparately — different ruling).
- Gear, dungeon, hero, localization groups.
- PROD-010's opt-in whole-set offline pull — do not rip it out and do not make family packing a second first-run UX.
- Default Local Group layout.
- Scenes.

## 6. Result expected

`WorkOrders/WORK_ORDER_1257_enemy_models_family_pack.RESULT.md` with: before/after address dump (empty address diff), bundle registry showing per-family `enemy_models_assets_enemyfam-*` names and sizes, duplicate-dependency / layout-report note for `enemyfam-shared`, R2 upload proof, and the hollow-only pull measurement.
