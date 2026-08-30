# WORK ORDER 1281 — Command Center decision dashboard: sales, retention, and progression

**Status:** IMPLEMENTED (2026-08-30) — Phase 1 audit done at source and Phase 2 built. NOT closed: acceptance 1 and 2 need a phone-width capture of the deployed page, which is the PO's felt-verify. Two items are declared NOT INSTRUMENTED on the surface itself rather than faked (session length is an estimate; pushing a SKU has no server switch) — see the Implementation record at the foot of this file.
**Minted:** 2026-08-29 from the owner's 2026-08-28 Command Center discussion; CLI main-line banner bumped 1281 → 1282 in the same edit.
**Lane:** Command Center / product analytics. Not gameplay.
**Related:** WO-1169 (vision), WO-1244 (console), WO-1267 (identified operational drill-downs).

## Owner outcome

The Command Center currently exposes many tabs and large amounts of data that do not help answer the
questions the owner actually has. It must become a compact decision surface that answers:

1. **What is selling?**
2. **Do players return after trying the game?**
3. **Are returning players actively progressing and leveling?**
4. **Are players playing once and never returning?**

Raw event volume, database row counts, and long always-open tables are supporting diagnostics, not the
default product view.

### ✅ OWNER ADDITION 2026-08-30 — a fifth question

*"Average online time"* — session length. Alongside the four above, the landing view must answer
**how long a player actually stays in a session**, because it is the metric that distinguishes
"they bounced" from "they played and left" and it is the one that makes the churn number
interpretable. Show it in the RETENTION area next to return-rate, windowed the same way
(Today / 7d / 30d), with median AND mean if the data supports both — a mean alone is distorted by
one idle session left open on a locked phone, which this project has already seen
(`STUCK WORLD HOLD: 'pause-menu' outstanding for 10863s`, 2026-08-30). **If sessions are not
explicitly closed, say so on the card rather than reporting a mean that silently counts a
backgrounded app as engagement.**

Restated owner outcome 2026-08-30: *"Better more intuitive design something I can use on my phone.
Push skus see revenue what level players are playing to. Are we growing or losing players."*
→ phone-first is a REQUIREMENT, not a nice-to-have; and "push SKUs" means the surface must let the
owner ACT (promote/feature a SKU), not only observe.

## Information architecture: minimized by default

- The landing view contains four summary areas, ordered by business value: **Sales**, **Retention**,
  **Progression**, then **Diagnostics**.
- Each area is collapsed/minimized by default after its headline metrics. Tap expands details; tap
  again collapses them. Remember the operator's choice for the current browser session.
- Only one detailed area needs to be open at a time on phone width. Opening another may collapse the
  prior area so the page remains scannable one-handed.
- Diagnostics and raw tables live behind an explicit secondary disclosure; they do not dominate the
  landing page merely because the data exists.
- Every card has plain-language empty, loading, stale, and error states. Never show zero when the query
  failed, never use color alone, and never ellipsize load-bearing labels or values.

## Sales: what is actually selling

The authority is settled server purchase data, never client `purchase_completed` events. For Today,
7-day, and 30-day windows show:

- settled value and purchase count;
- unique purchasing players;
- units and settled value by SKU/pack, ranked best to worst;
- first-time versus repeat purchasers;
- quote → settled conversion and active client/server purchase disagreements;
- comparison with the immediately preceding equal-length window, showing both values.

Do not blend promotional grants, test transactions, client intent, or failed quotes into settled sales.
Each metric declares its source, filters, timezone, and last successful refresh.

## Retention: do players come back?

Define the cohort from the first proven authenticated/wallet-bound gameplay session. Anonymous events
may describe traffic but cannot inflate player counts.

- New players/cohort size by day and week.
- D1, D7, and D30 retention: returned and performed a qualifying gameplay action in the window.
- Returning active players versus new active players.
- Sessions per retained player and active days per retained player.
- Cohort trends so a launch spike cannot hide weak return behavior.

Define the qualifying gameplay-action allowlist before shipping. Boot, login, heartbeat, banner fetch,
store impression, or background resume alone do **not** count as playing.

