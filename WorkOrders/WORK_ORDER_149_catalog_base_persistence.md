<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 149 — Catalog-Driven Base Persistence (save RECIPES, replay through the factory)

**Status: READY TO IMPLEMENT**
**Priority: HIGH — P0 keystone follow-on.** WO-148 makes structure creation ONE repeatable process;
this WO makes a player-designed base *survive a session* by persisting the **recipes** that the factory
replays. Without it, every base the player builds in Build Mode (WO-108) evaporates on quit. This is the
data half of the CREATE verb.
**Lane:** Persistence / code — **Core (new pure-data recipe types + additive `SaveSchema` field) +
Village (new replay loader that calls `StructureFactory`).** Runs in the Combat/AI + catalog code lane
(CLAUDE.md §9), isolated from the world/scene lane. **Touches NO scene file, bakes nothing, does NOT edit
the frozen `VillageSceneBuilder.cs`.**
**Created:** 2026-05-30
**Depends on:**
- **WO-148** (`StructureFactory.Create(entry, pose, parent)` + `CreateGroup` + `CatalogBootstrap`
  populating `CatalogRegistry`). Persistence is the **THIRD caller** of `Create` — alongside the editor
  builder (WO-148 §4) and runtime player placement (WO-148 §5). Conform to WO-148's method signatures and
  the **stable string `entryId`** (`CatalogEntry.id`).
- **The existing save system** — verified below: `Assets/_Modules/Core/State/SaveSchema.cs`,
  `GameStateService.cs`, `GameState.cs`, `SaveMigrator.cs`. This WO adds **one additive field** to that
  pipeline exactly the way `aetherCrystals` (v11) was added.

---

## North Star tie-in (`docs/NORTH_STAR.md`)

This is the persistence layer under the CREATE verb (CoC × Warcraft base-building, placement = role). A
player-designed base is the core retained artifact of the whole base-design business model — it MUST
persist locally and be portable to the (later) backend as the canonical record. Saving recipes (not live
objects) is what makes the base a small, portable, sync-able JSON document.

---

## The owner's insight — honor it literally

> Persistence was feared "impossible" because a placed structure is a live, polymorphic Unity object and
> you "can't serialize an undefined type." **You don't.** You never serialize the object. You save a flat
> list of **RECIPES** — `{ entryId, pose, state }` — and on load you **replay each recipe through
> `StructureFactory.Create`** to reconstitute the live objects.

- **Save = a list of recipes.** **Load = the factory replays them.**
- The database only ever stores **strings + numbers** — no C# type, no interface, no `MonoBehaviour`, no
  prefab reference ever crosses the persistence boundary. *That is exactly why it works,* and it is the
  **standard base-builder pattern** (Clash of Clans, Townscaper, every grid base-builder persists a
  placement manifest, never serialized scene objects).
- Persistence therefore becomes the **third caller** of the one creation method WO-148 built. No new
  creation code — just capture (recipe out) and replay (recipe in).

---

## Verified current state (read before implementing)

