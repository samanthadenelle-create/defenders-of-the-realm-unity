# Art Pipeline & Asset Audit — V1

**Date:** 2026-06-28
**Author:** asset-audit agent (read-only survey)
**Scope:** What art exists (KayKit, Tripo, Blink, polyperfect, Quaternius, Supercyan, VFX
packs — most gitignored) vs. what V1 needs; the import/seating/magenta/bake pipeline; and
optimization (decimate/compress/atlas). Ends with the **asset-gap list** (the deliverable).

> **This does not re-derive the inventory.** The factual per-pack map already lives in
> `docs/asset-inventory/01–05` (surveyed 2026-06-24) and `docs/polyperfect-asset-catalog.md`.
> This doc *assesses* that inventory against the V1 bar and the pipeline tooling. Read the
> inventory docs for raw counts; read this for gaps + pipeline health.

---

## 0. Bottom line

- **Characters for V1 are COVERED and shipping.** The single-Knight hero + Orc-family
  enemies (combat pivot north-star) are live Tripo FBX in `Resources/Heroes` +
  `Resources/Enemies`, loaded at runtime. No character art is blocking V1.
- **The pipeline is mature for the systems we use** (KayKit auto-import, Tripo material fix,
  Offset Forge seating, a global MagentaGuard + ~8 targeted fixers, gitignore-safe UI mirroring).
