# WO-1388: "Builder's Hour" - a cheap starter pack (small basket + 6 h temporary builder) and the store funnel telemetry

**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:53:29, build 2026.09.07.358574). PRIOR STATUS: FIXED - in 9b47c9ad9, on Firebase App Distribution as build 2026.09.05.356329 (05:55). Gated: COMPILE_GATE_OK, REGRESSION_OK 378/378 incl. [temporary-builder-pack]. Awaiting owner felt-test (the pack shows FEATURED with the FIRST BUY badge; a devnet purchase grants the basket and the Builders chip gains a crew for 6 h) and the three copy approvals below. Owner still to approve the pack NAME ("Builder's Hour"), the BASKET (wood 600 / iron 300 / stone 300) and the BADGE copy ("FIRST BUY"). Rulings complete 2026-09-04 23:14; sequenced AFTER WO-1386 (wallet at any price on Solana) and WO-1387 (training is time-only) land, because both change what the pack means

## Owner, verbatim (2026-09-04 23:08-23:14)
> "can we add a pack which has some resources and faster building for a short duration" / "something cheap but
> to try to get at least a micro transaction" / "we have 0 sales" / "6 hours, and yes add the funnel telemetry"

## What exists (read at source tonight - reuse, never greenfield)
- 28 SKUs in `Assets/Resources/Data/Canonical/packs.json`, 17 visible; cheapest visible $1.99 (four impulse
  single-resource packs); `starters-hand` $4.99 "BEST START"; `permanent-builder` $9.99 (+1 crew, WO-1253).
- A timed build convenience ALREADY EXISTS in code and is sold by NOTHING:
  `BuildTimerService.TryGrantTemporaryBuilder(durationSeconds, out failure)` (`:231`) - one extra crew on
  the Builder line for `BuildTimerConfig.temporaryBuilderSeconds` (24 h authored, `:235`); refuses to stack;
  idempotent. No convenience KIND routes to it (`ConvenienceRedeemer` has instant-build / instant-repair /
  xp-weekend / harvest-auto-collect; `PackCatalog` has permanent-builder).
- Timed-redeemer pattern to mirror: `ConvenienceRedeemer.StartTimed(PrefXpEnds, TimedWindowSeconds, "xp-weekend")`.
- Telemetry today: `EventTracker.Track("bundle_viewed")` (`PackStore.cs:1828`) and `purchase_completed`
  (`:3338`, `:3853`). NOTHING between them - no store_opened, no pack_tapped, no checkout_started/failed. With
  0 sales nobody can say where players drop. `api/admin/stats.js` reads `analytics_events` by `event_name`.
- Covenant (`docs/monetization-v2-spec.md` s2): convenience compresses TIME, never sells power; caps untouched.

## The pack (rulings applied)
- SKU `builders-hour` (name for the owner to approve; ASCII), `storeSection: featured`, `storeBadge: "FIRST
  BUY"`, tier next-free; `pricing.usd 1.99` on the ladder's $1.99 rung (`usdc 1.99, sol 0.018, skr 25`).
- `contents.economy`: a SMALL basket - `wood 600, iron 300, stone 300` (about half a Cord of Timber, priced as
  a nudge not a ration; the owner may retune - every number is a tunable).
- `contents.convenience`: `{ kind: "temporary-builder", count: 1, description: "One extra builder crew for six
  hours." }` -> new `ConvenienceRedeemer.KindTemporaryBuilder` calling `BuildTimerService.TryGrantTemporaryBuilder(
  6 * 3600, ...)`; the 6 h is a NEW `BuildTimerConfig.packTemporaryBuilderSeconds` (do not repurpose the 24 h
  `temporaryBuilderSeconds`, which the crystal path uses) and goes on the tunables rail per the 09-02 rule
  (RemoteTunables + RemoteTunablesService + api/_lib/tunables.js + docs/PROD022_TUNABLE_FLAGS.md in ONE change;
  `[tunable-defaults]` pinned).
- Refusal when a temporary builder is already active: the grant is DEFERRED, not burned (queue the second
  6 h after the first ends) - never silently burn a purchase (HarvestBoostService's rule).
- Wallet: on the Solana channel this pack requires an attested wallet like everything else (WO-1386); the CTA
  for a guest is the connect-wallet sentence.

## The funnel (same build)
`EventTracker.Track` at: `store_opened` {door: hud-card|shortfall|settings}, `pack_tapped` {sku, section,
priceUsd}, `checkout_started` {sku, channel, rail}, `checkout_failed` {sku, reason: wallet-required|provider-
refused|cancelled|error}, alongside the existing `bundle_viewed` and `purchase_completed`. One place each
(PackStore open path, the pack CTA, the provider call, its failure branches). `api/admin/stats.js` gains a
`store_funnel` block: counts per event for 7d/30d so "0 sales" reads as WHICH step is 0.

## Acceptance
- [ ] Buying `builders-hour` (devnet canary) grants the basket AND starts a 6 h temporary builder; the Builders
      chip reads 3/3 (or 2/2 -> 3/3) for 6 h; a second purchase inside the window queues, never burns.
- [ ] `PackGrantRegression` / `BuyGateAndPriceLadderRegression` / `[tunable-defaults]` green; new
      `[temporary-builder-pack]` case proven RED first.
- [ ] The five funnel events appear in `analytics_events` from one device session; `stats.js` shows them.
- [ ] Owner approves the name, the basket and the badge copy.
