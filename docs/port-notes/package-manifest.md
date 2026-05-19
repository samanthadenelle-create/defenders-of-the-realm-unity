# Package Manifest Spec — Defenders of the Realm v2 (Unity port)

**Target editor:** Unity `6000.4.7f1` (Unity 6.0 LTS) — confirmed from
`ProjectSettings/ProjectVersion.txt`.
**Render pipeline:** Universal Render Pipeline (URP). Mobile-first; HDRP out of scope.
**API compatibility level:** .NET Standard 2.1 (required by the Solana Unity SDK — see
`v2-unity-port-spec.md` §2 / §Player settings).
**Date of research:** 2026-05-18.

This document specifies the exact `Packages/manifest.json` for the project. It is a
*spec only* — applying it is a separate build step.

---

## 1. Research method & a caveat on sources

`WebSearch` / `WebFetch` were **not available** in this environment (permission
denied), so live registry pages and the Solana SDK GitHub README could not be fetched
directly. Instead, the **eight official Unity registry/bundled packages** were pinned
from the **authoritative editor package manifest** shipped inside the installed editor:

```
C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\Resources\PackageManager\Editor\manifest.json
```

That file is Unity's own "which package version is validated for this exact editor
build" list (`metadataPackageName: com.unity.package-manager.metadata-6000.4`). Every
version below for a `com.unity.*` package is copied verbatim from it — these are the
**editor-validated** versions for `6000.4.7f1` and carry the lowest compatibility risk.

The three **non-Unity / external** packages (Cinemachine 3.x, UniTask, Solana Unity
SDK) are specified from established package knowledge as of 2026-05. **Each is flagged
below as REQUIRES VERIFICATION** — before running `Package Manager`, confirm the exact
current version against the live source listed in §6. The git-URL form auto-resolves to
the repo's default branch when no `#tag` is appended, so the manifest will still
function if a version has moved; pinning a tag is preferred for reproducibility.

---

## 2. Ready-to-paste `manifest.json`

Replace the project's `Packages/manifest.json` with the following. It keeps every
built-in module Unity 6 scaffolds by default (omitting them would uninstall engine
modules) and adds the port's required packages plus the two scoped registries.

```json
{
  "dependencies": {
    "com.unity.render-pipelines.universal": "17.4.0",
    "com.unity.cinemachine": "3.1.4",
    "com.unity.inputsystem": "1.19.0",
    "com.unity.localization": "1.5.8",
    "com.unity.addressables": "2.9.1",
    "com.unity.timeline": "1.8.12",
    "com.unity.test-framework": "1.6.0",
    "com.unity.testtools.codecoverage": "1.3.0",
    "com.unity.nuget.newtonsoft-json": "3.2.2",
    "com.unity.ugui": "2.0.0",
    "com.cysharp.unitask": "2.5.10",
    "com.solana.unity_sdk": "https://github.com/magicblock-labs/Solana.Unity-SDK.git",

    "com.unity.modules.accessibility": "1.0.0",
    "com.unity.modules.adaptiveperformance": "1.0.0",
    "com.unity.modules.ai": "1.0.0",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.assetbundle": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.cloth": "1.0.0",
    "com.unity.modules.director": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.particlesystem": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.physics2d": "1.0.0",
    "com.unity.modules.screencapture": "1.0.0",
    "com.unity.modules.terrain": "1.0.0",
    "com.unity.modules.terrainphysics": "1.0.0",
    "com.unity.modules.tilemap": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.umbra": "1.0.0",
    "com.unity.modules.unityanalytics": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.unitywebrequestassetbundle": "1.0.0",
    "com.unity.modules.unitywebrequestaudio": "1.0.0",
    "com.unity.modules.unitywebrequesttexture": "1.0.0",
    "com.unity.modules.unitywebrequestwww": "1.0.0",
    "com.unity.modules.vectorgraphics": "1.0.0",
    "com.unity.modules.vehicles": "1.0.0",
    "com.unity.modules.video": "1.0.0",
    "com.unity.modules.vr": "1.0.0",
    "com.unity.modules.wind": "1.0.0",
    "com.unity.modules.xr": "1.0.0"
  },
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.cysharp.unitask"
      ]
    }
  ]
}
```

