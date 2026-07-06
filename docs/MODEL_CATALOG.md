# MODEL_CATALOG — Runtime-Loadable Models (Resources)

**Defenders of the Realm — single index of every model the game loads at runtime.**

This is the doc that would have caught **BUG#22** (an Archer Tower pointing at a
lumber-pile prefab). Every row below is **verified from the actual file on disk**
on the date stamped, NOT from comments or memory (CLAUDE.md mandate). When a
mapping in code/data points at a name, check it here first: if the file isn't
listed, `Resources.Load` will return null and the system silently degrades
(tinted-capsule enemy / mesh-less structure).

- **Verified:** 2026-06-13 (branch `feat/tower-core-loop`)
- **Loader contract:** everything here is reachable by a **Resources-relative path**
  (e.g. `Resources.Load("Enemies/Troll")`, `VisualFactory.Skin(t, "Structures/Well", …)`).
  Anything NOT under an `Assets/Resources/**` folder (e.g. the gitignored
  `polyperfect` packs) is **NOT** runtime-loadable by path — see
  `docs/polyperfect-asset-catalog.md` for those (they must be mirrored into
  Resources first).
- **Disk ground-truth (reproducible):** `Defenders > Catalog > Regenerate Model
  Catalog` (batchmode `-executeMethod
  DeNelle.Editor.Catalog.ModelCatalogGenerator.Generate`) writes
  `docs/MODEL_CATALOG.generated.md` — the raw file-by-file inventory of every
  loadable model under Resources. **This** file (`MODEL_CATALOG.md`) is the
  hand-curated map (id → model + findings); diff it against the generated dump
  to catch any new catalog↔reality drift.

---

## 1. Resources/Enemies — runtime enemy models

Loaded by `EnemyFactory.ModelForEnemy(def)` → `VisualFactory.Skin(go, "Enemies/<Model>", …)`.
A miss here makes the enemy a **tinted capsule** (silent family-variety loss).
Rig is resolved by `EnemyAnimatorFactory.RigFor(model)`.

| Model file | Path | Rig family (approx) | Used by (EnemyFactory id → model) |
|---|---|---|---|
| `Skeleton_Minion.fbx`    | `Enemies/Skeleton_Minion`    | KayKit Generic → HumanoidEnemy | `hollow-walker` grunt |
| `Skeleton_Warrior.fbx`   | `Enemies/Skeleton_Warrior`   | **AccuRig** Humanoid → SkeletonHumanoid | `hollow-warrior`, ATB default grunt (`skeleton` def) |
| `Skeleton_Rogue.fbx`     | `Enemies/Skeleton_Rogue`     | **AccuRig Ranger** Humanoid → SkeletonHumanoid | `hollow-rogue`, `feral-wolf` stand-in |
| `Skeleton_Healer.fbx`    | `Enemies/Skeleton_Healer`    | **AccuRig** Humanoid → SkeletonHumanoid | `hollow-acolyte` healer |
| `Skeleton_Mage.fbx`      | `Enemies/Skeleton_Mage`      | **AccuRig** Humanoid → SkeletonHumanoid | `hollow-apprentice` (ATB), caster silhouette |
| `Skeleton_Golem.fbx`     | `Enemies/Skeleton_Golem`     | KayKit Generic → LargeEnemy | size-default brute (Height ≥ 2.3); `caveman` stand-in |
| `Necromancer.fbx`        | `Enemies/Necromancer`        | Boss rig | `necromancer` elite |
| `Orc_Berserker.fbx`      | `Enemies/Orc_Berserker`      | OrcWarband (Tripo, +X-fwd → -90 yaw) | `orc-raider`, `orc-berserker`, family `orc` (non-caster) |
| `Orc_Shaman.fbx`         | `Enemies/Orc_Shaman`         | OrcWarband | `orc-shaman`, family `orc` caster |
| `Orc_Necromancer.fbx`    | `Enemies/Orc_Necromancer`    | OrcWarband | `orc-necromancer`, `orc-warlord` (outpost raid boss) |
| `Troll.fbx`              | `Enemies/Troll`              | KayKit HumanoidMedium (see ⚠ note) | `troll`, `caveman`, family `troll` |
| `OgreMage.fbx`           | `Enemies/OgreMage`           | (verify) | `ogre`, `ogre-mage`, family `ogre` |
| `Demon.fbx`              | `Enemies/Demon`              | (verify) | `demon`, `tiefling-cultist`, family `demon`/`cult` |
| `Dragon.fbx`             | `Enemies/Dragon`             | (verify) | `boss-dragon`, `dragon`, family `dragon` |
| `Boss_Dragon.prefab`    | `Enemies/Boss_Dragon`        | composed prefab (DragonBoss) | apex flyby/boss — `WaveManager`, `DragonCinematicFlyby`, DevPanel "Spawn Syndrath" |

