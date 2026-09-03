# WORK ORDER 1354 - The "Close the Gap" rail is built once and never repriced

**Status:** FIXED 2026-09-03 - ON HER DEVICE (installed, R2_PUSH_OK + R2_PARITY_OK). The "CLOSE THE GAP" rail is now rebuilt from `Render()` when quotes arrive, instead of being stamped once in `EnsureBuilt` BEFORE any quote existed and then skipped forever by `Render()`'s `continue` past `StoreBand.Gap`. Same root, two faces - a frozen UNAVAILABLE, or (if `PacksInBand(Gap)` was 0 on that one pass) no rows at all and never again - which is why it read as intermittent. The empty state now draws WORDS rather than vanishing. The server was healthy all along: my GET probe was the wrong call shape (the endpoint is POST-only, so a known-good SKU 400'd too), and my link to the Vercel-log 400s was wrong - those name /api/entitlements and /api/catalog/collection. Nav doors isolated from a catalogue failure. Gates COMPILE_GATE_OK + REGRESSION_OK 358/358; the oracle's mutation is HEAD itself. AWAITING HER FELT-VERIFY that all three catch-up offers show real prices - then Owner Validation closes it. Follow-up: banner-correct WO-1335's RESULT, whose "no server row" premise is disproven.
*(This ticket sat at IMPLEMENTED, NOT SHIPPED until the install, under her correction this session:
**FIXED means it is on her device to test** - not "code complete", not "committed". It became FIXED at
the moment it reached her Seeker, and not before.)*
**Silo / Lane:** Store / monetization surfacing
**Type:** EXISTING surface, a build-order defect
**Minted:** 2026-09-03 (CLI) on her report. ⚠ Minted AFTER the fix was written, which is the process
miss her ruling addresses: *"new issue we create a ticket"*.
**Severity:** P1 - the revenue surface. The catch-up offers are the packs a stuck player is most likely
to buy, and they were unbuyable for whole sessions.

## Her report

> *"close the gap pacsk have no price available"* (earlier today)
> *"the close the gap are back to not showing"*

⚠ **"back to" is the load-bearing word.** WO-1335 investigated the first report and concluded the client
was fine and *"the quote service held no server row on that pass"*. **That premise is now disproven** -
its RESULT should be banner-corrected rather than left to re-seed the next investigation.

## The proven cause - a build-order defect, not a data or server one

The client data is correct. All three rows carry what the shelf needs:

```
impulse-wood-medium    storeVisible=True  shelfCurated=True  band=gap  usd=2.99
impulse-iron-medium    storeVisible=True  shelfCurated=True  band=gap  usd=2.99
impulse-stone-medium   storeVisible=True  shelfCurated=True  band=gap  usd=2.99
```

In the **landscape** composition (her Seeker) the Gap band is not on the shelf at all -
`PackStore.Render()` explicitly skips it (`if (_utilityContent != null && band == StoreBand.Gap)
continue;`) because those three rows live in the right-hand "CLOSE THE GAP" rail instead.

**That rail is built ONCE, in `EnsureBuilt` -> `BuildLandscapeGapOffers()`, and nothing ever rebuilt
it.** The chain:

1. `OnEnable` -> `EnsureBuilt()` -> rail built -> `Render()` -> `RefreshQuotedPrices()`.
2. The rail is therefore stamped **before any server quote exists**. A 0 amount makes
   `SolanaPackPricing` deliberately return the words `"Price unavailable"`, which the row renders as
   literally **`UNAVAILABLE`** - the exact word she photographed.
3. When the quote list lands, `RefreshQuotedPrices` calls `Render()`, which repaints Basket and
   Patronage with real figures and **`continue`s past Gap**. The store host is spawned once and kept
   for the session, so `EnsureBuilt` never runs again either.

⭐ **Same root, two faces, which is why it read as intermittent:** normally the rows appear with a
frozen `UNAVAILABLE`; but if `PacksInBand(Gap)` returns 0 on that one build pass, the builder returns
early and **the heading and all three rows never appear at all and never come back**. That is the
literal "not showing".

## ⚠ THE SERVER IS HEALTHY - AND MY OWN DIAGNOSIS WAS WRONG TWICE

`api/purchases/quote.js:158-160` is **POST-only** with `bodyParser: false`; a GET returns
`quietFail(res, 400, METHOD_NOT_ALLOWED)`. **My GET probe returned 400 for every SKU including a
known-good one, and I reported that as evidence of a defect. It was my call shape.** Called correctly,
against production:

```
POST /api/purchases/quote -> 200  mode=list  rate=0.01925478  rateSource=coingecko:seeker:low_24h
impulse-wood-medium      156 SKR  usdAnchor 2.99  sellable=true
impulse-iron-medium      156 SKR  usdAnchor 2.99  sellable=true
impulse-stone-medium     156 SKR  usdAnchor 2.99  sellable=true
impulse-crystals-medium  156 SKR  usdAnchor 2.99  sellable=true
```

**No DB row is missing. No env var is absent. Nothing server-side needs applying.** And ⛔ **the
Vercel-log 400s are NOT this bug** - those name `/api/entitlements` and `/api/catalog/collection`;
`/api/purchases/quote` does not appear in that export's failure set. I linked them and the link was
wrong. Both of those remain unticketed and unrelated.

## The fix (in the tree)

`Assets/_Modules/Wallet/PackStore.cs`
- New `RebuildLandscapeGapOffers()` - clears **only** `_gapUtilityContent` and rebuilds.
  `_utilityContent` (ACTIONS / REDEEM / MONTHLY LEDGER) is a separate transform and is untouched, so
  the nav doors survive a catalogue failure.
- Called from `Render()`, so a returning quote repaints the catch-up offers with every other band.
- `BuildLandscapeGapOffers` gains a **worded** empty state instead of a bare `return`: the heading plus
  `"Catch-up offers - Unavailable right now"`. ASCII, no hue carrying meaning, plus a `FlowTrace.Warn`.

⛔ Nothing touched: prices, SKUs, entitlements, grants, `pricing.usd`, the SKR peg,
`purchase-catalog.js`. Both `packs.json` twins verified byte-identical and unmodified by sha256, and
each row still carries exactly one economy key.

## Oracle - proven RED against HEAD

`ImpulsePackRegression` CASE 13 `[gap-reaches-shelf]`, wired into the existing `Run()`. Two halves:
- **data:** every `storeVisible && shelfCurated` impulse row must be `BandOf == Gap` and pass
  `IsOnBrowsableShelf`, and the Gap band must have >= 1 browsable row.
- **source, pinned as a PAIR:** if `Render()` skips `StoreBand.Gap` then `RebuildLandscapeGapOffers()`
  must exist **and be reached from inside `Render()`** - not merely from `EnsureBuilt`, which is the
  whole defect. Plus: the empty-catalogue branch must draw words.

**The mutation is HEAD itself** - the code she is running:
```
HEAD:  skipsGapOnShelf=True hasRail=True rebuilds=False reachedFromRender=False -> RED x3
tree:  GREEN
```

## Acceptance

- [x] Cause proven from source; server proven healthy by a correct POST.
- [x] Fix rebuilds the rail on every render; nav doors isolated from a catalogue failure.
- [x] Empty state reads in WORDS, not an absence.
- [x] Oracle proven RED against HEAD.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` on fresh logs (lead).
- [ ] Committed and shipped to her device -> **only then does this become FIXED**.
- [ ] ⛔ Owner sees real prices on all three catch-up offers and signs off in Owner Validation.
- [ ] Follow-up: banner-correct WO-1335's RESULT, whose "no server row" premise is disproven.
