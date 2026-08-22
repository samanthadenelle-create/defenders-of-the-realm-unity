# CANON GROUND TRUTH — 2026-08-21

**This supersedes `CANON_GROUND_TRUTH_2026-08-18.md`.** Keep exactly ONE current; supersede by date.
Every session and every agent checks docs against THIS file (CLAUDE.md §15).

---

## THE ONE CORRECTION THAT CHANGES HOW YOU PRICE RISK

> ## ⛔ THE GAME IS PUBLISHED, BUT THE PAY PATH HAS NEVER BEEN ACTIVATED.
> Owner, 2026-08-21: *"there are no live games as pay path has never been activated"*.
> The app IS live on the Solana dApp Store (next submission is an UPDATE, not a new listing), and
> **nobody has ever bought anything.** `FeatureFlags.RealmStorePurchase` has always been
> `defaultOn:false` and the mainnet block has never been lifted.

**"Published on a store" and "taking money" are DIFFERENT facts, and this repo's canon states the
first loudly while the second has never been true.** Currency/economy REMOVALS are therefore clean
purges, not balance-preserving migrations — there is nobody to grandfather or compensate. Still
read-migrate a removed save field so existing dev/test saves LOAD (ordinary defensive
deserialisation, not value preservation).

⚠ This does NOT license flipping the payment flags. Monetization stays OFF until R5 is ruled.

---

## STATE AS OF TONIGHT

- **Branch:** `wip/village2-and-f8-tickets`. **Save schema v38** — read it off
  `SaveSchema.CurrentVersion`, never off a doc. Nothing today bumped it.
- **Gate:** `COMPILE_GATE_OK` clean on a fresh log. `DataRegression` **245/247** —
  `REGRESSION_OK` is **ABSENT**, and the two failures are ticketed ASSET gaps
  (**WO-1135** wall tier materials were never tracked; **WO-1136** `staff_A` is geometrically
  symmetrical so no sheathe orientation is derivable). Neither is fixable in code.
- **Build:** Seeker APK 546.7 MB at `Builds/Android/DefendersOfTheRealm.apk`, with
  **`R2_PARITY_OK` 42 objects verified** — content is proven hosted, so no capsule enemies (§16).
- **Board:** derived — `python tools/board_build.py`. ~1016 work orders.

## SHIPPED TONIGHT (12 lane commits)

Night Market store redesign (WO-1050) · PvE siege cadence + persisted Defense Report (WO-1026,
**DONE**) · per-camp raid cooldown + scaled attrition (WO-728) · battle pass season track + monthly
cards (WO-1053) · chest drops with silhouette identity (WO-1132) · convex Finish-Now curve +
rescale parity (WO-1129) · per-mesh weapon seating · village cosmetic seam + armorer
instrumentation · realm map pins, dungeon status, offline accrual trust · enemy art pipeline.

## OWNER RULINGS 2026-08-21 (all verbatim-sourced)

| Ruling | Value |
|---|---|
| Raid cooldown | Regular **4h** / Hard **8h** / Extreme **12h** — THE crystal bound |
| Attrition | **5 / 20 / 45 min** by difficulty (was flat 120s = no loop) |
| Reward escalation | **sub-linear**, never lockstep with difficulty |
| Ladder terminus | **12 / 18 / 24 clears**, then camps **PLATEAU and REMAIN repeatable** |
| Loss stakes | theft **ALLOWED** — 15% of banked wood/food/iron, floor-protected below ~20% of capacity, **crystals NEVER stealable**, offline sieges included |
| WO-874 | the 2026-08-04 **WIRE** ruling **STANDS** |
| WO-1126 | **purge glimmer**; **retire `BattlePassManager`** |
| WO-887 | unblocked by the owner's own VFX tags (5 surface impacts) |
| WO-838 | **CLOSED** — felt-verified, raids render correctly |

⛔ **The ladder terminus deliberately DIVERGES from `TribeManager.ClearsUntilGone`:** copy the shape
of a terminating ladder, **never the vanishing.** A camp that disappears deletes the loop.

⚠ **The stakes ruling reversed TWICE inside one exchange** (clicked option -> *"No resource theft"*
-> **"Allow theft"**). The third is live. WO-1026 records all three with the superseded block struck
through — read it there before implementing WO-1139.

## WHAT IS STILL OFF

`FeatureFlags.Siege` **OFF** until **WO-1139** (the ruled stakes) lands — the cadence would
otherwise open sieges that resolve and report but take nothing.
`FeatureFlags.RealmStorePurchase` **OFF**, mainnet block unlifted.
No cosmetic or SKR rows are authored in the battle pass, and a regression **fails the build** if
either is authored before its gate opens (no art; no `ISkrLedger`).

## THE LESSON OF THE NIGHT — worth more than any single fix

**Gates that report success without proving anything were found in TWO separate suites in one run.**
Six hollow passes in the cosmetic suite (a missing dependency did `note + return`, and notes feed
the SUCCESS string, so a skip WAS a pass); one silently vacuous raid-cooldown case; and a
raid-cooldown "product defect" that turned out to be a demolished test fixture wearing a bug's
costume. Only ONE of the six was caught by the existing ratchet — the other five escaped because
its detection window is **four lines**, i.e. its coverage depends on code formatting (**WO-1138**).

A gate that reports success without proving it does not merely fail to catch a bug — it **actively
asserts the bug is absent**, and work proceeds on that assertion. That is strictly worse than
having no gate. Related: **WO-1137**, a fallback catalog covering 3 of 28 rows that has drifted four
times and would hand the player a silent, different, 3-row game.

## OWED

Owner felt-test of tonight's APK. Then: **WO-1139** (stakes), **WO-1126** (glimmer purge +
`BattlePassManager` retirement), **WO-874** (wire elite VFX), **WO-887** (map the 5 tagged surface
impacts), **WO-1133** (inventory redesign — design delivered, half of it is removal), **WO-1134**
(endgame loop — fully ruled).

Still owner-owed: **823** first-raid softness · **1029/PROD-012** backend + online-required ·
**R5/R6** buy button and season pass.
