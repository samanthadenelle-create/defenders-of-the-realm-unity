# WO-1532 RESULT - Command centre: a read-only SKU catalog view with contents and rail parity

**Status:** DONE (edit-only lane; NOT committed, NOT deployed - the CLI seat commits)
**Date:** 2026-09-06
**Suite:** `node --test "test/*.test.js"` -> **tests 424 / pass 424 / fail 0** (36 test files)
`node --test test/admin.skus.view.test.js` -> **23 / 23**

---

## RED first, then GREEN

Before any implementation, the new suite ran against the tree and failed **7** cases:

```
X ?view=skus answers the whole catalog with DATABASE_URL unset
X every row carries the descriptive fields the owner asked for
X contents are read from the canonical file, not summarised away
X the parity columns are computed on the SERVER, against the real tables
X the two promo-grant-only packs are LISTED and are not counted as a gap
X anchors_without_pack names the Monthly Ledger cards instead of vanishing them
X the console renders a SKUs tab and fetches it through the read gate
```

The three gate cases passed from the start, because they exercise the refusal block that
already existed at HEAD `f957bdbaa` - which is the correct RED shape: the gate is reused,
not rebuilt.

## Files

| File | Lines | What |
|---|---|---|
| `tools/gen-sku-catalog.mjs` | NEW | copies `packs.json` verbatim to `api/_lib/` |
| `api/_lib/sku-catalog.generated.json` | NEW | the copy (29 packs, 51288 bytes, LF, no BOM) |
| `api/_lib/sku-catalog.js` | NEW | the join + the pure `parityRow()` |
| `api/admin/stats.js` | +40 | `?view=skus`, dispatched above `neon()`; header GET list |
| `api/admin/console.js` | +130 | SKUs tab, `renderSkus()`, `load()` fetch, CSS, READ list |
| `test/admin.skus.view.test.js` | NEW | 23 cases |

## Findings worth the owner's attention

1. **No server code has ever read canonical game data from `Assets/` at runtime, and it
   cannot.** `.vercelignore` allowlists only `/api`, `/Builds/WebGL` and the configs, and
   `vercel.json` sets `git.deploymentEnabled:false`, so production is a CLI upload of that
   allowlist. A `require('../../Assets/...')` in `stats.js` would throw at MODULE LOAD in
   production and take down **every** view on the endpoint while working perfectly on this
   machine. The precedent followed instead is the tunable manifest's:
   generate into `api/_lib/`, pin the copy with a test that reds on drift.
2. **The refusal status is 400, not 401.** `api/admin/stats.js:314` returns
   `res.status(400).json({error:'Unauthorized'})`, copied verbatim from `api/admin/db.js`
   so the admin surface has one auth scheme. The spec asked for 401; the code is the
   authority and the test is written to it.
3. **Reverse parity found two real orphans.** `USD_ANCHORS` carries `monthly-wayfarer` and
   `monthly-keeper`, which are authored in `battle_monthly.json` `monthlyCards[]` and are
   NOT rows in `packs.json`. A list titled "All SKUs" built only from `packs.json` would
   have silently omitted two priced products - the WO-1165 s2 defect in reverse. They are
   named in `anchors_without_pack` and rendered on the page.
4. **No pack today has a parity gap.** Every one of the 29 packs is either anchored and
   Play-typed, or is `promoGrantOnly` (`welcome-500`, `welcome-100`), which is never
   offered for sale and so is not counted as a gap. The MISSING path is therefore proven
   on a **synthetic** pack through the pure builder and on injected state through the
   render harness - an unexecuted failure column is decoration.

## Not done, and deliberately

* **Not committed, not deployed.** Edit-only lane.
* `.vercelignore` untouched. Un-ignoring one file under `Assets/` cannot be proven from a
  lane that may not deploy, and a wrong gitignore pattern uploads the whole Unity tree.
* No anchor or product type was authored to close a gap. This ticket reports; it never
  repairs a money table.
* **Unproven from here:** that the deployed function serves `?view=skus` - that needs a
  deploy, which this lane may not do. What IS proven is that the module tree loads and the
  view answers with `DATABASE_URL` unset, which is the failure mode a copy-vs-Assets
  mistake would have produced.

## Follow-up for the CLI seat

`node tools/gen-sku-catalog.mjs` must be re-run whenever `packs.json` changes.
`test/admin.skus.view.test.js` reds if it is not, so the suite is the reminder - but
wiring the generator into whatever pre-deploy chain exists would make it structural.
Raised, not done, because that chain is outside this lane.
