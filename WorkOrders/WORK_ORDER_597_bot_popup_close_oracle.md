# WO-597 — Fleet oracle: every popup must have a working close trigger

**Status:** READY TO IMPLEMENT
**Lane:** Combat/AI-adjacent DevTools (AutoPilot) — file-disjoint from UI redo work
**Origin:** owner directive 2026-07-02 ("with the bots, every pop up window should have a close trigger")

## Why
The shared-Close rule is UI canon (one consistent Close per panel, no X buttons —
`obsidian-panel-chrome-black-gold-shared-close`), but nothing ENFORCES it. A panel that opens
with no (or a broken) close trigger is a softlock for a player and today only a human notices.
The fleet should prove closability mechanically, every run.

## Design — the POPUP-CLOSABLE oracle
In the AutoPilot driver (`Assets/_Modules/DevTools/AutoPilotDriver.cs` family), add an oracle pass:
1. **Enumerate every openable UI surface** the bot can reach (reuse the existing panel-open phase —
   the fleet already opens store/inventory/quests/settings/etc. for its ui-shots; extend to ALL
   registered panels, ideally driven from the panel registry / PanelId enum rather than a hardcoded
   list — data-driven so new panels are covered automatically).
2. For each: open → assert a close affordance EXISTS (the shared Close in the master-frame chrome;
   ESC handler counts only if it actually routes) → trigger it → assert the panel is actually
   CLOSED (inactive/destroyed, input released, HUD restored).
3. **Violation = `FlowTrace.Fail("PopupClose", "POPUP_NO_CLOSE :: <panel>")`** (error-level so it
   lands in `break-log.jsonl` headless — Step/Warn do NOT capture, per the instrumentation scar)
   + it feeds the ranked ticket file like existing oracles.
4. Timeout guard — **a hang IS the bug (owner 2026-07-02)**: a panel that swallows input and never
   closes is precisely what the oracle exists to catch. The bounded close-wait does not hide the
   hang — it CONVERTS it into a named diagnosis (`POPUP_NO_CLOSE :: <panel>` with the panel and the
   attempted close route in the message) instead of the generic 180s `possible_softlock`. Then the
   run force-continues (scene reload if needed) so one broken panel doesn't cost coverage of the rest.
   The generic softlock detector stays as backstop; this oracle upgrades its signal.

## Slice 2 — HEADED visual-conformance run (owner 2026-07-02)
The same panel-walk, run WITH graphics (no `-nographics`), turns the oracle into a style harness:
1. A `-VisualPass` fleet mode (1 instance, headed, real GPU) walks every registered panel and
   captures REAL ui-shots (`ui-shots/panel_<name>.png` — today those are blank under -nographics).
2. Output = a per-run gallery of every screen the game can show, machine-gathered.
3. Compare each shot against our style canon (the Blink Obsidian templates +
   `docs/UI_BLINK_CONFORMANCE_AUDIT_2026-07-02.md` axes: chrome, canvas, font, layout) —
   CLI does the compare per `ui-work-cli-owns-docs-first-screenshot-compare`; automated pixel-diff
   against golden shots comes later once screens stabilize (goldens churn too fast mid-redo).
4. This is how UI redo work gets verified from now on: redo a screen → headed visual pass →
   compare its shot → done. The owner is never the detector.

## Acceptance criteria
- [ ] Oracle runs in every fleet pass; per-panel verdicts visible in `autopilot-summary.json`
- [ ] A deliberately-broken panel (test: temporarily disable a Close handler) produces
      `POPUP_NO_CLOSE` in break-log + a ranked ticket
- [ ] Panel list is registry-driven, not hardcoded (new panels auto-covered)
- [ ] No hangs: close-wait is bounded; a stuck panel fails loud and the run continues
- [ ] Chaos bots (seeded random paths) also assert it on any panel they randomly open

## Do NOT touch
- The panels themselves (fixing violations the oracle finds = separate tickets through the pipeline)
- The castle/raise files (fresh verified fixes)

## Notes
- Complements the UI conformance audit (`docs/UI_BLINK_CONFORMANCE_AUDIT_2026-07-02.md`) — the
  audit found the chrome violations statically; this oracle catches the behavioral ones forever.
- The owner is never the detector (`never-dragdrop-or-manual-playtest`): this makes closability
  self-reporting.