> **Notes on what changed vs. the current project manifest**
> - Current `Packages/manifest.json` is a bare Unity 6 scaffold: only built-in modules
>   plus `com.unity.multiplayer.center`. **None** of the port's required packages are
>   installed yet — this manifest adds all of them.
> - `com.unity.multiplayer.center` (`1.0.1`) was dropped: it is an editor-only
>   multiplayer onboarding helper, not needed for this single-player port. Harmless to
>   keep if preferred; re-add `"com.unity.multiplayer.center": "1.0.1"` if so.
> - `com.unity.ugui` `2.0.0` is added explicitly. In Unity 6 the legacy `TextMeshPro`
>   package is **deprecated and merged into `com.unity.ugui`**; the port spec uses
>   UGUI world-space canvases and TextMeshPro text, so this package must be present.
> - The modules block is copied verbatim from the existing scaffold so applying this
>   manifest does not uninstall any engine module. `com.unity.modules.hierarchycore`
>   and `com.unity.modules.subsystems` are pulled in transitively (they show in
>   `packages-lock.json` at `depth: 1`) and do not need to be listed.

---

## 3. Per-package notes — the 8 Unity registry/bundled packages

All versions below are taken verbatim from the editor manifest for `6000.4.7f1`. Risk
is **Low** unless noted.

| Package | Version | Notes |
|---|---|---|
| `com.unity.render-pipelines.universal` | `17.4.0` | URP for Unity 6.0 LTS. URP major version tracks the editor (17.x = Unity 6). Editor-validated. Pulls in `com.unity.render-pipelines.core` `17.4.0` and `com.unity.shadergraph` `17.4.0` transitively. After install, create a URP asset + renderer and assign it under Graphics + Quality settings (mobile renderer with the post-processing the audio/visual specs need). |
| `com.unity.inputsystem` | `1.19.0` | New Input System. On first install Unity prompts to set **Active Input Handling**; choose **Both** (or **Input System Package**) and let the editor restart. Required for the `PlayerInput` action map (WASD/joystick, Q/W/E/R hotkeys, tap-to-move, pinch-zoom) per spec §2. |
| `com.unity.localization` | `1.5.8` | Unity Localization. Backs `StringTable.en` for tooltips and canon strings. Depends on Addressables (satisfied below) and `com.unity.addressables.android` is *not* required unless using Play Asset Delivery. |
| `com.unity.addressables` | `2.9.1` | Addressables 2.x — the Unity 6 line (1.x is the pre-Unity-6 line). Mandatory for KayKit GLTF loading and per-scene dungeon streaming. Pulls in `com.unity.scriptablebuildpipeline` `2.6.1`. Configure `Local` + `Remote` profiles per spec §2. |
| `com.unity.timeline` | `1.8.12` | Timeline for the studio bumper + title cinematic. Depends on `com.unity.modules.director` (already in modules block). |
| `com.unity.test-framework` | `1.6.0` | Unity Test Framework — supports **both EditMode and PlayMode** test assemblies in one package (no separate package needed). Pulls in `com.unity.ext.nunit`. Needed for `SchemaTests.cs` and the per-data-file schema tests in spec §Data layer. |
| `com.unity.testtools.codecoverage` | `1.3.0` | Optional but recommended companion to UTF for coverage reports in CI. Drop this line if coverage is not wanted. |
| `com.unity.nuget.newtonsoft-json` | `3.2.2` | Newtonsoft.Json, Unity's official UPM redistribution. Chosen over `JsonUtility` per the decisions log (2026-05-22) — handles nested generic dictionaries + polymorphic types in the data files. This is the **registry** package; do **not** drop a raw `Newtonsoft.Json.dll` into `Assets/` (causes duplicate-assembly errors, since Addressables also depends on this package). |

**Cinemachine is intentionally NOT in this table** — see §4, it is a special case.

---

## 4. Cinemachine — version caveat (REQUIRES VERIFICATION)

The port spec (§2 and the decisions-log review-triggers list) explicitly requires
**Cinemachine 3.0+** (the new component-based `CinemachineCamera` API), used for the
village FreeLook rig, the dungeon follow-cam, and ATB battle framing.

**Caveat:** the editor `6000.4.7f1` *bundles* Cinemachine **`2.10.7`** (the legacy
`CinemachineVirtualCamera` API) — that is the only Cinemachine `.tgz` shipped in the
editor, and the editor manifest's recommended `version` field reads `2.10.7`. The
editor manifest's `version` is the *bundled* version, **not** the latest available on
the registry. Cinemachine **3.x is a separate, fully Unity-6-compatible registry
release** and must be pinned explicitly in `manifest.json` (as done in §2) — it will
**not** appear as the default in Package Manager.

