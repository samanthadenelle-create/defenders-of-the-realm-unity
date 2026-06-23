# WORK ORDER 172 — Build/Upgrade Timers + Rewarded-Ad Speedup (the CoC time-sink)

**Status: READY TO IMPLEMENT (phased)**
**Priority:** Medium-High — the core idle/retention sink + a key rewarded-ad monetization touchpoint.
**Date:** 2026-05-31
**Lane:** gameplay/economy code (CLI). `DeNelle.Village` + GameState + monetization (ads) seam. No bake.
**Source:** owner — *"add build times on buildings & upgrades — long enough to drag out the reward but
rewarding enough to justify; players can watch ads to pass time faster for an X duration."*

---

## The system
Placing a building or buying an upgrade is **not instant** — it starts a **timed construction** that
completes after a duration. The wait is the CoC/idle retention hook (come back when it's done), and
**rewarded ads** let the player skip a chunk of the timer (the F2P monetization lever, opt-in).

### 1. Build/upgrade timers
- A build or upgrade enqueues a **construction job**: `{ targetId, type (build/upgrade), startTime,
  duration, finishTime }`, persisted in GameState (survives app close — it counts down in real time, incl.
  offline; ties the offline accrual mindset). On `finishTime`, the structure completes / the tier applies.
- **Visual state:** the building shows an **under-construction** look (scaffold/placeholder + a timer
  bar/countdown) while building; completes with a little finish flourish (SFX + VFX).
- **Duration curve (HYBRID pacing — matches RESOURCE_ECONOMY_DESIGN):** short early (seconds–minutes for
  first builds — keep onboarding snappy), scaling **super-linearly** up to hours/long for high-tier
  upgrades (the endgame drag that drives ad-watches + spend). All durations in a **tunable SO/constants**,
  never hard-coded — *"long enough to drag out the reward, rewarding enough to justify."*
- **Build queue:** decide concurrency — one job at a time (a single builder, CoC-style scarcity → builders
  become a purchasable slot) vs. N concurrent. Recommend **a small number of build slots** (1–2 free,
  more unlockable) — scarcity makes the timer *matter* and is a clean monetization/progression lever.

### 2. Rewarded-ad speedup (opt-in, X duration per watch)
- A **"Watch ad → skip X time"** button on an in-progress job: each rewarded ad knocks **a fixed chunk**
  off the remaining timer (e.g. −15 min, or −X% — tunable). Watch again (subject to a cooldown/daily cap)
  to skip more.
- **Opt-in only, never a wall** (NORTH_STAR ad discipline): the timer always completes on its own; the ad
  is a *shortcut*, not a gate. **Store-build only** — keep ads out of the crypto build (NS two-build rule).
- Reuse the rewarded-ad provider seam (NORTH_STAR: Unity Ads / LevelPlay, the rail abstraction) — this is
  a prime rewarded-ad placement. Also offer a **premium/crystal instant-finish** (the paid skip) alongside
  the free ad-skip (convenience IAP, not power — NS "flex not power").

### 3. Ties to existing systems
- **Build mode (WO-108):** placement starts a timed job, not an instant structure. The BaseLayout entry
  carries a "constructing until T" state until done.
- **Village progression/crafting (WO-151):** building/Forge upgrades route through the same timer + ad-skip.
- **Economy (RESOURCE_ECONOMY_DESIGN):** resources are spent on *enqueue* (committed up front); the timer
  is the gate between paying and getting. Pairs with offline accrual (your haul builds while you wait).
- **Refineries/crafting:** the same timer pattern can later cover smelting/crafting jobs (refine takes time
  too) — design the timer system generic enough to serve any "job with a duration."

## Constraints
- Timers are **real-time + persisted** (count down offline; on load, elapsed time applies). One source of
  truth in GameState; coordinate the field-add (additive) per the parallel-lane rules.
- Durations/skip-amounts/caps in a **tunable SO/constants** — no magic numbers in logic.
- Ad provider via the existing rewarded-ad seam; **store-build only**, opt-in, never a wall.
- No UXML (code-built construction UI/timer bars); brace-gate; no bake; Village→Core only.

## Acceptance criteria
1. Placing a build / buying an upgrade starts a **persisted real-time timer**; the structure completes at finish (counts down offline).
2. Under-construction visual + countdown; finish flourish on completion.
3. Duration curve is **hybrid (short early → long late), tunable** in an SO — not hard-coded.
4. **Watch-ad → skip X time** (opt-in, capped/cooldown), reusing the rewarded-ad seam; timer still completes on its own (never a wall); **store-build only**.
5. Optional premium instant-finish (crystal/IAP) alongside the ad-skip.
6. Build-slot concurrency decided + implemented; routes for both WO-108 placements and WO-151 upgrades.
7. Brace balance; tunable constants; no bake; no UXML.

## Open questions for owner
- **Build slots:** 1 builder (scarcity, CoC-style, sell more slots) or unlimited concurrent? (Recommend 1–2, more unlockable.)
- **Ad-skip amount:** fixed minutes per watch, or % of remaining? And daily cap? (Recommend a fixed chunk + a daily cap.)
- **Does the timer apply to crafting/refining too** now, or buildings/upgrades first? (Recommend buildings/upgrades first; generic system, extend to crafting later.)

## Done checklist (CLAUDE.md §10)
- [ ] Persisted real-time build/upgrade timers (offline countdown); under-construction visual + finish
- [ ] Hybrid tunable duration curve (SO); build-slot concurrency
- [ ] Watch-ad skip (opt-in, capped, store-build only) via rewarded-ad seam; never a wall; optional premium finish
- [ ] Routes WO-108 placements + WO-151 upgrades; generic enough for crafting later
- [ ] Brace balance; no bake/UXML; Village→Core only
- [ ] `WORK_ORDER_172_build_times_and_ad_speedup.RESULT.md` when complete
