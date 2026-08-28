# WORK ORDER 1266 — Post-load live Top 3 players

**Status:** IMPLEMENTED — compile green; production reader is live-shaped, but the current production board has zero rows until authenticated score submission is wired.
**Minted:** 2026-08-28 by Codex CLI from the owner's unnumbered direction; banner bumped 1266 → 1267 in the same edit.
**Lane:** Social/leaderboard presentation. Not PROD.

## Objective

Immediately after the loading flow reaches the first safe gameplay moment, show a compact “Top 3
Players” moment sourced from the production `highest_wave/alltime` leaderboard.

## Hard rules

- Never show `LocalStubLeaderboardSource` sample rivals as if they are live players.
- Use the existing public `/api/leaderboard?metric=highest_wave&period=alltime&limit=3` contract.
- Show at most once per app session, after onboarding, outside battle, and when no other modal is open.
- Network failure, malformed response, or an empty board skips quietly and never delays gameplay.
- Display username when present; otherwise use a privacy-safe shortened wallet identity.
- Keep the full leaderboard dock/panel independent.

## Acceptance

- Production source returns at most three server-ranked rows in rank order.
- The post-load surface says “Top 3 Players,” shows rank/name/best wave, and has one Continue control.
- No fake/sample rows can reach this surface.
- Compile gate and focused source regression pass.
