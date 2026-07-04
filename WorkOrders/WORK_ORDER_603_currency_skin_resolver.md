# WORK ORDER 603 — Currency Skin Resolver

**Status:** READY TO IMPLEMENT
**Lane:** Monetization/Backend (isolated — no scene files, no gameplay dependencies)
**Priority:** HIGH — blocks Seekerthon submission (deadline: July 13, 6:30 PM)
**Created by:** UI (2026-07-03)

---

## Context

The game currently has a single live deployment skinned for **Pi Network** (Pi auth, π symbol,
Pi branding). We are submitting to **Seekerthon** — a Solana-based hackathon platform for the
Seeker Genesis NFT community. The owner is a Genesis holder with ~1M staked $SKR.

We need a second deployed instance skinned for **Solana/$SKR** without duplicating the codebase.

The architecture is already currency-agnostic by design (Cloudflare Worker abstraction layer,
NeonDB backend, JSON-driven content). The skin resolver formalizes this at the UI/auth layer.

**Rule:** if the feed/config indicates Pi Network → skin as Pi. Otherwise → skin as SKR (Solana).

CLI decides the injection mechanism (build-time env var, runtime JSON, URL param, or other).
Pick whatever is cleanest given the existing Worker + Vercel deployment setup.

---

## Acceptance Criteria

- [ ] A Pi-fed deployment renders exactly as production does today (no regression)
- [ ] A Solana-fed deployment shows:
  - Solana wallet connect in place of Pi sign-in button
  - `$SKR` in place of π wherever currency is displayed
  - "Seeker" / SKR branding in place of Pi branding (wordmark, any Pi-specific chrome)
  - Store/economy labels updated ("Spend $SKR" not "Spend Pi")
- [ ] Switching between skins requires no code change — only config/env
- [ ] Both deployments share the same build artifact OR the build process clearly documents how to produce each
- [ ] NeonDB player identity key works for both (wallet address for Solana, Pi UID for Pi)
- [ ] A Vercel preview URL for the Solana/SKR skin is produced and reported back to UI

---

## What to skin (known surface area)

| Element | Pi value | SKR value |
|---|---|---|
| Auth button | Pi sign-in (Pi SDK) | Solana wallet connect |
| Currency symbol | π | $SKR |
| Currency name | Pi | SKR |
| Player identity key | Pi UID | Wallet address (pubkey) |
| Any Pi wordmark/logo | Pi Network logo | Seeker / SKR logo (or omit) |
| Store CTA labels | "Spend Pi" | "Spend $SKR" |

There may be additional Pi-specific references in the Worker or UI — CLI to audit and list in RESULT.

---

## What NOT to touch

- Game content (scenes, enemies, gear, JSON data files)
- Combat, locomotion, world scenes
- Tutorial, dialogue, VFX
- `Village.unity` (abandoned, corruption risk)
- NeonDB schema beyond the identity key column (if a migration is needed, flag it before running)

---

## Suggested approach (CLI decides)

Option A — **env var at build time** (`CURRENCY_SKIN=pi` / `CURRENCY_SKIN=skr`): two Vercel
deployments, each with their own env. Simplest. Zero runtime cost.

Option B — **runtime `skin.json`** fetched on load before first render: one build artifact,
skin resolved at runtime. More flexible but adds a fetch round-trip on cold start.

Option C — **Cloudflare Worker header / subdomain**: Worker injects a `X-Currency-Skin` header
based on the request origin; client reads it. Clean if the Worker is already the entry point.

Any of these is acceptable. Document the chosen approach in the RESULT file.

---

## Deliverables

1. Skin resolver implemented and both skins verified
2. Solana/SKR Vercel preview URL (hand to UI for Seekerthon submission)
3. `WORK_ORDER_603_currency_skin_resolver.RESULT.md` with:
   - Chosen injection mechanism + rationale
   - Any Pi references found beyond the known surface area above
   - NeonDB migration note (if any)
   - Both deployment URLs

---

## Seekerthon submission fields (for context — UI owns submission)

Once CLI delivers the SKR preview URL, UI will submit:

- **Project name:** Defenders of the Realm: Echoes of Elarion
- **Demo URL:** (the Solana/SKR Vercel preview URL from this WO)
- **Tech stack:** Unity, C#, WebGL, Addressables, Pi Network, Cloudflare Workers, NeonDB, Solana
- **Description:** references the currency-agnostic architecture as a feature

The demo video is owner-recorded separately (castle hub → south gate seam → Village2 raid → economy panel, ~90 sec MP4 under 50MB).
