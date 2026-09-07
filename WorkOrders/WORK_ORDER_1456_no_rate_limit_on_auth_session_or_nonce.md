# WO-1456: /api/auth/session renewal and /api/auth/nonce have no rate limit at all

**Status:** READY TO IMPLEMENT
**Silo:** `api/auth/session.js` + `api/auth/nonce.js`, reusing the existing promo IP-budget helper.
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1456 -> 1457 in the same edit).

## 1. EVIDENCE

A repo search for `rateLimit` and `budget` returns ZERO tokens in `api/auth/session.js` and
`api/auth/nonce.js`. The promo rail has the helper; the auth rail never adopted it.

Renewal is a token-driven DATABASE WRITE loop - each call updates a row. `nonce` mints a row per call. Both
are unauthenticated-cheap for a caller and expensive for Neon, and WO-1452 shows renewal can be driven
indefinitely from a single stolen token.

## 2. FIX SHAPE

- Apply the existing promo IP-budget helper to both routes. Do not write a second limiter.
- Budget the nonce route per IP and the session route per IP AND per wallet, since a token holder is one
  wallet by definition.
- Return the same shape the promo rail returns on refusal, so clients already handle it.

## 3. WHAT NOT TO DO
- Do not invent a new limiter or a new refusal code; two limiters is duplicated state.

## 4. ACCEPTANCE
- [ ] Both routes call the shared helper (file:line in the RESULT).
- [ ] `node --test` cases proving refusal past budget AND that a normal single call still succeeds
      (memory `prove-the-success-path-not-just-the-refusal`).
- [ ] `node --test` green across `test/`.
