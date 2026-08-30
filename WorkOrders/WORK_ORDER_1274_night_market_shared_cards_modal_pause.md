# WORK ORDER 1274 - Night Market on shared cards with purchase-safe pause

**Status:** FIXED 2026-08-29 — Night Market consumes the shared card/collection/modal contracts with packaged fallback, channel-preserving price behavior, deterministic paging, and nested pause ownership; awaiting owner Seeker test.
**Minted:** 2026-08-28 by Codex CLI under WO-1271.
**Lane:** Wallet/Night Market presentation. No payment activation or economy rebalance.

## Goal

Move Night Market product presentation onto the same Generic Card, Card Collection, and Focused Modal
Host as Build. The player must be able to read an offer and decide without combat, damage, enemy
movement, or gameplay timers advancing behind the store.

## Requirements

- `packs.json` remains the packaged fallback; DB collections point to stable pack SKUs.
- Free Welcome Pack SKUs remain `storeVisible=false` and never appear as purchasable cards.
- Product cards show title, basket contents, badge, tender-appropriate price/status, and action clearly.
- Approximately 80-90% of phone safe area is available to the market; overflow pages/swipes.
- The shared pause lease persists through product detail, confirmation, wallet handoff, success,
  cancellation, timeout, and failure. It releases exactly once when the full flow closes.
- A failed rail may offer an allowed fallback rail only through existing platform policy; this WO does
  not invent or enable new payment rails.

## Acceptance

- Night Market renders a fixture DB collection and packaged fallback with identical SKU identity/order.
- Prices and pack contents are readable on Seeker at normal viewing distance.
- Missing/invalid remote data falls back without blank cards or changing payment behavior.
- Pause remains held through simulated success, rejection, cancellation, timeout, and exception.
- Purchase settlement/entitlement semantics and server-vs-client evidence remain unchanged.
- Existing monetization, pack, and Night Market regression suites remain green; add focused modal/pause coverage.

## Must not

- Do not turn on payment flags, change live prices, or alter SKR/Pi/USD rail selection.
- Do not merge client purchase events with server entitlements.
- Do not make free/reward SKUs purchasable.
- Do not fork a second card component for the market.
