# WORK ORDER 1276 - Animated Town Showcase and Visit Top 10 Towns

**Status:** SPEC - follow-up after WO-1272/1275; not part of the overnight card implementation.
**Minted:** 2026-08-28 by Codex CLI under WO-1271.
**Lane:** Social/showcase + snapshot backend. Read-only visitor experience.

## Goal

Let players explicitly publish a sanitized town snapshot that friends and leaderboard visitors can
load as an animated, read-only Town Showcase. The leaderboard Top 10 links to eligible showcases.

## Published snapshot

- stable player/showcase id and snapshot version
- placed structure SKUs, levels, positions, rotations, and equipped cosmetic SKUs
- selected public army/hero lineup and levels
- owner-selected public Echoes plus aggregate `Echoes Saved`
- banner/title, town level, public achievements, and leaderboard rank
- minimum client/catalog versions and safe fallbacks

The client reconstructs the snapshot locally. Echoes roam and armies patrol/train through ambient,
deterministic local animation. This is not the owner's live simulation.

## Privacy and safety

- publishing is explicit and previewable; no unpublished town is visitable
- no wallet address, balances, private roster, inventory, save blob, or account identifier is exposed
- visitors cannot move/upgrade buildings, collect resources, trigger combat, or mutate owner state
- snapshot ingestion is server-sanitized, rate-limited, reportable, and versioned

## Acceptance before promotion from SPEC

- Owner rules publish cadence, unpublish behavior, profile naming, and which army/Echo fields are public.
- Top 10 entries show `Visit Town` only when a compatible published snapshot exists.
- Missing remote SKUs render explicit fallbacks without losing the remainder of the town.
- Visit scene has bounded entity/asset budgets and cannot invoke progression or economy writes.
- Return navigation restores the exact leaderboard position and supports next/previous Top 10 visits.

## Must not

- Do not load another player's raw save.
- Do not synchronize live NPC coordinates or run authoritative combat in a visit.
- Do not make publishing automatic.
- Do not block WO-1272 through WO-1275 on this follow-up.

