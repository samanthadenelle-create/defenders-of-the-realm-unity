# WO-1114 — IMPLEMENTATION PLAN

**Companion to:** `WorkOrders/WORK_ORDER_1114_dungeon_status_field.md` (READY TO IMPLEMENT, owner-ruled).
**Status:** PLAN — executable without re-derivation.
**Written:** 2026-08-17. All facts below were read at source this session; every claim carries a `file:line`.
**Constraint under which this was written:** an APK build held the tree, so this document is `.md`-only —
no `.cs`, no `.json`, no batchmode was run. Nothing here has been compiled.

> **Do not re-design.** The WO is owner-ruled. This plan chooses *where* and *how*, not *what*.

---

## 0. TL;DR for the implementing agent

| | |
|---|---|
| **Gating point** | `DungeonPortal.Update()` — the `MobileInteractButton.Request(...)` line at `Assets/_Modules/Village/Buildings/DungeonPortal.cs:126`. A closed dungeon **never registers `EnterDungeon` as the tap callback**. Backstop guard as the first statement of `EnterDungeon()` at `:181`. |
| **New assembly homes** | Status model + fetch/cache → `DeNelle.Core`. Door UI + gating → `DeNelle.Village`. Oracle → `DeNelle.EditorRegression`. |
| **Player copy** | 8 new keys in `canon-strings.json` (BOTH copies), read via `VillageStrings.Canon(key)` (`Assets/_Modules/Village/VillageStrings.cs:51`). **No 4th string loader.** |
| **Cache** | `Application.persistentDataPath/dungeon-status-cache.json`, Newtonsoft, modelled on `StructureOrientationLocalStore` (`Assets/_Modules/Village/Catalog/StructureOrientationLocalStore.cs:40,77,127`). |
| **Backend** | `api/dungeon-status.js` (CommonJS, `@neondatabase/serverless`, public GET, no auth) + one table in `api/schema.sql`. |
| **Kill switch** | `FeatureFlags.DungeonStatus` → `Get("dungeonstatus", defaultOn: true)`. `ff.dungeonstatus = 0` forces all-open with no rebuild. |
| **Ship order** | Phase 1 client (all-open) → Phase 2 dialogue+gating against a hand-written cache file → Phase 3 oracle → Phase 4 backend. The client is correct and shippable after Phase 3 with **no backend at all**. |

---

## 1. Ground truth established this session (read this before you touch anything)

### 1a. The real `dg_*` id set — 8 ids, three classes

| id | class | evidence |
|---|---|---|
| `dg_starter_loop` | **player dungeon** | `AuthoredPortal` row `Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs:118`; injected def `:1216` |
| `dg_sunken_vault` | **player dungeon** | row `:122`; def `:1224`; `DungeonEgressRegression.cs:73-78` ContentLayouts |
| `dg_bonecrypt` | **player dungeon** | row `:126`; def `:1232`; ContentLayouts |
| `dg_ember_deep` | **player dungeon** | row `:130`; def `:1240`; ContentLayouts |
| `dg_hollow_roads` | **tunnel/crossroads, NOT a dungeon** | derived portal via `TryDeriveHollowRoadsPortal` `:359-449`; id const `Assets/_Modules/Core/World/BiomeRoads.cs:95`; its own graph `_comment` says *"It is a CROSSROADS, not a dungeon"* |
| `dg_descent_probe` | **test fixture** | `DungeonEgressRegression.cs:81-85` ControlGroupLayouts |
| `dg_stair_rig` | **test fixture** | ControlGroupLayouts |
| `dg_stairwell_probe` | **probe** | 3 nodes/2 edges; only referenced from `DungeonMultiLevelRegression` |

Non-id: `dg_not_yet_baked` (`Assets/Editor/Regression/TownSuspendSceneFloorRegression.cs:162`) is a deliberately
non-existent string. **Never put it in a status table.**

**Rules that follow:**
- The status system's **domain is the four `AuthoredPortal` ids only**. Fixtures and probes have no portal, so they
  can never be gated; the tunnel is gated by `FeatureFlags.BiomeRoads`, not by this.
- **Change no id.** `dg_hollow_roads` is bound in four independent runtime string paths — `BiomeRoads.cs:95`
  (scene name == graph id), `HubScenes.IsComposedDungeon` prefix test (`Assets/_Modules/Core/HubScenes.cs:133-137`,
  which keys the WO-1112 hero-ability carry), `HollowRoadsDropInjector.cs:151,212-214,276-283`, and
  `BiomeRoadsRegression.cs:65-66,222,231,260-269`. Three of those fail *silently* at runtime.

### 1b. The portal chain, end to end

```
DungeonWorldPortalSpawner.Bootstrap()            :221   [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
  gated on FeatureFlags.DungeonPortals           :229
  Update() -> TryPlace()                         :256
    LoadDefs()                                   :1190  (Resources/Dungeons + injected dg_* defs :1216-1245)
    TryGetAuthored(id, out AuthoredPortal)       :451   (table :110-164; hollow-roads derivation :455-457)
    SeatOnGround(...)                            :475
    BuildPortal(def, pos, yaw)                   :499
      BuildArch(...)                             :514 -> MakeBox :724 (placeholder cube arch)
      SphereCollider trigger r=2.5               :530
      root.AddComponent<DungeonPortal>()         :540
      portal.Configure(def.DungeonId, name)      :541
      AttachGateVfx / AttachThresholdAura / AttachPortalCircle   (if already discovered) :564
      SwapInSharedStructureAsync(entry).Forget() :570   (real portal art swaps in over the cubes)

DungeonPortal (Assets/_Modules/Village/Buildings/DungeonPortal.cs)
  Update()                     :73    proximity 3.0 m, 0.15 s throttle
    ShowPrompt()/HidePrompt()  :166/:175   world-space bubble ("〔 Tap / F 〕 " + name, :169)
    MobileInteractButton.Request(this, "Enter: " + _displayName, EnterDungeon)   :126   <<< THE GATE
    MobileInteractButton.Release(this)                                           :128
  OnTriggerEnter(Collider)     :153   WO-777: arms VFX ONLY, no auto-route
  EnterDungeon()               :181
    scene-name resolve + CanStreamedLevelBeLoaded dead-latch guard   :190-214
    SceneRouter.GoDungeonScene(sceneName)                            :240
```

### 1c. The one appearance owner (WO-1035) — reuse it, do not duplicate

The per-portal `Portal` record (`DungeonWorldPortalSpawner.cs:190-214`) parented under `Portal.Root`
(the `DungeonWorldPortal_{id}` GameObject created at `:508`) owns **all four** visual layers:

| field | line | attached by | torn down by |
|---|---|---|---|
| `GateVfx` (pooled) | `:198` | `AttachGateVfx` `:823-840` (key `"PP_GroundFog"` `:820`, fixed scale `:821`) | `TickDiscovery` `:759` |
| `ThresholdVfx` (pooled) | `:204` | `AttachThresholdAura` `:963-998` — **measured**: `MeasurePortalBounds` `:1091`, `OpeningCentre` `:1124`, `OpeningTargetSize` `:1136`, `VFXManager.ResolveFitScale` `:975`, `VFXManager.PlayKey` `:976-978` | `:760` |
| `CircleVfx` (plain child) | `:208` | `AttachPortalCircle` `:1034-1079` — prefab `"VFX/Portal/PortalCircleDarkStar"` `:1027`, loaded via `VfxAssetLoader.LoadVfxPrefab` `:1045`, measured `:1061-1064`, `Instantiate(..., p.Root)` `:1068` | `Destroy` `:763` |
| `Swap` (shared art) | `:212` | `SwapInSharedStructureAsync` `:912-961` → `PortalStructure.SwapInAsync` `:919`; **re-seats both measured VFX at `:951-956`** | `PortalStructure.Release` `:766` |