- Manifest entry used: `"com.unity.cinemachine": "3.1.4"`.
- **VERIFY** the current Cinemachine 3.x version before applying: open Package Manager →
  Unity Registry → Cinemachine → see the latest `3.x` offered for Unity 6, or check the
  registry (§6). As of 2026-05 the 3.1.x line is current; if Package Manager offers a
  newer `3.x`, prefer it.
- Cinemachine 2.x → 3.x is an **API-breaking** change (`CinemachineVirtualCamera` and
  `CinemachineFreeLook` are replaced by `CinemachineCamera` + composable components).
  All camera code in the port must be written against the 3.x API from the start —
  do not follow 2.x tutorials.
- Cinemachine 3.x is a normal Unity registry package — **no scoped registry, no git
  URL** required. It just needs the explicit version pin.

---

## 5. External packages — not from the Unity registry

### 5.1 UniTask (`com.cysharp.unitask`) — OpenUPM scoped registry (REQUIRES VERIFICATION)

UniTask (Cysharp) is **not** on the Unity registry. Two supported install methods:

1. **OpenUPM scoped registry** *(recommended — used in §2)*. UniTask is published to
   OpenUPM as `com.cysharp.unitask`. Adds a `scopedRegistries` entry pointing at
   `https://package.openupm.com` scoped to `com.cysharp.unitask`, then a normal
   versioned dependency. This is the approach in the §2 manifest:
   - dependency: `"com.cysharp.unitask": "2.5.10"`
   - scoped registry: the `package.openupm.com` block in §2.
2. **Git URL** *(alternative — no scoped registry needed)*. UniTask's `package.json`
   lives in a subfolder of its GitHub repo, so the git URL must include that path:
   ```
   "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask"
   ```
   Append `#2.5.10` (or the current tag) to pin a version. Use this only if the
   OpenUPM registry is undesirable.

- **VERIFY** the current UniTask version (the `2.5.x` line is current as of 2026-05;
  confirm via OpenUPM or the GitHub releases page in §6) and update both the version
  string and any pinned tag.
- UniTask is source-only, has **no dependencies**, and is Unity-6 compatible. It
  supplies `async UniTask` for wallet calls, scene loads, and Addressables loads per
  spec §"async UniTask for async flows".
- If the OpenUPM method is used, the `scopedRegistries` block **must** be present or
  Package Manager cannot resolve `com.cysharp.unitask`.

### 5.2 Solana Unity SDK — Git URL (REQUIRES VERIFICATION)

The port spec (§2) names "the official Solana Unity SDK from
`solana-mobile/solana-unity-sdk`" and instructs the agent to **fetch current install
instructions from that repo's README at spinup**, because the package URL may have
moved. `WebFetch` was blocked here, so the README could not be retrieved live — the
following reflects the SDK's established install model and **must be re-verified
against the README before applying.**

**Install method: Git URL (UPM), not the Unity registry, not the Asset Store.** The
Solana Unity SDK is distributed as a Git-URL UPM package.

- The `solana-mobile/solana-unity-sdk` repository is the **Solana Mobile-maintained
  entry point**; the SDK package itself is developed under the **`magicblock-labs`**
  org (formerly `garbles-labs`). The actual UPM package commonly resolves to:
  ```
  "com.solana.unity_sdk": "https://github.com/magicblock-labs/Solana.Unity-SDK.git"
  ```
  This is the form used in the §2 manifest. **VERIFY** against the README in §6: the
  README may instead direct you to install via **Package Manager → Add package from
  git URL** with a specific `#vX.Y.Z` release tag, or to download a `.unitypackage`
  release. Append the current release tag (e.g. `#v2.x.x`) for a reproducible build.
- **Unity 6 compatibility:** the SDK targets Unity 2021.3+ and is used on Unity 6 /
  Solana Mobile (Seeker) projects; it provides the **Mobile Wallet Adapter (MWA)** the
  spec needs for Android/Seeker. **VERIFY** the README's stated minimum/maximum editor
  version still includes `6000.x`.
