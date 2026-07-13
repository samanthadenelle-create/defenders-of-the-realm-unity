# Overnight Queue — 2026-06-03 (gatekeeper auto-run)

Owner signed off after the hero-pipeline win. Process this when Unity frees from the
in-flight IL2CPP WebGL build (task bkz3w8v02, 30–60 min). Then ONE bake + ONE build
carries everything below.

## BLOCKED ON: Unity is locked by the wave-1 WebGL build (bkz3w8v02).
Wait for its completion notification (it pushes wave-1 = commit on feat/tower-core-loop:
auto-advance/Glimmer/Knight-color/repair-disc). Verify it pushed to itch, THEN proceed.

## STEP 1 — Integrate wave 2 (wholesale-copy each file from its worktree → main):
| Ticket | Worktree | File(s) | Braces |
|---|---|---|---|
| DEF-235 floating hero | agent-a0bd960fd3bb39fc7 | Assets/_Modules/Village/Hero/HeroBodySwapper.cs | 63/63 |
| DEF-197 store affordability | agent-a7f9bb2127203cfd5 | Assets/_Modules/HUD/CosmeticShopPanel.cs | 95/95 |
| DEF-199 raid alert (NEW) | agent-a6254e207939f09ca | Assets/_Modules/Village/Waves/AlertIntelSystem.cs | 20/20 |
| DEF-239 gate-quest (NEW) | agent-a5083c684eeb48583 | Assets/_Modules/Village/Quests/DailyQuestGateBridge.cs | 16/16 |
| DEF-241 N/S stairs | agent-a48f89d32d9e1e968 | Assets/Editor/VillageSceneBuilder.Fortify.cs | (read its report) |

- HeroBodySwapper (DEF-235) was built ON TOP of the committed DEF-231 Knight-color — it
  contains both; copying it is correct.
- New .cs files have no .meta yet — Unity generates them on import (the gate/bake run).
- Brace-check each after copy.

## STEP 2 — Compile-gate: run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run
  (marker: COMPILE_GATE_OK). Unity must be closed.

## STEP 3 — BAKE (DEF-241 stairs need it): DeNelle.Editor.WorldBakeOrchestrator.BakeFullWorld
  Verify "marked 1 terrain(s)" + the NavMesh marks. (Only DEF-241 needs the bake; the other
  4 are runtime/code.)

## STEP 4 — Commit (explicit paths only, NOT -A — LFS), push, then ship-webgl.ps1 -NoBrotli.

## STEP 5 — Close on push: DEF-235, DEF-197, DEF-199, DEF-239, DEF-241.
  DEF-239 note: quest-3 (bond-rank) still untracked — no runtime bond-up signal exists; left a TODO. Keep that as a separate open gap.

## THEN — keep the silo machine running (owner: "fill backlog always, run in silos"):
Dispatch next non-conflicting code lanes from Backlog, e.g. DEF-189 (node visibility),
DEF-186 (building upgrade panel), DEF-187 (enemy camps), DEF-184/183 (audio) — distinct files,
no VillageSceneBuilder collisions (that's serial). Integrate→gate→build→close, rinse/repeat.

## *** RCA-FIRST RULE (owner mandate 2026-06-03) ***
RECURRING bugs (re-fixed many times) MUST get an EXACT root-cause analysis BEFORE any
implementation — NO surface patches. This applies to: DEF-211 (first-30-sec P0),
DEF-204 (hero-select layout), DEF-155 (walks-backwards), DEF-132 (body-yaw), and ANY
issue labeled Regression or that has reopened.
PROCESS for these: dispatch a READ-ONLY investigation agent (Explore / no edits) that
returns the exact root cause + file:line proof + why prior fixes didn't hold. ONLY after
the root is nailed, dispatch an implementation agent. Do NOT batch-patch them in a silo.
Fresh/new feature tickets (no recurrence) silo normally without the RCA gate.

## FRONT-DOOR RCA RESULTS (2026-06-03 overnight) + sequencing:
- DEF-155 + DEF-132 → CLOSED (RCA: resolved by DEF-232/234 + avatar-bind + camera-relative basis; owner "movement feels good").
- DEF-211 (first-30-sec) → ROOT: SafeStage timeout never CANCELS the cold-open cinematic → 14-beat loop renders over title. Implementation agent a57ca72fa4c6bffe8 RUNNING (TitleController.cs + StoryIntroController.cs): (1) ForceHide cancels token + SetActive(false); (2) cinematic loop early-exits on _cts cancel; (3) BuildTitleScreen nulls _titleDocument.visualTreeAsset after Clear; (4) BuildCards loud-guard if Heroes.Length!=4.
- DEF-204 (hero-select layout) → ROOT: card-row justifyContent=SpaceBetween + flexGrow:1/flexBasis:0/maxWidth:25% + no NoWrap → cards collapse/wrap in portrait. FIX (queue AFTER DEF-211 integrates — SAME FILE TitleController.cs, do NOT run in parallel): card-row justifyContent=Center + flexWrap=NoWrap; ConnectWallet zIndex=100; ReflowForSize also sets detail marginTop; VerifyFourCardsEven auto-rebuilds on mismatch (not log-only).
- SEQUENCE: wave-2 build frees Unity → integrate DEF-211 impl → dispatch DEF-204 impl on top → integrate → compile-gate front-door → build → push → close DEF-211 + DEF-204.

### DEF-211 IMPL DONE (ready to integrate when Unity frees from wave-2 build bi7vmvw0l):
- Worktree agent-a57ca72fa4c6bffe8 → copy 2 files to main:
  - Assets/_Modules/Onboarding/TitleController.cs (103/103)
  - Assets/_Modules/Onboarding/StoryIntroController.cs (45/45)
- THEN: compile-gate → commit DEF-211 → push → dispatch DEF-204 impl (merges DEF-211, edits TitleController.cs: card-row Center+NoWrap, ConnectWallet zIndex=100, ReflowForSize detail-margin, VerifyFourCardsEven auto-rebuild) → integrate → gate → build → push → close DEF-211 + DEF-204.

## Owner-facing on morning: hero pipeline (Tripo→Blender-embed→AccuRIG) is the win; buildings
next via Tripo (no rig) / polyperfect _M to fix the "half farm" town (DEF-240). No budget — free pipeline only.
