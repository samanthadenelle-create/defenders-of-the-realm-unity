# WO-1409: the Night Market without a wallet is nine "unavailable"s with no reason, and its right rail overlaps

**Status:** READY TO IMPLEMENT - minted 2026-09-05 from the merged UI review (sprint: the reason to tap the next screen)

## Evidence
- `Builds/ui-capture/NightMarket_2670x1200.png` (09-05 07:02, no wallet) - SEEN (`REVIEW_MERGED.md` row 8):
  six `Price unavailable`, three `UNAVAILABLE`, a `CONNECT WALLET` button at the bottom with no sentence linking
  them; `MONTHLY LEDGER` row clipped under the `CLOSE THE GAP` header; badges `BEST ST...` / `FIRST BUY` overlap
  the art leaving a `BS` fragment; contents truncate mid-list (`7,000 stone, 2,`); copy `2.7x the Hearth Spark's
  wood` compares to a pack not on screen; the "never required to spend" seal is ~14 px.
- Device `docs/qa/UI_REVIEW_2026-09-05/11-research-upgrade-door.png` (WITH wallet): prices render
  `1023 SKR ~ $19.99` and the LEDGER clip is present there too - the figure exists, the layout defect is not fixture.
- Both reviewers: `REVIEW_A_independent.md` E-3 / E-4, `REVIEW_B_independent.md` E6.
- CODE: `Assets/_Modules/Wallet/PackStore.cs:599-633,:705` document the "Price unavailable" path (the SKR quote
  needs a wallet; the USD anchor does not). The wordmark is one source already (WO-1398, `HudStrings.StoreFaceLabel`
  `Core/UI/HudStrings.cs:121`).

## What the player experiences
A store that says nine times that it cannot sell, and never says why in one place. The Realm deck promised
"clearly priced realm offers" a tap earlier.

## Fix shape (one mechanism)
- ONE banner under the wordmark: `Connect a wallet to buy - prices shown in USD` (single kit label; the nine
  per-card "unavailable" strings are replaced by the USD anchor `~ $19.99` each, from the pack's authored price).
- ACTIONS rail gets two full rows before the `CLOSE THE GAP` header (layout budget, `AuditGeometry` no-overlap).
- Badges reduce to one word top-left (`BEST` / `FIRST`); contents list capped at N lines with `+N more`; the
  comparison copy names only packs on screen or is dropped; the seal renders at legible size or as a text line.
All in `PackStore.cs` view code bound to the existing pack VM; no new data.

```
THE NIGHT MARKET
Connect a wallet to buy - prices shown in USD                [ CONNECT WALLET ]
[BEST]  Hearth Spark   ~ $4.99   7,000 stone, 2,000 wood +2 more
```
Trace: `FlowTrace.Step("Store", "shelf wallet=<bool> anchors=<n> banner=<bool>")` once per open.

## Implementation log

**2026-09-06 - second pass (the first pass was PARTIAL, and its own capture proves it).**
Commit `3c677027e` (lane D) landed the copy/anchor half. Verified at source, and against
`Builds/ui-capture/NightMarket_2670x1200.png` re-taken at 09-05 **23:56**, i.e. 44 minutes AFTER
that commit:
- CLOSED - the nine refusals are gone. Every card reads a `$` anchor (`PackStore.cs:2951` returns
  `pack.UsdReference` when walletless; `:2977` drops the minor). No `Price unavailable`, no `UNAVAILABLE`.
- CLOSED - the `MONTHLY LEDGER` clip. The redundant `ACTIONS` heading was retired
  (`PackStore.cs:1792-1794`), returning 70 px; the row now clears `CLOSE THE GAP` on the frame.
- CLOSED - contents capped at 2 + `+N more` (`DescribeContents`); the off-shelf comparison sentence
  is dropped (`CompareLine`, `IsOnBrowsableShelf` guard).
- **STILL OPEN, and fixed here:**
  - **The banner was never on the screen.** `_balanceLabel` is born empty (`PackStore.cs:1133`) and its
    only writer was `RenderBalanceLabel`, reached only from `RefreshWalletMirror()` inside `OnEnable`
    (`PackStore.cs:431`). The capture harness composes by Awake -> EnsureBuilt -> Render
    (`Assets/Editor/UICaptureLaunch.cs:3688-3730`) and never enables the object, so the header drew `""`
    beside nine USD prices. `Render()` now repaints it, so the header is a function of state rather than
    a value one lifecycle hook happened to leave behind.
  - **The one-word badge shipped truncated.** The same commit moved the pill left AND shrank it from the
    authored 0.70 of the card to 0.40 (`0.04..0.44`) without re-deriving the fit budget. `FontBadge` is 30
    and `ElarionUi.FontFloorMobile` is ALSO 30, so `FitSingleLine` has no room and degrades to Ellipsis:
    `BEST` (4 glyphs) fits, `FIRST` (5) does not - the frame reads `FIR...`. The band is now DERIVED
    (`PackStore.OneWordBadgeX0/X1`, arithmetic in the block above them) at `0.04..0.54`.
  - The `banner=` field of the WO's own trace restated the predicate instead of reading the label, so it
    would have printed `banner=true` over that empty header. It now reads the rendered `TMP_Text` and
    `FlowTrace.Warn`s when the two disagree.

**Stale in this ticket vs the tree:** the `BS` fragment is NOT a badge. It is
`StorePackCard.Initials(model.Name)` at 84 pt (`StorePackCard.cs:734`), the ASCII fallback drawn when
`NightMarketArt.Load` returns null - `builders-hour` authors no `artResource` in `packs.json`, so it is
`B`+`H` initials, not a clipped badge. Missing pack art is a separate finding.

**Not done here (named, not silently dropped):** the "never required to spend" seal is the owner's
AUTHORED `covenant-plaque` art with the sentence baked in (`PackStore.cs:1066`), not code-drawn copy.
Rendering it "at legible size or as a text line" replaces her artwork and is her call, not a code fix.

## Acceptance

> ⚠ ALL THREE BOXES ARE STILL UNTICKED ON PURPOSE. `NightMarketNoWalletRegression.cs` is WRITTEN and
> registered (`DataRegression.cs`, `[night-market-no-wallet]`), but this lane holds no Unity lock: it has
> never been compiled or run, so RED-first is a claim, not a measurement. Nothing here may be ticked
> until the gate seat runs it - once RED on the pre-fix tree if that is still reachable, then green -
> re-shoots `NightMarket_2670x1200.png`, and OPENS the frame.

- [ ] RED first: `NightMarketNoWalletRegression` - no-wallet fixture: exactly one banner label containing
      `Connect a wallet`; every pack card carries a `$` anchor; no label reads `Price unavailable`; `AuditGeometry`
      reports no overlap between the LEDGER row and the CLOSE THE GAP header, nor badge vs art. Fails on the current tree.
- [ ] Headless: `NightMarket_2670x1200.png` regenerated (`UI_CAPTURE_OK`), opened; `HudLabelFitRegression` green.
- [ ] Device: open the Night Market with and without the wallet; both screencaps read - one banner, anchors, no clip.

## Not in scope
Pack names / basket / badge semantics (WO-1388); the two storefronts (WO-1395); the HUD card size (ruling #9);
the SKR quote path itself.

## Owner ruling
- Section 2 #8 USD-without-wallet? - written to the default YES.
