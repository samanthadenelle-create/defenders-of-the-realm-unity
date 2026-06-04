# Glimmer Economy — OPEN for Creative + Monetization review

> Owner (2026-05-31): the Glimmer earn rate / sources are a **creative + monetization decision**, not a
> UI-lane call. This doc captures the **current state (verified in code)** + the open question, so that
> review starts from reality. **No decision made here** — routed to creative/monetization.

## What Glimmer IS (verified)
- **Glimmer = the cosmetic soft-currency** (`GlimmerCurrencyService`). Spent on cosmetics in the Cosmetic
  Shop. Persisted wallet + owned-cosmetics set.
- **Hard rule locked in code/spec:** *"Crystals to Glimmer is not allowed"* (§2.3) — Glimmer and the
  gameplay Crystal/resource economy are **separate**. Cosmetics are earned by play, not bought with build
  resources. Matches the shop's "Beauty is earned, never required" + the NORTH_STAR "sell flex, not power."

## Current earn sources (already wired, verified)
| Source | Where |
|---|---|
| **Enemy kills** (per-enemy `GlimmerReward`) | `Enemy.cs:794` / `TryAwardGlimmer` |
| **Tier / progression milestones** (bonus Glimmer) | `TierSystem.cs:189` |
| **Daily quests** | `DailyQuests.cs` |
| **Battle Pass** tiers | `BattlePassManager.cs` |
| **Packs / IAP / crypto** (paid top-up) | `PackStore.cs` / `CryptoPaymentManager.cs` |
| **Starting seed** | 25 Glimmer (`StartingGlimmer`) |

So the *mechanism* exists end-to-end; the **rates + balance + which sources to emphasize** are the open call.

## OPEN QUESTION — for Creative + Monetization (NOT decided)
**What should the Glimmer acquisition model be — rates, sources, and the free↔paid balance?** Considerations
to weigh (for that review, not answered here):
- **Earn rate / grind length** — how much per kill/quest/tier; how long to afford a 50-Glimmer cosmetic.
  Pace it like the hybrid economy (RESOURCE_ECONOMY_DESIGN): generous enough to feel rewarding, scarce
  enough that the paid top-up + battle pass have value.
- **Free vs paid split** — how much Glimmer the free majority earns by playing vs. how much is sold (packs).
  NORTH_STAR: cosmetics monetize the spenders; rewarded ads / free play keep the majority engaged. Where's
  the line so it's "flex not power" and not pay-walled beauty?
- **Which sources to lean on** — kills (grind), quests (daily engagement), battle pass (retention/season),
  packs (revenue)? Emphasis shapes the play pattern.
- **Rewarded-ad Glimmer?** — should watching an ad grant Glimmer (ties WO-172 ad rail)? A free-player faucet.
- **Sinks** — is the cosmetic catalog deep enough that Glimmer always has somewhere to go (no capped-out wallet)?

> Routed to: **Creative** (what feels fair/rewarding, tone) + **Monetization** (the free/paid balance, the
> revenue model). UI/CLI implement the rates once decided — they live in tunable data/SO, not hard-coded,
> so the decision is a tuning pass, not a rebuild.

🤖 State captured by UI lane (verified against GlimmerCurrencyService / Enemy / TierSystem / DailyQuests /
BattlePassManager / PackStore). Decision deferred to Creative + Monetization. No code/bake.
