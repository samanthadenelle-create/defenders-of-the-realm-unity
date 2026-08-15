# WORK ORDER 974 — The Addressables content build has no seam; it rides a machine-local Editor Preference

**Status:** DONE — AddressablesContentBuild.EnsureBuilt on all player builds; abort on fail (2026-08-15).
**Lane:** Build path / Addressables
**Minted:** 2026-08-10 (CLI), from an architect verification ordered by the owner:
*"make sure that addressables are implemented as supposed to be. Have an architect read and verify."*
**Verdict from that audit:** **CORRECT-BUT-FRAGILE.** Safe to ship *from this machine*; this WO and
WO-975 are the two defects that bite a different machine or CI.

---

## 1. The defect

`Assets/AddressableAssetsData/AddressableAssetSettings.asset:61`

```
m_BuildAddressablesWithPlayerBuild: 0
```

Read at package source rather than assumed —
`Library/PackageCache/com.unity.addressables@…/Editor/Settings/AddressableAssetSettings.cs:210-215`:

```
PlayerBuildOption.PreferencesValue = 0   // "use the global settings stored in preferences"
```

So the value `0` does not mean "don't build content". It means **"ask this machine's Editor
Preferences"** — a setting that is not in the repo, not in version control, and different per seat.

And there is **no explicit content build anywhere to compensate.** All three build entry points call
`BuildPipeline.BuildPlayer` and nothing else:

- `Assets/Editor/WebGLBuild.cs:127`
- `Assets/Editor/DesktopBuild.cs:241`
- `Assets/Editor/AndroidBuild.cs:105`

No `BuildPlayerContent` call exists anywhere under `Assets/`.

## 2. Why this is dangerous *because* it currently works

On this machine the preference is evidently ON — the 2026-08-10 builds emitted fresh bundles into
`Builds/WebGL/StreamingAssets/aa/` (`catalog.bin`, `catalog.hash`, `settings.json`,
`AddressablesLink/link.xml`, six `WebGL/*.bundle`, ~15 MB, incl. `gear_assets_all_*.bundle` at
15,056,364 bytes).

That is luck, not construction. On a fresh clone, a CI runner, or any seat that ever toggled the
preference, the player ships **stale or absent** `StreamingAssets/aa` — and **nothing fails loudly.**
The build succeeds, the marker prints, the APK/WebGL uploads. Addressables simply cannot resolve
`gear/*` at runtime, and the failure surfaces as missing weapons/armour in-game, far from its cause.

This is the same class as every other bug this project has paid for twice: a green marker that
asserts something it never checked.

## 3. Fix (either, prefer B)

**A.** Set `m_BuildAddressablesWithPlayerBuild: 1` (`BuildWithPlayer`) so the seam lives in the repo
instead of a preference.

**B. (preferred)** Add an explicit `AddressableAssetSettings.BuildPlayerContent(out var result)` at
the head of each of the three build entry points, and **log its result** — bundle count, total bytes,
and a `FlowTrace.Fail` if it returns an error. B is preferred because it makes the content build
*visible in the build log*, which is what turns the next occurrence into a one-read diagnosis rather
than a field report.

## 4. Deliberately not landed in the release window

This was found during the 2026-08-10 overnight release chain (APK → Firebase → WebGL production). It
is a **build-path change**, and changing the build path inside a release window is how you lose the
ability to say what shipped. Land it at the start of a session, then do a full build to prove it.

## 5. Acceptance criteria

- [ ] Content build is invoked explicitly (or by committed setting), not by an Editor Preference.
- [ ] Build log states bundles built + total size for WebGL, Desktop and Android.
- [ ] A build failure in the content step **fails the build** — it must not print a success marker.
- [ ] Verified by deleting `Library/com.unity.addressables` (or the local bundle output) and
      confirming a clean build regenerates `StreamingAssets/aa` without touching any preference.
- [ ] Brace balance + 0 NUL bytes on every `.cs` touched (§1, §0).

## 6. What is NOT wrong (so nobody "fixes" it)

The rest of the setup is coherent and should be left alone:

- Five groups, all on the **Local** profile pair —
  `Local.BuildPath = [Addressables.BuildPath]/[BuildTarget]`,
  `Local.LoadPath = {Addressables.RuntimePath}/[BuildTarget]`
  (`AddressableAssetSettings.asset:78-81`).
- `Remote.LoadPath` is literally `<undefined>` (`:84-85`) and `m_BuildRemoteCatalog: 0` (`:20`) —
  **nothing remote, no CDN, no catalog update on start.**
- **The WebGL load path is correct** and does *not* repeat the `File.ReadAllText`-throws-in-WebGL
  mistake: `Addressables.RuntimePath` resolves to `Application.streamingAssetsPath`, which under
  WebGL is an **HTTP URL fetched by UnityWebRequest**, not a filesystem read.
- **No dual-loading.** Addressables owns gear/armor/skins/hero textures; `Resources/` owns canonical
  JSON + hero art. Disjoint — the `Resources/Data/Canonical` dual-copy law is not violated.

Minor, harmless, but worth knowing: `Assets/_Modules/Core/Addressables/AddressablesGroupConfig.cs:49-180`
declares ~40 `AssetReference` fields (towers, pets, VFX, pack-store UXML) but **no `.asset` instance
of it exists**, and `Default Local Group` has **0 entries**. It reads as configured content that is
not actually wired to anything.

## 7. Related

- **WO-975** — the `Gear` group points at a gitignored art pack. Same audit, same blast radius; that
  one is the *content*, this one is the *seam*.
- WO-545 / WO-282 (heroes out of `Resources/` into Addressables) never landed, which is why the WebGL
  `.data` is 165 MB. That is a load-time problem, not a ship blocker — see the corrected
  `docs/webgl-hosting-notes.md`.
