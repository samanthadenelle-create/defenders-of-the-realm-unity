# WORK ORDER 1267 — Command Center identified ops drill-downs

**Status:** SPEC — owner direction captured; implementation requires metric/query and access-control review.
**Minted:** 2026-08-28 by Codex CLI from the owner's unnumbered side note; banner bumped 1267 → 1268 in the same edit.
**Lane:** Command Center / operations. Not PROD gameplay.

## Product direction

The Command Center should organize around identified operational objects and actionable counts, not
anonymous session volume. Event volume is useful but remains a small supporting metric.

## Surfaces

1. **Gate cards:** every gated/maintenance area shows a numeric open-issue count. Tapping the count
   opens a scoped drill-down of affected wallet-keyed players, recent failures, refs, versions, and
   current gate state.
2. **Promo popup:** campaign configuration, active/expiry state, tier progress, successful wallets,
   refusal/error counts, and remaining first-tier capacity. Promo codes are never logged or displayed
   outside authorized operator context.
3. **Player issue tickets:** a wallet-keyed support queue with status, first/last seen, build/platform,
   affected area, correlation refs, and links to related promo/purchase/reconciliation records.
4. **Money/value context:** issue drill-downs may show whether a player has a settled purchase,
   unresolved entitlement, promo grant, or refund/reconciliation state. Do not infer spend from events.
5. **Events:** show compact trend/health summaries only; events support diagnosis but are not player identity.

## Identity and privacy

- No anonymous session is counted as a distinct player. Unique-player metrics require a proven wallet or
  authenticated server session mapped to that wallet.
- General cards use shortened/pseudonymous wallet labels; full wallet IDs are restricted to authorized
  drill-downs and copy actions.
- Never expose signatures, nonces, session tokens, raw request bodies, promo codes, or secrets.
- Every operator mutation and sensitive lookup is audited.

## Data contract before UI

- Define one server query/API per card and drill-down; no browser-side joins over raw tables.
- Counts and rows must share the same filters/time window so tapping a number explains that number.
- Paginate player/ticket lists; return stable correlation refs and explicit empty/error states.
- Command Center access control must fail closed before any wallet-level data is returned.

## Acceptance

- Gate count → matching scoped issue list.
- Promo card → tier/redemption health popup without leaking the code publicly.
- Player ticket → related gate, promo, and settled-money records where present.
- Anonymous/event-only traffic cannot inflate unique-player counts.
- Read-only views ship before any new mutation controls.