## Progression: are players actually leveling?

- players gaining hero XP and players gaining at least one hero level;
- median and distribution of hero level for active players;
- waves attempted/cleared and highest-wave advancement;
- dungeon entries/completions and first-stone acquisition where authoritative;
- structures placed/upgraded and tower upgrades;
- time to first build, wave clear, dungeon completion, level-up, and purchase;
- funnel drop-off between those milestones.

Use unique-player counts for player questions. Event totals are supporting detail and must be labeled.

## One-and-done and deletion risk

The console must not claim to know a player deleted the APK: the present Android/Solana/Pi telemetry
does not provide a reliable per-player uninstall fact. Show honest observable cohorts:

- **One-session:** exactly one qualifying gameplay session and no return after 24 hours.
- **Tried and left:** no return within seven days of first qualifying play.
- **Stalled:** returned, but gained no XP/level and completed no meaningful milestone in the window.
- **Early-exit step:** last proven milestone before inactivity.

Label these as churn-risk/inactivity signals, never “deleted.” Future platform aggregate uninstall data
must remain separate and must not be mapped to individual wallets without proof.

## Data and trust contract

- One server query/API per card and drill-down; no browser-side joins over raw tables.
- Counts and rows share filters, time windows, identity rules, and timezone.
- Use server-authoritative purchases and persisted progression where available. Telemetry estimates say
  **estimated** and document dedupe rules.
- Exclude known operator/test wallets and automated probes via an audited server-side rule.
- Bound queries, paginate detail rows, and expose query timestamp/data freshness.
- Preserve WO-1244's read/write separation and WO-1267's fail-closed operator access. This ticket adds
  no grant, refund, entitlement edit, or other money mutation.
- Aggregate cards expose no identity. Wallet drill-down requires restricted operator scope and audit.

## Required audit before implementation

Inventory production events and persisted fields against every metric. Record source table/event,
identity key, timestamp, dedupe key, exclusions, retention window, and historical coverage. Missing
instrumentation is an explicit gap; do not manufacture a green metric from unrelated events.

## Acceptance

1. Phone-width capture shows four compact areas without long raw tables taking over.
2. Expand/collapse works by real tap and remains readable in text.
3. Sales reconcile exactly with bounded server purchase authority for the same window.
4. D1/D7/D30 fixtures prove denominators, return windows, identity dedupe, and boundary timestamps.
5. Boot-only users are excluded; qualifying returning players are counted once.
6. Progression fixtures distinguish app opens, XP gain, level gain, and milestone completion.
7. One-session/tried-and-left cards never claim an uninstall/deletion fact.
8. Query failure/staleness cannot render as a trustworthy zero.
9. Test/operator traffic exclusion is visible in metric metadata.
10. Sensitive drill-downs fail closed and leak no wallet, signature, token, promo code, or secret.

## Explicit non-goals

- Predictive ML churn scoring before observable cohorts are trustworthy.
- Individual APK deletion tracking without a proven platform source.
- Replacing WO-1267 support/incident drill-downs.
- Adding new Command Center money mutations.

---

## Implementation record (2026-08-30, CLI)

**Files:** `api/admin/stats.js` (new `?view=command` read), `api/admin/console.js` (the landing
surface), `test/command-center.test.js` (14 new pins; three proven RED by mutation before being
accepted). Nothing else in `api/` was touched.

### Phase 1 audit - metric to backing, read at source

