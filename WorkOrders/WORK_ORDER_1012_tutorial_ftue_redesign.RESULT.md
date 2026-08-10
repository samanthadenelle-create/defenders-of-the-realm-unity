# WO-1012 RESULT — tutorial / FTUE redesign (P1 + P2 + P3)

**Status:** IMPLEMENTED — owner felt-verify owed
**Landed:** 2026-08-10 (authored by the UI/design seat's sequential pipeline; the two blocker edits in
the lane-fenced `DataRegression.cs` were applied by the orchestrator; verified, gated and committed by
the CLI seat)

## What shipped

The tutorial gets its own presentation kit instead of borrowing HUD pieces, and the guide identity obeys
the owner's amendment to §2a — **"person A never guides person A."**

New, all code-built through `ElarionUiKit` (no UXML):

- `Assets/_Modules/Core/Tutorial/TutorialGuide.cs` — the guide identity seam.
- `Assets/_Modules/Core/UI/ObjectiveStripUi.cs` — the persistent objective strip.
- `Assets/_Modules/Core/UI/GuidePointer.cs` — the ONE moving cue (a gold chevron + a ghost finger).
- `Assets/_Modules/Core/UI/GuideLineUi.cs` — the guide's spoken line.
- `Assets/_Modules/Core/UI/TutorialSkipUi.cs` — ONE skip, not several.
- `Assets/_Modules/Village/Tutorial/V2/TutorialGuideIdentityInstaller.cs` — binds the identity per step.

Changed: `TutorialFlow.cs`, `TutorialWorldAnchors.cs`, `TutorialSignals.cs`, `TutorialStepModel.cs`,
`TutorialHighlightRegistry.cs`, `UiSpotlight.cs`, `ObjectiveBannerUi.cs`, `DialogueViewModel.cs`,
`DialogueView.cs`, `OnboardingFlow.cs`, plus the dual-copy `tutorial-steps.json` and `dialogues.json`
and the wireframes at `UI_REVIEW/tutorial_flow_redesign_wireframes.html`.

## Committer's edits (the two the lane could not make)

`Assets/Editor/Regression/DataRegression.cs` is lane-fenced — nine agents editing it in parallel would
collide — so its two blocking changes were applied by the orchestrator: the mandatory-pin count 7 → 8,
and `TutorialBandRepelled` added to `KnownSignal`. Without them the tutorial registry check fails a
legitimate step and an authored completion signal reads as unknown.

## Conformance fix by the committer

`GuidePointer.Build()` hand-rolled its two `Image` widgets
(`new GameObject("Chevron", typeof(RectTransform), typeof(Image))`), which the `[ui-obsidian]` ratchet
hard-fails on any NEW file — and it did, in this wave's first full regression run. Both now build through
`ElarionUiKit.AddImage(...)` with `rounded: false`, because each carries an authored sprite the kit's
9-sliced rounded sprite would overwrite. The ratchet is green with zero NEW offenders.

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK :: scripts compiled clean`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` — including the tutorial registry
  check, `[ui-obsidian]` (0 NEW), and the tutorial-step reachability / FTUE-honesty suites
- `Builds/ui-capture-settle.log` → `UI_CAPTURE_OK 62` + `UI_CAPTURE_FIDELITY_OK 44`; the
  `UI_GEOMETRY_FAIL x16` is WO-941's pre-existing RumorBoard/RealmMap baseline, not this lane

## Honest limits

- Every headless proof here is registry/lint/geometry. **Whether the tutorial TEACHES is a felt
  judgement** — the ten-year-old test — and only the owner's hands close it.
- The lane's own §7 records its remaining open findings; they are unchanged by this commit.

## Owner felt-verify

New Game / Play Intro (never Continue — hero select self-skips when the save records a class), then walk
the whole FTUE: one objective strip, one skip, one moving cue, and a guide who is never guiding herself.
