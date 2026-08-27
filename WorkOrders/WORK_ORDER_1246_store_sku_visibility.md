# WORK ORDER 1246 - The live store sells three copies of one product, and hides SKUs for four different reasons

**Status:** CLOSED 2026-08-27 — owner Pass (felt-validated, APK 2026.08.27.343878).
**Silo:** Backend (`api/`) + store catalog
**Severity:** P1 for revenue. The store is LIVE and takes REAL MONEY.
**Origin:** Owner ruling 2026-08-27 - re-minted out of WO-1175, which was CLOSED AS MOOT. This was
riding as a footnote under a phase whose premise was dead, which is exactly how it would have been
closed by accident.

---

## Why this is its own ticket

WO-1165 §8 found that most non-incidental SKUs are hidden from the live store. WO-1175 then
compressed that finding into a single false cause - *"9 of 13 hidden for exactly one reason,
cosmetics do not render"* - which is **not what WO-1165 says**. Believing the single-cause version
means building one fix and expecting the store to open up. It will not.

⭐ **The real finding: they are hidden for at least FOUR distinct reasons, and each needs different
work.** Read WO-1165 §8 at source before starting; do not re-derive it and do not trust any summary
of it, including this one.

| Reason | Shape of the problem | Shape of the fix |
|---|---|---|
| **Duplicate clones** | `frostfall`, `embergrove`, `bloomtide` are **one product** sold three times | a catalog/merge decision, needs the owner |
| **Dominated pricing** | e.g. `impulse-wood-small` - a row no rational player picks because another row beats it outright | a pricing decision, needs the owner |
| **Tokens with no redeemer** | e.g. `echo-patron-pack` grants a token nothing consumes | code: build the redeemer, or stop selling it |
| **Inert buffs** | the SKU sells a buff that does nothing | code: implement, or withdraw the SKU |

Only the last two are engineering. **The first two are OWNER DECISIONS and must be raised as
questions, not guessed at.**

## ⛔ Money is real

Mainnet sales and SKR are live as of 2026-08-27. Consequences that bind this ticket:

- **A balance or catalog change can destroy something a player already paid for.** "No purchase has
  ever completed" is retired and is not available as a safety argument.
- ⛔ **Never delete or renumber a SKU id that has ever been sold.** Ids are live keys in
  `purchase_entitlements`. Withdraw by making it unpurchasable, never by removing the row a
  settlement points at.
- Never render or log a wallet, an email, or a real name anywhere in this work.

## Required

1. **An inventory, from source, of every SKU and why it is or is not visible today** - one row per
   SKU with its actual cause from the four above (or a fifth you can prove). This is the deliverable
   even if no SKU ships as a result.
2. For the two ENGINEERING causes: build the redeemer / implement the buff, or produce a
   recommendation to withdraw.
3. For the two OWNER causes: a question each, with a recommendation and the revenue consequence
   stated.
4. **Do not blend the four causes into one number.** "9 hidden" is the sentence that started this.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts read off the marker.
2. A regression pinning that every VISIBLE SKU has a working grant path - i.e. a SKU cannot become
   visible while the thing it sells does nothing. Prove RED first (WO-1138).
3. The SKU inventory table, source-cited, in the RESULT.
4. Owner felt-verifies the store on device.

## What NOT to touch

- ⛔ The purchase/settlement rail itself (`api/purchases/*`, `purchase_quotes`,
  `purchase_entitlements` schema). This ticket is about WHAT is offered, not how it is paid for.
- ⛔ `api/admin/db.js` / `stats.js` read-only contract.
- ⛔ Do not "fix" pricing or merge clones without the owner's ruling.