| Fact | Evidence |
|---|---|
| Save format is **Newtonsoft JSON in PlayerPrefs**, key `dotr-save` | `SaveSchema.cs:18` (`using Newtonsoft.Json`), `:36` (`PlayerPrefsKey = "dotr-save"`); `GameStateService.Save():238` `JsonConvert.SerializeObject(file, SaveSchema.JsonSettings)` → `PlayerPrefs.SetString` → `PlayerPrefs.Save()`. **NOT `JsonUtility`** — Newtonsoft, so `List<T>`/`Dictionary<,>`/nullable are all fine. |
| The envelope | `SaveSchema.SaveFile` (`:75`): `format` / `storeVersion` / `exportedAt` / `wallet` / `state`. `state` is `PersistedState` (`:100`). |
| The payload is `PersistedState` — **every field nullable** (`.partial()` parity) | `SaveSchema.cs:100–151`. A field an older save omits stays `null` and `ApplyPersisted` keeps the SO default → **forward/backward compatible by construction.** |
| Exactly how a NEW persisted field is added (the template to mirror) | **`aetherCrystals` v11**, touched in five places: (1) `GameState.cs:52` SO field `public int AetherCrystals = 0;`; (2) `SaveSchema.PersistedState:150` `[JsonProperty("aetherCrystals")] public double? AetherCrystals;`; (3) `GameStateService.Snapshot():279` `AetherCrystals = s.AetherCrystals`; (4) `ApplyPersisted():332` `if (p.AetherCrystals.HasValue) s.AetherCrystals = (int)p.AetherCrystals.Value;`; (5) `SaveSchema.Validate():222` clamp. The version bumped `CurrentVersion 10→11` (`SaveSchema.cs:30`) with **NO migration step** — a nullable add needs none. **This WO follows the identical 5-touch + version-bump recipe.** |
| The version cascade tolerates additive fields with no migration | `SaveMigrator.MigrateForImport():71` runs steps only for `version < CurrentVersion`; a missing new field is simply `null` post-migrate and `ApplyPersisted` skips it. v11 added no `MigrateToV11`. We add **no `MigrateToV12`** for the same reason. |
| `Snapshot()` builds the save payload; `ApplyPersisted()` writes it back onto the SO | `GameStateService.cs:266` / `:321`. These are the two functions our new field plugs into. |
| The backend-sync seam already exists and is **stub/never-deployed** | `GameStateService.cs:555–937`: `SyncToBackend`/`SendDelta` POST the full `Snapshot()` JSON to a real-but-undeployed Vercel URL (`:572`). Per project memory **backend was NEVER deployed** → **local-first is canonical**; the sync seam carries whatever `Snapshot()` contains, so a base added to `PersistedState` rides the existing seam for free. We do **not** implement any backend call. |
| `CatalogEntry.id` is the stable string key | `CatalogEntry.cs:31` `public string id;`. `CatalogRegistry.Get(id)` (`CatalogRegistry.cs:37`) resolves it. This `id` IS the persistence `entryId`. |
| `CellPlacement` is the composite member primitive | `CatalogEntry.cs:13` (`cellEntryId`, `Vector3 offset`, `float yRotation`); WO-148 `CreateGroup` replays it. Relevant to the group-on-save decision below. |

**Verdict:** the save pipeline is a clean, additive, Newtonsoft-JSON, nullable-field pipeline with a proven
5-touch pattern (aetherCrystals/v11) and a no-op migration story for additive fields. The catalog gives a
stable string id. WO-148 gives the replay method. **All three prerequisites for recipe-persistence exist;
this WO connects them.**

---

## Goal

1. A pure-data **`PlacedStructure`** recipe record (Core) — `entryId` + pose + primitive state. No object
   refs, no polymorphism, JSON/Newtonsoft-friendly.
2. A **`PlayerBase`** = `List<PlacedStructure>` (Core) — the player's base as a flat recipe manifest.
3. **Additive `SaveSchema` field** `playerBase` — added with the exact 5-touch + version-bump recipe used
   for `aetherCrystals`/v11.
4. **SAVE (capture):** a Village-side capturer turns all player-placed structures into the recipe list and
   hands it to `GameStateService` to persist.
5. **LOAD (replay):** a Village-side loader clears existing player-placed structures, then
   `foreach recipe → StructureFactory.Create(CatalogRegistry.Get(entryId), pose, root)` and applies state.
   Missing `entryId` / unknown schema → `LogWarning`-skip, **never crash**.
6. The **stable-id contract**, **versioning/forward-compat rule**, and the **backend-sync seam** stated as
   hard rules.

---

## Section 1 — `PlacedStructure` recipe record (Core, pure data)

**File (new):** `Assets/_Modules/Core/State/PlacedStructure.cs` — `namespace DeNelle.Core.State`,
assembly `DeNelle.Core`. **`using` UnityEngine only for nothing object-bearing** — store primitives so the
type is engine-agnostic and Newtonsoft round-trips it with zero converters. **NO interfaces, NO
`MonoBehaviour`/`GameObject`/prefab fields, NO `CatalogEntry` reference** (id string only).

