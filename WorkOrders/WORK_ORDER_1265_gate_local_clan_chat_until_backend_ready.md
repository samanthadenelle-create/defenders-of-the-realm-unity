# WORK ORDER 1265 — Gate local Clan Chat until multiplayer backend is real

**Status:** IN PROGRESS — player-build gate now; backend/social implementation deferred.
**Minted:** 2026-08-28 by Codex CLI from the owner's unnumbered direction; banner bumped 1265 → 1266 in the same edit.
**Lane:** Social/backend readiness. Not PROD.

## Finding

`ClanService` and `ClanChatPanel` are a local single-player PlayerPrefs simulation. The only current
coverage is seven `ClanChatVMTests` over a fake source. Those tests pass, but do not exercise the real
service, phrase catalog, persistence, panel, a second wallet, delivery, reconnect, server authorization,
rate limits, moderation, reporting, or takedown. No clan/chat production API or Neon tables exist.

## Immediate scope

- Hide the Chat dock entry and suppress `ClanChatPanel` bootstrap in player builds.
- Keep the local stub/source available for later development; do not delete it or pretend it is live.
- Add a source regression that fails if either public entry point bypasses the readiness gate.

## Readiness sequence before reopening

1. Server-owned clan, membership, role, invite/join, and message persistence keyed to signed wallets.
2. Preset phrases only unless moderation/reporting/takedown is explicitly owned; free text stays closed.
3. Rate limits, authorization, leave/leader succession, reconnect, and two-real-wallet tests.
4. Command Center surface showing clan/chat readiness, health, membership/message counts, and an explicit
   operator launch control. Do not expose the control before the server seal exists.
5. Add the clan leaderboard near the beginning of the player clan experience once server-owned clan
   identities and ranking inputs exist; do not derive it from the local PlayerPrefs stub.
6. Only then enable the player Chat entry point and run device UI + two-wallet acceptance.

## Acceptance

- Published/player builds expose no Clan Chat door while `ClanFeatureGate.PlayerFacingEnabled` is false.
- Direct bootstrap also refuses, so hiding one button is not the safety boundary.
- Existing leaderboard remains independent and untouched.
- Existing local clan data is not deleted.

