# WORK ORDER 190 — Fresh WebGL Build on Current Green Tree

**Status:** READY TO IMPLEMENT
**Lane:** Build/Infra — CLI, batchmode (editor CLOSED). No gameplay code change.
**Source:** owner — wants a shareable web link; existing `Builds/WebGL/` is ~1 day stale (predates the WO-173 world fix).
**Priority:** P1 — unblocks the itch.io deploy (see `DEPLOY_WEBGL_ITCH_GUIDE.md`).

## Goal
Produce a fresh, green WebGL build from the current branch (`feat/tower-core-loop`, green through `475b04a` —
includes the WO-173 terrain fix) so the web link shows the world, not the old void build.

## Steps
1. Confirm on `feat/tower-core-loop`, clean tree, latest commit (`475b04a` or newer), and the project COMPILES green first (don't WebGL-build a red tree).
2. **Editor must be CLOSED** (no project lock). Run the existing pipeline:
   - `build-webgl.ps1` (repo root) → calls `Assets/Editor/WebGLBuild.cs` in batchmode. Use the established
     command/flags already in that script. Do NOT hand-roll a new build path.
3. Output target: `Builds/WebGL/` (overwrite the stale build). Expect IL2CPP + **Brotli (.br)** compression,
   `index.html` at the WebGL root, `Build/WebGL.data.br` + `.wasm.br` + `.framework.js.br` + `.loader.js`,
   `TemplateData/`, `StreamingAssets/`.

## Acceptance
- `Builds/webgl-build.log` shows `BuildResult.Succeeded`, return code 0, no errors.
- `Builds/WebGL/index.html` present; `Build/*.br` present and Brotli-compressed; total size logged (expect ~150–190 MB).
- The two `vercel.json` header files still present (`Builds/WebGL/vercel.json` + root). Don't remove them.
- Note the final total size + the `WebGL.data.br` size in the RESULT (the .data size decides whether Vercel
  is viable vs itch.io-only — see the guide).
- (Optional but ideal) smoke-test the build loads in a browser locally before handing off — confirm it boots
  to the title/village, terrain renders (not void), basic input works. Note any in-browser-only issues
  (touch input, audio-after-gesture, CORS) for follow-up.

## Do NOT
- Change gameplay code, scenes, or assets. This is a build-only order.
- Bake scenes (the village/world bake already landed on this tree).

## Gate
Build green; commit only if build artifacts are tracked per repo policy (the WebGL build output is large —
follow existing `.gitignore`/LFS rules; do not `git add -A` the 180 MB `.data.br` unless that's the established
practice). Write `WORK_ORDER_190_webgl_rebuild_current_tree.RESULT.md` with the final sizes + smoke-test notes.