```csharp
[System.Serializable]
public sealed class PlacedStructure
{
    // ── IDENTITY — the stable catalog key (CatalogEntry.id). NEVER an index/ordinal. ──
    [JsonProperty("entryId")] public string EntryId;

    // ── POSE — flat primitives (NOT Vector3/Quaternion; keep the wire trivial) ──
    [JsonProperty("x")]   public float X;
    [JsonProperty("y")]   public float Y;
    [JsonProperty("z")]   public float Z;
    [JsonProperty("yaw")] public float Yaw;     // degrees, world Y rotation (90° steps for grid)

    // ── STATE — primitives/strings ONLY (no object refs) ──
    [JsonProperty("tier")]       public int    Tier;            // upgrade tier (0 = base)
    [JsonProperty("currentHp")]  public float? CurrentHp;       // null = spawn at full; replay = damaged base
    [JsonProperty("cosmeticId")] public string CosmeticId;      // null = default skin

    // ── PROVENANCE — group-on-save (see §2). null = standalone placement. ──
    [JsonProperty("groupId")] public string GroupId;            // composite entry id this member came from, or null
}
```

- **Why flat floats not `Vector3`/`Quaternion`:** keeps the JSON tiny + human-diffable + identical across
  Newtonsoft versions, and avoids serializing `Quaternion`'s redundant `w`. The loader rebuilds
  `new Vector3(X,Y,Z)` and `Quaternion.Euler(0, Yaw, Yaw==... )` — yaw-only is sufficient for a grid base
  (matches `CellPlacement.yRotation` being a single float). If a future structure needs full 3-axis
  rotation, add `pitch`/`roll` additively (same forward-compat rule).
- **Why nullable `CurrentHp` / `CosmeticId`:** a `null` means "no override, spawn at the factory default,"
  so an older save (or a freshly placed structure) never forces a wrong value.

---

## Section 2 — `PlayerBase` manifest + the group-on-save decision

**File (new):** `Assets/_Modules/Core/State/PlayerBase.cs` — `namespace DeNelle.Core.State`,
assembly `DeNelle.Core`. Pure data.

```csharp
[System.Serializable]
public sealed class PlayerBase
{
    [JsonProperty("schemaVersion")] public int SchemaVersion;          // stamp for forward-compat (see §5)
    [JsonProperty("structures")]    public List<PlacedStructure> Structures = new List<PlacedStructure>();
}
```

### The group-on-save decision: **FLATTEN groups to members, keep a `groupId` breadcrumb. (Recommended.)**

When the player (or the castle author) places a WO-148 **composite** (`CreateGroup`), we have two options:

| Option | What it stores | Verdict |
|---|---|---|
| **A — store the groupId only** (replay via `CreateGroup`) | one recipe per group; the composite definition rebuilds members | **Rejected as the storage form.** It assumes the composite definition is *immutable forever* — if the castle composite's member offsets change in a later build, every saved base silently relocates walls. It also can't represent a base where the player deleted/moved one member of a placed group. |
| **B — FLATTEN to member `PlacedStructure`s, each tagged `groupId`** | one recipe per *member*, with the originating composite id as provenance | **CHOSEN.** Each member is an independent, replayable cell recipe (the factory already creates members one-by-one inside `CreateGroup`). The base is fully described by member recipes, so it is robust to composite-definition drift and to per-member edits. `groupId` is kept as a *non-load-bearing breadcrumb* (UI "this came from Castle Pack", future re-group, analytics) — **the loader replays members individually and does NOT need the composite to still exist.** |

So: **on SAVE, expand any placed group into its member `PlacedStructure`s** (each member's world pose
captured directly from its live transform, `groupId` = the composite entry id). **on LOAD, replay each
member through `StructureFactory.Create`** (the per-cell path), never through `CreateGroup`. This makes the
member recipe the single source of truth and removes any runtime dependency on the composite definition
surviving. (If a future "place pack as one undoable unit" feature wants group replay, it can opt into
`CreateGroup` by grouping members by `groupId` at load — additive, not required now.)

---

## Section 3 — Additive `SaveSchema` field (mirror the aetherCrystals/v11 recipe exactly)

**Edit `Assets/_Modules/Core/State/SaveSchema.cs`:**
1. Bump `public const int CurrentVersion = 11;` → **`12`** (`:30`), with a comment
   `// v12 — added playerBase (catalog-driven base persistence)`.
