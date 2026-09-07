# WO-1506: /api/events/track accepts a client-asserted playerId with no auth and no rate limit

**Status:** CLOSED 2026-09-07 - owner felt-test PASS (validated 2026-09-07T14:07:35, build 2026.09.07.359076). PRIOR STATUS: FIXED - 2026-09-06: identity bound from X-Session / X-Guest-Id, else tagged unverified; shared IP budget (fail-open); 12 tests; client headers (EventTracker.cs:293) and ANALYTICS_EXCLUDED_PLAYER_IDS=unverified are the follow-ups named in the file header
**Silo:** `api/events/track.js`. (WO-686 webtrace ingestion hardening is CLOSED and does not cover this route.)
**Source:** read-only audit fleet 2026-09-06 (CLI seat), minted from the banner
(`CLI_LANES_WO_NUMBERS.md`, main line 1506 -> 1507 in the same edit).

## 1. EVIDENCE

`api/events/track.js` writes analytics rows keyed on a playerId the CLIENT asserts. Its own header comment
records the shape:

```
BoundWallet | "anonymous"
```

There is no auth check and no rate limit on the route. `ANALYTICS_EXCLUDED_PLAYER_IDS` exists, which shows the
project already expects junk in this table - the exclusion list is a cleanup for a hole that is still open.

So anyone can write unbounded analytics rows attributed to any wallet. The rows then feed retention and
funnel readings that the owner makes business decisions from.

## 2. FIX SHAPE

- Bind the row to the caller: a verified session (the wallet rail) or an explicit GUEST identity minted
  server-side. A client-asserted wallet id is never accepted as-is.
- Apply the existing IP budget helper (the same one WO-1456 adopts for the auth routes). Do not write a second
  limiter.

## 3. WHAT NOT TO DO
- Do not extend `ANALYTICS_EXCLUDED_PLAYER_IDS` as the fix; that is cleaning up after the hole.
- Do not reject anonymous events entirely - pre-wallet funnel data is the point of the route. Bind them to a
  server-minted guest id instead.

## 4. ACCEPTANCE
- [ ] A client-asserted wallet id is refused or overridden; `node --test` case proving both.
- [ ] Anonymous events still land under a server-minted guest id (success path proven, memory
      `prove-the-success-path-not-just-the-refusal`).
- [ ] IP budget applied via the shared helper.
- [ ] `node --test` green across `test/`.
