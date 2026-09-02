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

---

# RCA ADDENDUM — 2026-09-02 (read-only agent). THREE of this ticket's own claims are WRONG.

## ⚠ NEW DEFECT the ticket missed, and it is the worst one: the number is a LIE

`WaveFeedbackDirector.cs:125-126` passes **`CurrentCrystals()`** — the player's **crystal wallet
balance** — into the `enemiesDefeated` parameter, and `HudKitController.cs:459-461` formats it as
`enemiesDefeated + " foes defeated. "`.

**The owner's "400 foes defeated" was her crystal balance.** This is not merely a duplicated
announcement; it is a factually false one. The stale `// WO-38` comment at `WaveFeedbackDirector.cs:
120-123` admits the value was being passed to fill a "+N diamond" line on a banner that no longer
exists. Fix this even if the rest of the ticket is deferred.

## The cut point is NOT where the ticket says

The ticket names `VillageHudController.cs:183`. That is the **relay**, not the origin. Both the modal
and the toast fire off the same `WaveManager.OnWaveCleared` UnityEvent via two independent listeners:
- modal: `WaveCelebrationManager.cs:582` -> `EndStateView.Show(EndStateVM.FromWaveClear(n))` (`:441-443`)
- toast: `WaveFeedbackDirector.cs:104` -> `:126` -> `IVillageHud.ShowWaveClearBanner` -> `VillageHudController.cs:181-184` -> `HudKitController.cs:457`

**The cut point is `WaveFeedbackDirector.cs:126`.** Deleting the `HudKitController` adapter alone
leaves a live call chain behind it.

## "It lands off-zone" — the ticket's HEADLINE claim looks FALSE

`ShowToast` mounts to `HudArea.Feedback`, which `HudAreasHost.cs:141` defines as
`Vector2.zero -> Vector2.one` (full-screen, so zone fractions are screen fractions with no
intermediate rect to drift). `HudLayoutBands.ApplyToastZone` (`HudLayoutBands.cs:129`) then applies
`Rect.MinMaxRect(0.375, 0.203, 0.625, 0.308)` as pure anchors with **zero offsets**.

That resolves deterministically to screen-x 37.5-62.5%, screen-y 20.3-30.8% from the bottom - dead
centre, lower third, above the action bar exactly as documented. **The zone is not mispositioned
relative to its spec; the SPEC puts the card on top of the player character**, who stands at screen
centre with feet in the lower third. The missing constraint in `HudLayoutBands.cs:113-128` is the
hero's screen footprint - its "verified clear of every band" audit checks HUD bands only and never
considers the world-space hero. Re-choose the band; do not chase a resolution bug that is not there.

## "Every toast routes through this method" — FALSE, and the one-zone law is ALREADY broken

`HudKitController.ShowRepairPrompt` also mounts `HudArea.Feedback` and builds the same `ToastCard`,
but hand-authors its own seat at `HudKitController.cs:397-399` (`anchorMin (0.08,0.66)` /
`anchorMax (0.92,0.94)`) - top of screen, bypassing `ApplyToastZone` entirely. So the "ONE RESERVED
TOAST ZONE" invariant is violated inside the same file. Whoever fixes this must decide which of the
two seats is canon.

(The owner's earlier clipped yellow "That structure is undamaged" bar came through
`ShowRepairFeedback` `:451-455` -> `ShowToast`, so THAT one was in the zone. The repair PROMPT is not.)

## Inherited colourblind violation — in scope for any tone work

`HudKitController.cs:452-453` picks `ToastTone.Danger` vs `Confirm` from a bool, and
`ElarianUiKit.cs:1354-1363` (`ToastAccent`) turns tone into an accent-bar **colour only** (red vs
green). There is **no text, icon or position difference between a success and a failure toast.** The
owner is red/green colourblind, so today these are indistinguishable to her.

## Only a screenshot can settle the legibility complaint

`ElarionUiKit.ToastCard` (`:1385-1445`) takes a sprite branch at `:1396-1402` when `RpgUiCatalog`
returns a Notification plate (tinted, `fillCenter = true`); the near-black obsidian fill + gold rim
only happens in the **null-art fallback** (`:1403-1408`). If the sprite resolves but its 9-slice
centre is thin or near-transparent, you get exactly the reported "white text on a hairline rule".
Which branch ran at runtime **cannot be determined from source.** Capture before fixing.
