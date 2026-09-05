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

## Acceptance
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