2. Add to `PersistedState` (alongside the v11 block at `:150`):
   ```csharp
   // ── v12 — Catalog Base Persistence ──
   [JsonProperty("playerBase")] public PlayerBase PlayerBase;
   ```
   (A reference type → naturally null-when-absent, same as `Dungeons`/`Quests`; no `?` needed.)
3. **`Validate()`** (`:199`): add a guard that tolerates a null/empty base and **skips entries with a
   null/empty `EntryId`** (a corrupt member is dropped, never fatal — mirror the per-list clamp style).
   Do NOT reject the whole save for one bad member; `LogWarning` + drop it, consistent with the loader.

**Edit `Assets/_Modules/Core/State/GameState.cs`:**
4. Add the SO field next to the other persisted fields:
   `public PlayerBase PlayerBase = new PlayerBase();` (fresh = empty base).
   Add it to `Reset()` in `GameStateService` → `s.PlayerBase = new PlayerBase();` (New Game wipes the base).

**Edit `Assets/_Modules/Core/State/GameStateService.cs`:**
5. `Snapshot()` (`:266`): `PlayerBase = s.PlayerBase,`.
6. `ApplyPersisted()` (`:321`): `if (p.PlayerBase != null) s.PlayerBase = p.PlayerBase;`.
7. `Reset()` (`:514`): `s.PlayerBase = new PlayerBase();`.

**Migration:** **NO `MigrateToV12`** — a reference field that is `null` on an older (v≤11) save simply
stays null and `ApplyPersisted` keeps the fresh empty `PlayerBase`. This is identical to how v11
(`aetherCrystals`) shipped with no migration step (`SaveMigrator` has no `MigrateToV11`). State this in the
RESULT.

> **Assembly note (CLAUDE.md §5):** `PlacedStructure` + `PlayerBase` live in **`DeNelle.Core`** (pure data,
> no Village ref) so the **save layer (Core)** owns the data and the **replay loader (Village)** consumes
> it. Village → Core only. Core never references the factory. ✓

---

## Section 4 — Capture (SAVE) + Replay (LOAD) in Village

**File (new):** `Assets/_Modules/Village/Catalog/BasePersistence.cs` — `namespace DeNelle.Village.Catalog`,
assembly `DeNelle.Village`. This is the **third caller** of `StructureFactory`. Runtime-safe
(`using UnityEngine` only — NO `UnityEditor`). All cross-module calls null-guarded (`?.`).

### 4.1 — Marking player-placed structures
- The capturer must distinguish **player-placed** structures from baked scene geometry. Add a tiny marker
  component the runtime factory caller stamps on player placements: **`PlacedStructureTag`** (new, Village,
  `Assets/_Modules/Village/Catalog/PlacedStructureTag.cs`) carrying the live recipe fields
  (`string entryId; int tier; string cosmeticId; string groupId;`). `TowerPlacementSystem`'s catalog
  confirm path (WO-148 §5) `AddComponent<PlacedStructureTag>()` and fills `entryId`/`groupId` on placement.
  **Baked structures are NOT tagged → never captured** (they belong to the scene/builder, not the save).
- Rationale: the save must capture only what the player *added*, so LOAD can clear-and-replay the player
  layer without disturbing baked village geometry.

### 4.2 — `CaptureBase()` (SAVE)
```
public static PlayerBase CaptureBase()
```
- `FindObjectsByType<PlacedStructureTag>(FindObjectsSortMode.None)` (Unity 6 API; not the obsolete
  `FindObjectsOfType`).
- For each tag: read its transform → `PlacedStructure { EntryId=tag.entryId, X/Y/Z=t.position,
  Yaw=t.eulerAngles.y, Tier=tag.tier, CurrentHp = <IDamageableStructure.CurrentHp if present, else null>,
  CosmeticId=tag.cosmeticId, GroupId=tag.groupId }`. (Pull `CurrentHp` off the structure's
  `IDamageableStructure` via `GetComponent`, null if it has none — keeps damaged-base state.)
- Stamp `SchemaVersion = SaveSchema.CurrentVersion`, return the `PlayerBase`.
- A thin `public static void SaveBase()` writes it: `GameStateService.Instance?.State.PlayerBase = base;`
  then `GameStateService.Instance?.Save();` — reuses the existing PlayerPrefs write. (Do NOT add a second
  save file; the base rides the one envelope.)