| Metric | Backing | Answerable? |
|---|---|---|
| Settled revenue / units / buyers | `purchase_entitlements` (`usd_anchor`, `wallet`, `created_at`) | YES, and legitimately EMPTY - no purchase has ever settled |
| Units + value by SKU | `purchase_entitlements` GROUP BY sku, joined to the server price ladder `_lib/purchase-catalog.USD_ANCHORS` | YES |
| First-time vs repeat buyer | `purchase_entitlements`, ROW_NUMBER over wallet | YES |
| Quote to settle conversion | `purchase_quotes` (`consumed_at`, `expires_at`) | YES - the only view of players TRYING to buy |
| Client/server purchase disagreement | `analytics_events.purchase_completed` vs `purchase_entitlements.tx_signature` | YES (count only; the list + acknowledge action stay on `?view=purchases`) |
| **Push / feature a SKU** | none | **NO.** The shelf flag the client honours is `storeVisible` in the PACKAGED `packs.json` (`PackCatalog` reads Resources/StreamingAssets, never the network). `packs.store_visible` exists in Neon and NOTHING reads it. `catalog_collections` is a live remote read but feeds the BUILD browser. |
| New players / cohort size | `analytics_events`, first event on the qualifying-play allowlist | YES |
| D1 / D7 / D30 return | same, exact-day return | YES |
| Returning vs new active, growth vs prior window | same | YES |
| One-session / tried-and-left / stalled | same, plus a milestone set | YES as INACTIVITY cohorts; never as "deleted" |
| Early-exit step | last non-boot event per quiet player | YES |
| **Average online time** | **no `session_end` exists anywhere in the client** - `EventTracker.Start` fires `session_start`; `OnApplicationPause`/`OnApplicationQuit` only flush the queue | **ESTIMATE ONLY**, from telemetry gaps, labelled as such, median first |
| Hero level: median, spread, share past level 1 | `player_data.game_state->>'heroLevel'` (persisted since SaveSchema v29) | YES - server-persisted, and shipped with its own coverage count |
| Best wave, waves cleared, structures placed | `game_state` `bestWave` / `wavesCompleted` / `baseLayout` + the `wave_completed` event | YES |
| **Players who GAINED XP or a level in a window** | none - only a CURRENT value is stored, no history | **NO** |
| **Dungeon entries / completions** | none - no client event; `dungeon_status` is a per-dungeon seal setting | **NO** |
| **Structure/tower UPGRADES, time-to-first-milestone** | none beyond the tutorial timings | **NO** |

Production population could not be proved from this seat (no DB credentials, and asserting it from
a schema would be exactly the hollow proof this ticket forbids). So every card MEASURES ITS OWN
BACKING instead: the progression area publishes how many saves actually carry each field, and the
diagnostics area publishes identified-vs-anonymous telemetry volume. A field that is not arriving
reads as "not arriving", never as "every player is at zero".

### Phase 2 - what was built

* `GET /api/admin/stats?view=command` - one bounded read, still SELECT-only, same `ADMIN_DASH_KEY`
  gate. Every block carries `backing`, `state` (`ok`/`empty`/`not_instrumented`/`error`) and
  `read_ok`; a probe that throws lands in `errors[]` and its area renders COULD NOT READ.
* Landing view is a phone-first accordion in business order - Sales, Retention (return rate,
  growth, session length, one-and-done), Progression, Diagnostics. One area open at a time, held in
  a tab-lifetime variable (nothing is written to any browser store - the key rule stands).
* The six operational tabs are unchanged, behind an explicit **More tools** disclosure.
* Operator/test exclusion is server-side from `ANALYTICS_EXCLUDED_PLAYER_IDS`, never from the
  request; the COUNT is published and the ids are not.
* Growth is a WORD (GROWING / SHRINKING / FLAT / TOO FEW TO CALL / NO DATA). No state on the page
  needs a hue to read.

### Open decisions for the owner

1. **Pushing a SKU needs three things that do not exist yet** - a shop-context remote read the
   client consults for shelf membership, a client release that consumes it, and an audited write
   action on `api/admin/ops.js` behind the second key. Half of it would be a button that silently
   does nothing. Worth its own ticket.
2. **A `session_end` event** (or a periodic in-play heartbeat) would turn average online time from
   an estimate into a measurement. One line in `EventTracker`, plus a duration property.
3. **A hero-level-up event or a progression snapshot** is the only way "who levelled this week"
   becomes answerable.
4. `ANALYTICS_EXCLUDED_PLAYER_IDS` is unset on the deployment. Until the owner's own test wallets
   are listed there, her play counts as a player in every retention figure.