- **.NET Standard 2.1 is REQUIRED.** Set **Project Settings → Player → Other Settings →
  Api Compatibility Level = .NET Standard 2.1** *before* importing the SDK, or it will
  not compile. This matches spec §Player settings ("API compatibility level: .NET
  Standard 2.1 — the Solana Unity SDK's minimum").
- **Dependencies / caveats:**
  - The SDK bundles its own crypto/serialization dependencies (Chaos.NaCl, Solana
    transaction libs, a Newtonsoft.Json dependency). Because the project already
    installs `com.unity.nuget.newtonsoft-json` (§3), watch for a **duplicate
    Newtonsoft assembly** conflict — if the SDK ships its own `Newtonsoft.Json.dll`,
    delete the SDK's copy and let it use the UPM package, or the build will fail with
    a duplicate-assembly error.
  - MWA only functions on physical Android/Seeker devices with a compatible wallet
    app; on iOS and desktop the spec mandates a deep-link wallet fallback.
  - All wallet operations are **devnet-only** in v2 foundation (spec §Wallet, §"Does
    not push real-mainnet").
- This package **cannot** be pinned to a simple semver in the dependency list the way
  registry packages are — it is a git source. It also cannot use the OpenUPM scoped
  registry.

---

## 6. Sources / URLs

Web fetch/search was blocked in this environment; the URLs below are the sources to
**verify against manually** before applying the manifest.

**Authoritative local source actually used (the 8 Unity packages):**
- `C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Data\Resources\PackageManager\Editor\manifest.json`
  — editor-validated package versions for `6000.4.7f1`
  (`metadataPackageName: com.unity.package-manager.metadata-6000.4`).
- `C:\Users\Kayden-Laptop\Documents\defenders-unity\ProjectSettings\ProjectVersion.txt`
  — confirms editor `6000.4.7f1`.
- `C:\Users\Kayden-Laptop\Documents\defenders-unity\Packages\packages-lock.json`
  — current scaffold dependency graph.

**To verify the 8 Unity packages (live):**
- URP: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.4/
- Input System: https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/
- Localization: https://docs.unity3d.com/Packages/com.unity.localization@1.5/
- Addressables: https://docs.unity3d.com/Packages/com.unity.addressables@2.9/
- Timeline: https://docs.unity3d.com/Packages/com.unity.timeline@1.8/
- Test Framework: https://docs.unity3d.com/Packages/com.unity.test-framework@1.6/
- Newtonsoft JSON: https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/

**To verify the 3 external packages (live — DO THIS BEFORE APPLYING):**
- Cinemachine 3.x: https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/ —
  confirm the current `3.x` version offered for Unity 6.
- UniTask: https://github.com/Cysharp/UniTask (releases) and
  https://openupm.com/packages/com.cysharp.unitask/ — confirm current `2.5.x` version.
- Solana Unity SDK: https://github.com/solana-mobile/solana-unity-sdk (README — the
  spec-mandated entry point) and the underlying
  https://github.com/magicblock-labs/Solana.Unity-SDK — confirm the exact git URL,
  current release tag, and stated Unity-version range.

---

## 7. Apply checklist

1. Set **Player → Api Compatibility Level → .NET Standard 2.1** first.
2. **Verify** the three external-package versions/URLs (§4, §5) against §6.
3. Replace `Packages/manifest.json` with the §2 block (adjust the three external
   versions if verification found newer ones).
4. Let Package Manager resolve. Accept the Input System "Active Input Handling" restart
   prompt (**Both**).
5. Create + assign the URP asset and mobile renderer in Graphics/Quality settings.
6. If the Solana SDK ships a bundled `Newtonsoft.Json.dll`, delete it (keep the UPM
   `com.unity.nuget.newtonsoft-json`).
7. Confirm a clean compile, then commit `Packages/manifest.json` +
   `Packages/packages-lock.json` together.

---

## 8. Summary table

| Package | Version | Source | Risk |
|---|---|---|---|
| `com.unity.render-pipelines.universal` | `17.4.0` | Unity registry | Low |
| `com.unity.cinemachine` | `3.1.4` * | Unity registry (explicit pin) | Med — verify 3.x version; 2.x→3.x API break |
| `com.unity.inputsystem` | `1.19.0` | Unity registry | Low |
| `com.unity.localization` | `1.5.8` | Unity registry | Low |
| `com.unity.addressables` | `2.9.1` | Unity registry | Low |
| `com.unity.timeline` | `1.8.12` | Unity registry | Low |
| `com.unity.test-framework` | `1.6.0` | Unity registry | Low |
| `com.unity.testtools.codecoverage` | `1.3.0` | Unity registry (optional) | Low |
| `com.unity.nuget.newtonsoft-json` | `3.2.2` | Unity registry | Low |
| `com.unity.ugui` | `2.0.0` | Unity registry (TMP merged in) | Low |
| `com.cysharp.unitask` | `2.5.10` * | OpenUPM scoped registry | Med — verify version |
| Solana Unity SDK (`com.solana.unity_sdk`) | git `main` (pin a tag) * | Git URL | High — verify URL/tag/Unity-6 support; Newtonsoft dup risk; needs .NET Std 2.1 |

`*` = REQUIRES VERIFICATION against §6 before applying (web fetch was blocked here).
