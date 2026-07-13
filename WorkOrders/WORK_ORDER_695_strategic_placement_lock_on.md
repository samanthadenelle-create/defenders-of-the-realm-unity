# WORK ORDER 695 — Strategic placement: remove the flag, lock ON in build *(renumbered from WO-682, 2026-07-13 collision cleanup)*

**Status: READY TO IMPLEMENT** (owner ruling 2026-07-12, verbatim intent: "I want to see the
blank template and add buildings. Have that ff removed and set to lock in build").
**Lane:** Build Mode / Progression. **Supersedes:** WO-673's "default OFF until felt-pass" —
the owner is calling it: player placement IS the game. Flag-gate-then-delete canon: this is
the delete step.

## The change

1. **Remove `ff.strategicplacement`** (`FeatureFlags.cs:534`) — all call sites become
   unconditional TRUE paths. Sweep every consumer found by grep (known set: CatalogType/
   build-categories Town+Walls tabs, GameStateService.ResetToNewGame seed,
   HubStructureVisualInjector + station-injector standdown, ResourceCollectorBootstrap
   standdown, CastleVendorNpcInjector anchors, StrategicPlacementMigration,
   BaseLayoutLoader.ShouldReplayRecord, DevPanel/OwnerDevToolsOverlay toggle rows — REMOVE the
   toggle rows). Delete the dead flag-off branches, don't stub them (no zombie code).
2. **Behavior locked in every build:** new game = authored shell (walls/floor/decor) + ZERO
   functional buildings + the raised core-kit seed + Town/Defenses/Walls palette. Existing
   saves = the v30 one-shot migration converts auto-placed structures to movable records at
   their current positions (marker already gates re-runs — verify it stays one-shot with the
   flag gone).
3. **FTUE guard is now MANDATORY, not deferred** (WO-673 L7 punted it by keeping the flag OFF
   for the tutorial cohort — that escape hatch dies with the flag). Minimal V1 shape: the
   tutorial's structure-dependent beats (vendor talk-routes, first-tower anchor) must survive a
   zero-functional-building new game. Census the exact beats (WO-673 review §4 tutorial row);
   ship either (a) a guided "place your Forge" step before the first vendor beat, or (b) a
   grace default (tutorial pre-places the Forge as the player's first record — still movable).
   CLI picks the cheaper that passes the fleet tutorial probe; note the pick in the RESULT.
4. **Canon in the same breath (§15):** update `PIPELINE_STATE`/ground-truth flag ledger +
   `SESSION_CANON_LOADER` line (strategic placement = always-on), banner WO-673's flag language.

## Gates (the WO-673 §5 tests now run as THE default path)

- [ ] Existing-save round-trip: v29/v30 fixture → migrate → reload → structure count + id set
      identical; every record replays; NO double-spawn (exactly one Building per id).
- [ ] New game: blank functional town; palette offers Town/Defenses/Walls; core kit affordable;
      place Forge → vendor NPC anchors to it → talk-route resolves; persists across reload.
- [ ] Tutorial: full fleet tutorial probe green on a fresh save (AssertTutorialFirstTower +
      dialogue chain + the FTUE guard beat).
- [ ] Enemies target placed buildings (WO-672 damage/report/repair chain on a placed Forge).
- [ ] COMPILE_GATE_OK + REGRESSION_OK + full fleet on ONE build; owner felt-pass = the blank
      template experience end-to-end (PO closes; push stays held for her word).

## What NOT to touch
- The migration marker semantics (one-shot, v30) — removing the flag must not re-trigger it.
- MainCastle_Hall scene (no rebake — standdown stays runtime-side).
- WO-677 (mobile move) is untouched but NOTE: felt-pass the blank-template flow on DESKTOP
  until 677 lands; mobile placement judging waits on it.

*Cross-refs:* WO-673 + `docs/WO673_ARCHITECTURE_REVIEW.md` (§3 standdown/migration, §5 gate
tests, §4 tutorial census) · WO-674 (walls verb rides the same palette) · WO-680 (panel
legibility — parallel lane, file-disjoint).
