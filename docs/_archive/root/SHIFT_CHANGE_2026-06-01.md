# Shift-Change Notes — 2026-06-01 (CLI gatekeeper session)

**Branch:** `feat/tower-core-loop` — **ahead 2, NOT pushed** to `origin`.
**Seat:** CLI gatekeeper (sole committer). Owner offline (driving home).
**No player build was cut this session.**

---

## 1. What landed (2 commits, both compile-gated green)

- **`0a55f69` feat(build): Player Build Mode P0+P1 + WO-127 tower-manage fix**
  - WO-108 **P0+P1 only**: `PlacedStructureData` (in `Core.State` — Core can't ref Village), `GameState.BaseLayout`, SaveSchema **v13→v14** + v13→v14 migrator step, `BuildMode/` module (PlacementGrid, BuildModeController, code-built BuildPaletteUI, GhostPreview, PlacedStructure, BaseLayoutLoader). Reuses CatalogRegistry/StructureFactory/TowerPlacementSystem — not greenfield. Charges AFTER commit from persisted wallet (WO-131). **P2 (move/sell/upgrade) + P3 (server-auth seam) DEFERRED.**
  - WO-127: BuildMenu upgrade screen retargeted `Building`→ live `Tower`, real `Upgrade()`, hidden at MaxLevel.
  - Tooling: `Assets/Editor/CompileGate.cs` (`DeNelle.Editor.CompileGate.Run` logs `COMPILE_GATE_OK`) — the authoritative headless compile check.

- **`c7ea1bf` feat(onboarding): WO-133 first-run FTUE + WaveManager first-run gate**
  - `OnboardingFlow` placed in `VillageSceneBuilder` (UIDocument sortingOrder 100), 5 seams wired at runtime by new `OnboardingIntegrator` (reflection bridge, attached by VillageController), code-built coach-mark overlay fallback (UXML doesn't render in builds), `WaveManager.Start()` gated on `!IsFirstRun()`.
  - **Bake-verified**: re-baked `Village.unity` via `VillageSceneBuilder.BuildVillage`; log shows "OnboardingFlow (FTUE) placed", scene re-saved + NavMesh rebaked. NavMesh.asset committed alongside.
  - WO-126 Bug 2 (Farm) confirmed already-clear (Z=14, no-op); material repair runs in-bake.

- **WO-135** (P1 bug cluster) — verified **already on branch** from a prior session; no-op. Queue was stale.

---

## 2. Uncommitted working-tree state — LEAVE AS-IS unless you know why

The FTUE bake left side-effects I deliberately did NOT commit (kept commits focused):
- `Assets/Scenes/OuterWorld.unity` — `BuildExterior` side-effect of BuildVillage.
- `Assets/Prefabs/Village/Generated/Building_workshop.prefab`, `Enemy_HollowWalker.prefab` — pre-existing mods from a prior session.
- `ProjectSettings/ProjectSettings.asset`, `TowerLoopDevHarness.cs`, `WORK_ORDER_169_*.md`, plus untracked `Assets/Action/*`, `Assets/Art/Crystals/*`, new audio mp3s, `.claude/` — all pre-existing, not mine.
- LFS texture pointer noise (Heroes/Black Dragon `.png/.jpg`) — **NEVER `git add -A`** (mass-converts to LFS pointers). Stage by explicit path only.

3 leftover agent worktrees under `.claude/worktrees/agent-af707…`, `agent-a2dd8…`, `agent-ac29d3…` — safe to `git worktree remove` when convenient.

---

## 3. LANDMINE caught this session (don't repeat)

`run-unity-method.ps1` **must be called directly** — do NOT wrap it in an `if/else`/inline guard in the same PowerShell call. The first FTUE bake did that and **silently no-op'd** (logged "complete" but never wrote the scene; its `Builds/` log was never created). Verify a bake by: (a) `Builds/<log>` exists, (b) the expected placement log line, (c) target scene **mtime advanced**, (d) git shows the scene modified — **NOT** the wrapper exit code (Unity forks; exit code is a false signal).

---

## 4. Next actions (recommended order)

1. **Cut a Windows playtest build** (`build-windows.ps1`, editor closed) so Tricia can verify FTUE + Build Mode end-to-end. Per the playtest card convention, write plain-language steps. THEN push (ahead-2).
2. **WO-108 P2** — move / sell (50% refund) / rotate-edit of placed structures (the rest of the CREATE verb).
3. **WO-191 Phase 1** — WebGL mesh decimation + fresh WebGL→itch build.
4. **Owner-blocked (need Samantha):** backend go-live (`npm i tweetnacl bs58` → Neon `schema.sql` → deploy → **rotate exposed Neon credential** → flip `BackendAuthConfig.Enforced`); WO-190 character roster (owner decimates in Blender); gate-ward visual; wall-extent (±28/±21 vs ±32/±24) + commerce-tier values.

---

## 5. Verification gate that worked (reuse it)
Agents write code in worktrees (STEP 0: `git merge feat/tower-core-loop --no-edit` — worktrees fork stale) → signal READY. Gatekeeper: copy files Windows-native (never the mount) → brace + junk scan → `CompileGate.Run` batchmode (editor CLOSED; judge by marker + 0 `error CS`) → for builder edits, `BuildVillage` bake + verify scene mtime → commit code-only by explicit path. `python3` is NOT installed; use bash brace counts.
