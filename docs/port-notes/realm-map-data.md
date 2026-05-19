# Realm Map Data Extraction

**Date:** 2026-05-18
**Purpose:** Author the canonical `realm-map.json` data file for the Realm Map — the game's region-progression overworld — so the React v1 engine and the Unity v2 port draw region content from one source of truth.
**Status:** Complete, with gaps noted below. The Realm Map is a later-week / v1.1 feature; this file is authored ahead of the Unity port of the regions system, at the owner's explicit request.

## File produced

- `Assets/StreamingAssets/Data/Canonical/realm-map.json` — the canonical Realm Map definition: the Avalon home base, five authored regions, the Withering border, and the persisted progress-ledger shape. Valid JSON; `_`-prefixed keys (`_comment`, `_sources`, `_schemaNotes`) carry provenance and should be stripped/ignored on ingest.

The React v1 project was treated strictly read-only; nothing was written into it.

## What the Realm Map is

The village (**Avalon**) is the permanent home base. The **Realm Map** is the stylized parchment overworld it sits inside — reached from a village building (the Wayshrine). Each region is a fog-shrouded node with a derived state:

- `locked` — fogged; the gate requirement is shown but the region cannot be travelled to.
- `discovered` — the gate is met; the region is revealed and travellable; its main objective is not yet done.
- `cleared` — the region's one-time main objective (a wave defense) is complete; replayable content stays.
- `threatened` — a transient Weekly-Realm-Threat overlay; never persisted.

A region is discovered when its **gate** is met, then cleared by completing its **wave-defense main objective**, which pays a one-time `clearReward`.

## Schema

`realm-map.json` top-level keys: `version`, `homeBase`, `regions`, `withering`, `progressLedger` (plus `_`-prefixed metadata).

Each entry in `regions` mirrors React's `RegionDef` (`src/contracts/region.ts`) **exactly** — a Unity loader can deserialize a region entry straight into a `RegionDef` record:

| Key | Type | Source |
| --- | --- | --- |
| `id` | string (`RegionId`) | stable key into the persisted ledger; never change once shipped |
| `title` | string | display name (e.g. "The Thornwood") |
| `biome` | string | palette token; matches a `BIOME_ELEMENTS` key in `biomeElements.ts` |
| `propSet` | string | environment prop-set token for the region scene |
| `waveCount` | int | number of defense waves in the main objective |
| `elementBias` | string[] | `Element` tokens the enemy mix skews toward |
| `gate` | object | `RegionGate` discriminated union — see below |
| `clearReward` | object | partial currency bundle (`ResourceCost`) paid once on first clear |

`gate` is a discriminated union on `kind`:
- `{ "kind": "bestWave", "value": int }` — unlocks when the player has cleared ≥ `value` village waves.
- `{ "kind": "regionCleared", "regionId": string }` — unlocks when a prerequisite region is cleared.

**Additive map metadata** (not on `RegionDef`; a typed loader should treat as optional): `description`, `mapPoint` (`{x,y}` percent-of-viewport coords), `mapOrder`, `dungeonRegion`, `adjacency` (neighbouring node ids).

`progressLedger` documents the persisted `RegionProgress` shape (`regionsSlice.ts`) — two flat maps `discovered`/`cleared` keyed by region id. It is **not** authored content; it is the runtime save shape, included so the Unity `SaveSchema`/loader matches it. `emptyRegionProgress()` (`{ discovered:{}, cleared:{} }`) is the v9→v10 save-migration seed. `RegionState` is **derived** from this ledger at runtime, never stored in the file.

## Sources used