`OpeningSpanFraction = 1f/3f` `:889`; clamps `MinFitScale = 0.02f` / `MaxFitScale = 20f` `:895-896`.

**The sigil treatment MUST go through this same record and these same three measurement helpers.** Adding a
second spawner, a second holder GameObject, or a second teardown path violates CLAUDE.md §7 and will not be
re-seated when `SwapInSharedStructureAsync` swaps the real art in — the sigil would end up floating over the
placeholder cube geometry the moment the shared portal structure loads.

### 1d. `FlowTrace` system tags already in use (do not invent a fourth)

- `"DungeonPortal"` — spawner `:324,365,390,435,444,487,1203`; `DungeonPortal.cs:209,244`
- `"DungeonPortals"` — spawner `:501,504,525,578,639,644`
- `"Portal"` — **the appearance lane**, spawner `:835,958,982,992,1049,1073`; matches `PortalStructure.Sys` (`Assets/_Modules/Core/World/PortalStructure.cs:51`)

**This WO uses exactly two tags:** `"DungeonStatus"` for fetch/cache/parse/resolution, and `"Portal"` for
anything that changes the door's appearance.

### 1e. There is no existing per-dungeon availability concept

Confirmed absent. The adjacent-but-different things — do **not** overload any of them:
- `DungeonDef.SceneExists` (`Assets/_Modules/Village/Dungeons/DungeonDef.cs:45`) — build truth, not world state; no `dg_*` def authors it (they are synthesized by `MakeDef` `:1273`).
- `Application.CanStreamedLevelBeLoaded(...)` (`DungeonWorldPortalSpawner.cs:1217,1225,1233,1241`) — scene-in-build only.
- `ComposedLockedPort` / `ComposedKeyLock` — **intra-dungeon** key doors. Different axis, similar names. Do not reuse either name.
- The current "closed" mechanism is a **commented-out table row**: `DungeonWorldPortalSpawner.cs:131-137` (Folk's Granary, WO-776, *"a real door into a hollow room"*). That hand-edit is exactly what this WO retires.

---

## 2. Every file to create or modify

### 2a. CREATE — `Assets/_Modules/Core/World/DungeonStatusCatalog.cs`
**Assembly:** `DeNelle.Core` (`Assets/_Modules/Core/DeNelle.Core.asmdef` — references `UniTask`, `Unity.TextMeshPro`, `Unity.Addressables`, `Unity.ResourceManager`).
**Namespace:** `DeNelle.Core.World` (sits beside `BiomeRoads.cs` and `PortalStructure.cs`).

Why Core: `DeNelle.Village` (the portal) and `DeNelle.Dungeons` both already reference `DeNelle.Core`, and
`DeNelle.Core` references neither of them. It is the only home that does not invert a dependency.

```
public enum DungeonDoorState { Open, Sealed, Collapsed, Rescue, Flooded }

public readonly struct DungeonDoorInfo
{
    public readonly DungeonDoorState State;
    public readonly string Headline;   // may be null/empty -> caller falls back to canon copy
    public readonly string Body;       // may be null/empty
    public readonly string Sigil;      // may be null/empty -> default seal
    public bool IsOpen => State == DungeonDoorState.Open;
}

public static class DungeonStatusCatalog
{
    public const string Sys = "DungeonStatus";
    public const int PayloadVersion = 1;

    public static DungeonDoorInfo For(string dungeonId);   // ALWAYS returns; unknown id => Open
    public static bool IsOpen(string dungeonId);           // convenience; the hot path
    public static void ApplyPayload(string json, string provenance);  // Guard-wrapped parse+swap
    public static void Clear();                            // test hook -> all-open
    public static string Provenance { get; }               // "live" | "cache" | "default" | "flag-off"
    public static bool Loaded { get; }
}
```

**Behavioural contract (each line is a §6 failure mode — see §5 for the mapping):**
- `For(null)`, `For("")`, `For(<unknown id>)` → `Open`, never a throw.
- `ApplyPayload` swaps the whole table atomically (build a new `Dictionary`, assign the field). It **never
  clears an existing good table on a bad parse** — a malformed live payload must leave the cached table standing.
- Status string parse is **case-insensitive**; an unparseable value maps to `Open` and logs
  `FlowTrace.Warn(Sys, ...)`. Never fail closed.
- `PayloadVersion` mismatch is a `FlowTrace.Warn` + **still parse** (forward-compatible: a v2 payload with extra
  fields must not blank the world). Only a hard parse failure rejects.
- No `MonoBehaviour`, no coroutines, no `UnityWebRequest` in this file. It is pure state + parse, which is what
  makes the oracle in §6 able to drive it headlessly.

### 2b. CREATE — `Assets/_Modules/Core/World/DungeonStatusService.cs`
**Assembly:** `DeNelle.Core`. **Namespace:** `DeNelle.Core.World`.

Owns the fetch/cache lifecycle only. `DungeonStatusCatalog` stays transport-free.

```
public static class DungeonStatusService
{
    private const string Endpoint = BackendBase + "/api/dungeon-status";
    private const int RequestTimeoutSeconds = 15;   // matches GameStateService.cs:1166
    private const string CacheFileName = "dungeon-status-cache.json";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap();                 // mirrors DungeonWorldPortalSpawner.cs:221
    public static UniTaskVoid RefreshAsync();        // fire-and-forget, .Forget()
    public static string CachePath { get; }          // Path.Combine(Application.persistentDataPath, CacheFileName)
}
```

- **Base URL:** `https://defenders-of-the-realm-v2.vercel.app`. It is a `private const string` per file across the
  codebase (`GameStateService.cs:1145`, `BackendRequestSigner.cs:50`, and nine others). Follow the house pattern —
  a `private const string BackendBase` in this file. Do **not** refactor the eleven duplicates in this WO.
- **`AfterSceneLoad`, not `BeforeSceneLoad`.** `BeforeSceneLoad` runs before any scene exists; and `AfterSceneLoad`
  is what `DungeonWorldPortalSpawner.Bootstrap` (`:221`) already uses, so the portal and the status arrive on the
  same tick ordering the rest of the system already assumes.
- **Non-blocking is structural, not a comment.** `Bootstrap()` calls `RefreshAsync().Forget()` and returns. Nothing
  awaits it. Copy the shape of `PersistenceBridge.cs:128` (`LoadFromBackendAsync().Forget();`) including the
  `await UniTask.Delay(200)` first-yield so scene `Awake`s run first (`PersistenceBridge.cs:117-129`).
- **HTTP idioms are non-negotiable** (all three exist because of real production bugs):
  1. `using var req = UnityWebRequest.Get(url);`
  2. `req.timeout = RequestTimeoutSeconds;` — without it a captive-portal socket never completes. The reasoning is
     written out at `GameStateService.cs:1152-1165`.
  3. **`try/catch` around `await req.SendWebRequest()` AND a separate `req.result != Success` check.** The UniTask
     awaiter throws on non-2xx — see the WO-769 comments at `GameStateService.cs:1421-1431`. Checking only one of
     the two is the bug.
  4. Deserialize with **Newtonsoft `JsonConvert`** and `[JsonProperty]`-attributed DTOs (`BackendRequestSigner.cs:213-215`).
     `JsonUtility` cannot express the `dungeons` map.
- **No auth headers.** This endpoint is public read and must resolve before sign-in (WO §5). Do not call
  `BackendRequestSigner`.

### 2c. CREATE — `Assets/_Modules/Village/Buildings/DungeonSealedDoorPanel.cs`
**Assembly:** `DeNelle.Village`. **Namespace:** `DeNelle.Village`.

The Obsidian dialogue shown when the player taps a closed door.

**Copy `Assets/_Modules/Village/Crafting/JewelPolishConfirmPanel.cs` verbatim as the skeleton** — it is the most
recent in-tree modal and it already satisfies both UI gates:

```csharp
var modal = ElarionUiKit.BuildObsidianModal(
    PanelName, headline.ToUpperInvariant(),
    new Vector2(0.14f, 0.16f), new Vector2(0.86f, 0.86f),
    onClose: Close, sortingOrder: 31040,
    frameName: RpgUiCatalog.FrameCore);
if (modal == null || modal.canvas == null || modal.chrome == null || modal.chrome.content == null)
{
    FlowTrace.Fail(Sys, "BuildObsidianModal returned no usable chrome - sealed-door dialogue NOT shown.");
    if (modal != null && modal.canvas != null) UnityEngine.Object.Destroy(modal.canvas);
    return false;
}
```
(`JewelPolishConfirmPanel.cs:93-104` — the null-guard-and-destroy is part of the pattern, not optional.)

**MANDATORY — arbiter registration.** `ModalArbiterRegistrationRegression` (`Assets/Editor/Regression/ModalArbiterRegistrationRegression.cs`)
treats **any bare `BuildObsidianModal(` call as a top-band build** (`:63`, `CallDefaultsIntoTopBand` `:176+`) and
hard-fails a file that does not contain `PanelManager` + `Register` + `NotifyOpened` + `NotifyClosed`
(`RegistersWithArbiter` `:192-195`). Copy `JewelPolishConfirmPanel.cs:170-173` and `:209`:

```csharp
if (s_handle == null) s_handle = PanelManager.Register(PanelName, Close, () => IsOpen);
if (!PanelManager.NotifyOpened(s_handle)) { FlowTrace.Warn(Sys, "PanelManager rejected the sealed-door dialogue."); return false; }
...
if (s_handle != null) PanelManager.NotifyClosed(s_handle);
```

**Content:** headline in the frame header, body as an `ElarionUiKit.Label` in `modal.chrome.content`
(`ElarionUiKit.cs:1708`), and a single Close. The close is auto-built by `BuildObsidianPanel`/`BuildObsidianModal`
via `ObsidianCloseButton` (`ElarionUiKit.cs:868`). **Never draw an X** — owner ruling 2026-07-03, documented at
`ElarionUiKit.cs:850-867`; it is a labelled "Close" obsidian box in the frame's measured close zone.

**No error styling.** No red, no warning icon, no `⚠`. Use the ordinary parchment/gilt body palette
(`ElarionUi.Parchment` / `ElarionUi.ParchmentDim` / `ElarionUi.Gilt`) exactly as the crafting panel does.
This is world prose, not an error dialog — that is the whole point of the WO.

### 2d. MODIFY — `Assets/_Modules/Village/Buildings/DungeonPortal.cs`  ← **THE GATE**

Four surgical edits. Nothing else in this file changes.

1. **Add a cached door-state field + a throttled refresh.** Reuse the existing 0.15 s proximity tick
   (`_nextProximityCheck` / `CheckInterval`, `:53-54`) — do not add a second timer. On each tick:
   `_door = DungeonStatusCatalog.For(_dungeonId);`
   This is what makes criterion 3 (flip within a cache period, no rebuild) true and criterion 5 (never eject
   mid-run) true — the state is read **at the door**, and a hero already inside the dungeon scene has no
   `DungeonPortal` in scope to re-read it.

2. **`:125-128` — the interact registration. This is the gating point.**
   ```csharp
   if (_isInRange)
   {
       if (_door.IsOpen)
           MobileInteractButton.Request(this, "Enter: " + _displayName, EnterDungeon);
       else
           MobileInteractButton.Request(this, DoorHeadline(), ShowSealedDoor);
   }
   else MobileInteractButton.Release(this);
   ```
   `EnterDungeon` is **never handed to the button** while the door is closed. Since WO-777 removed the walk-in
   auto-route (`OnTriggerEnter` `:153-162` arms VFX only) and `MobileInteractButton` is the sole entry path
   (comment `:134-136`), this single branch closes the door completely.

3. **`:166-173` — `ShowPrompt()` uses the headline** when closed, instead of `"〔 Tap / F 〕 " + _displayName`.
   Keep the existing `BuildBubble` (`:250-292`) — it uses legacy `TextMesh`, which is **outside** the
   `UiObsidianConformanceRegression` StrongSmells regex (that regex matches `Text`, `TextMeshProUGUI`, `TMP_Text`
   — see `UiObsidianConformanceRegression.cs:83-91`). ⚠ If you rewrite the bubble with
   `AddComponent<TextMeshProUGUI>()` you will hard-fail `[ui-obsidian]` unless the file names `ElarionUiKit`.
   **Do not rewrite the bubble in this WO.**

4. **`:181` — the backstop.** First statement of `EnterDungeon()`:
   ```csharp
   if (!DungeonStatusCatalog.IsOpen(_dungeonId))
   {
       FlowTrace.Warn("DungeonStatus", $"EnterDungeon blocked at the door: id='{_dungeonId}' state={_door.State} " +
                                       $"(provenance={DungeonStatusCatalog.Provenance}). No scene load attempted.");
       _loading = false;
       ShowSealedDoor();
       return;
   }
   ```
   Note the `_loading = false` — same dead-latch discipline as the existing `CanStreamedLevelBeLoaded` guard at
   `:207-214`, so a future flip to `open` leaves the portal live rather than permanently dead.

**Why the gate is here and nowhere else:**
- It is **before** `SceneRouter.GoDungeonScene(sceneName)` at `:240`. No scene load is started, so there is
  categorically no load-then-eject — the outcome the WO §4a calls "indistinguishable from a crash".
- It is **at the portal**, in the object that already owns proximity, prompt and entry. No new component,
  no new lifecycle, no ordering question with the spawner.
- `SceneRouter.GoDungeonScene` (`Assets/_Modules/Core/SceneRouter.cs:609`) is a **shared route** also reachable
  from the dev overlay. Gating there would break debug jumps and would be a load-time gate, not a door gate.
- Gating in `DungeonWorldPortalSpawner` (e.g. skipping the `AuthoredPortal` row) would **remove the door** —
  explicitly forbidden by WO §8 ("the door stays, the state changes"), and it is the WO-776 hand-edit
  (`DungeonWorldPortalSpawner.cs:131-137`) this WO exists to retire.

Both the branch at (2) and the backstop at (4) are required. (2) is the player experience; (4) is the invariant.

### 2e. MODIFY — `Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs`  (appearance only)

One new private method + two call sites. **No new spawner, no new holder, no new teardown path.**

- `ApplyDoorState(Portal p, DungeonDoorInfo info)` — placed next to `AttachPortalCircle` (`:1034`).
  When `info.IsOpen` → current behaviour, unchanged. When closed:
  - stop `p.ThresholdVfx` and destroy `p.CircleVfx` exactly as `TickDiscovery` `:759-763` does;
  - attach the sigil through the **same measured path**: `MeasurePortalBounds` `:1091` → `OpeningCentre` `:1124`
    → `OpeningTargetSize` `:1136` → `VFXManager.ResolveFitScale` `:975`/`:1064` → store the handle in the
    **existing** `Portal.ThresholdVfx` / `Portal.CircleVfx` fields (`:204`/`:208`) so the existing teardown
    at `:759-763` still owns it.
  - `FlowTrace.Step("Portal", ...)` with the resolved bounds line (`BoundsLine` `:1145`) so the seating is
    provable from a headless log.
- **Call sites:** (a) `BuildPortal` at the existing attach point `:564`; (b) inside
  `SwapInSharedStructureAsync` at the re-seat block `:951-956`, so the sigil re-seats when the real portal
  art swaps in over the placeholder cubes. Missing (b) is the single most likely bug in this WO.
- **Sigil art keys are NOT invented here.** Per memory `vfx-map-owner-tags-no-creative-pick`, the CLI maps a
  key → a named hook verbatim and never picks or substitutes. Until the owner tags art for `seal` / `rubble` /
  `water`, **every closed state uses the DEFAULT treatment**: threshold aura + circle removed (the portal reads
  as dark and inert — which is already correct world-language for "this does not open"), and an unresolved or
  absent sigil key logs `FlowTrace.Once("Portal", "sigil-unresolved-<key>", ...)` exactly as the existing
  unresolved-key paths do (`:992-997`, `:1049-1054`). Ship the default; add art keys when tagged.

### 2f. MODIFY — `Assets/_Modules/Core/FeatureFlags.cs`

One line, following the house pattern (`FeatureFlags.cs:137`, `:873`):
```csharp
/// <summary>WO-1114 dungeon door status. Default ON. PlayerPrefs "ff.dungeonstatus" = 0 forces
/// every door OPEN with no rebuild — the kill switch if a bad payload ever locks content.</summary>
public static bool DungeonStatus => Get("dungeonstatus", defaultOn: true);
```
`Get` reads `PlayerPrefs.GetInt("ff." + name, -1)`; `0` = off, `1` = on, absent = the compiled default
(`FeatureFlags.cs:8-14`). When off, `DungeonStatusCatalog` reports `Provenance = "flag-off"` and `For()` returns
`Open` for everything; `DungeonStatusService.Bootstrap` returns without a fetch.

⚠ **Do NOT add this flag to `s_urlActivatableFlags`** (`FeatureFlags.cs:~60`). That allow-list is deliberately
restricted to read-only presentation flags; a URL-flippable content gate is a security regression.

### 2g. MODIFY — `Assets/Resources/Data/Canonical/canon-strings.json` **and**
### `Assets/StreamingAssets/Data/Canonical/canon-strings.json`

**Both copies, byte-identical.** Multiple suites assert on each independently
(`VfxAuraDifferentiationRegression.cs:47-48,123-124`). The file is flat `lowerCamelCase` string→string, and it is
**versionless by design** — `DataWebRegression.cs:124` exempts it from the schema-version gate, so there is no
version bump. Adding keys is safe; changing or removing an existing key can red
`VfxAuraDifferentiationRegression` / `DungeonLoreReadableRegression` / `EchoResourcePickerRegression`.

Eight new keys (values are §4c of the WO — **see §8 of this plan, they are UNRATIFIED**):

```
"dungeonSealedHeadline", "dungeonSealedBody",
"dungeonCollapsedHeadline", "dungeonCollapsedBody",
"dungeonRescueHeadline", "dungeonRescueBody",
"dungeonFloodedHeadline", "dungeonFloodedBody"
```

**Read them via `VillageStrings.Canon(key)`** (`Assets/_Modules/Village/VillageStrings.cs:51`).

> ⚠ **Assembly trap — this is why the copy resolution lives in Village, not Core.** `CanonStrings`
> (`Assets/_Modules/Onboarding/CanonStrings.cs:33`) is in assembly **`DeNelle.Onboarding`**, which
> `DeNelle.Core`, `DeNelle.Village`, `DeNelle.HUD` and `DeNelle.Dungeons` all **cannot see**. The sanctioned
> Village twin is `VillageStrings` (`VillageStrings.cs:35`, header at `:9-13` explains it is a scoped mirror).
> Both consumers of the default copy — `DungeonPortal.DoorHeadline()` and `DungeonSealedDoorPanel` — are in
> `DeNelle.Village`, so `VillageStrings` covers them. **Do not add a fourth canon-strings loader**; three
> already exist and are logged as debt at `docs/reference/DATA_CLASS_MAP.md:362`.

An unknown key returns the visible marker `"[[missing:key]]"` and does not throw (`CanonStrings.cs:149-155`;
same `Resolve` contract in `VillageStrings.cs:126+`). The §6 oracle asserts all eight keys exist in **both**
copies precisely so that marker can never ship.

### 2h. CREATE — `Assets/Editor/Regression/DungeonStatusRegression.cs`
**Assembly:** `DeNelle.EditorRegression`. See §6.

### 2i. MODIFY — `Assets/Editor/Regression/DataRegression.cs`  (one line, by the committer)
See §6. ⚠ `DataRegression.cs` is lane-fenced — the orchestrator/committer adds this line, not a parallel agent.

### 2j. CREATE — `api/dungeon-status.js`  ·  MODIFY — `api/schema.sql`  ·  MODIFY — `api/admin/db.js`
See §7.

### 2k. CREATE — `Assets/Editor/DungeonStatusDevMenu.cs` (optional but recommended)
**Assembly:** `DeNelle.Editor`. Two menu items under `Defenders/Dungeon Status/` that write and delete
`Application.persistentDataPath/dungeon-status-cache.json` with a stub payload. This is how a human reproduces
acceptance criterion 2 without a backend, and it exercises the **real** cache path rather than a parallel test path.

---

## 3. The exact gating point (restated for the record)

> **`Assets/_Modules/Village/Buildings/DungeonPortal.cs:126` — the
> `MobileInteractButton.Request(this, "Enter: " + _displayName, EnterDungeon)` registration inside `Update()`.**
> A closed door registers `ShowSealedDoor` instead of `EnterDungeon`, so `EnterDungeon` is never invoked.
>
> **Backstop:** first statement of `EnterDungeon()` at `:181`, before the scene-name resolution at `:190`
> and therefore long before `SceneRouter.GoDungeonScene(sceneName)` at `:240`.

Nothing loads. Nothing ejects. The sealed door is the content.

---

## 4. Fetch / cache / fallback lifecycle

```
                 [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
                            DungeonStatusService.Bootstrap()
                                        |
                    FeatureFlags.DungeonStatus == false ? --yes--> Provenance="flag-off"; ALL OPEN; return
                                        | no
                        (1) LOAD CACHE SYNCHRONOUSLY  (a single small local file read)
                            persistentDataPath/dungeon-status-cache.json
                            hit  -> ApplyPayload(json, "cache")     [table populated at frame 0]
                            miss -> Provenance="default"            [ALL OPEN — the safe ground state]
                                        |
                        (2) RefreshAsync().Forget()   <-- NOTHING AWAITS THIS
                            await UniTask.Delay(200)                (PersistenceBridge.cs:117-129 idiom)
                            UnityWebRequest.Get(Endpoint), timeout=15
                            try { await req.SendWebRequest(); } catch { ... }   // awaiter throws on non-2xx
                            + separate req.result != Success check              // GameStateService.cs:1421-1431
                                        |
                            success -> ApplyPayload(body, "live")
                                       then WRITE the cache file (only after a SUCCESSFUL parse)
                            failure -> FlowTrace.Warn; table stays exactly as (1) left it
```

**Resolution order, exactly as WO §4d requires:** live → cached → all-open.
The cache read is step (1) and is **synchronous and local** — a few KB from `persistentDataPath`, the same
class of read `StructureOrientationLocalStore.cs:127` already does at load. Only the **network** leg is async.
This is what makes the client fully correct with the backend off: on a device that has never reached the
network, step (1) misses, the table is empty, `For()` returns `Open` for every id, and the game is exactly
what it is today.

**Write-after-parse ordering is load-bearing.** The cache is written only after `ApplyPayload` succeeds, so a
malformed live payload can never poison the cache and brick the door state across restarts.

**Never kicked mid-run (criterion 5)** falls out of the design for free: status is read only by `DungeonPortal`,
which exists only in the hub scene. A hero inside a `dg_*` scene has no `DungeonPortal` instance, so a mid-run
`ApplyPayload` has nothing to act on. **No code needs to enforce this — but the oracle asserts it** (§6, Case 6)
so a future refactor cannot quietly introduce a mid-run eject.

---

## 5. Every WO §6 failure mode → where it is handled → the exact log call

| # | Failure | Handled in | Behaviour | Log (§12: no silent catches) |
|---|---|---|---|---|
| 1 | Backend down / DNS / offline | `DungeonStatusService.RefreshAsync`, the `catch` around `SendWebRequest` **and** the `req.result` check | table untouched; whatever step (1) produced stands (cache, else all-open) | `FlowTrace.Warn("DungeonStatus", $"fetch failed ({req.responseCode}) {ex.GetType().Name} — keeping provenance={Provenance}")` |
| 2 | Malformed JSON (live) | `DungeonStatusCatalog.ApplyPayload`, body wrapped in `Guard.Try("DungeonStatus", "parse payload", ...)` | existing table **preserved**, cache **not** overwritten | `Guard.Try` reports via `FlowTrace.Fail` automatically (`Guard.cs:Report`); add an explicit `FlowTrace.Fail("DungeonStatus", "payload rejected — keeping <provenance>")` on the false return |
| 3 | Malformed JSON (cache file) | same `ApplyPayload`, provenance `"cache"` | fall through to all-open; **delete the corrupt cache file** so the next boot is clean | `FlowTrace.Fail("DungeonStatus", "cache rejected + deleted; falling back to all-open")` |
| 4 | Unknown `status` string | `DungeonStatusCatalog` status parser | → `Open` (never fail closed) | `FlowTrace.Warn("DungeonStatus", $"unknown status '{raw}' for id='{id}' — treating as OPEN")` |
| 5 | Unknown dungeon id in payload | `ApplyPayload`, per-row loop under `Guard.TryEach("DungeonStatus", "row", ...)` | row **kept** in the table (harmless — nothing queries it) but counted | `FlowTrace.Step("DungeonStatus", $"payload carries unshipped id '{id}' — ignored")` |
| 6 | Id present in game, absent from payload | `DungeonStatusCatalog.For` — `TryGetValue` miss | → `Open`. **Absence is not a closure.** | no per-call log (hot path); one `FlowTrace.Step` summary at `ApplyPayload` naming which of the four portal ids the payload covers |
| 7 | Slow response | structural: `Bootstrap` never awaits `RefreshAsync`; `req.timeout = 15` | doors resolve `Open` (or cached) until the payload lands; boot never stalls | `FlowTrace.Step("DungeonStatus", $"live payload landed after {ms} ms, provenance live")` — proves non-blocking in the headless log |
| 8 | Unwritable cache dir | cache write wrapped in `Guard.Try("DungeonStatus", "write cache", ...)` | in-memory table unaffected; next boot refetches | `Guard` → `FlowTrace.Fail` |
| 9 | `BuildObsidianModal` returns no chrome | `DungeonSealedDoorPanel` null-guard (`JewelPolishConfirmPanel.cs:98-104` shape) | destroy the orphan canvas, return false; the door stays closed, no half-built UI | `FlowTrace.Fail(Sys, "BuildObsidianModal returned no usable chrome — sealed-door dialogue NOT shown.")` |
| 10 | `PanelManager` rejects the modal | same panel, `NotifyOpened` false branch | dialogue not shown; door still closed | `FlowTrace.Warn(Sys, "PanelManager rejected the sealed-door dialogue.")` |
| 11 | Sigil art key unresolved | `DungeonWorldPortalSpawner.ApplyDoorState` | default treatment (aura + circle removed) | `FlowTrace.Once("Portal", $"sigil-unresolved-{key}", ...)` — mirrors `:992-997` / `:1049-1054` |
| 12 | Canon copy key missing | `VillageStrings.Canon` returns `"[[missing:key]]"` | visible marker (by design) | prevented at build time by the §6 oracle Case 3 |

**Not one `catch` in this WO is allowed to be empty.** Prefer `Guard.Try` / `Guard.TryEach`
(`Assets/_Modules/Core/Diagnostics/Guard.cs`) over hand-written try/catch — they log through `FlowTrace` and
land in the F8 break-log automatically.

**§12 compliance note for the implementer:** the client half of this WO is *new construction*, not a bug hunt,
so the instrument-before-you-edit gate is satisfied by building the instrumentation in from the first line —
which is what the table above specifies. If a defect appears during bring-up, the traces above are already the
captured data; read them before theorising.

---

## 6. The regression oracle

**File:** `Assets/Editor/Regression/DungeonStatusRegression.cs`
**Assembly:** `DeNelle.EditorRegression` (`Assets/Editor/Regression/DeNelle.EditorRegression.asmdef` already
references `DeNelle.Core`, `DeNelle.Village`, `DeNelle.Data`, `DeNelle.Dungeons` — **no asmdef edit needed**).
**Namespace:** `DeNelle.Editor.Regression`. **Tag:** `[dungeon-status]`. **Markers:** `DUNGEON_STATUS_OK` / `DUNGEON_STATUS_FAIL`.

**Shape — copy `GlossaryRegression.cs:82-124` verbatim:**
```csharp
public static void RunAll()
{
    if (Run(out string reason)) Debug.Log("DUNGEON_STATUS_OK - " + reason);
    else Debug.LogError("DUNGEON_STATUS_FAIL: " + reason);
}

public static bool Run(out string reason)      // never throws; true = green
{
    var failures = new List<string>();
    var notes = new List<string>();
    try { Case(failures, "banned-copy", () => Case1_BannedCopy(failures, notes)); /* ... */ }
    catch (Exception ex) { failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message); }
    if (failures.Count == 0) { reason = "DUNGEON STATUS OK - ..." ; return true; }
    reason = "dungeon-status FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
    return false;
}
private static void Case(List<string> failures, string name, Action body)
{ try { body(); } catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); } }
```
Static class, no MonoBehaviour, no NUnit, no `Assert`. `Run` logs nothing — pass/fail is the bool, detail is
the `reason` string. Use `RegressionOutcome.Skip(out reason, ...)` (`RegressionOutcome.cs:66-71`) for a stand-down.

### The cases

**Case 1 — THE BANNED-STRING ORACLE (WO criterion 4; the rule most likely to rot).**
```csharp
private static readonly string[] BannedInDoorCopy =
{
    "construction", "coming soon", "disabled", "dev", "WIP", "TODO",
    "placeholder", "not implemented", "unfinished", "work in progress",
};
```
Scan, case-insensitively:
- (a) **every one of the eight `dungeon*Headline` / `dungeon*Body` values** in **both** copies of
  `canon-strings.json` — the parsed VALUES only, never the raw file (the authoring `_comment` must remain free
  to name the words it bans; this is exactly why `GlossaryRegression.cs:372-373` scans parsed fields, not text);
- (b) any string literal in `DungeonSealedDoorPanel.cs` and in `DungeonPortal.cs` that reaches the player —
  use `RegressionSourceText.StripCommentsAndStrings` inversely, i.e. extract the string literals, so this
  oracle's own needle list cannot match its own prose.

⚠ **`"dev"` is a substring of ordinary English** ("devour", "devastation", "devout" — all plausible in this
game's register). Match `"dev"` on a **word boundary** (`\bdev\b`, case-insensitive) and match the multi-word
needles as literal substrings. Write the regex explicitly; a naive `IndexOf("dev")` will red on the first piece
of good prose the owner writes and the oracle will get disabled — which is the failure this case exists to prevent.

Prefer **extending** `GlossaryRegression.BannedInPlayerCopy` (`GlossaryRegression.cs:73-79`) if the owner wants
one global list. This plan keeps a separate list because the door needles ("dev", "WIP") are far more
false-positive-prone than the glossary's retired-canon needles ("Avalon", "Garran") and should not be able to
red the glossary suite.

**Case 2 — fallback resolution.** Drive `DungeonStatusCatalog` directly (it is transport-free, so this runs
headlessly with no network and no PlayMode):
- `Clear()` then `For("dg_bonecrypt").IsOpen` == **true** (all-open default).
- `ApplyPayload(<garbage>, "test")` == false, and the table is **unchanged** (not blanked).
- `ApplyPayload(<valid, bonecrypt sealed>, "test")` then `For("dg_bonecrypt").State == Sealed`
  **and `For("dg_ember_deep").IsOpen == true`** — the "every other dungeon is unaffected" half of criterion 2.
- `ApplyPayload(<status:"banana">)` → that id resolves `Open`.
- `ApplyPayload(<id:"dg_nonexistent">)` → no throw; the four real ids still resolve `Open`.
- `For(null)` / `For("")` → `Open`, no throw.

**Case 3 — canon keys present.** All eight keys exist, are non-empty, and are **identical in both copies** of
`canon-strings.json`. Guarantees `"[[missing:key]]"` can never reach a player. Also assert a minimum length
(`GlossaryRegression.cs:69` uses `MinDefinitionChars = 20` for the same reason — "shorter than this is a
placeholder, not an answer").

**Case 4 — id contract.** Every id the status system knows about is one of the four `AuthoredPortal` rows.
Lint `DungeonWorldPortalSpawner.cs:110-164` by const path for the four literals (the `EchoWorldPresenceRegression.cs:71-74`
source-lint-by-const-path idiom) and assert the plan's id set matches. Explicitly assert `dg_hollow_roads`,
`dg_descent_probe`, `dg_stair_rig`, `dg_stairwell_probe` are **absent** from any status table or default map.

**Case 5 — one appearance owner.** Source-lint `DungeonWorldPortalSpawner.cs`: assert `ApplyDoorState` exists,
that it references `MeasurePortalBounds`, `OpeningCentre` and `OpeningTargetSize` (so the sigil is measured, not
guessed), and that the file contains **no second** `Instantiate(` of a portal-visual prefab beyond the existing
`AttachPortalCircle` site at `:1068`. Assert `ApplyDoorState` is called from **both** `BuildPortal` and
`SwapInSharedStructureAsync` — the re-seat call site is the easy one to forget.

**Case 6 — the gate is at the door, and only at the door.** Source-lint `DungeonPortal.cs`:
- `EnterDungeon` must be **conditionally** registered — assert the `MobileInteractButton.Request(` region
  references `IsOpen` (or `_door`);
- `EnterDungeon`'s body must contain a `DungeonStatusCatalog` guard **before** the `SceneRouter.GoDungeonScene`
  call (compare character offsets in the method body);
- assert **no** `DungeonStatusCatalog` reference appears in `SceneRouter.cs` or in any `Assets/_Modules/Dungeons/**`
  file — that would be a load-then-eject or a mid-run kick, and it is the shape criterion 5 forbids.

**Case 7 — no auth on the read path.** Assert `DungeonStatusService.cs` does not reference
`BackendRequestSigner`, `X-Wallet`, `X-Nonce` or `X-Signature`. The endpoint must resolve before sign-in
(WO §5, §8 "do NOT gate this behind sign-in or a wallet").

### Registration (committer, one line)

Add **above the `>>> REGISTERED ORACLE SUITES — END FENCE <<<` comment** in `DataRegression.RunAll`
(`Assets/Editor/Regression/DataRegression.cs`, currently ~`:926` — **locate the fence comment, do not trust the
line number**):

```csharp
DeNelle.Core.Diagnostics.Guard.Try("Regression", "dungeon-status suite", () => { if (!DeNelle.Editor.Regression.DungeonStatusRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[dungeon-status] " + r); });
```

The `[dungeon-status]` tag must be the **first character** of the appended line — `CountOracleTagLines`
(`DataRegression.cs:1027-1036`) counts lines starting with `[`, and `RegressionMarkerRegression.TryGetExpectedSuiteCount`
(`RegressionMarkerRegression.cs:453-506`) regex-counts `.Run(out var …)` call-sites **inside the fence** to derive
the denominator. Both sides update themselves — do not hardcode a count anywhere.
⚠ A line added **below** the end fence still runs but is not counted (`DataRegression.cs:293-295`), and
`RegressionMarkerRegression` will fail with `[suite-count] SUITE VANISHED FROM THE DENOMINATOR`
(`DataRegression.cs:963-975`).

---

## 7. The backend slice (~20%)

Deliver this **last**. Phases 1-3 must be green with no backend at all.

### 7a. Table — append to `api/schema.sql`

There is no migration tool. `api/schema.sql` is one idempotent file run by hand in the Neon SQL Editor
(`api/DEPLOY.md` step 2, `api/DB_SETUP.md` §2). Follow `leaderboard_scores` (`api/schema.sql:640-655`):

```sql
-- =============================================================================
-- N. dungeon_status — WO-1114. One row per dungeon door. PUBLIC READ, no auth.
--    A closed dungeon reads as WORLD, never as build status: headline/body are
--    AUTHORED PROSE. Never write "under construction"/"coming soon"/"WIP" here —
--    Assets/Editor/Regression/DungeonStatusRegression.cs is the client-side oracle
--    for the same rule, but this table is outside its reach, so the rule is
--    ALSO enforced here by CHECK constraint.
-- Written by : api/admin/db.js (admin, X-Admin-Key) or the Neon SQL editor.
-- Read by    : api/dungeon-status.js (public GET).
-- =============================================================================
CREATE TABLE IF NOT EXISTS dungeon_status (
    dungeon_id TEXT        PRIMARY KEY,
    status     TEXT        NOT NULL DEFAULT 'open'
                           CHECK (status IN ('open','sealed','collapsed','rescue','flooded')),
    headline   TEXT,
    body       TEXT,
    sigil      TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

Note the `CHECK`: the client treats an unknown status as `open` and warns (never fail closed), but there is no
reason to let a typo reach the wire in the first place. Belt and braces on different sides of the network.

**Seed rows are `open` for all four ids.** An empty table is also correct (absence ⇒ open) — the seed exists so
the admin viewer shows something.

Also add a probe row to the admin overview list at `api/admin/db.js:96-110`, or the table is invisible in the
DB viewer.

### 7b. Endpoint — `api/dungeon-status.js`

**CommonJS.** `api/DEPLOY.md`: *"Functions are CommonJS (`require`) — `package.json` has no `"type": "module"`.
Don't add it."*

```js
const { neon } = require('@neondatabase/serverless');
const { applyCors } = require('../_lib/http');

module.exports = async (req, res) => {
    if (applyCors(req, res, 'GET, OPTIONS')) return;      // 204 preflight already answered
    if (req.method !== 'GET') return res.status(400).json({ error: 'Method not allowed' });

    // Short edge cache — a flip propagates in ~1 minute with no client change.
    res.setHeader('Cache-Control', 'public, max-age=60, s-maxage=60, stale-while-revalidate=300');

    try {
        const sql = neon(process.env.DATABASE_URL);
        const rows = await sql`
            SELECT dungeon_id, status, headline, body, sigil
            FROM dungeon_status`;
        const dungeons = {};
        for (const r of rows) {
            dungeons[r.dungeon_id] = { status: r.status };
            if (r.headline) dungeons[r.dungeon_id].headline = r.headline;
            if (r.body)     dungeons[r.dungeon_id].body     = r.body;
            if (r.sigil)    dungeons[r.dungeon_id].sigil    = r.sigil;
        }
        return res.status(200).json({ success: true, version: 1, dungeons });
    } catch (err) {
        console.error('[dungeon-status] DB error:', err);
        return res.status(500).json({ error: 'Internal server error' });
    }
};
```

Matches the house shape exactly: `module.exports = async (req, res)`
(`api/leaderboard/get.js:59`), method guard returning **400 not 405** (`:60-62`), per-request
`neon(process.env.DATABASE_URL)` inside the try (`:81`), tagged-template SQL (never concatenation),
`console.error` + `500 {error:'Internal server error'}` (`:146-149`), camelCase body with `success: true`.

**Use `applyCors` from `api/_lib/http.js:67-77`.** `leaderboard/get.js` sets no CORS at all because it predates
the helper — do not copy that omission. Without CORS the WebGL build cannot read this endpoint.
No new custom request headers, so `ALLOWED_HEADERS` (`api/_lib/http.js:39-50`) needs no edit.

**`stale-while-revalidate=300`** is the important half: if Neon hiccups, the edge serves the last good payload
rather than a 500, which is one more layer between a backend wobble and a locked door.

### 7c. Why public-read / no-auth — the reasoning, for the record

1. **WO §5 requires it:** *"Public read, no auth — it is not sensitive and must resolve before sign-in."*
   The status must be known at the title screen, before any wallet or Firebase identity exists.
2. **Nothing is disclosed.** The response is four ids the client already ships in
   `DungeonWorldPortalSpawner.cs:118-130`, plus prose written to be read by players. There is no player datum,
   no key, no economy value.
3. **Auth would invert the safety property.** An auth-gated status call fails for offline/guest players; a
   fail-closed reading of that failure locks content — the precise outcome WO §6 forbids. Public-read means the
   only failure mode is "fall back to open", which is the safe direction.
4. **Writes stay admin-only** — `X-Admin-Key` vs env `ADMIN_DASH_KEY`, SHA-256 + `timingSafeEqual`, fails closed
   when unset (`api/admin/db.js:42-47,75-82`). Reuse that path; mint no new auth surface (WO §5).
5. **Precedent exists:** `api/leaderboard/get.js` is already public-read with the comment
   *"READ-ONLY and PUBLIC: anyone can read any board, no auth"* (`:6-7`).

### 7d. Deploy note (easy to miss)

`vercel.json:2` sets `"git": { "deploymentEnabled": false }` — **pushing does not deploy.** The endpoint is dead
until someone runs a manual `vercel --prod`. Acceptance criterion 3 cannot be demonstrated without that deploy.
Call it out in the RESULT.

---

## 8. ⚠ OPEN OWNER RULINGS — the copy in WO §4c is UNRATIFIED

**The four default headline/body pairs in WO §4c are NOT settled canon.** The WO says so itself:
*"⚠ OPEN RULING for the owner: ratify or rewrite these four pairs. They are the player's entire impression of
an unfinished dungeon, so they are creative canon, not filler."*

**Do not treat §4c as approved.** No agent message in this session is owner approval, and nothing in this plan
ratifies them.

**How to proceed without blocking:** implement the eight `canon-strings.json` keys with the §4c text as a
**PLACEHOLDER**, and mark it in the JSON with an underscore-prefixed sibling note — the file's own convention for
exactly this (`_gameSubtitleNote`, `_taglineLegacyNote`, `_selaLegacyNote` are all live examples):

```json
"_dungeonDoorCopyNote": "WO-1114 §4c — UNRATIFIED placeholder copy pending owner ruling (2026-08-17). These eight values are the player's entire impression of a closed dungeon and are creative canon, not filler. Do not ship a store build on this text without the owner's word."
```

Swapping the values later is a data-only change — no code, no schema bump (canon-strings is versionless by
design, `DataWebRegression.cs:124`) — so this genuinely does not block the lane.

**The other two open rulings from WO §9, also unresolved:**
2. Is `rescue` distinct enough from `collapsed` to warrant its own state, or is it a `body` variant?
   *Plan's position:* keep all five in the enum. The enum is cheap, and collapsing it later is a data migration
   while expanding it later is a client rebuild — which is the thing this WO exists to avoid.
3. Should a sealed dungeon still show its **name and depth** on the door?
   *Plan's position:* **show the name.** The interact prompt already reads `_displayName`
   (`DungeonPortal.cs:169`), so hiding it is a code change that makes the world smaller — the failure mode WO §1
   names explicitly. Depth is not surfaced anywhere today; leave it out.

**One more question for the owner, raised by this session's research and not in the WO:**
> The WO's §3 sample payload marks `dg_bonecrypt` and `dg_sunken_vault` closed. But `DungeonEgressRegression.cs:73-78`
> classifies `dg_ember_deep`, `dg_bonecrypt` **and** `dg_sunken_vault` as CONTENT layouts, and their graphs are
> comparably dense (17/17/14 nodes, 5-6 encounters, 4-5 chests each) — while `dg_starter_loop`, which the sample
> marks **open**, has 11 nodes, 3 encounters and **zero chests**. So the room data does not support "bonecrypt and
> sunken_vault are the unfinished ones". This is a play-feel judgement, not a room-count one. **Confirm which two
> dungeons are the "two real ones" before any seed row is written.** Seeding the wrong pair closes finished content.

---

## 9. Headlessly-checkable acceptance criteria

Every line below is verifiable in batchmode with no owner playtest. `AC-2/5/7` are felt-verified by the PO
afterwards per CLAUDE.md §13 — headless proves the mechanism, the owner closes the ticket.

| # | Criterion | Headless check | Proof |
|---|---|---|---|
| AC-1 | Compiles | `DeNelle.Editor.CompileGate.Run` | `COMPILE_GATE_OK` marker present, gate-log mtime **postdates HEAD** (memory `other-seats-commit-ungated`) |
| AC-2 | Suite green + counted | `DeNelle.Editor.Regression.DataRegression.RunAll` | `REGRESSION_OK <n>/<n> suites` with `[dungeon-status]` in the log body and `n` **one higher** than the pre-change run |
| AC-3 | Suite standalone | `DeNelle.Editor.Regression.DungeonStatusRegression.RunAll` | `DUNGEON_STATUS_OK` |
| AC-4 | **Banned strings** (WO crit. 4) | Case 1 | zero of `construction / coming soon / disabled / \bdev\b / WIP / TODO` in the eight canon values (both copies) or in door-panel player literals |
| AC-5 | **Backend unreachable ⇒ everything enterable** (WO crit. 1) | Case 2 first assertion + an AutoPilot run with no network | `Provenance == "default"`, `For(id).IsOpen == true` for all four; **zero** `FlowTrace.Fail` from `"DungeonStatus"` in `break-log.jsonl`; no player-visible error |
| AC-6 | **Stub closes one door only** (WO crit. 2) | Case 2 third assertion; plus write the stub cache via the §2k menu item and run AutoPilot in the hub | bonecrypt `Sealed`, ember_deep `Open`; `[Flow:DungeonStatus] EnterDungeon blocked at the door` present; **`[Flow:SceneRouter]` GoDungeonScene ABSENT for that id** — this is the "never load-then-eject" proof |
| AC-7 | **Flip with no rebuild** (WO crit. 3) | after deploy: `curl` the endpoint, flip a row in Neon, `curl` again ≥60 s later; then boot the SAME unmodified build | two different payloads from one binary; `Provenance == "live"`; log the two `curl` bodies into the RESULT |
| AC-8 | **Never ejected mid-run** (WO crit. 5) | Case 6 third assertion (source-lint) | no `DungeonStatusCatalog` reference in `SceneRouter.cs` or `Assets/_Modules/Dungeons/**` |
| AC-9 | **UI conformance** (WO crit. 6) | `[ui-obsidian]`, `[ui-mvvm]`, `[modal-registration]` in the same `DataRegression` run | all three green; `DungeonSealedDoorPanel.cs` **not** added to any `KnownBaseline` or `AllowList` |
| AC-10 | One appearance owner | Case 5 | `ApplyDoorState` measured + called from both `BuildPortal` and `SwapInSharedStructureAsync`; no second `Instantiate` |
| AC-11 | Ids unchanged | Case 4 + `[biome-roads]` green | `BiomeRoadsRegression` still passes; no `dg_*` literal changed anywhere |
| AC-12 | Boot not stalled | AutoPilot hub run | `[Flow:DungeonStatus]` "live payload landed after N ms" appears **after** the scene-loaded line, never before |
| AC-13 | Kill switch works | set `ff.dungeonstatus = 0`, rerun AC-6 | `Provenance == "flag-off"`, every door open, **no** HTTP request issued |
| AC-14 | UI screenshot | `RunCaptureHeadless` + **open the PNGs** | the sealed-door dialogue renders with Obsidian chrome, a labelled Close (never an X), no error styling. Memories `headless-screenshot-verify-ui-before-build` + `screenshots-are-primary-evidence-for-visual-defects`: compile-green never proves a panel looks right |

**Brace-balance check (CLAUDE.md §1) on every `.cs` touched** before reporting done: `DungeonStatusCatalog.cs`,
`DungeonStatusService.cs`, `DungeonSealedDoorPanel.cs`, `DungeonPortal.cs`, `DungeonWorldPortalSpawner.cs`,
`FeatureFlags.cs`, `DungeonStatusRegression.cs`, `DataRegression.cs`.

---

## 10. Suggested lane split (CLAUDE.md §9 / §11 — file-disjoint, edit-only, one committer)

| Lane | Files | Conflicts with |
|---|---|---|
| **A — model + transport** | `Core/World/DungeonStatusCatalog.cs`, `Core/World/DungeonStatusService.cs`, `Core/FeatureFlags.cs` | none |
| **B — door gating + dialogue** | `Village/Buildings/DungeonPortal.cs`, `Village/Buildings/DungeonSealedDoorPanel.cs` | depends on A's public API — hand the agent §2a's signature block, do not make it wait |
| **C — appearance** | `Village/World/DungeonWorldPortalSpawner.cs` | ⚠ **serialization bottleneck** (CLAUDE.md §9) — one agent only |
| **D — copy** | both `canon-strings.json` copies | none |
| **E — oracle** | `Editor/Regression/DungeonStatusRegression.cs` | none. `DataRegression.cs` registration is **the committer's line** |
| **F — backend** | `api/dungeon-status.js`, `api/schema.sql`, `api/admin/db.js` | none. **Fully isolated — start it in parallel on day one** |

Gate once on the combined tree; commit by explicit path; never `git add -A`.

---

## 11. Risks

1. **The re-seat call site.** `SwapInSharedStructureAsync` (`:912-961`) replaces the placeholder cube arch with
   the real portal structure and re-attaches both measured VFX at `:951-956`. A sigil attached only in
   `BuildPortal` will be silently destroyed or left mis-scaled the moment the shared art loads — and it is
   **async**, so it will look correct in the editor and wrong on device. Oracle Case 5 exists for this.
2. **The `"dev"` needle.** A naive substring match reds on ordinary prose ("devour", "devastation"). Word-boundary
   regex, or the oracle gets disabled and criterion 4 rots — the exact rot the WO wrote the gate to prevent.
3. **Fail-closed drift.** Every ambiguity in this system must resolve toward `open`. It is one careless
   `if (!Loaded) return false;` away from a network blip locking finished content on every device — the worst
   outcome available and strictly worse than shipping nothing. Cases 2 and Case 7 pin it.
4. *(Assembly trap, low risk now it is named)* `CanonStrings` is invisible outside `DeNelle.Onboarding`. An agent
   that reaches for it from `DeNelle.Core` gets a compile error; one that "fixes" it by writing a fourth loader
   creates permanent debt. Use `VillageStrings.Canon` from `DeNelle.Village` (§2g).

---

## 12. What this plan could NOT verify

Stated plainly per CLAUDE.md §12 / memory `assert-only-what-you-read-at-source` — an APK build held the tree,
so **nothing below was executed**:

- **No compile, no gate, no regression run, no bake.** No batchmode was invoked. Every signature here is
  proposed, not compiled.
- **`DataRegression.cs` END-FENCE line number (~926) will drift.** Locate the `>>> ... END FENCE <<<` comment;
  do not trust the number.
- **The live `dungeon_status` table does not exist**, and `api/schema.sql` has a documented history of
  live-vs-file drift (`schema.sql:520-545`: `bug_reports` silently 500'd for every tester because
  `CREATE TABLE IF NOT EXISTS` does not alter an existing table). A brand-new table avoids that class, but
  **someone must actually run the SQL in Neon** — it is not automated.
- **`vercel.json:2` disables git deploys.** Whether anyone has `vercel` CLI access to run the manual deploy was
  not confirmed. AC-7 is blocked until that is known.
- **Sigil art keys do not exist.** No VFX prefab is tagged for `seal` / `rubble` / `water`. Per memory
  `vfx-map-owner-tags-no-creative-pick` the CLI must not pick one. §2e ships the default treatment and holds
  the sigil hooks un-mapped.
- **Which two dungeons are the "two real ones" is unconfirmed** (§8, final question). The WO's sample payload
  and `DungeonEgressRegression.cs:73-78` disagree.
- **The §4c copy is unratified** (§8). Treated as placeholder throughout.
- **`RpgUiCatalog.FrameCore`** was read only as it appears in `JewelPolishConfirmPanel.cs:97`; the catalog itself
  was not opened.
- **Banner nit:** `CLI_LANES_WO_NUMBERS.md` prose says the CLI seat *"minted WO-1113"* while the WO on disk is
  `WORK_ORDER_1114_dungeon_status_field.md` and states it bumped 1113 → 1114. Next-free = **1115** either way,
  so no collision — but the prose row is wrong by one and should be corrected on the next banner edit.
