# WORK ORDER 09 — RESULT (WebGL build SUCCEEDS; render-quality + browser smoke are the follow-up)

## UPDATE 2026-05-25 (overnight) — module installed, build SUCCEEDED
- Installed **WebGL Build Support** for 6000.4.8f1 via Unity Hub CLI (`--headless install-modules -m webgl`, exit 0).
- Ran `build-webgl.ps1`: **`[WebGLBuild] SUCCEEDED — 310 MB in 00:34:45`** → `Builds/WebGL/index.html` + `Build/` + `StreamingAssets/` + `TemplateData/`. **The project's first-ever WebGL build works** (IL2CPP + Brotli compile clean, no build errors). AC1 ✅.
- ~310 MB on disk (uncompressed dir); the Brotli `.data` payload vs the ≤300 MB cap (AC5) still needs measuring — it's right at the line, so a texture-downscale pass (§2.5) is likely needed.
- **NOT done (require a browser + a cross-build-affecting edit — controlled, not unattended):**
  - §2.2 URP shader-stripping (add Always-Included URP/Lit/SimpleLit/Unlit + ForceFieldGate to `GraphicsSettings.asset`; uncheck Strip-Unused-Variants on `DeNelle-URP.asset`). Needed so KayKit/Tripo shaders don't magenta in WebGL2 — but it grows the Windows+Android builds 50–100 MB (commit separately, per the hard rule) and its only proof is an actual in-browser render, so I did **not** make this blind cross-build edit unattended.
  - §2.3 WebGL code paths (audio-after-first-gesture, `UnityWebRequest` CORS/screenshot, SeekerBootstrap `#elif UNITY_WEBGL`).
  - §2.4/§0.3 browser smoke (Chrome + Firefox: village renders no-magenta, WASD, abilities, audio-after-click) — needs eyes-on a browser.
  - §2.5 Brotli payload measurement + texture downscale if >300 MB.
- **Next (controlled run):** apply §2.2 → rebuild (~35 min) → `cd Builds/WebGL; python -m http.server 8000` → open `http://localhost:8000` and run the §2.4 smoke. The infra makes each build a one-liner now.

---

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025 — at owner's explicit ask (the WO otherwise defers to Phase 5 / post-WO-17 overnight).
**Original outcome (now superseded by the UPDATE above):** Build infrastructure (§2.1) written + compiles; build was blocked on the WebGL module not being installed.
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
