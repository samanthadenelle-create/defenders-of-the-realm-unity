# WORK ORDER 1315 — The WebGL build built WINDOWS Addressables content. Occurrence FIVE of the sec.16 class.

**Status:** DONE (fixed in this commit; the follow-up guard below is NOT done)
**Silo:** Build / Content
**Minted:** 2026-09-02 (CLI), caught while building the owner's web UI overnight.
**Severity:** P1 — a shipped web build resolving a stale catalog, with every marker green.

## The captured line — this is the whole ticket

From `Builds/webgl-build.log`, a WebGL player build run immediately after a Windows player build:

```
ADDRESSABLES_CONTENT_OK 751 locations :: WebGLBuild target=StandaloneWindows64
  (12.0s -> Library/com.unity.addressables/aa/Windows/settings.json)
```

**A WebGL build reported content success for `StandaloneWindows64`.** Corroborated on disk:
`ServerData/WebGL`'s newest file was still `catalog_2026.08.30.347462.bin` (Aug 30) while
`ServerData/StandaloneWindows64` had been regenerated that morning at 04:43.

## Mechanism

Addressables builds for the **ACTIVE EDITOR TARGET**, not for the target named in
`BuildPlayerOptions`. `WebGLBuild.BuildWebGL` called:

```csharp
if (!AddressablesContentBuild.EnsureBuilt("WebGLBuild"))   // no target, no prior switch
```

That is the **back-compat overload whose own XML doc warns against using it**: *"Prefer the overload
that takes an expected target — WO-1124 exists because a content build that cannot state which
platform it built for shipped Windows bundles inside an Android APK, with every marker in the chain
green."* The WebGL path used the very overload that comment was written to deprecate.

## Why this is occurrence FIVE, not a new bug

WO-1124 fixed **exactly this** on the Android path on 2026-08-19 and **left the WebGL path open**:

| | switches target first | explicit target to EnsureBuilt |
|---|---|---|
| `AndroidBuild.cs:163-176` | YES | `EnsureBuilt("AndroidBuild", BuildTarget.Android)` |
| `WebGLBuild.cs:130` (before) | **NO** | **`EnsureBuilt("WebGLBuild")`** |

A fix applied to one of two symmetric call sites is the same duplicated-state class as CLAUDE.md
sec.2's stale WO block and sec.5's retired dependency table.

## Player impact

The deployed web build would have resolved the **Aug 30** WebGL catalog. It is live on R2 and returns
HTTP 200, so **the game would have run** — with content three days stale, missing the owner's Tripo
watchtower re-point and the Synty structure re-addressing from 2026-09-01. Silent, green, and wrong:
the signature of this entire class.

## Fix applied

`WebGLBuild.cs` now switches the active target to WebGL **before** the content build and passes
`BuildTarget.WebGL` **explicitly**, mirroring `AndroidBuild`. The switch lives in the method, not in
`build-webgl.ps1`, for the reason AndroidBuild records in-code: the whole failure class is assuming a
human step, and in-method it holds for the menu item, batchmode, CI and the ship chain alike.

## ⛔ Follow-up NOT done — the actual hole is the missing guard

The fix repairs the WebGL path. **It does not stop a sixth occurrence on a sixth call site.** What is
missing is a regression that fails when ANY player-build entry point calls the target-less
`EnsureBuilt(string)` overload. That oracle would have caught this without a build, and would catch
the next one. Candidate shape: scan `Assets/Editor/*Build*.cs` for `EnsureBuilt("` with a single
argument, and fail naming the file.

Consider also deleting the `EnsureBuilt(string)` overload outright. Every remaining caller should be
able to state its platform; an overload whose documentation begs you not to use it is a trap that
stays loaded.

## What NOT to touch

- ⛔ Do NOT move the switch out of the method into a wrapper script. That reintroduces the
  assumed-human-step this class is made of.
- ⛔ Do NOT change `Assets/AddressableAssetsData/**` while fixing this.
- ⛔ Do NOT treat `R2_PARITY_OK` as covering it. Parity proves the bucket holds what a catalog names —
  it cannot tell you the catalog was built for the wrong platform. On 2026-08-19 every marker was green.
