# WORK ORDER 1309 — The wave-clear toast duplicates the modal, and lands outside its own reserved zone

**Status:** READY TO IMPLEMENT
**Silo:** HUD / Feedback
**Minted:** 2026-09-02 (CLI) from an owner screenshot during a live felt-test.

## Owner report, verbatim + screenshot

> **"this yellow box makes no sense. if important needs to be in a rewgualr modal, but i cannot see it"**

Her screenshot (`Main_Castle_Overworld`, Wave 2 HUD) shows a thin yellow strip reading
"Wave 1 cleared! 400 foes defeated." at MID-SCREEN, occluded by the hero's own body and shield,
white text on a hairline rule. It is illegible.

## Defect 1 — the same event is announced TWICE, by two uncoordinated paths

- **Modal:** `WaveCelebrationManager.cs:444` -> `EndStateView.Show(EndStateVM.FromWaveClear(waveNumber))`.
  Carries the spoils rows, the damage rows and the Repair CTA. This is the real announcement.
- **Toast:** `VillageHudController.cs:183` -> `HudKitController.ShowWaveClearToast` (`:457`).

`ShowWaveClearToast`'s own comment: *"Wave-clear push adapter — routes the **old no-op banner**
through the shared toast."* So a legacy banner that did NOTHING was wired to the toast zone, which
made an invisible thing visible AND redundant. Nobody noticed because the thing it duplicates is a
modal that draws over it.

⚠ **The two paths are NOT coordinated.** The modal has exactly one suppression branch
(`WaveCelebrationManager.cs:434`, when `BattleArena.AnyBattleInProgress` — protecting the arena
victory summary, after a real incident where the owner tapped the banner's dismiss instead of
Continue and was stranded in the arena with the HUD locked in Battle). The TOAST has no such branch
and fires either way.

**Owner's ruling is already satisfied by the modal.** Remove the duplicate.
⛔ Do NOT "solve" this by toasting in the arena-suppressed case instead — that re-creates the exact
collision the suppression comment exists to prevent. In that case the arena victory summary IS the
announcement.

## Defect 2 — the reserved toast zone is not where the code says it is. THIS IS THE BIGGER ONE.

`HudKitController.ShowToast` (`:465+`) documents: *"⭐ WO-1219 — THE ONE RESERVED TOAST ZONE, centred
**above the action bar**. Every transient toast on this screen lands here, whichever module raised
it."*

The screenshot shows it at roughly mid-screen height, behind the hero. **Every toast in the game
routes through this method**, so if the zone resolves to the wrong place, every transient message in
the game is landing on the player character. Note the owner has ALREADY reported a second illegible
toast from this same zone — the "That structure is undamaged" clipped yellow bar she called horrible
(closed by silencing that message, which treated the symptom, not this zone).

Establish AT SOURCE, with a capture: where does `_host.Mount(HudArea.Feedback)` actually resolve, and
does the card land above the action bar as documented? Fix the zone, not the individual messages.

## Method

Instrument first (CLAUDE.md sec.12), then fix what the data names. A screenshot is PRIMARY evidence
for a spatial defect — verify with a fresh capture and OPEN THE PNG. A green `UI_CAPTURE_OK` marker
proves pixels were written, not that a toast is readable or correctly placed; that exact marker was
green over a wave-clear panel carrying four visible defects earlier the same night.

## Acceptance criteria

1. Clearing a wave produces ONE announcement, not two.
2. Every toast lands in the documented reserved zone, above the action bar, unoccluded by the hero.
3. Toast text is legible at all three captured widths — real contrast, not white-on-hairline.
4. The arena suppression branch still holds: no wave-clear announcement of ANY kind can steal the
   arena victory summary's Continue action.
5. Proven by a fresh capture PNG that a human opened, not by a marker.

## What NOT to touch

- ⛔ `Assets/_Modules/Village/UI/EndState/*` — a separate lane already reworked that panel today
  (CTA sizing, wide-row splits, title seating). Do not re-enter it.
- ⛔ Do not silence or re-route other modules' toasts to work around a mispositioned zone. Fix the zone.
- ⛔ Do not remove the arena suppression or its FlowTrace.Warn.
- ⛔ Colour alone must never carry meaning — the owner is red/green colourblind. A "Confirm" tone that
  is only distinguishable by hue is not accessible; tone must also read in text/icon/position.
