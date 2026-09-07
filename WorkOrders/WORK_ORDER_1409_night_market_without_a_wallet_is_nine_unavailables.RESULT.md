# WO-1409 RESULT - the walletless store carries one banner, a USD anchor per card, and whole badges

**Status:** IMPLEMENTED AND SUITE-GREEN IN A PRE-COMMIT RUN. The suite's own hollow-pass guard was one of the
two reds in that run and is fixed in the same commit, so the run must be repeated before this reads green.
**Commit:** `eb161dc98` (2026-09-06 20:10). The copy and anchor half landed earlier in `3c677027e`.
**Files:** `Assets/_Modules/Wallet/PackStore.cs` (+98; the banner branch documented at `:1463`, the badge band
constants `OneWordBadgeX0` / `OneWordBadgeX1` at `:3309` / `:3311` applied at `:2105-2106`, the walletless USD
anchor returns at `:2946` / `:2952` / `:2967`), `Assets/_Modules/Wallet/StorePackCard.cs` (+8),
`Assets/_Modules/Wallet/StoreStrings.cs` (+27), `Assets/Editor/UICaptureLaunch.cs` (+86), new suite
`Assets/Editor/Regression/NightMarketNoWalletRegression.cs` (+865), registered in
`Assets/Editor/Regression/DataRegression.cs:1658` as `[night-market-no-wallet]`.

## What landed

The nine refusals are gone: every browsable card falls back to the pack's authored `UsdReference` when no
signing wallet is present. The header banner is now repainted by `Render()` rather than only by
`RefreshWalletMirror()` inside `OnEnable`, which is why the capture harness used to compose an empty header
beside nine USD prices - the header is now a function of state, not of a lifecycle hook. The one-word badge
band was derived rather than guessed: `0.04..0.54`, replacing an earlier `0.04..0.44` that left `FitSingleLine`
no room above the 30px mobile floor and truncated `FIRST` to `FIR...`.

Two items are named, not silently dropped. The "never required to spend" seal is the owner's authored
`covenant-plaque` art with the sentence baked in (`PackStore.cs:1066`); replacing it with code-drawn copy is her
call. The `BS` fragment the ticket read as a clipped badge is not a badge at all - it is
`StorePackCard.Initials` at 84pt, the ASCII fallback drawn when `NightMarketArt.Load` returns null because
`builders-hour` authors no `artResource`. Missing pack art is a separate finding.

## Gates

`COMPILE_GATE_OK` on `Builds/cg-quiet.log` (2026-09-06 20:04:44, 54305 bytes). `Builds/reg-quiet.log`
(20:07:39) emitted `REGRESSION_FAIL: 2 failure(s) (417/419 registered suites green, 0 skipped)` - NOT
`REGRESSION_OK`. One of the two reds is THIS lane's own suite: `REGRESSION MARKER FAIL (1): hollow pass:
NightMarketNoWalletRegression.cs:761 [A-missing-dependency] guard 'm == null' - returns out of a
null/empty/missing-dependency guard having asserted nothing`. The other was a UI-MVVM violation on
`BuildPreviewModal.cs:252-253` (the WO-1411 lane). Both were fixed at source and committed in `eb161dc98` at
20:10, AFTER both gate logs, so neither log postdates `eb161dc98` or the current working tree.

## Acceptance

- [x] The suite exists, is registered and reported OK on its own behaviour in the 20:07 run, composing the
      store for real at 2670x1200 without a signing wallet: `[banner-on-screen] one banner:
      PackStoreUI/ObsidianPanel/PanelFill/NightMarket/TopBar/Text = 'Connect a wallet to buy - prices shown in
      USD'`; `[anchors] 6/6 composed cards carry a $ anchor`; `[badge-budget] 'FIRST' 87px fits the 122px box
      (41% margin)`. RED-first remains a claim, not a measurement - this lane never held the pre-fix tree.
- [ ] The suite passes the MARKER oracle. It did not at 20:07; the hollow-pass guard is fixed in `eb161dc98`
      and the fix is unproven until the gate is re-run.
- [ ] Headless: `NightMarket_2670x1200.png` regenerated and opened, `HudLabelFitRegression` green.
- [ ] Device: the Night Market opened with and without the wallet, both screencaps read.

Still owed: the wave-two regression run at HEAD (which is what turns the suite's OK into a trustworthy pass),
a fresh capture opened, and a Seeker screencap of the store in both wallet states.
