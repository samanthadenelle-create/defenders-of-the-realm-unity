# WORK ORDER 799 — Queue cancel verb + refund plumbing (engine side of the WC3 queue)

**Status: READY TO IMPLEMENT** (engine lane; independent of the WO-798 VISUAL design pack —
the portrait production dock renders whatever this exposes. Panel-row UI work in §4 lands
only after the owner signs the WO-798 mockups; the cancel verb + refund plumbing can ship first.)
**Origin (owner, verbatim, 2026-07-30):** "we can put an image of the item being upgraded or
built in each slot, so they can cancel the items in queue if they need to change order, on
cancel refund the values paid for it"
**Reference:** docs/design/WC3_BUILDING_REFERENCE_2026-07-30.md (owner: "ideas we are playing
against"). WC3 refund table: construction 75% / research 100% / units 100% / upgrades 75%.
**Owner ruling here: refund the values PAID** (full refund) — simpler and player-friendly;
revisit toward the WC3 percentages only if cancel-abuse shows up.
**Builds on:** the 2026-07-30 WC3 5-deep queue view (ObsidianQueueGate.QueueEntry rows on the
HUD chip) — that ships as the glance layer; THIS WO adds images + interaction.

## Mobile-touch constraint (shapes the design)

MinTouchPx = 112. Five cancelable slots do not fit the HUD-side chip rect. So:
- **HUD chip rows** = glance only (add a small item icon per row; no per-row tap; the chip
  itself already opens the Work Queue panel).
- **Work Queue panel (ObsidianQueueHud)** = the interactive queue: one >=112px row per job
  with item image + name + state (countdown / "Queued") + a Cancel button per row.

## Build

1. **Cancel verb — BuildTimerService.** `bool CancelJob(ChannelId, string structureId)`:
   removes a PENDING job outright; an ACTIVE job stops + frees its crew slot, then the next
   pending job auto-starts (existing cascade). RaiseQueueChanged (chip + panel repaint free).
   FlowTrace both paths.
2. **Refund plumbing.** Jobs do not store their cost — add `CostWood/Food/Iron/Crystals/Coins`
   to BuildJobData (serialized; save-schema bump + migration default 0 = old jobs refund
   nothing, traced). Charge sites (BuildingUpgradeService.TryUpgrade, BarracksService x3,
   BuildModeController.Place) write the charged cost onto the job at enqueue. Cancel refunds
   via ResourceLedger.Grant (same single wallet the charges use).
3. **Item images.** Portrait resolver precedent: BuildingUpgradePanelMvvm.BuildingArt
   (Portraits/<slug>[-tier]). Factor a shared `JobArt(structureId)` helper (strip @suffix ->
   catalog slug -> portrait sprite; null-safe: fall back to the kind glyph).
4. **Panel rows.** ObsidianQueueHud already lists jobs in layout.body + MakeScrollZone; add
   per-row: 96px image, label, state, red-face Cancel (ObsidianButtonColor rules: keep panel
   black, button face colour-coded but text-encoded "Cancel"). Confirm dialog NOT needed —
   full refund makes mis-taps free (owner ruling); revisit with partial refunds.
5. **Chip icons (glance layer).** QueueEntry gains `ArtKey`; chip rows render a 24px icon
   before the text when the sprite resolves.
6. **Oracle.** Extend [obsidian-queue]: cancel-pending refunds exactly the stored cost;
   cancel-active frees the slot + cascades; v-next save migration (old jobs cost 0) holds.

## Acceptance

- [ ] Queue 3 upgrades, cancel the middle one: refund lands (wallet delta == stored cost),
      order closes up, chip + panel repaint within 1s.
- [ ] Cancel the ACTIVE job: crew freed, next pending starts, refund lands.
- [ ] Old-save job (no stored cost) cancels without refund + traces the migration case.
- [ ] Screenshot: panel rows with images + Cancel at 1920x1080 and 2340x1080 (>=112px rows).
- [ ] COMPILE_GATE_OK + REGRESSION_OK incl. extended [obsidian-queue].

## Do NOT touch

- The train-strip instant path (TroopTrainingVM) — WO-771.8 is PO-gated separately.
- WC3 percentage refunds — parked unless the owner re-rules.
