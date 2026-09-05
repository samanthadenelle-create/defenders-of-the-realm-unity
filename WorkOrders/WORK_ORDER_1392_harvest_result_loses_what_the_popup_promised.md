# WO-1392: COLLECT loses resources the welcome-back popup just promised - the harvest result does not reconcile

**Status:** FIXED - in da90ddc0f, on Firebase App Distribution as build 2026.09.05.356329 (05:55). Gated: REGRESSION_OK 378/378 (`echo-spec` re-pinned from "silo reset" to conservation, new [silo-overflow-stays]), WELCOME_BACK_CAPTURE_OK 3/3. Awaiting owner felt-test: with storage nearly full, the popup warns before COLLECT and the result reports the remainder as still waiting, never lost. Known gap left visible: `BankOverflowStatus` has no retained-vs-lost field, so the silo row still reads with the generic sentence (ruling on the morning list). Found on the headed walk 2026-09-04 23:41 (build 355952).

## Evidence (two consecutive captures, one tap apart)
- `docs/qa/UI_REVIEW_2026-09-05/00-title-or-hub.png`: welcome-back "YOUR REALM WORKED FOR 27m - WOOD WAITING
  +672 / IRON WAITING +403 / STONE WAITING +874", one COLLECT.
- `docs/qa/UI_REVIEW_2026-09-05/01-harvest-result-modal.png`, after COLLECT: "Wood storage 2021 / 4000 -
  Collected 1979 of 2393 | Uncollected: 414 - they are lost. Upgrade a Lumberyard, or spend wood, before
  collecting again." and "Iron 2771 / 4000 - Collected 1229 of 2392 | Uncollected: 1163 - lost."
- Trace: `[Flow:Harvest] ambient collector chip -> CollectAll banked=5171` then `collector status -> full=0/3
  maxFill=0% pending=0`.

## What does not reconcile
1. The popup said 672 wood was waiting; the result says 2393 were collected-from and 414 lost. The popup
   sums `ResourceCollector.PendingAmount` per resource (Lane G, `OfflineHarvestService.AttachPendingCollectors`);
   the result reports the harvest-node + collector total through `HarvestOverflowModal` (WO-1370). Two
   producers, two numbers, one tap - the player cannot tell which is true.
2. Wood storage reads 2021 / 4000 AFTER banking 1979, yet 414 were "lost" for lack of room - 2021 + 414 =
   2435 < 4000. The cap that refused them is not the storage cap the modal names. Read
   `HarvestOverflowModal.cs:55-60` and the collect path's clamp (which cap: town bank, silo, per-collector?)
   and make the modal name the REAL ceiling.
3. The loss is decided AT COLLECT with no warning before the tap. The popup already knows the pending totals
   and the storage headroom; it should say "Storage nearly full - 414 wood will be lost" BEFORE COLLECT, or
   collect up to the cap and leave the rest pending (never burn), per the owner's covenant that a purchase
   or a harvest is never silently burned (HarvestBoostService header).

## Rulings to honour
Never burn silently; one number per resource across the two screens; words name the cap that bit.

## Acceptance
- [ ] Popup rows and the harvest result agree to the unit on the same collect (headless: seed collectors,
      run the popup's producer and the collect path, assert equality).
- [ ] With headroom < pending: the popup warns before COLLECT and the collect leaves the overflow PENDING
      (re-collectable after spending), the result reports 0 lost.
- [ ] `HarvestResultCopyRegression` / `OfflineHarvestRegression` / `CollectorIncomeRegression` green; new pin RED first.