- **The real V1 gaps are SMALL, specific, and mostly "wire/mirror what we own," not "make new art":**
  1. **Crown tier sprites are ABSENT** (`Resources/RpgUi/crown/` missing) — Victory rating
     falls back to TMP glyphs. Blocks task #41.
  2. **Store pack icons** — `packs.json` has no icon field and no pack-icon art exists.
  3. **Dungeon kit** owned (KayKit Dungeon Remastered) but **not mirrored to Resources** for
     the dungeon generator (task #46) — same "owned-but-not-loadable" pattern as Supercyan.
  4. **No Mesh Baker / combine tool exists** — the planned 6-submesh→1 per-character bake
     (memory `tripo-roster-knight-orcs-first`) is **unbuilt**. This is an optimization gap,
     not a blocker, but it is the single biggest perf lever for the Tripo characters.
  5. **Arena spell-cast VFX = blocky purple cubes** (task #44) — procedural fallback firing
     because ~910 owned Spells-Pack/Mirza prefabs are unwired into `VFXCatalog`.

---

## 1. The V1 bar — what art V1 actually needs

Derived from the combat pivot (`docs/COMBAT_PIVOT_NORTHSTAR.md`, memories
`combat-pivot-single-hero-northstar`, `overworld-encounter-isolated-battle`,
`tripo-roster-knight-orcs-first`):

| Need | Definition of "done for V1" |
|---|---|
| **Hero** | ONE Tripo Knight, self-rigged Humanoid, static armor, weapon/shield swap via seating offsets |
| **Enemies** | Tripo Orc family (Warrior / Tank / Mage) in the isolated BattleArena; Dragon boss |
| **Dungeon kit** | Composable dungeon from chunks (rooms→connectors→graph) — KayKit Dungeon art |
| **UI** | Obsidian/Blink black+gold panel chrome, shared frame template, screens drop content |
| **Crowns** | Victory tier rating: crown_tier1/2/3 + perfect capstone (font-independent sprite) |
| **Pack icons** | Store/monetization pack thumbnails for `packs.json` entries |
| **VFX** | Spell projectiles / impacts / casts that are not procedural placeholder cubes |
| **World/structures** | Castle hub + OuterWorld + Village2 raid target (already built) |

---

## 2. What we actually have (rollup)

Full detail: `docs/asset-inventory/01–05`. Condensed against the V1 bar:

### Characters / rigs
| Source | Rigged? | gitignored | Loadable now? | Role for V1 |
|---|---|---|---|---|
| **Tripo Heroes** (`Resources/Heroes`: Knight/Mage/Ranger/Cleric.fbx) | YES (Humanoid) | NO | **YES** (`HeroBodySwapper`) | **SHIPPED hero** — Knight is canon |
| **Tripo Enemies** (`Resources/Enemies`: Orc Warrior/Tank/Mage + Skeletons/Troll/Ogre/Demon/Dragon) | YES | NO | **YES** (`EnemyAnimatorFactory`) | **SHIPPED enemies** — Orc family is V1 |
| `Models/People` (4 Tripo townsfolk + 3-LOD FighterClass) | YES | NO (LFS) | path-ref in `enemies.json` | NPCs |
| **KayKit** Adventurers + Mystery Monthly 4/5 (~42 humanoids) | YES (shared rig) | YES | NO (not mirrored) | UNUSED shared-rig library |
| **Supercyan** Fantasy (8 humanoids: Knight/Archer/Mage/Wizard/Barbarian/Orc/Skeleton/Demon + ~51 anims) | YES (shared rig) | YES | NO (not mirrored) | UNUSED — cleanest shared-rig option |
| **Blink** (~292 char prefabs, modular armor, rigged Orc/OrcBoss) | YES | YES (~13 GB) | NO | **JUNKED** hero-armor source; end-of-polish PURGE |
| polyperfect People_M / Animals_M | NO rig wired | YES | mirror-only | static display meshes |

**Read:** V1 character needs are met by the Tripo set already in Resources. Three *additional*
shared-rig libraries sit unused — that is an art-direction option, **not** a V1 gap.

### Animations
- **`Assets/Action/`** — 401 Mixamo Humanoid clip FBX (Knight 99, Wizard 15, Ranger 13,
  Shared 15, Enemies 20), **tracked**. The retarget source feeding hero + orc controllers.
  Canon method: `docs/ANIMATION_PIPELINE.md` (Shared/ base + per-type folder, one retarget).
- Two more shared retarget libraries owned but unused: KayKit Character Animations 1.1,
  Supercyan Fantasy anims.

### Weapons / props
- KayKit Fantasy Weapons (~48: sword A–G, axes, daggers, bows, staves, spears, shields A–D)
  + Adventurers weapon subset (~58). Mesh names (`sword_A`, `shield_A`) match the equip /
  Offset-Forge id convention. Tripo weapon props ship in `Resources/Heroes/Props`.

### VFX / Audio
- **~1,030 VFX prefabs owned, ~38 wired.** Spells Pack (466: clean element × effect matrix)
  + Mirza Beig (564: storm/fire/shock/nova/portal) + Lana Studio (128, *tracked*).
  `VFXCatalog.asset` maps ~50 VFXTypes → 38 GUIDs via 9 project wrapper prefabs in
  `Resources/VFX/Projectiles/`. Everything unmapped falls to **procedural `AbilityVfxKit`**
  (the source of the "blocky purple cube" placeholder, task #44).
- **Audio:** 18 music mp3 + 1 mixer (tracked). **SFX is fully procedural** (`ProceduralSfx.cs`)
  — no SFX clip files. Music covers battle/world/raid/stingers.

### UI art
- **RpgUi** mirrored to `Resources/RpgUi/` (badge, bars, button, decoration, frame, icons,
  panel, potion, silhouette, slot) via `RpgUiImporter`. **Blink** Obsidian frames mirrored
  via `BlinkUiImporter`. Item icons: 484 in `Resources/ItemIcons` (850 are leftover `blink_*`
  armor icons — purge candidates with the junked Blink armor system).

### Environment / structures
- polyperfect `_M` (3,080 prefabs), Quaternius MegaKit (~304 building modules), KayKit
  Medieval Hexagon + Forest Nature, Tripo owner structures (`Art/TripoStructures`). All wired
  through `StructureFactory` / builders / `RotationCorrectionRegistry`. Not a V1 gap.

---

## 3. Pipeline assessment

### 3.1 Import
- **KayKit auto-import:** `Assets/Editor/AssetImportPostprocessor.cs` — an `AssetPostprocessor`
  scoped to `Assets/Models/KayKit/` applies spec import settings on every (re)import: Optimize
  Mesh ON, Read/Write OFF, Mesh Compression Medium, Generic anim, lightmap UVs for static,
  texture max 1024 (256 props), ASTC 6×6, sRGB on albedo / off on normal-mask. Also fixes the
  KayKit "no .mat → white/magenta" problem by assigning a shared URP/Lit atlas material on
  `OnAssignMaterialModel`. **Mature — covers the KayKit warehouse correctly.**
- **Tripo import:** Heroes/Enemies carry `.tripo-extracted` markers; `TripoMaterialFixer.cs`
  + `TripoExtract` logic repair Tripo PBR materials into URP. Per-model, runs at load.
- **GAP:** the auto-import scope is **KayKit-only**. Tripo / Supercyan / Blink models rely on
  per-asset settings + runtime fixers, not an import-time postprocessor. Fine today (small set),
  but any *new* mirrored character pack needs its own import discipline.

### 3.2 Seating / offset (the "AI cannot resolve by eye" part)
- **Offset Forge** (`Assets/OffsetForge/`, WO-490) — standalone EditorWindow: load any model,
  dial attachment offset (rot/pos/scale) by eye, SAVE to `offsets.json` (flat `{id,rot,pos,scale}`
  table keyed by mesh name, e.g. `sword_A`, `shield_A`). **Asset Store product #1.**
- **Runtime apply:** `AttachmentOffsetRegistry.cs` loads the *same* JSON (no forked format) →
  `TryGetOffset(key)` → `EquipmentController` composes onto the grip root the instant a prop
  parents to the hand bone. Kills euler-guessing. `RigAttachmentRegistry` + `rig-profiles.json`
  hold per-rig bone maps.
- **In-game seating editor** (`SeatingEditorOverlay.cs`, WO-577) — owner can adjust offsets at
  play time. **Mature.** Open follow-ups (tasks #45): expose the orient/seating tool on the
  weapon + armor (Gear) screens.

### 3.3 Magenta-proofing
- **Global net:** `MagentaGuard.cs` (TKT-1) — on every scene load, scans active renderers;
  any material on a null / `Hidden/InternalErrorShader` / Standard / Legacy / Specular shader is
  swapped in-place to URP/Lit (carrying base color + main tex + emission), logged to break-log so
  the offender self-identifies. WebGL-safe, idempotent. Catches build-time shader-strip magenta
  that editor scans miss.
- **Targeted fixers (~8):** Tripo, Polyperfect (`PolyperfectUrpFix`), Tree
  (`EnvironmentTreeMaterialFixer` / `TreeOfLifeMaterialFixer`), Portal, Worker, HeroArmor.
- **Editor scanner:** `MagentaMaterialScanner.cs` + `EnsureShadersIncluded.cs` (shader-strip
  prevention at build). **This is the most mature sub-pipeline** — strong instrumentation per §12.

### 3.4 Mesh-bake / combine
- **DOES NOT EXIST.** No Mesh Baker asset, no `CombineMeshes` utility for characters. The
  `*NavMeshBaker` / `ArenaPrefabBuilder` hits are navmesh + arena scenery, not mesh-merge.
- The planned **6-submesh → 1 per Tripo character** bake (memory `tripo-roster-knight-orcs-first`,
  "Mesh Baker 6→1 per char") is **unbuilt**. Tripo heroes ship as multi-submesh FBX → multiple
  draw calls + multiple materials per character.
- **Action:** build or buy a combine step (an editor postprocess that merges a character's
  submeshes + atlases its textures into one material). Biggest single perf lever on mobile.

### 3.5 Optimization (decimate / compress / atlas)
- **KayKit:** handled at import (compression Medium, ASTC 6×6, capped texture sizes) — good.
- **polyperfect:** single shared atlas → cheap batching by design — good.
- **Tripo characters:** the weak spot. No decimation step, no texture atlasing, multi-submesh.
  Knight.fbx 1.3 MB, Orc_Warrior 1.16 MB — acceptable counts but un-batched. **Decimate +
  atlas + combine is the open optimization work** for the shipped character set.
- **Blink ~13 GB** dead weight on disk — end-of-polish purge (memory `asset-purge-deferred-to-polish-end`).
- General purge deferred to end-of-polish by owner directive (do not trim mid-dev).

### 3.6 Gitignore-safe mirroring (the key constraint)
Most art is gitignored vendor packs **outside** `Resources/` → **not `Resources.Load`-able**.
The established pattern to make owned art loadable + commit-safe is an **importer** that copies
the needed slice into `Resources/`:
- `RpgUiImporter.cs` → `Resources/RpgUi/*`; `BlinkUiImporter.cs` → Obsidian frames.
- **This same pattern is the fix for the dungeon-kit and (potentially) Supercyan gaps below.**

---

## 4. ASSET-GAP LIST (V1) — the deliverable

Priority: **P0** = blocks a V1 task now · **P1** = needed for V1 polish · **P2** = optimization / later.

| # | Gap | Have? | Evidence | Action | Pri |
|---|---|---|---|---|---|
| G1 | **Crown tier sprites** (`crown_tier1/2/3`, `crown_perfect`) | **NO** — `Resources/RpgUi/crown/` ABSENT | `BattleArenaHud.cs` falls back to TMP glyphs; `RpgUiCatalog.cs:143-146` names them | Author/source 4 crown sprites → mirror to `Resources/RpgUi/crown/` via `RpgUiImporter`. Unblocks task #41 | **P0** |
| G2 | **Arena spell-cast VFX** (real, not purple cubes) | Owned but unwired | task #44; ~910 Spells/Mirza prefabs not in `VFXCatalog`; procedural `AbilityVfxKit` fallback | Wire Spells Pack element matrix (Projectile/Cast/Explosion_Arcane…) into `VFXCatalog.asset` via new wrapper prefabs in `Resources/VFX/` | **P0** |
| G3 | **Dungeon kit loadable** for the generator | Art owned (KayKit Dungeon Remastered), NOT in Resources | `DungeonComposer.cs`/`DungeonSceneBuilder.cs` exist; only 2 `.asset` in `Resources/Dungeons` | Mirror needed dungeon modules into `Resources/` (RpgUiImporter pattern) for the chunk composer. Supports task #46 | **P1** |
| G4 | **Store pack icons** | **NO** — no icon field, no art | `packs.json` has no icon/sprite key; no pack thumbnails in `ItemIcons` | Add `iconKey` to `packs.json` schema + author/source pack thumbnails → `Resources/ItemIcons` or a `Resources/Packs/` | **P1** |
| G5 | **Character Mesh Baker / combine** (6 submesh→1, atlas) | **NO TOOL** | §3.4 — no combine utility exists | Build editor combine+atlas postprocess for Tripo chars (or buy Mesh Baker). Biggest mobile perf lever | **P2** |
| G6 | **Tripo decimate + texture atlas** | Partial — import only on KayKit | §3.5 — Tripo chars un-atlased, multi-submesh | LOD/decimate + atlas pass on shipped Knight + Orc family | **P2** |
| G7 | **SFX clips** | Procedural only | `ProceduralSfx.cs`; no `.wav/.ogg` | Optional for V1 (procedural works); source a real SFX set for polish | **P2** |
| G8 | **WaveManager dangling load path** | Broken ref | inv-01: `KayKit/enemies/*.glb` dirs are empty placeholders | Repoint `WaveManager` to the live `Resources/Enemies/*` Tripo set or mirror the glb | **P1** |
| G9 | **Blink ~13 GB purge** | Dead weight | inv-03; armor junked | End-of-polish purge (keep leftover icons only if still referenced) | **P2** |

### Gaps that are NOT real gaps (owned + loadable, do not re-make)
- Hero (Tripo Knight), Orc-family enemies, Dragon boss — shipped in Resources.
- Weapons/shields — KayKit + Tripo props, names match offset ids.
- UI chrome — RpgUi + Blink Obsidian frames mirrored.
- Environment/structures — polyperfect/Quaternius/KayKit/Tripo all wired.
- Animations — Action library (401 clips) tracked + retargeting.

---

## 5. Recommendations (priority order)

1. **G1 crowns + G2 arena VFX** — both are P0 (block live tasks #41/#44), both are "wire/mirror
   what we own or author 4 small sprites." Fastest visible wins.
2. **G8 WaveManager path + G3 dungeon mirror** — correctness + unblock the dungeon generator.
3. **G4 pack icons** — needed before the store is shown; small art + a schema field.
4. **G5/G6 character bake + decimate/atlas** — the optimization debt. Schedule before mobile
   perf testing; it is the largest single lever and currently has **no tooling at all**.
5. **G9 Blink purge** — defer to end-of-polish per standing owner directive.

> **Pipeline verdict:** import + magenta + seating are mature and well-instrumented. The two
> structural holes are (a) **no character mesh-combine/atlas tooling** and (b) the recurring
> **"owned-but-not-mirrored-to-Resources"** pattern (Supercyan, KayKit chars, dungeon kit) — both
> are tooling gaps, not missing art. V1 is not art-blocked; it is wiring-blocked on a short list.
