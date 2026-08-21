**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 677 — Build mode on mobile web: Move/Sell unreachable (touch Cancel bar never renders)

**Status: READY TO IMPLEMENT** (owner report + screenshot 2026-07-12; ticket MOB-1 on the board).
**Lane:** Build Mode / Input / UI. **Type:** EXISTING (regression-class — the verbs are built,
the touch path to them is broken). **QA triage:** read-only RCA (UI seat), static candidates below —
**§12 applies: CLI must capture the proving line before editing.**

## Symptom (owner, mobile WebGL)

In build mode on mobile web there is NO way to move (or sell/upgrade) placed structures.
Desktop web is fine (arrow keys + right-click/Escape available). Screenshot also shows the
gold PLACE banner overlapping the palette's Done button (separate defect, lane C).

## Read-only RCA — the causal chain (static, cited; prove with data before fixing)

1. **Move/Sell/Upgrade is BUILT and uGUI-safe.** `BuildSelectionUI` (code-built uGUI + kit,
   WO-108 P2) shows on select; `BuildModeController.SelectStructure` :1222; move loop + commit
   exist (`BeginMoveSelected` :1510, `CommitMove` :1542); the PLACE button shows during a move
   (`SetVisible(_armed != null || _movingSelected)` :402), so move-commit on touch is wired.
2. **Selection only runs in IDLE mode.** `Update()` routes exclusively: `_movingSelected` →
   move loop; `_armed != null` → place loop; else idle → `UpdateSelectLoop` :597 (tap on a
   `PlacedStructure` → select). The palette is deliberately STAY-ARMED after each placement.
3. **The only touch disarm is the Cancel button in `LeanTouchBuildDriver`'s button bar** (its
   header: "[Cancel] button → Cancel — replaces right-click / Escape").
4. **⛔ THE SUSPECTED ROOT: that button bar is UIToolkit.** `LeanTouchBuildDriver.Awake` adds a
   `UIDocument` and `AdoptPanelSettings()` copies a **sibling UIDocument's** PanelSettings
   (LeanTouchBuildDriver.cs:210-241, "Mirrors BuildPaletteUI"). But the palette was since
   converted OFF UIDocument (BuildPaletteUI.cs:17 — "that was a UIDocument requirement") and the
   live HUD is code-built uGUI (project law §8: UXML/UITK panels are the retired path;
   OnboardingPanelGuard additionally polices stray UIDocuments in gameplay scenes). With no
   sibling PanelSettings the driver logs
   `"[LeanTouchBuildDriver] No sibling PanelSettings found — touch buttons will not render."`
   (:239) and the bar — Rotate pair + **Cancel** — silently doesn't exist on screen.
5. **Chain:** no rendered Cancel → touch can never leave the armed state → idle mode never
   reached → `UpdateSelectLoop` never runs → tap-select, and therefore Move/Sell/Upgrade,
   unreachable on mobile. Desktop unaffected (right-click/Escape disarm via `DesktopBuildInput`).
6. **Secondary suppressors to verify while instrumented** (they gate idle taps too,
   `PlaceConfirmedThisFrame` :517-560): joystick-zone suppression (`VirtualJoystick.IsInZone`)
   and the pickable-UI suppressor — both already FlowTrace themselves.

## PROOF REQUIRED before the fix (§12 / pipeline rule 0)

From ONE instrumented mobile-web (or device-sim) session capture, quote:
- The `:239` "No sibling PanelSettings found" warning (or, if a PanelSettings IS adopted, the
  bar's actual render state — then re-diagnose against candidates 6).
- A `[Flow:Build] finger tap … overGui=…` line while idle showing the tap latched, and the
  matching `PlaceConfirm check / SUPPRESSED` line naming which gate ate it (if any).

## The fix (bounded)

**Lane A — rebuild the touch verb bar as code-built uGUI (the root fix).**
Replace the UIDocument bar in `LeanTouchBuildDriver` with an `ElarionUiKit`-built uGUI strip on
the build-mode canvas (same right-edge stack: ⟲ / ⟳ 45° rotate pair per WO-673 L5 + **Cancel**).
Kill `AdoptPanelSettings` and the UIDocument dependency entirely — this class of silent
non-render dies with it (project law: code-built uGUI only). Buttons must register as GUI for
`finger.IsOverGui` (EventSystem raycast — uGUI Graphic raycasters count) so bar taps still don't
double-fire placement.

**Lane B — make idle-select robust on touch.**
No new mechanics: with Cancel restored, tap-select flows through the existing
`PlaceConfirmedThisFrame`. Keep the joystick-zone + pickable-UI suppressions (legit), but add the
already-standard step-in/out traces to `UpdateSelectLoop` (currently silent when the raycast
misses or no `PlacedStructure` parent is found — name the miss).

**Lane C — the PLACE / Done overlap (screenshot).**
`BuildPlaceButton` anchors collide with the palette header's Done button. Seat PLACE clear of the
palette strip (e.g. right-edge above the touch verb bar, or left of the tray) — verify at both
16:9 and a narrow mobile aspect. Done must always be tappable (it's the only build-mode exit).

**Lane D — regression + probe.**
- Fleet/headless probe: enter build mode → arm → place via UI latch → **Cancel via the new bar's
  seam** (expose the same `RequestUiCancel()` latch pattern as `RequestUiPlaceConfirm` so the
  probe drives the real path) → assert idle reached → simulate tap on a placed structure →
  assert `BuildSelectionUI` shown → Move → commit → assert record moved cell.
- EditMode: none needed beyond compile; this is input/UI wiring.

## Acceptance

- [ ] Mobile web: arm → place → **Cancel visible and works** → tap a placed structure →
      Move/Upgrade/Sell panel opens → Move → reposition → PLACE commits → layout persists.
- [ ] Desktop web/Windows: unchanged behavior (Escape/right-click still disarm; arrow nudge intact).
- [ ] Done button fully visible/tappable with PLACE shown, both aspects.
- [ ] Proving lines quoted in the RESULT (pre-fix root + post-fix pass — two verbatim captures,
      pipeline rule 0).
- [ ] `COMPILE_GATE_OK` + fleet green (incl. the new probe) + owner felt-pass ON A PHONE (the
      report is mobile — a desktop pass does not close this, PO closes).

## What NOT to touch

- `PlaceConfirmedThisFrame` consumption order / the PLACE-latch bypass (just fixed 07-12 — works).
- Stay-armed CoC placement semantics (deliberate; Cancel is the exit, not auto-disarm).
- `BuildSelectionUI` internals. DEF-171 joystick-zone suppression stays.
- §0 Windows path; explicit-path commit; push held for owner word.

*Cross-refs:* ticket MOB-1 (board #4) · WO-673 G5 (mobile touch driver) + L5 (45° rotate pair —
land it in the SAME new uGUI bar, one touch surface) · CLAUDE.md §8 (UXML/UITK-in-builds law) ·
`docs/TICKET_PIPELINE.md` rule 0 · WO-674 (wall drag mode will ride this same touch bar — fix
this FIRST).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
