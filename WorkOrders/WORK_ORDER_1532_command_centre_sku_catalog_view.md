# WO-1532 - Command centre: a read-only SKU catalog view with contents and rail parity

**Status:** DONE
**Minted:** 2026-09-06 (CLI lane, api/web silo)
**Silo:** Monetization/Backend (api/ + test/ only - no Unity, no .cs)
**Owner ask, verbatim (2026-09-06 20:52):** "can we add a list in command center of All SKU's and contents"

---

## 1. The question this answers

There is no one place that says what the store actually sells. The truth is spread over
three files that nobody reads side by side:

* `Assets/Resources/Data/Canonical/packs.json` - the authored catalog: name, tagline,
  tier, section, shelf flag, prices, and the CONTENTS (cosmetics, economy, convenience).
* `api/_lib/purchase-catalog.js` `USD_ANCHORS` - the server-authoritative price ladder.
  A SKU with no row here cannot be quoted, so it cannot be bought on the wallet rail.
  That exact failure already shipped once (WO-1165 s2: the Monthly Ledger cards were
  authored with a real `pricing.usd`, had no anchor, and were silently unbuyable).
* `api/_lib/google-play-purchases.js` `PRODUCT_TYPES` - the Play billing product type.
  A SKU with no row here fails `validRequest` and cannot be bought on the Play rail.

The gap between the three is invisible until a player taps Buy. This ticket makes it a
column on a page.

## 2. Scope

A NEW READ VIEW and a NEW TAB. Nothing that writes, nothing that changes the rails.

### 2a. Loader - follow the precedent, do not invent a second one

`.vercelignore` allowlists ONLY `/api`, `/Builds/WebGL` and the configs, and
`vercel.json` sets `git.deploymentEnabled:false`, so production is a CLI upload and
`Assets/` NEVER reaches the function. A runtime read of the canonical file would throw
at module load and take down every `stats.js` view, not just this one.

The in-repo precedent for canonical game data reaching the server is a GENERATED or
committed JSON under `api/_lib/`, pinned by a test:
`api/_lib/tunable-manifest.generated.json` (from `tools/gen-tunable-manifest.mjs`) and
`api/_lib/dungeon-manifest.json` (pinned by `test/dungeon-status.manifest.test.js`).

So:
* `tools/gen-sku-catalog.mjs` copies `Assets/Resources/Data/Canonical/packs.json`
  VERBATIM to `api/_lib/sku-catalog.generated.json`.
* `api/_lib/sku-catalog.js` is the ONE reader. It exports `packs()`, a pure
  `parityRow(pack, anchors, productTypes)` and `build(...)`.
* A test asserts the generated copy is byte-for-byte the parse of the canonical file.
  Drift goes RED. A copy with no oracle is duplicated state (CLAUDE.md s2/s5/s16).

### 2b. `GET /api/admin/stats?view=skus`

Behind the existing `ADMIN_DASH_KEY` read gate, dispatched BEFORE `neon()` is called so
the view structurally cannot open a database connection.

Per pack: `sku`, `name`, `tagline`, `tier`, `section`, `store_visible`, `founder_only`,
`promo_grant_only`, `pricing` (usd/usdc/sol/skr), `contents`
(`cosmetics[]`, `economy[{resource,amount}]`, `convenience[{kind,count,description}]`),
and the parity columns computed on the server:
`usd_anchor` (+ `usd_anchor_present`), `play_product_type` (+ `play_product_type_present`),
`sellable` (anchor present AND `store_visible`), `parity_gaps[]`.

Plus the REVERSE direction, because a list titled "All SKUs" that silently omits a row is
the same defect in a new costume: `anchors_without_pack` and `product_types_without_pack`.
The Monthly Ledger cards (`monthly-wayfarer`, `monthly-keeper`) live in
`battle_monthly.json`, not `packs.json`, and MUST show up there rather than vanish.

### 2c. Console tab

`api/admin/console.js` gains a "SKUs" tab under the existing "More tools" nav, fetched in
`load()` through the same `getJson` path as every other view. One row per pack, contents
as a nested list, and the WORD `MISSING` on any parity gap.

* NOT inlined at serve time like the tunable manifest: `packs.json` carries non-ASCII and
  the served page is pinned 7-bit ASCII (`test/command-center.test.js`, "the served page
  is 7-bit ASCII from end to end").
* The key-in-memory model is UNCHANGED.
* The state is a WORD, never a hue - the owner is red/green colourblind. `MISSING` carries
  the meaning; the red class is decoration only.

## 3. Files

| File | Change |
|---|---|
| `tools/gen-sku-catalog.mjs` | NEW - generator |
| `api/_lib/sku-catalog.generated.json` | NEW - generated copy of the canonical file |
| `api/_lib/sku-catalog.js` | NEW - the one reader + pure parity builder |
| `api/admin/stats.js` | `view=skus`, dispatched before `neon()`; header GET list |
| `api/admin/console.js` | "SKUs" tab, `renderSkus()`, `load()` fetch, READ list |
| `test/admin.skus.view.test.js` | NEW - gate, shape, parity, generated-copy parity, SELECT-only |

## 4. What NOT to touch

* No write path. No `INSERT`/`UPDATE`/`DELETE` anywhere - the SELECT-only lint governs.
* `.vercelignore` stays as it is. Un-ignoring one file under `Assets/` cannot be proven
  from an edit-only lane and a wrong pattern uploads the whole Unity tree.
* The refusal-logging block in `stats.js` is built ON, not replaced.
* `USD_ANCHORS` and `PRODUCT_TYPES` are READ. This ticket reports gaps; it never closes
  one by authoring a row.

## 5. Acceptance

1. `GET ?view=skus` with no `X-Admin-Key` is refused and logs one `[ops-refusal]` line
   with `view:'skus'`. (The refusal status is **400**, not 401 - `stats.js` returns 400
   for `Unauthorized`, copied verbatim from `db.js` as one shared admin auth scheme.)
2. With the key: 200, one row per pack in `packs.json`, in authored order.
3. Every row carries the six parity fields; a pack with no anchor reports
   `usd_anchor_present:false` and a `parity_gaps` entry naming it.
4. A SYNTHETIC pack with no anchor and no product type, passed to the pure builder,
   produces both gaps - proven without HTTP and without the canonical file.
5. `anchors_without_pack` contains the two monthly cards.
6. `api/admin/stats.js` stays SELECT-only under the existing lint, and the skus view
   opens no DB connection (proven by running it with `DATABASE_URL` unset).
7. The generated copy equals the canonical file.
8. Whole suite green.

---

**RESULT (2026-09-06):** implemented as specified. RED first (5 failing assertions on the
missing view), then GREEN. `node --test test/` summary recorded in the RESULT file.