⚠ `Troll` is mapped to the OrcWarband-rotation **only if** `RigFor("Troll")` returns
`OrcWarband`; the EnemyFactory comment notes RigFor currently falls it to KayKit
(HumanoidMedium) → **0 rotation**. Playtest a spawned Troll for facing.

**Materials:** `Resources/Enemies/Materials/` (shared, not loaded by path directly).

---

## 2. Resources/Structures — build-mode structure visuals

Loaded by `StructureFactory.Create` from the **structures catalog**
(`Resources/Data/Canonical/structures-catalog.json`) via the row's
`visualPrefabPath` + `upgradeVisualPath[]` → `VisualFactory.Skin(root, "<path>", …)`.
A miss = a structure with **no mesh** (LogWarning per CLAUDE.md §4).

These are polyperfect `_M/Medieval_M` (+ a few `Fantasy_M`/`Empire_M`) prefabs
**mirrored into Resources** so they are loadable by path (the pack itself is
gitignored and outside Resources).

| Prefab file | Path | Catalog id(s) that point here |
|---|---|---|
| `Tower_Castle_Round.prefab`     | `Structures/Tower_Castle_Round`     | `tower_ground_archer` **base + L2** (the BUG#22-corrected archer art) |
| `Tower_Medieval_Big.prefab`     | `Structures/Tower_Medieval_Big`     | `tower_ground_archer` L3; `tower_wall_wizard`; `arcane-tower`; `tower_arcane_spire` |
| `Tower_Medieval_Wood.prefab`    | `Structures/Tower_Medieval_Wood`    | **UNREFERENCED by catalog** — the old BUG#22 archer art (lumber-pile look); kept in Resources, no live row |
| `Ballista.prefab`               | `Structures/Ballista`               | `tower_siege_tower` |
| `Catapult.prefab`               | `Structures/Catapult`               | `tower_catapult` |
| `Wall_Medieval_Wood.prefab`     | `Structures/Wall_Medieval_Wood`     | `wall_wood` |
| `Wall_Medieval_Stone.prefab`    | `Structures/Wall_Medieval_Stone`    | `wall_stone` |
| `Gate_Medieval_Medium.prefab`   | `Structures/Gate_Medieval_Medium`   | `gate_stone` |
| `Well.prefab`                   | `Structures/Well`                   | `mine_crystal` |
| `Torche_Wall.prefab`            | `Structures/Torche_Wall`            | `deco_torch` |
| `Stables_Medieval.prefab`       | `Structures/Stables_Medieval`       | `pet-house` (Echo Hollow) |
| `House_Medieval_Medium.prefab`  | `Structures/House_Medieval_Medium`  | `workshop` (Forge), `forge` (Armorer) |
| `House_Medieval_Large.prefab`   | `Structures/House_Medieval_Large`   | `market` |
| `House_Medieval_Small.prefab`   | `Structures/House_Medieval_Small`   | `jeweler` |
| `Windmill_Medieval.prefab`      | `Structures/Windmill_Medieval`      | `mill` |
| `Watermill_Medieval.prefab`     | `Structures/Watermill_Medieval`     | `lumbermill` (Sawmill) |
| `Marketplace_Stand_Simple.prefab` | `Structures/Marketplace_Stand_Simple` | market front-prop dressing (no catalog row) |
| `Altar.prefab`                  | `Structures/Altar`                  | Heart-of-Elarion dressing (no catalog row) |
| `Anvil.prefab`                  | `Structures/Anvil`                  | forge/workshop prop dressing (no catalog row) |
| `Pillar_Ionic.prefab`           | `Structures/Pillar_Ionic`           | Heart/plaza dressing (no catalog row) |

**Catalog ↔ disk reconciliation (2026-06-13):** every `visualPrefabPath` and
`upgradeVisualPath` in `structures-catalog.json` resolves to a file above —
**no broken structure references.** The catalog's `_bug22` note references
`Structures/Tower_Tribal_Tier1..Tier3` as the *future owner-preferred* art, but
those are **NOT present in Resources** (they live only in gitignored
`polyperfect/_T/Prefabs_T/Tribal_T/`). Until mirrored in, the archer ladder
correctly uses `Tower_Castle_Round` / `Tower_Medieval_Big`. See FINDINGS.

---

## 3. Resources/Heroes — hero / companion models

Loaded by `Resources.Load<GameObject>("Heroes/" + slug)` (`HeroBodySwapper`,
`AtbCombatantSwapper`) and `VisualFactory.Skin(go, "Heroes/" + slug, …)`
(`StoryCompanionInjector`). Slugs: `Knight`, `Mage`, `Ranger`, `Cleric`.
These are **`.fbx` files** (Unity loads an FBX as a GameObject) — there are **no
`.prefab`** files for the heroes themselves.

| Model file | Path (slug) | Hero class / role |
|---|---|---|
| `Knight.fbx`  | `Heroes/Knight`  | Grom — Knight (tank) |
| `Mage.fbx`    | `Heroes/Mage`    | Thrain — Wizard/Mage (also used to render the Cleric portrait) |
| `Ranger.fbx`  | `Heroes/Ranger`  | Sylas — Ranger (archer; bow = `Heroes/Props/Bow`) |
| `Cleric.fbx`  | `Heroes/Cleric`  | Elara — Healer/Cleric |

**Animator controllers** (built by `HeroAnimatorFactory`): `Heroes/<slug>.controller`
for Knight/Mage/Ranger/Cleric, loaded via `Resources.Load<RuntimeAnimatorController>("Heroes/"+slug)`.

**Prop:** `Heroes/Props/Bow.prefab` (`Heroes/Props/Bow`) — Ranger bow attachment.

**Textures** (loaded by path for swaps, `Resources/Heroes/Textures/`): e.g.
`ranger_basecolor`, `Cleric_basecolor`, `remesh_12_combined_Bake_Diffuse` (Knight),
`tripo_mat_9b343081_Pbr_Diffuse` (Mage). See `HeroBodySwapper.cs` for the slug→texture map.

---

## 4. Resources/Towers — TowerData ScriptableObjects (`.asset`)

These are **data assets** (`DeNelle.Core.Data.TowerData`), not models. Each
`upgrades[].visualPrefab` is a **direct GUID prefab ref** into the gitignored
polyperfect pack (NOT a Resources path). Listed here because a wrong GUID is the
same class of bug as BUG#22. Resolved 2026-06-13:

| Tower asset | Tier visualPrefab → resolved polyperfect prefab |
|---|---|
| `ArcherTower.asset` | L1 `Tower_Medieval_Wood` · L2 `Tower_Castle_Square` · L3 `Tower_Castle_Round` |
| `FrostTower.asset`  | L1 `Tower_Medieval_Wood` · L2 `Tower_Castle_Square` · L3 `Tower_Castle_Round` |
| `MageTower.asset`   | L1/L2 `Tower_Medieval_Big` · L3 `Tower_Castle_Round` |
| `DevTower.asset`    | L1 `Tower_Pirate_Wood` · L2/L3 `Tower_Pirate_Stone` |

> NOTE: these `.asset` GUID refs point at prefabs **inside the gitignored
> polyperfect pack** — they resolve in-editor (pack imported) but the runtime
> build-mode loop uses the **JSON structures catalog** (§2) by Resources path,
> not these `.asset` files. Confirm which path each tower-spawning system uses
> before treating a TowerData ref as authoritative.

---

## FINDINGS — catalog ↔ reality mismatches (2026-06-13)

1. **BUG#22 (Archer Tower → lumber pile) — root cause confirmed, currently mitigated.**
   The archer-tower row once had `visualPrefabPath: Structures/Tower_Medieval_Wood`,
   which renders as a tall wooden lumber-pile silhouette. It now points at
   `Structures/Tower_Castle_Round` (base + L2) and `Structures/Tower_Medieval_Big`
   (L3) — real towers. The owner-preferred final art is the polyperfect Tribal tower
   ladder (`Tower_Tribal_Tier1..Tier4`), which is **not yet in Resources**.
2. **`Tower_Medieval_Wood.prefab` is now orphaned** in `Resources/Structures` — no
   catalog row references it. Safe to leave, but it's the old BUG#22 art; don't
   re-point a tower at it.
3. **`Skeleton_Warrior.fbx` is orphaned** in `Resources/Enemies` — `EnemyFactory`
   has no live case that maps to it (the 2026-06-13 Wildlands fix moved `orc-raider`
   off it to `Orc_Berserker`). Kept on disk; no id resolves to it.
4. **Tribal tower ladder gap:** to honor the owner's Tribal towers, mirror
   `polyperfect/_T/Prefabs_T/Tribal_T/Tower_Tribal_Tier1..Tier3` into
   `Assets/Resources/Structures/` and update the `tower_ground_archer`
   `visualPrefabPath`/`upgradeVisualPath` (Tier4 exceeds the maxLevel=3 cap).
5. **No broken Resources-path references found** in `structures-catalog.json` or
   `EnemyFactory.ModelForEnemy` — every model name resolves to a real file as of
   this verification.
