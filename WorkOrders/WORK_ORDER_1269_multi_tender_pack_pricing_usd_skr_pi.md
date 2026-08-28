# WORK ORDER 1269 — Multi-tender pack pricing (USD / SKR / Pi)

**Status:** IMPLEMENTED — SOURCE READY FOR NEXT TESTER APK
**Minted:** 2026-08-28 by Codex CLI from Samantha's unnumbered request; banner bumped 1269 → 1270 in the same edit.
**Lane:** Store/payment architecture. Not PROD.

## Goal

One pack catalog and entitlement basket, with channel-owned price display and payment rails:

- Solana dApp Store: live server-quoted SKR.
- Google Play: localized fiat price returned by Google Play Billing (USD or the player's local fiat).
- Pi Browser: authored/approved Pi amount and Pi SDK provider, once the server approval/completion rail exists.

SKR must never be assumed to be the only tender. Display selection and payment authority may be
separate, but an unavailable rail must fail closed and must never fall through to another channel.

## Current-state audit

- `packs.json` already authors `pricing.usd`, `usdc`, `sol`, and `skr`; it has no Pi field.
- `PackStore` defaults to `CurrencyKind.Skr`, uses SKR for its balance-after preview, and several
  provider branches special-case Google Play rather than the provider-neutral contract.
- Google Play already owns localized fiat display through `IPaymentProvider.GetDisplayPrice`.
- Pi SDK primitives (`IPiPlatform.CreatePayment`, approval/completion callbacks) exist, but there is
  no registered Pi `IPaymentProvider` and the game API explicitly delegates Pi approve/complete to
  an external worker. A client-only Pi grant would be unsafe and is out of scope.

## Next tester APK slice

1. Add optional `pricing.pi` catalog support without changing existing rows or inventing a Pi/USD rate.
2. Make store display/purchase routing channel-aware and provider-neutral; Google/Pi channels cannot
   accidentally fall through to the SKR wallet rail when their provider is absent.
3. Continue to display live quoted SKR on the Solana channel and localized fiat from Google Play.
4. Pi displays an authored Pi amount only when supplied; otherwise it says price unavailable and has
   no Buy action until a verified Pi provider is registered.
5. Keep `welcome-500` and `welcome-100` hidden, `promoGrantOnly`, and non-purchasable.

## Follow-up

- Obtain product/economy-approved Pi amounts; do not infer them from USD or SKR.
- Implement/register a Pi payment provider backed by server `/approve` + `/complete`, idempotent
  entitlement settlement, reconciliation, and device proof in Pi Browser.
- Complete Google Play product/receipt configuration under WO-1255.

## Acceptance

- [x] Catalog can deserialize and format an optional Pi price.
- [x] Solana, Google Play, and Pi channel display paths are distinct and fail closed.
- [x] External provider purchase routing is not hardcoded to Google Play.
- [x] No visible hidden/promo-only Welcome Pack SKU and no purchase path to either SKU.
- [x] Focused tests 6/6, `COMPILE_GATE_OK`, and `REGRESSION_OK 314/314 suites`.

## Delivered / cut line

**Lands in the next tester APK:** optional `pricing.pi`; Pi-aware price label; provider-neutral
Google/Pi display and purchase dispatch; missing/mismatched external provider refusal with no SKR
fallthrough; hard `promoGrantOnly` purchase refusal; unchanged localized Google price and live SKR quote.

**Not in the already-built tester APK `345316`:** this WO was implemented afterward and requires the
next APK build to reach a device.

**Follow-up:** actual Pi charging remains off until approved Pi amounts and a registered provider with
server approval/completion and durable entitlement settlement exist. No Pi/USD conversion was guessed.
