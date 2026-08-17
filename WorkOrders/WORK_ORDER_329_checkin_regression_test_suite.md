> ⚠ **NUMBER COLLISION — this document does not own WO-329; `WORK_ORDER_329_pet_deploy_timing.md` does.**
> Referred to hereafter as **WO-329-B (check-in regression test suite)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

# WORK_ORDER_329 — Check-in regression test suite (UI static gate + CLI Unity tests + manual QA)

**Status: READY TO IMPLEMENT** (scaffolds authored by agent; CLI build-verifies the C# tests)
**Branch:** feat/tower-core-loop · **Lane:** 0 (Verify/build) · **Origin:** owner request 2026-06-06
**Reconcile with:** CLAUDE.md §1 (brace gate), `DeNelle.Editor.CompileGate`, `run-unity-method.ps1` / `build-windows.ps1` / `ship-webgl.ps1`, `QA_CHECKLIST_FILLED.md`

## Goal
A regression suite **both teams run on every check-in**: UI/Cowork (Linux sandbox, no Unity) runs the static
gate; CLI (Windows, Unity) runs the full gate (compile + Unity tests + build). One entry point, clear pass/fail.

## Deliverables
1. **Static gate (UI-runnable, no Unity)** — `tools/regression/static_gate.(py|sh)`:
   brace-balance on all/changed `.cs`; forbidden-pattern checks (new `System.Reflection` in bridge scripts;
   missing `using DeNelle.Core.Combat;` in `IDamageableStructure` implementers; raw `Resources.Load` where
   Addressables expected); asmdef boundary check (Village/HUD → Core only); JSON validity for `Assets/Data/Canonical/*`.
2. **Full gate (CLI-runnable, Windows+Unity)** — `tools/regression/checkin_gate.ps1`:
   runs the static gate, then `DeNelle.Editor.CompileGate.Run` (expect `COMPILE_GATE_OK`), then Unity
   EditMode + PlayMode tests (`-runTests -testPlatform`), then optional `build-windows.ps1`. Prints a summary + exit code.
3. **Unity Test Framework tests (CLI build-verifies):**
   - EditMode (pure logic): EconomyService grant/spend + `TerritoryMultiplier`; `AnimParams` hashes/`Dead` latch;
     GameState save/load roundtrip (+ wallet-keyed roster, WO-301); catalog JSON parse (weapons/armor/recipes).
   - PlayMode smoke: Village scene loads; hero spawns non-null + has Animator/controller; `WaveManager.ForceBeginNextWave()`
     advances a wave (WO-327); no NullReferenceException during a short headless run (guards WO-328).
   - New `DeNelle.Tests` asmdef(s) referencing the assemblies under test.
4. **Manual QA regression checklist** — `tools/regression/MANUAL_QA_CHECKLIST.md`: the visual/play items that
   can't run headless (no T-pose/backwards walk, HUD readable, build preview isolated, DTT grounded, etc.),
   extending `QA_CHECKLIST_FILLED.md`.
5. **Docs** — `tools/regression/README.md`: how each team runs it on check-in; what blocks a merge.

## Acceptance criteria
- [ ] UI can run the static gate in the Linux sandbox and get a clear pass/fail (no Unity needed).
- [ ] CLI can run the full gate on Windows: static → CompileGate → Unity tests → (optional) build, with one exit code.
- [ ] EditMode + PlayMode tests compile + run green under Unity Test Framework (CLI verifies).
- [ ] Manual QA checklist covers the non-headless items.
- [ ] README documents the check-in workflow for both teams.
- [ ] Test C# brace-checked + CompileGate OK; no edits to gameplay `.cs` or `.unity`.

## Do NOT touch
- No gameplay `.cs` changes, no `.unity` edits. Tests are additive (own asmdef). Don't fork the existing ps1 scripts — call them.