- **Hook:** call `SaveBase()` from the same lifecycle hooks the rest of the save uses — Build-Mode exit
  (WO-108) and the existing `PersistenceBridge` quit/scene-change flush. WO-149 provides `SaveBase()`;
  WO-108 wires the Build-Mode-exit call. Do NOT add new `OnApplicationQuit` handlers here (the service
  already has them).

### 4.3 — `ReplayBase(PlayerBase, Transform root)` (LOAD — the replay)
```
public static void ReplayBase(PlayerBase playerBase, Transform root)
```
1. **Clear the existing player layer:** find all `PlacedStructureTag`s under `root` (or globally if root
   null) and `Destroy` their hosts. (Idempotent — replaying twice yields one base, not two. Never touches
   untagged baked geometry.)
2. **Guard:** `playerBase?.Structures == null` → return (fresh/empty base, nothing to replay).
3. **Forward-compat gate:** if `playerBase.SchemaVersion > SaveSchema.CurrentVersion` → `LogWarning`
   ("base from newer build; replaying best-effort") and continue (don't hard-fail; unknown extra fields
   were already dropped by Newtonsoft).
4. `foreach (var rec in playerBase.Structures)`:
   - `var entry = CatalogRegistry.Get(rec.EntryId);`
   - **`if (entry == null) { Debug.LogWarning($"[BasePersistence] unknown entryId '{rec.EntryId}' — skipped"); continue; }`** (retired/renamed id → skip, never crash).
   - `var pose = new StructurePose { position = new Vector3(rec.X, rec.Y, rec.Z), rotation = Quaternion.Euler(0f, rec.Yaw, 0f) };`
   - `var go = StructureFactory.Create(entry, pose, root);` — **the replay; the factory IS the load path.**
   - If `go == null` (factory hard-fail, already logged) → continue.
   - **Re-stamp** `PlacedStructureTag` on `go` from the recipe (so a re-save round-trips), and **apply
     state:** `Tier` (call the structure's upgrade-to-tier hook if present), `CurrentHp` (set on
     `IDamageableStructure` if non-null), `CosmeticId` (apply skin if non-null). All via `GetComponent` +
     `?.` null-guards; a structure lacking a hook simply ignores that state field.
5. Log a one-line summary: `[BasePersistence] replayed N/{total} structures (M skipped)`.

- **Hook:** call `ReplayBase(GameStateService.Instance?.State?.PlayerBase, villageRoot)` once after the
  catalog registry is populated (WO-148 `CatalogBootstrap` runs `BeforeSceneLoad`) and after the village
  scene + `GameStateService.Load()` are ready. WO-149 provides `ReplayBase`; the village scene-ready hook
  (a small `MonoBehaviour` `Start()` or the existing village init seam) calls it. Do **not** add this call
  inside `VillageSceneBuilder.cs` (frozen) — use a runtime init component.

---

## Section 5 — The hard contracts (state verbatim in code comments + RESULT)

### 5.1 — STABLE-ID CONTRACT (non-negotiable)
- `PlacedStructure.EntryId` is the **stable `CatalogEntry.id` string, forever.** Saved bases reference it
  across every future build.
- **NEVER** persist an array index, enum ordinal, registry slot, or display name as the identity. Indices
  reorder; ordinals shift when an enum value is inserted; display names are localized/renamed.
- **Renaming `displayName` is fine.** **Repurposing or reusing an existing `id` for a different structure
  is FORBIDDEN** (it would silently mutate every saved base that referenced the old meaning). Retiring an
  id is fine — the loader skips unknown ids. New structure = new id.

### 5.2 — VERSIONING / FORWARD-COMPAT RULE
- `PlayerBase.SchemaVersion` is stamped on capture (`= SaveSchema.CurrentVersion`).
- The save envelope already versions via `SaveFile.storeVersion` + `SaveMigrator`; `PlayerBase` rides it.
- **Loader is tolerant by construction:** unknown `entryId` → `LogWarning` + skip; newer
  `PlayerBase.SchemaVersion` → best-effort replay, no hard-fail; Newtonsoft drops unknown fields; a missing
  `playerBase` (older save) → fresh empty base. **The loader must NEVER throw on a malformed/outdated
  base.** Worst case = a partial base, logged.

