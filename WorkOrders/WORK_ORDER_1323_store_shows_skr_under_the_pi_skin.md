# WORK ORDER 1323 — The Night Market prices everything in $SKR under the Pi skin

**Status:** DONE
**Silo:** Monetization / Pi / Store
**Minted:** 2026-09-02 (CLI) from an owner felt-test in REAL Pi Browser.
**Severity:** P1 — a Pi player is quoted a token they do not hold and cannot spend.

## Owner report + screenshot

> *"All in skr"*

Signed in as Pi (Title chip reads **"Pi: samanthadenelle"**), skin resolved to Pi — and the Night
Market shows **1022 SKR**, **2555 SKR**, **511 SKR**, **BUY - 255 SKR**, plus *"Connect a wallet to
see your balance"* and a Mainnet/SKR readiness chip.

## Two measured causes

1. **There are no Pi prices to show.** Every pack's `pricing` block in
   `Assets/Resources/Data/Canonical/packs.json` carries exactly `['skr','sol','usd','usdc']`.
   **No `pi` key exists on any of the 28 packs.**
2. **The shelf is not skin-aware.** `Assets/_Modules/Wallet/PackStore.cs:81` -
   `[SerializeField] private CurrencyKind _defaultCurrency = CurrencyKind.Skr;` and the surface is
   written throughout in SKR terms (balance chip, "Connect a wallet to see your balance", the
   valuation refresh at `:574`).

WO-1318 wired the Pi **quote endpoint** for ONE sku (`hearth-spark`) via `/api/pi/quote`. It never
touched the shelf's DISPLAY. So the rail exists and the storefront still speaks SKR.

## ⚠ The owner's standing rulings that constrain this

- **SKR is Solana Mobile's governance token. It is not ours, never minted, never held.**
  `PackStore.cs:851-854` states it in-code: *"THE GAME NEVER HOLDS SKR AND MUST NEVER READ AS IF IT
  DOES. There is NO in-game SKR ledger, earn loop or spend loop."* Showing SKR to a Pi player is that
  same error pointed at the wrong audience.
- **Pricing uses the CoinGecko `low_24h` floor** (owner: *"just like with SKR we're gonna do the floor
  of 24 hour window"*), server-side, fail-closed — already implemented in `api/_lib/pi-payments.js`.
  **The client must never compute or invent a Pi price.** Ask `/api/pi/quote`; if it refuses, refuse.
- ⛔ **Do NOT hardcode `pi` numbers into `packs.json`.** The Pi price is a live-rate derivation, not an
  authored constant. `usd` is the anchor; `pi` is derived per request. Authoring a static `pi` value
  would drift the moment the rate moves and would bypass the server as the pricing authority.

## What "correct" looks like

Under `SkinAuthMode.PiSdk` the shelf shows Pi amounts sourced from the server quote, alongside the USD
anchor it already displays, and the wallet/balance furniture is replaced by Pi-appropriate wording.
Under the SKR skin nothing changes at all.

⚠ `hearth-spark` carries `storeVisible: false` (WO-1069 — deliberately shelved as dominated by
`starters-hand`). **The only Pi-enabled sku is therefore not on the shelf.** Do not flip that flag; it
reverses a pricing ruling. Either surface it through the existing `StoreFocusRequest` spotlight latch,
or extend Pi quoting to the visible skus — **that choice is the owner's, and is listed below.**

## Acceptance criteria

1. Under the Pi skin, no `$SKR` figure, balance chip or "connect a wallet" string is reachable in the
   store. Prove it from a CAPTURED Pi Browser session, not from reading code.
2. Prices shown come from `/api/pi/quote` (server, `low_24h`). A quote failure shows a clean refusal —
   never a locally computed or stale number.
3. Under the SKR skin the store is byte-for-byte unchanged. This is the regression risk; prove it.
4. No `pi` price constant is added to `packs.json`.
5. `COMPILE_GATE_OK` + `REGRESSION_OK`, with a suite pinning "Pi skin => no SKR string in the store
   surface".

## What NOT to touch

- ⛔ Do not alter the SKR/Solana purchase path, `walletAllowed`, or `MAINNET_SALES_ENABLED`.
- ⛔ Do not flip `storeVisible` on any pack.
- ⛔ Do not let the client price anything. Server is the pricing authority (WO-1318's whole security model).
- ⛔ Do not weaken `PiIsPiBrowser` / the WO-787 host routing — WO-1317 proved it correct in the field.

## Open question for the owner

Only `hearth-spark` is Pi-quotable today, and it is off the shelf. Does Pi pricing extend to the
visible packs (`starters-hand`, the Resource Packs, the cosmetic bundles), or does the Pi shelf show
just the one spotlighted starter until the rail is proven by a real purchase?

---

# OWNER RULING 2026-09-02 — spotlight `hearth-spark`, do NOT widen Pi pricing yet

Asked directly, given that a Pi player currently sees every pack in USD with **no buy control
anywhere**, she chose: **surface the one Pi-priced pack through the existing `StoreFocusRequest`
latch, without touching `storeVisible`.**

Her reasoning, and the reason this is the right call: **no purchase has ever completed in this game.**
Proving `quote -> approve -> complete -> grant` on ONE sku is worth more than a full shelf where a
single rail bug hits all 28 at once. Widening afterwards is a server-side list change, not code.

## What this authorises

- Use the EXISTING `StoreFocusRequest` spotlight latch to make `hearth-spark` reachable and buyable
  under the Pi skin.
- ⛔ **`storeVisible: false` on `hearth-spark` STAYS.** It is a WO-1069 pricing ruling (dominated by
  `starters-hand` at the same $4.99) and this does not reverse it. The pack is spotlighted, not shelved.
- ⛔ Do NOT add other skus to the server's Pi-quotable list.
- The honest empty-shelf copy the implementation added stays for every OTHER pack — it is correct.

## What must still be proven before widening

A real Pi purchase completing end to end, evidenced by `[Flow:PiPay] purchase COMPLETE` in the live
trace sink plus a `pi_payments` row at `state='granted'`. Not by reasoning.

## ⚠ Carry-over risk flagged by the implementation, still open

`/api/pi/quote` is called for DISPLAY without `EnsurePaymentsScope` (deliberate — browsing must never
raise a Pi consent sheet). It takes `{sku, uid}` and the uid falls back to
`PiSignInController.SignedInUid`, which may be empty. **If the server requires a scoped uid, the shelf
degrades to the words "Priced in Pi at checkout" and the Pi figure never appears** — honest, but the
spotlight would show no price. Verify this against the live endpoint before calling the spotlight done.
