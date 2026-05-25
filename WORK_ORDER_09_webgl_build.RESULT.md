# WORK ORDER 09 — RESULT (PARTIAL — infra scaffolded; build blocked on the WebGL editor module)

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025 — at owner's explicit ask (the WO otherwise defers to Phase 5 / post-WO-17 overnight).
**Outcome:** Build infrastructure (§2.1) is written and **compiles**, so a WebGL build is one command away — **once the WebGL Build Support editor module is installed.** That module is **not** installed, which hard-blocks the actual build (§2.4) and everything that depends on observing it (§2.2 shader-stripping, §2.3 WebGL code paths, §2.5 size, smoke test). Those are deliberately left for the post-install run.
**Editor:** Unity 6000.4.8f1

---

## Preflight (§0)

| Check | Result |
|---|---|
| `build-webgl.ps1` exists | was ❌ → **created** |
| `Assets/Editor/WebGLBuild.cs` exists | was ❌ → **created** |
| `Builds/WebGL/index.html` exists | ❌ (never built for WebGL) |
| **WebGL Build Support module** (`…/PlaybackEngines/WebGLSupport`) | ❌ **NOT installed** ← hard blocker |

Per preflight, infra was missing → full WO. I executed the **non-destructive, no-side-effect** part (§2.1 infrastructure) and stopped at the module-gated work.

## What I did (§2.1 — scaffolding, committed)

- **`build-webgl.ps1`** — mirrors `build-windows.ps1`: WebGL target, `Builds/WebGL` output, `DeNelle.Editor.WebGLBuild.BuildWebGL` entry point, 60-min deadline (first IL2CPP compile), success = `index.html`, plus an early warning if the WebGL module is absent. Parses clean.
- **`Assets/Editor/WebGLBuild.cs`** — `[MenuItem("Defenders/Build/WebGL Player")]` + headless entry, applying the WO's Player Settings: IL2CPP (mandatory), `WebGLCompressionFormat.Brotli`, `memorySize = 512`, `exceptionSupport = None`, `dataCaching = true`, `ManagedStrippingLevel.Minimal` for WebGL, `runInBackground = false`. **Compiles** against the editor API even without the module (the `PlayerSettings.WebGL` / `BuildTarget.WebGL` types live in `UnityEditor.dll`).

These are additive new files — they do **not** touch `GraphicsSettings.asset`, the URP profile, or any other build target.

## What I did NOT do (gated on the module + verification)

- **§2.4 the actual build** — `BuildPipeline.BuildPlayer(WebGL)` fails immediately without the module. Can't run.
- **§2.2 URP shader-stripping** (add Always-Included shaders, uncheck Strip Unused Variants) — this edits `ProjectSettings/GraphicsSettings.asset` + `Assets/Settings/DeNelle-URP.asset`, which the WO's own hard rule warns **grows Windows + Android builds 50–100 MB** and must be committed separately. I will not make that cross-build change *blind* — its whole purpose is to fix magenta-in-WebGL, which can only be confirmed by actually building+running WebGL. Deferred to the post-install run.
- **§2.3 WebGL code paths** (audio-after-first-gesture, `UnityWebRequest` CORS/screenshot, SeekerBootstrap `#elif UNITY_WEBGL`) — best applied + verified together with a real build.
- **§2.5 size, §0.3/§2.4 browser smoke test, §2.6 docs** — need a build to exist.

## Acceptance criteria

| AC | Status |
|---|---|
| 1. `build-webgl.ps1` exits 0 + `index.html` | ⛔ blocked (module not installed) — infra ready |
| 2–5. browser load / no-magenta / input+audio / ≤300 MB | ⛔ blocked (no build yet) |
| 6. `docs/port-notes/webgl-build.md` | ☐ post-build |
| 7. small focused commits | ✅ (infra) |
| 8. this RESULT.md | ✅ |

## Owner action to unblock

1. **Install WebGL Build Support** for 6000.4.8f1:
   - Unity Hub → Installs → 6000.4.8f1 → ⚙ Add Modules → **WebGL Build Support**, **or**
   - `"C:\Program Files\Unity Hub\Unity Hub.exe" -- --headless install-modules --version 6000.4.8f1 -m webgl`
2. Then `.\build-webgl.ps1` (30–60 min first run). If it produces `Builds/WebGL/index.html`, proceed with §2.2 shader-stripping (commit separately), §2.3 code paths, and the browser smoke test — ideally overnight per the WO's mode.

This stays consistent with the plan (WO-09 is the last item, Phase 5); the scaffolding just removes the §2.1 setup time from that future run.
