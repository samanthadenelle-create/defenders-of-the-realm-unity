**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_323 — Trees render all white (missing material/shader)

**Status: READY TO IMPLEMENT**
**Branch:** feat/tower-core-loop · **Lane:** 1 (World/Env) · **Origin:** owner playtest 2026-06-06 (screenshot)
**Reconcile with:** `Defenders/Art/Fix Polyperfect URP Materials`, `TreeOfLifeMaterialFixer`, material/import pipeline

## Problem
Environment trees render **all white** (untextured / no albedo) — materials aren't resolving to URP (missing
albedo or wrong/standard shader on the tree prefabs). Classic Polyperfect/imported-asset URP material issue.

## Goal
Trees render with correct materials/textures (foliage + trunk), URP-lit, no white/untextured meshes.

## Where to look
- Run/extend `Defenders/Art/Fix Polyperfect URP Materials` (gitignored pack re-import path per CLAUDE.md §4)
  — the trees' materials likely point at a non-URP/standard shader or have no base map.
- If the trees are Quaternius/KayKit (not Polyperfect), apply the equivalent URP material assignment.
- Confirm base color/albedo textures are assigned and the shader is `Universal Render Pipeline/Lit`.

## Acceptance criteria
- [ ] Trees render with proper foliage/trunk materials (URP-lit) — no white/untextured trees.
- [ ] Fix is at the material/import level (re-runnable), not a one-off scene tweak.
- [ ] No regression to other environment materials.
- [ ] Brace check on any .cs touched; CompileGate OK; build SUCCESS; verify in play.

## Root cause (triage 2026-06-06)
**Confidence: Likely (where-to-look correct).** White trees = materials not resolved to URP (no base map /
non-URP shader) on the imported tree prefabs — the classic gitignored-pack re-import issue (CLAUDE.md §4).
There is an existing fixer for the Heart tree only (`Assets/_Modules/Core/TreeOfLifeMaterialFixer.cs`); the
environment trees are not covered by it. This is a material/import fix, not a scene tweak.
**Suggested minimal fix:** run/extend `Defenders/Art/Fix Polyperfect URP Materials` to assign
`Universal Render Pipeline/Lit` + bind the foliage/trunk base maps on the env tree materials (if the trees are
Quaternius/KayKit rather than Polyperfect, apply the equivalent URP assignment). Make it re-runnable at the
material/import level. If it does not touch `VillageSceneBuilder.cs` it can run parallel to Lane 1; if it does,
serialize with the builder. Missing pack → re-import per §4, LogWarning not error.

## Do NOT touch
- No `.unity` hand-edits. This is a material/import fix — if it doesn't touch `VillageSceneBuilder.cs` it can
  run parallel to Lane 1; if it does, serialize with the builder. Missing pack → re-import per §4, LogWarning not error.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
