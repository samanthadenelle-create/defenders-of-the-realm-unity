# WO-1386: nothing is guest-buyable on the crypto rail - every Seeker purchase requires an attested wallet

**Status:** IN PROGRESS 2026-09-04 23:13 - edit-only lane on PurchaseGate + its consumers + the two pinning suites

**Owner (2026-09-04 23:12), verbatim:** "nothing should be guest buyable on a crypto account otherwise we can
never persist change"

## What stands today (read at source)
- `Assets/_Modules/Wallet/PurchaseGate.cs:106` `public const double WalletRequiredAboveUsd = 4.99d;` and `:113`
  `RequiresWallet(priceUsd) => priceUsd > 4.99 + eps` - the WO-1121 ruling ("wallet above $4.99"), whose own
  header says a guest-local key dies on reinstall and the entitlement cannot be restored; it accepted that
  loss below $4.99 as "a lost entitlement is an apology, not a lawsuit".
- Consumers: `PackStore.cs:2440` (refusal copy), `StoreStrings.cs:54,181` (copy), `HasDurableIdentity` reads
  `GameStateService.HasAttestedWalletIdentity`.
- Pins: `BuyGateAndPriceLadderRegression` (rule 2 "wallet required above $4.99"),
  `StorePiSkinCurrencyRegression` (the Pi rewording of the same line).
- Channel: `Core/Payments/PaymentChannelResolver.cs:22,28` -> `PaymentChannel.GooglePlay` /
  `PaymentChannel.SolanaDappStore`.
- Shelf today: 17 visible SKUs; the four $1.99 and four $2.99 impulse packs are the only guest-buyable ones.

## The ruling, made precise
- On `PaymentChannel.SolanaDappStore`: `RequiresWallet(...)` is TRUE for every price. The const stays as the
  documented history, the predicate becomes channel-aware (one authority, no `requiresWallet` field on packs -
  the header's duplicated-state warning still binds).
- On `PaymentChannel.GooglePlay`: unchanged - Play's account is the durable key.
- Refusal copy on Seeker says WHY, in the owner's sense: "Connect a wallet so this purchase is yours on every
  device" (ASCII; never a bare "wallet required").
- The impulse "Short N wood" door (PackStore `_pendingShortfall*`) must route a guest to the wallet connect,
  not to a pack they cannot buy.

## Acceptance
- [ ] `BuyGateAndPriceLadderRegression` rule 2 re-pinned: on SolanaDappStore a $1.99 SKU requires a wallet; on
      GooglePlay the old threshold holds; proven RED first (mutation recorded).
- [ ] `StorePiSkinCurrencyRegression` copy pin updated with the dated reason.
- [ ] A guest on the Seeker tapping any pack sees the connect-wallet sentence, never a checkout.
- [ ] Owner felt-test.

## Not in scope
The starter pack itself (owner rulings on price / boost window still open), Play packaging (WO-1363/1364).

## Owner ruling 2026-09-04 23:32 - Pi follows the same rule
Verbatim: "mark anything for Pi as same logic based on USD". `PaymentChannel.PiBrowser` = wallet required at
every USD price (same as SolanaDappStore); `Unknown` fail-closed the same way; only `GooglePlay` keeps the
USD threshold. The USD ladder in packs.json `pricing.usd` stays the one price authority on every channel.
