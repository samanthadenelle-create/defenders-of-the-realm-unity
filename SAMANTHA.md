# SESSION BOOT CONFIRMATION — hand this to the CLI before ANY work (owner: paste this first)

You are a fresh CLI session inheriting a live, mid-flight operation. **Do NOT write, edit,
build, assign, or fix ANYTHING until you have completed every item below and reported the
results to the owner in the format at the bottom.** The owner is explicitly measuring
whether you confirm state before acting — skipping ahead IS the failure being tested.

## STEP 1 — READ, in this order (no skimming; you will be quizzed by reality)
1. `CLAUDE.md` (repo root) — the non-negotiable rules. Note §11 orchestration, §12
   instrument-don't-guess, §15 canon maintenance, the preflight gate.
2. `SESSION_CANON_LOADER.md` — the SME primer.
3. Your auto-memory index — especially `pm-sme-pipeline-org-model` (the owner's org model:
   PM assigns/QA-checks, 6+ SMEs parallel, CLI = sole gate/fleet/commit hands, image-pair
   sign-off) and `follow-canon-orchestrate-not-solo-guess`.
4. `RESUME_2026-07-03_pipeline-handoff.md` — the live state of everything (wave 1 committed,
   in-flight lanes, landmines, next steps, the Fable benchmark you are being compared against).
5. `docs/UI_COVERAGE_MATRIX_2026-07-03.md` — the definition of done (76 rows × 4 cells).
6. `docs/PM_BOARD_2026-07-03.md` — the job board (NW-7..NW-18 briefs, risk register).

## STEP 2 — VERIFY the handoff's claims against reality (run these; paste the evidence)
- [ ] `git log --oneline -15` shows the wave-1 lane commits `e37c16d2..a97dc4d8` (PackStore,
      build-mode, Title/StoryIntro, HeroSelect, EndState, deletions) + the handoff/benchmark
      docs commits. Branch = `wip/village2-and-f8-tickets`, ahead of origin, PUSH HELD.
- [ ] `git status --porcelain` shows ONLY the deliberate leftovers (~14 paths: link.xml
      deletion, QA_F8_ARCHIVE/, _opener_frames/, docs zips, open-wos.txt, rename_armor_images.py,
      tools/AudioGen/, ProjectSettings/Packages/, Action/Economy metas) — PLUS possibly the
      tutorial-gap fixer's edits (Tutorial/V2, TutorialWaveSpawner, FeatureFlags,
      TutorialSignalAdapters) if that agent's work landed before the session ended. Anything
      ELSE dirty = investigate before touching.
- [ ] `Builds/Windows/DefendersOfTheRealm.exe` exists (BUILD_OK 15:48 2026-07-03) — the
      owner's Fable-end-state benchmark exe. Do not overwrite it until she has felt-tested,
      or copy it aside first.
- [ ] `Builds/compile-gate.log` tail contains `COMPILE_GATE_OK` (the committed tree compiles).
- [ ] `Assets/HeroPackages/Knight/Animations/Extracted/` contains ~61 `.anim` files (the
      Knight package — assets committed but NOT wired into the runtime hero; 3 owner
      decisions gate integration, listed in the handoff).
- [ ] The tutorial-gap fixer status: check whether `Assets/_Modules/Core/FeatureFlags.cs`
      ff.tutorialv2 default is ON or OFF, and whether TutorialFlow.cs implements the
      prepaidTower grant (vs the old no-op FlowTrace note at ~:221). If the fixes are absent,
      that agent died with the old session — re-brief it from the handoff's in-flight section
      + `docs/ONBOARDING_FLOW_AUDIT_2026-07-03.md`. If present but uncommitted: gate → commit.

## STEP 3 — CONFIRM the operating rules you are bound by (answer YES + one line each)
1. You only surface to the owner at 100% of the matrix or for owner-only decisions.
2. Nothing reaches her without the IMAGE PAIR (runtime bot capture + Blink reference).
3. You are the ONLY hands on batchmode + git; agents are edit-only, briefed with the
   landmine list (ASCII-only TMP glyphs; asmdef needs UnityEngine.UI + Unity.TextMeshPro
   when a module gains uGUI/TMP; no UIDocument/PanelSettings; arbiter battle-lock revert
   pattern; verify-before-delete with ref-grep).
4. Batchmode refuses while ANY Unity.exe runs (even her other project) — if the editor is
   open, arm an editor-close watcher; never collide.
5. Pipeline, not batch-and-wait: next wave implements WHILE bots verify the last.
6. §12: no fix without captured data; the preflight gate fires on every .cs edit — answer it
   honestly every time.
7. Her open decisions (defeat-screen secondary button; Knight gap-fill + package-shape
   — the BODY is DECIDED: Paladin is the new hero body, owner ruling 07-03;
   water taste slices; death-clip direction mapping) are HERS — do not decide them.

## STEP 4 — REPORT BACK, then WAIT
Post to the owner: (a) each STEP 2 checkbox with its actual evidence line, (b) any mismatch
between the handoff and reality (a mismatch is a finding, not a blocker — name it), (c) the
next mechanical step you propose (should be: verify/land the tutorial fixes, then run the
conveyor — proof-chain build → fleet → captures → image pairs → commit → next wave from the
PM board). **Then wait for her go.** Her first reply may just be "go" — but the confirmation
report must exist first. That report is also her Fable-vs-Opus comparison datum: make it
exact, make it honest, and do not pad it.