### 5.3 — BACKEND-SYNC SEAM (define, do NOT implement)
- The recipe list is small portable JSON and is **already inside `Snapshot()`** once added to
  `PersistedState` → it **automatically rides the existing `SendDelta` full-snapshot POST**
  (`GameStateService.cs:733`). No new network code in this WO.
- **Local-first is canonical** (backend never deployed — project memory). The seam = "the serialized
  `PlayerBase` is part of the snapshot the sync layer sends/receives." When the backend ships (separate
  React repo), the base is the canonical record with **zero Unity changes**.
- **Optional, additive (recommend, do not block on):** add `PlayerBase` to `BuildDeltaPayload`'s change
  detection (`GameStateService.cs:767`) so a base edit triggers a sync — a `BaseDiffer(cur, prev)` compares
  `Structures.Count` + a cheap hash. If included, gate it the same nullable/`?.` way as `TowersDiffer`.
  **Do NOT add backend endpoints or DB schema** — that lives in the React repo.

---

## Files to Create / Edit

**Create**
- `Assets/_Modules/Core/State/PlacedStructure.cs` (`DeNelle.Core.State`) — the recipe record (§1).
- `Assets/_Modules/Core/State/PlayerBase.cs` (`DeNelle.Core.State`) — the manifest (§2).
- `Assets/_Modules/Village/Catalog/PlacedStructureTag.cs` (`DeNelle.Village.Catalog`) — runtime marker (§4.1).
- `Assets/_Modules/Village/Catalog/BasePersistence.cs` (`DeNelle.Village.Catalog`) — `CaptureBase`/`SaveBase`/`ReplayBase` (§4).

**Edit**
- `Assets/_Modules/Core/State/SaveSchema.cs` — `CurrentVersion 11→12`; `playerBase` `PersistedState` field; `Validate()` tolerance (§3).
- `Assets/_Modules/Core/State/GameState.cs` — `public PlayerBase PlayerBase = new PlayerBase();` (§3).
- `Assets/_Modules/Core/State/GameStateService.cs` — `Snapshot()` add, `ApplyPersisted()` add, `Reset()` add; (optional) `BuildDeltaPayload` base-diff (§3, §5.3).
- `Assets/_Modules/Village/Buildings/TowerPlacementSystem.cs` — on catalog confirm (WO-148 §5), `AddComponent<PlacedStructureTag>()` + fill `entryId`/`groupId` (§4.1). **Additive only.**

**Verify-only (do NOT change)**
- WO-148's `StructureFactory.cs` / `CatalogRegistry.cs` / `CatalogEntry.cs` — bind to their signatures; never edit.

---

## What NOT to touch

- ❌ **`Assets/Editor/VillageSceneBuilder.cs` — FROZEN.** No edits; the `ReplayBase` call lives in a runtime init component, never in the builder.
- ❌ **`Village.unity`** — no scene hand-edits; **no bake fired** (CLAUDE.md §3).
- ❌ Do **NOT** serialize live objects, `GameObject`, `MonoBehaviour`, `CatalogEntry`, prefab refs, or any interface — recipes are **strings + numbers only**.
- ❌ Do **NOT** persist array indices / enum ordinals / display names as identity — `entryId` string only (§5.1).
- ❌ Do **NOT** add a second save file or PlayerPrefs key — the base rides the existing `dotr-save` envelope.
- ❌ Do **NOT** implement backend endpoints / DB schema / new network calls — define the seam only; backend lives in the separate React repo (local-first canonical).
- ❌ Do **NOT** `git add -A` / convert raw textures (LFS clean-filter trap) — CLI stages by explicit path.
- ❌ Do **NOT** fork `StructureFactory` or duplicate its creation logic — `ReplayBase` calls `Create`.
- ❌ Do **NOT** introduce `System.Reflection` in the persistence/bridge path.

---

## Acceptance criteria

