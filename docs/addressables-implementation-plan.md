# Unity Addressables — Implementation Plan
**Project:** Defenders of the Realm (Unity 6 LTS, URP)
**Owner:** DeNelle Studios
**Date:** 2026-05-28
**Status:** Implementation-ready

---

## ⚠ CURRENT PACKING LAW (2026-08-20) — supersedes anything below about grouping

The 2026-05-28 plan below describes a group layout the project never built. **The live
grouping is `Assets/AddressableAssetsData/AssetGroups/*.asset`; read it there, never here.**
What IS canon, by owner ruling 2026-08-20 ("I want this broken down to each family of
enemy" / "i want the structures one at a time"):

| Group | `BundleMode` | Result |
|---|---|---|
| `Enemy_Art` | `PackTogetherByLabel` (2) | ONE bundle per enemy FAMILY, keyed by an `enemyfam-*` label |
| `Structure_Art` | `PackSeparately` (1) | ONE bundle per structure asset |

**Why:** both were `PackTogether`, which is why the built files read
`enemy_art_assets_all_*.bundle` (64.45 MiB) and `structure_art_assets_all_*.bundle`
(19.71 MiB). Under PackTogether the first Hollow Skirmisher a player meets pulls all
64 MiB and the first hut pulls all 19.7 MiB. After the split a Hollow costs
19.26 MiB + a 0.48 MiB shared bundle, and a building costs 0.14–2.58 MiB.

**⛔ THE FAMILY LABELS ARE DERIVED, NOT TYPED.** `ContentPackingSetup.FamilyMap()` reads the
`family` / `modelKey` pairing out of `Assets/Resources/Data/Canonical/enemies.json`. Add a
family to that JSON and the grouper picks it up; do **not** hand-maintain a family list in a
second file (same drift class as the stale WO-number block, CLAUDE.md §2).

**⛔ RE-PACKING NEVER CHANGES AN ADDRESS.** Addresses are the contract —
`structures-catalog.json` authors them verbatim as `repo.visualPrefabPath` /
`repo.upgradeVisualPath` and the loaders resolve that exact string. Pinned by
`Assets/Editor/Regression/ContentPackingRegression.cs` (`CONTENT_PACKING_OK`), which also
fails if either group is reverted to `PackTogether`.

**KNOWN COST, MEASURED (not inherent to the split):** total content went
105,176,098 → 112,008,819 bytes (+6.5%). The Addressables build-layout report attributes
effectively all of it to THREE shaders — URP `Lit.shader`, URP `FallbackError.shader` and
core `FallbackShader.shader` — being implicit (non-addressable) dependencies, so each of the
now-37 bundles embeds its own copy. `Lit.shader` alone is 7.96 MB across 37 copies, 7.75 MB
of which is duplication. **FIX (open):** register those shaders as their own Addressable
entry so they land in one bundle; that is a NEW address and changes no existing one.

---

## 0. Golden Rules

1. **If it's not needed on game start, it should be in Addressables.**
2. **Skins never share a group with base assets.**
3. **Every handle opened must be released.** Memory leaks in Addressables are silent and cumulative.
4. **Never hardcode address strings in game code.** Use `AssetReference` fields on `AddressablesGroupConfig` ScriptableObjects instead.
5. **Remote catalog is the live-content lever.** Adding a new skin or tower never requires a new build — only a new catalog + bundle upload.

---

## 1. Group Catalog — Full Specification

| Group Name | Content | Load Mode | Build Type | Compression | Notes |
|---|---|---|---|---|---|
| `Core-Essential` | GameStateService, Camera, Input, essential prefabs | **Startup** | Local | LZ4 | Keep < 5 MB |
| `UI-Core` | Persistent HUD, PackStore frame, loading screen | **Startup** | Local | LZ4 | < 3 MB |
| `UI-Menus` | Inventory, Skin Browser, Settings, Dungeon Map | On Demand | Remote | LZ4 | High-use, cache aggressively |
| `UI-Debug` | DebugCanvasUI, AddressablesMemoryProfiler | **Editor Only** | Local | None | Stripped from player builds |
| `Towers-Base` | Base tower prefabs (all 4 types at L1–L3) | On Demand | Remote | LZ4 | Load when village scene opens |
| `Towers-Skins` | All tower skin variants | On Demand | Remote | LZ4 | Load only on equip / skin menu open |
| `Towers-Empowerment` | Empowerment nova + aura VFX prefabs | On Demand | Remote | LZ4 | Load when tower reaches L3 |
| `Pets-Base` | Base pet models (flame-pup, aether-sprite, etc.) | On Demand | Remote | LZ4 | Load when pet is deployed |
| `Pets-Skins` | Pet skin variants | On Demand | Remote | LZ4 | Load only on equip |
| `Heroes` | Hero models + all skins (Blaise, others) | On Demand | Remote | LZ4 | Highest individual memory cost |
| `VFX` | All VFX prefabs registered in VFXCatalog | On Demand | Remote | LZ4 | Batch-load at wave start |
| `Audio-Music` | Music tracks (.mp3 / .ogg) | On Demand | Remote | Streaming | Never decompress fully into RAM |
| `Audio-SFX` | Short SFX clips | On Demand | Remote | LZ4 | Pre-warm common clips at scene load |
| `Dungeons` | Dungeon scene assets, room prefabs | On Demand | Remote | LZ4 | Load on dungeon entry |
| `Marketplace` | Pack artwork, store UI assets | On Demand | Remote | LZ4 | Load when PackStore opens |

### Pack Separately policy
Groups `Towers-Skins`, `Pets-Skins`, `Heroes`, and `Towers-Empowerment` must be set to **Pack Separately** (one bundle per asset) so individual skins can be downloaded without pulling unrelated content.

---

## 2. Remote Catalog — Setup & Maintenance

### 2.1 Initial Setup (Addressables Settings window)

1. Open **Window → Asset Management → Addressables → Settings**.
2. Under **Profile Variables**, set:
   - `RemoteLoadPath` → your CDN root (e.g., `https://cdn.denellestudios.com/dotr/{BuildTarget}`)
   - `RemoteBuildPath` → `ServerData/{BuildTarget}`
3. Enable **Build Remote Catalog** ✓
4. Set **Catalog Build Path** → `Remote`
5. Enable **Use Asset Bundle Cache** ✓
6. Set **Max Concurrent Web Requests** → `5` (desktop) / `3` (mobile)
7. Enable **Disable Catalog Update on Startup** for Editor speed; **disable** it in production builds.

### 2.2 Content Update Workflow (adding a new skin)

```
1. Add new skin asset to appropriate group (e.g., Towers-Skins)
2. Run: Addressables → Build → Update a Previous Build
3. Upload ServerData/{BuildTarget}/ contents to CDN:
     catalog_YYYY.MM.DD.HH.mm.hash.json   ← new catalog
     catalog_YYYY.MM.DD.HH.mm.hash.bin
     *.bundle                              ← only changed bundles
4. Players receive new content on next app launch (catalog diff download)
```

No new player build required. New tower skins, pet skins, and VFX prefabs ship this way.

### 2.3 Vercel CDN (current infrastructure)

Since the project already uses Vercel for the backend (`defenders-of-the-realm` repo), serve Addressables bundles from Vercel's edge network via a static route:

```
/public/addressables/{BuildTarget}/*.bundle
/public/addressables/{BuildTarget}/catalog_*.json
/public/addressables/{BuildTarget}/catalog_*.bin
```

Set `RemoteLoadPath` to `https://<your-vercel-domain>.vercel.app/addressables/{BuildTarget}`.

Add to `vercel.json`:
```json
{
  "headers": [
    {
      "source": "/addressables/(.*)",
      "headers": [
        { "key": "Cache-Control", "value": "public, max-age=31536000, immutable" },
        { "key": "Access-Control-Allow-Origin", "value": "*" }
      ]
    }
  ]
}
```

Bundles are content-addressed by hash — `immutable` cache is safe and maximally efficient.

---

## 3. Memory Profiling — Tools & Strategy

### 3.1 Addressables Profiler (built-in)

Open **Window → Asset Management → Addressables → Event Viewer** (Unity 6 uses Event Viewer; earlier versions had a standalone Profiler window). It shows:
- All `AsyncOperationHandle` lifecycles
- Asset load / unload events
- Bundle download progress
- Reference count per asset

**Workflow:**
1. Enable **Send Profiler Events** in Addressables Settings ✓
2. Enter Play Mode, navigate through scenes and menus
3. Open Event Viewer and look for assets with **reference count > 0** after their scene unloads
4. Any asset with a persistent non-zero refcount that should have been unloaded = **leak**

### 3.2 Memory Profiler Package

Install via Package Manager: `com.unity.memoryprofiler`

Useful workflow:
```
1. Enter Play Mode, load a skin, navigate away, do NOT manually release
2. Take a snapshot (Memory Profiler → Capture)
3. Search for the skin's texture/mesh — if present, refcount > 0 = leak
4. Add Addressables.Release() to the unequip / scene-unload path
```

### 3.3 Runtime Leak Guard (AddressablesMemoryProfiler.cs)

See `Assets/_Modules/Core/Addressables/AddressablesMemoryProfiler.cs`.

This component:
- Tracks all handles opened via `SkinController` (and any caller that registers with it)
- Logs a warning every 30 seconds listing handles open longer than 5 minutes
- Integrates with `DebugCanvasUI` (F12 overlay) to show live handle count
- Editor-only by default; can be toggled on in development builds

### 3.4 Key Memory Rules

- **Release on unequip.** `SkinController.RemoveSkin()` calls `Addressables.Release(handle)` immediately.
- **Release on scene unload.** Use `SceneManager.sceneUnloaded` or `OnDestroy` on scene controllers.
- **Do not cache handles indefinitely.** Store the `AsyncOperationHandle<T>` in the component that owns the asset, not in a global dictionary.
- **Batch release.** When the player leaves the village scene, release all VFX and enemy handles. Re-load fresh when returning.
- **Audio streaming.** Music tracks use `Addressables.LoadAssetAsync<AudioClip>` with `AudioClipLoadType.Streaming` — these decompress on-the-fly and consume minimal RAM.

---

## 4. SkinController Architecture

See `Assets/_Modules/Core/Addressables/SkinController.cs` for full implementation.

### Interface

```csharp
public interface ISkinnable
{
    void ApplySkin(SkinController.SkinTarget target);
    void RemoveSkin();
    string CurrentSkinAddress { get; }
}
```

`SkinTarget` is a plain struct carrying the Addressables address string + skin type enum.

### Usage pattern

```csharp
// Equip skin on a tower
var skin = _tower.GetComponent<SkinController>();
await skin.ApplySkinAsync("Towers/BlastTower_GoldenAge");

// Unequip (releases handle immediately)
skin.RemoveSkin();
```

### Supported skin asset types (in priority order)

| Asset Type | Apply Method |
|---|---|
| `Material` | Swap `renderer.sharedMaterial` |
| `Texture2D` | Set `_BaseMap` on existing material clone |
| `GameObject` | Swap visual child (mirrors Tower.ApplyVisualForLevel) |
| `Mesh` | Swap `meshFilter.sharedMesh` |

### Adding SkinController to prefabs

1. **Tower prefab**: Add to the root Tower GameObject. `SkinController.SkinSlot = "body"`.
2. **Pet prefab**: Add alongside `Pet.cs`. `SkinSlot = "body"`.
3. **Hero prefab**: Add to the HeroBody child. `SkinSlot = "body"`.
4. For towers with separate muzzle / base skins: add two `SkinController` components, one per `SkinSlot`.

---

## 5. AddressablesGroupConfig — No Hardcoded Strings

See `Assets/_Modules/Core/Addressables/AddressablesGroupConfig.cs`.

Instead of:
```csharp
// BAD — hardcoded, refactor-hostile, no Inspector validation
var handle = Addressables.LoadAssetAsync<GameObject>("Towers/BlastTower_GoldenAge");
```

Use:
```csharp
// GOOD — Inspector-assigned AssetReference, refactor-safe
[SerializeField] private AssetReferenceGameObject _towerSkinRef;
var handle = Addressables.LoadAssetAsync<GameObject>(_towerSkinRef);
```

The `AddressablesGroupConfig` ScriptableObject acts as a typed registry: one instance per group, stored in `Assets/Configs/Addressables/`. Systems that need to load assets hold a `[SerializeField]` reference to the relevant config asset.

---

## 6. VFX Group — Batch Pre-warm Strategy

VFX prefabs are loaded in a batch at wave-start rather than one-by-one on first use (which causes a hitch on the first tower shot). Wire this in `WaveManager.OnWaveStart`:

```csharp
private List<AsyncOperationHandle<GameObject>> _vfxHandles = new();

private async Task PrewarmVFXAsync()
{
    // Load all VFX by label instead of individual addresses.
    // Label "vfx-combat" covers projectiles, impacts, cast effects.
    var handle = Addressables.LoadAssetsAsync<GameObject>(
        "vfx-combat", obj => VFXCatalog.Register(obj));
    await handle.Task;
    _vfxHandles.Add(handle);
}

private void OnWaveEnd()
{
    foreach (var h in _vfxHandles) Addressables.Release(h);
    _vfxHandles.Clear();
}
```

Assign the label `"vfx-combat"` to all entries in the `VFX` Addressables group.

---

## 7. Audio Group — Streaming + On-Demand

Music clips are large and should never sit fully decompressed in RAM.

```csharp
// AudioService.cs — async music load
private async UniTask<AudioClip> LoadMusicAsync(string address)
{
    var handle = Addressables.LoadAssetAsync<AudioClip>(address);
    await handle.Task;

    var clip = handle.Result;
    // AudioClip.loadType should be set to Streaming in the import settings
    // for all music assets in the Audio-Music group.
    return clip;
    // IMPORTANT: store handle on AudioService; release when track changes.
}
```

SFX clips are small — pre-warm common clips (victory, build, upgrade) at scene load, release at scene unload.

---

## 8. Implementation Order

Execute in this sequence to avoid breaking existing builds:

1. **Set up Addressables profiles** — create `Local` and `Remote` profiles in AddressableAssetSettings.
2. **Create groups** per §1 catalog table — start with `Core-Essential` and `UI-Core` (startup) first.
3. **Move assets into groups** — towers and pets first (highest gameplay impact).
4. **Add `SkinController` to prefabs** — test equip/unequip memory with Event Viewer.
5. **Add `AddressablesGroupConfig` ScriptableObjects** — one per group; replace hardcoded strings.
6. **Wire VFX pre-warm** into WaveManager — test that first tower shot has no load hitch.
7. **Configure remote build path** — test a full content update with a dummy skin change.
8. **Enable `AddressablesMemoryProfiler`** in development builds — let it run a full session and check the F12 overlay for leak warnings.
9. **Upload to CDN (Vercel)** — verify catalog download, bundle download, and cache-hit on second launch.

---

## 9. Open Questions / Owner Decisions

- [ ] CDN domain — confirm Vercel subdomain or custom domain for `RemoteLoadPath`
- [ ] Initial download gate — should players be prompted before downloading the Heroes bundle (~50 MB)?
- [ ] Skin unlock gating — should `SkinController.ApplySkinAsync` validate ownership first, or does the call site handle that?
- [ ] Empower VFX prefab source — are the nova/aura prefabs already authored? If so, assign them to `Towers-Empowerment` group now.
- [ ] Audio streaming import setting — all music assets need `AudioClipLoadType.Streaming` set in the importer (not just in code).
