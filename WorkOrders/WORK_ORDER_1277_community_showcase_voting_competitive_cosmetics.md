# WORK ORDER 1277 - Community showcase voting and competitive cosmetics

**Status:** SPEC - follows WO-1276 and requires product/abuse-policy rulings.
**Minted:** 2026-08-28 by Codex CLI under WO-1271.
**Lane:** Community events, ranking, rewards, and moderation.

## Goal

Turn published Town Showcases into recurring community competitions where cosmetics carry visible,
competitive prestige without granting combat statistics.

## Event shape

- players submit one eligible published snapshot per event period
- discovery is randomized/blinded before rankings are shown
- verified players cast limited votes in authored categories such as Community Favorite, Best Design,
  Strongest Theme, and Best Use of Space
- ranking may combine verified community votes, authored design criteria, achievement/progression
  inputs, and a moderated/judged component; weights must be server-authored and auditable

## Competitive rewards

Server grants stable cosmetic SKUs by placement tier (for example Top 10, Top 100, finalist,
participant). Rewards may include animated castle skins, banners, terrain themes, Echo trails, tower
effects, profile frames, titles, and permanent achievement trophies.

Temporary cosmetics carry authoritative expiry and safe unequip/fallback behavior through WO-1275.
Permanent trophies preserve provenance such as season and placement. Cosmetics change presentation
and visibility, never damage, defense, resource production, or matchmaking strength.

## Abuse and fairness gates

- one eligible vote per verified player per category; no self-voting
- minimum progression/account-age eligibility and rate limits
- immutable vote/reward audit records and idempotent grants
- wallet/device abuse signals, report flow, moderation tools, and reversible event results
- no public wallet identifiers

## Acceptance before promotion from SPEC

- Owner rules event cadence, voting eligibility, scoring weights, moderation owner, tie handling,
  reward tiers, temporary durations, and permanent trophy policy.
- A deterministic test fixture reproduces rankings from the same vote/event inputs.
- Duplicate votes and duplicate reward grants are rejected idempotently.
- Revoking a fraudulent result does not erase unrelated ownership or historical audit evidence.
- Winning cosmetics appear in leaderboard cards and Town Showcases and are Arena-ready later.

## Must not

- Do not sell votes, grant combat power, or make popularity determine PvP strength.
- Do not expose wallet addresses or allow anonymous-session voting.
- Do not implement before WO-1276 provides a safe published-showcase surface.
- Do not expand the overnight generic-card delivery with community backend work.