| Source | Result |
| --- | --- |
| `defenders-of-the-realm/docs/realms-spec.md` | Read in full. NOTE: "realm" there is the **social-group** system (a community of 5–12 players), not this overworld. It contributed only the `Wintermere` zone name reference. The map system is not its subject. |
| `defenders-of-the-realm/docs/map-content-dungeons-design.md` | Read in full. The primary design doc for the Realm Map (§2). Source for region states, the gate model, the Withering, the five-region concept. |
| `defenders-of-the-realm/src/contracts/region.ts` | Read in full. `RegionDef`, `RegionGate`, `RegionId`, `RegionState` — the exact data shapes mirrored. |
| `defenders-of-the-realm/src/contracts/region-run.ts` | Read in full. `ActiveRegionRun`, `RegionRunResult` — region-run shapes (documented in schema notes, not authored into the file). |
| `defenders-of-the-realm/src/state/slices/regionsSlice.ts` | Read in full. `RegionProgress`, `evaluateRegionGates`, `applyRegionRunResult`, `emptyRegionProgress` (the v10 seed). |
| `defenders-of-the-realm/src/modules/realm/region-content/index.ts` + `thornwood.ts` | Read in full. `ALL_REGIONS` — the **authoritative authored region catalog**. Region ids/titles/biomes/propSets/waveCounts/elementBias/gates/clearRewards were copied verbatim from here. |
| `defenders-of-the-realm/src/modules/realm/realm-map-layout.ts` | Read in full. `AVALON_POINT` + `REGION_POINTS` — node coordinates copied verbatim into `mapPoint`. |
| `defenders-of-the-realm/src/assets/biomeElements.ts` | Read in full. Confirmed `Element` taxonomy and biome tokens; region `biome`/`elementBias` values validated against it. |
| `defenders-unity/docs/v2-unity-port-spec.md` | Part 4 — canonical-data conventions (`_comment`/`_sources` metadata, `Assets/StreamingAssets/Data/Canonical/` location, JSON-wins-over-ScriptableObject). |
| `defenders-unity/docs/narrative-bible.md` | Searched. Canon for Avalon, the Withering, the Wound. The named regions do **not** appear here — see gaps. |
| `Assets/StreamingAssets/Data/Canonical/canon-strings.json`, `en.json` | Read for format reference (metadata-key convention, comment style). |

## Naming discrepancy — design doc vs. authored catalog (RESOLVED in favour of code)

The design doc `map-content-dungeons-design.md` §2.1 proposes five regions named **The Thornwood, Wintermere, The Sunken Causeway, The Emberwastes, The Hollow Deep**. The newer authored React catalog (`region-content/index.ts`, `realm-map-layout.ts`) instead ships **The Thornwood, The Mirewood, Hollowfrost Vale, The Emberwastes, The Starfall Reach**.

`realm-map.json` follows the **authored catalog** — it is live, compiled React source with stable `RegionId`s used as save-ledger keys, and the design doc is explicitly a "design doc, not a build-ready spec." Using the doc's names would produce ids that disagree with the React engine and break cross-stream save compatibility (the whole point of Part 4). The Thornwood and the Emberwastes are common to both; Wintermere / Sunken Causeway / Hollow Deep from the doc were superseded by Mirewood / Hollowfrost / Starfall Reach in code.

## Gaps / could not source — to be completed later

1. **Region names are not in the narrative bible.** `narrative-bible.md` names only Avalon, the Heart (Elarion), the Wound, and the Withering — it does **not** canonize Thornwood, Mirewood, Hollowfrost Vale, Emberwastes, or Starfall Reach. Those names are canon only at the React `region-content/` source level. They should be added to `narrative-bible.md` (and likely `canon-strings.json`) before ship so the names have a single canon authority. The brief said to copy canon region names verbatim from the bible — the bible has none, so names were taken verbatim from the authored React catalog instead.
2. **Region `description` text was authored here, not lifted from canon.** No canon prose exists for these regions. The descriptions paraphrase the brief one-liners in `region-content/index.ts` JSDoc + the `biomeElements.ts` biome lore. They are placeholder-grade canon-voice prose and should be reviewed/rewritten by the narrative owner.
3. **Dungeons / region nodes contain no dungeons.** The design doc §3 describes dungeons entered via a "Hollowmouth" portal and a "Hollow Deep" dungeon region. No dungeon region exists in the authored catalog and no dungeon-region data file exists yet; `dungeonRegion` is `false` on every region. When the dungeons system is authored, either a dungeon region is added or per-region dungeon node lists are added to each region entry.
4. **No per-region foraging / rewards beyond `clearReward`.** The design doc §4.3 describes foraging spots and signature materials per region; the authored `RegionDef` has no such field, so none was invented. To be added when the gear/crafting/foraging system lands.
5. **Adjacency is inferred, not authored.** `adjacency` was derived from the spiral node layout in `realm-map-layout.ts` (each region links to its mapOrder neighbours). React has no explicit adjacency graph — the map draws connectors from node positions. If a non-linear graph is wanted later, author it explicitly.
6. **Weekly Realm Threat is stubbed.** `withering.weeklyRealmThreat` is `false`; the event (design doc §5.4) is unbuilt. The `threatened` region state exists in the contract but is transient and event-owned.

## For the Unity port

- A typed `RealmMapData` / `RegionDef` C# record (Newtonsoft.Json) deserializes `regions[]` directly; mark the additive map-metadata fields optional.
- `RegionProgress` belongs in `SaveSchema.cs` (persisted), seeded empty by the migration analogous to React's v9→v10 step; `RegionState` is derived in the loader/UI, never serialized.
- Per Part 4, the JSON is the source of truth; any region ScriptableObject is a regenerable cache.
- Add a `SchemaTests.cs` case for `realm-map.json` so cross-stream drift (a renamed region id, a changed gate kind) is caught early.