- [ ] Compiles green (CLI build-gate); brace balance passes on every new/edited `.cs`.
- [ ] `PlacedStructure` + `PlayerBase` are pure data in `DeNelle.Core` — **zero** `GameObject`/`MonoBehaviour`/`CatalogEntry`/interface/prefab fields (grep clean); Newtonsoft round-trips them with no custom converter.
- [ ] **`playerBase` added to `SaveSchema.PersistedState`**, `CurrentVersion` is `12`, and it appears in `Snapshot()`, `ApplyPersisted()`, and `Reset()` — mirroring the v11 `aetherCrystals` 5-touch. A v≤11 save (no `playerBase`) loads cleanly to a fresh empty base (no migration step needed).
- [ ] **Round-trip:** place ≥2 catalog structures → `SaveBase()` → quit/relaunch (or `Load()`) → `ReplayBase()` reconstitutes the same structures at the same poses via `StructureFactory.Create`. The replayed structures are functional (a tower fires) — proving "save recipes, replay through the factory."
- [ ] **Forward-compat:** a recipe with an unknown/retired `entryId` is `LogWarning`-**skipped**, never throws; a `PlayerBase.SchemaVersion` newer than the build replays best-effort without crashing; a malformed base never aborts the whole load.
- [ ] **Group-on-save:** a placed composite is captured as flattened member `PlacedStructure`s each tagged `groupId`, and replayed member-by-member through `Create` (NOT `CreateGroup`) — does not depend on the composite definition still existing.
- [ ] **Player-vs-baked isolation:** only `PlacedStructureTag`-marked (player-placed) structures are captured; baked village geometry is never captured and `ReplayBase`'s clear step never destroys baked geometry.
- [ ] Idempotent replay: calling `ReplayBase` twice yields one base, not duplicates.
- [ ] **Core has zero references to `DeNelle.Village`** (asmdef boundary intact); the replay loader is in Village and calls `StructureFactory` (CLAUDE.md §5).
- [ ] The serialized base rides the existing `Snapshot()`/`SendDelta` sync seam unchanged — **no new backend/network code added** (sync seam defined, not implemented).
- [ ] No scene baked, `VillageSceneBuilder.cs` unchanged (`git diff` clean on that file).

---

## Done checklist (CLAUDE.md §10)

- [ ] Brace-balance check passed on every `.cs` touched (`PlacedStructure`, `PlayerBase`, `PlacedStructureTag`, `BasePersistence`, `SaveSchema`, `GameState`, `GameStateService`, `TowerPlacementSystem`).
- [ ] No `.unity` scene file hand-edited; **no bake fired**; `VillageSceneBuilder.cs` untouched.
- [ ] No new `System.Reflection` usage introduced.
- [ ] `using DeNelle.Core.Combat;` present in `BasePersistence.cs` (it reads `IDamageableStructure.CurrentHp`).
- [ ] Null-conditional `?.` on every cross-module service call (`GameStateService.Instance`, `CatalogRegistry.Get`, `StructureFactory.Create` result, `GetComponent` hooks).
- [ ] `PlacedStructure`/`PlayerBase` are `UnityEditor`-free and object-ref-free (verified by grep); `BasePersistence`/`PlacedStructureTag` are `UnityEditor`-free (runtime-safe).
- [ ] Assembly placement correct: recipe data + `SaveSchema` field in `DeNelle.Core` (pure); capturer/replayer/tag in `DeNelle.Village`. Village → Core only.
- [ ] Acceptance criteria reviewed line by line.
- [ ] `WORK_ORDER_149_catalog_base_persistence.RESULT.md` written when complete (state: confirmed no-migration decision for v12, the `CurrentHp`/`Tier`/`CosmeticId` apply hooks actually wired, whether the optional `BuildDeltaPayload` base-diff was included, and how the `ReplayBase` village hook was installed without touching the frozen builder).
```

---

## Why this is the keystone follow-on

WO-148 made structure creation **one repeatable process**. WO-149 makes the player's use of that process
**durable**: the base becomes a flat list of catalog recipes that the factory replays. No live object is
ever serialized — only `entryId` + pose + primitive state cross the boundary — which is precisely why the
pattern is sound and is the **standard base-builder approach**. The base is now a small portable JSON
manifest: it persists locally today (canonical) and is the canonical record the backend will sync later,
with zero Unity rework. Persistence is simply the **third caller** of `StructureFactory.Create`.
