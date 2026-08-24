# WORK ORDER 1159 — GO LIVE: real mainnet pack sales, on the full authored ladder

**Status:** FIXED 2026-08-23 — gated green (`COMPILE_GATE_OK`, `REGRESSION_OK 270/270 suites`, backend `node --test` 37/37), the new pin PROVEN red-then-green. ⚠ AWAITING OWNER FELT-TEST ON DEVICE, and ONE TREASURY ITEM IS OPEN (§5). Not closed by the CLI — the PO closes.

**Owner ruling (explicit, 2026-08-23):** *"we test everything and make live"*, *"by owner explicitly"*.
Scope ruled by the owner in the same exchange: **the full authored ladder, $1.99–$49.99.**

---

## 0. What this ticket is, in one line

`FeatureFlags.cs` has carried a four-step go-live order for weeks. The owner has now taken all
four, in that order. This ticket is the last two steps — replacing the unconditional mainnet
payment refusal with the ruled condition, and flipping the purchase flag's default — plus the
oracle work that keeps both honest.

## 1. The four steps, and where each one actually happened

The order is not ceremony. `FeatureFlags.cs` says *"Flipping this one first only ever produces a
Buy button in front of free goods"*, and that is exactly right: on Devnet the tokens are free test
tokens, the purchase chain COMPLETES, and real pack contents are granted against worthless SKR
while `purchase_completed` fires indistinguishably from real revenue.

| # | Step | Where |
|---|---|---|
| 1 | Mainnet decision + lift the mainnet block in `SendPayment` | owner explicit; the lift is **this ticket** |
| 2 | `WalletService.DefaultNetwork` off Devnet | `6802e2292` (already landed) — `WalletService.cs:242` reads `WalletNetwork.Mainnet` |
| 3 | A real signed transaction SETTLES on-chain | the 1-SKR mainnet canaries — **owner: "canary 2 success recorded"** |
| 4 | THEN this default, WO-931's seam refusal untouched | **this ticket** |

## 2. The code change — two files, and one thing deliberately NOT done

### `Assets/_Modules/Wallet/SolanaWalletProvider.cs` (the non-canary `#else` branch)

Was: refuse **every** mainnet payment (*"the v2 foundation is devnet-only (spec Part 10)"*).

Now: refuse a mainnet payment when it is **not SKR**, or when the **amount is not positive**.

⛔ **WHAT AUTHORIZES A SALE IS NOT THIS LINE, AND THE FILE SAYS SO.** The authority is the
**server-issued quote** (WO-1158): `api/purchases/quote.js` owns which SKUs are sellable
(`quotableSkus`) and the exact base-unit amount; `PackStore.Purchase` fails **CLOSED** when no
quote returns; `api/purchases/verify.js` re-derives its exact-equality contract from the persisted
quote row and **reads no amount from the request body at all**. This guard is defence in depth.

**There is deliberately NO SKU allowlist on the client.** One fact written down twice is this
repo's most expensive recurring failure — the stale WO-number block (CLAUDE.md §2), the retired
assembly table (§5), the hardcoded repo root (§0), the drifted R2 push (§16), WO-1137's 3-of-28
fallback catalog. A second sellable-SKU list here would be the next one. The two things the guard
asserts are things a provider can honestly know at that seam:

- **SKR is the rail** — the only currency with a live mainnet mint (`WalletEndpoints.SkrMintMainnet`)
  and a server quote contract behind it. A USDC/SOL mainnet transfer has no quote row, so `/verify`
  could only refuse it AFTER settlement: paid-but-not-granted, the exact family WO-1158 closed.
- **The amount is positive** — `PackDef.AmountFor(Skr)` returns **zero** when no server quote was
  issued (`PackCatalog.cs:293-318`), so zero means "nobody quoted this" and no transfer may be built.

### `Assets/_Modules/Core/FeatureFlags.cs`

`RealmStorePurchase` → `defaultOn: true`. The four-step block above the declaration is **kept and
extended**, not replaced, recording which step landed where and the one invariant that outlives it:

> If anyone ever moves `DefaultNetwork` back to Devnet, this default must come back to false in the
> SAME edit — those two values are only safe as a matched pair.

### NOT done, on purpose

- **WO-931's stub refusal is untouched.** `WalletService.Pay`/`PayFlat` still refuse a stub-typed
  provider unconditionally (not `#if`-guarded), before `SendPayment` is ever reached.
- **The `MAINNET_CANARY_TEST` branch is untouched** — the canary stays owner-wallet-only.
- **No pricing arithmetic moved to the client.** None existed after WO-1158; none was added.

## 3. The oracles — RE-POINTED, never softened

Two source pins asserted `defaultOn: false`. The ruled state moved, so the pins move with it. An
oracle that stops asserting because the answer changed is the hollow pass this repo pays most for
(WO-1138), so neither was deleted and neither was weakened.

- **`MonetizationActivationRegression.cs`** — re-pointed, and **now STRICTER than it was**. The old
  pin watched ONE value; the real invariant is a **PAIR**, so it now pins `defaultOn: true` **and**
  `WalletService.DefaultNetwork == Mainnet`. Moving either one back alone turns the suite red. Its
  success string was also corrected — it claimed *"both public flags remain OFF"*, which would have
  been a **false success string**, the precise WO-1138 defect class.
- **`WalletProviderSelectionRegression.cs`** — re-pointed, with the original free-pack hazard kept
  as recorded history and the reason the flip is now safe stated plainly: the hole was closed at a
  **stronger seam than the flag** (WO-931's unconditional stub refusal, asserted by that same
  suite's section 8). The gate did not weaken; a better one landed underneath it. Its "PROVE IT
  BITES" instruction is inverted rather than deleted — a pin nobody can re-prove is a pin nobody
  trusts.

## 4. Evidence — every marker read off a FRESH log, never an exit code

| Gate | Result |
|---|---|
| `Builds/wo1159-gate.log` | **`COMPILE_GATE_OK`**, `grep -c 'error CS'` = **0** |
| `Builds/wo1159-regression.log` | **`REGRESSION_OK 270/270 suites`** |
| `Builds/wo1159-bite.log` | flag reverted to `false` → **`REGRESSION_FAIL: 2 failures`**, both naming *"ruled GO-LIVE state (WO-1159)"* |
| `Builds/wo1159-regreen.log` | flag restored → **`REGRESSION_OK 270/270 suites`** |
| `node --test test/purchases.{quote,verify}.test.js` | **37/37 pass, 0 fail** — including *"the verified contract is built from the QUOTE ROW, so the body cannot carry a price"* and *"transferring a DIFFERENT amount than quoted is REFUSED"* |

The bite test is the load-bearing one. A money-path pin that has never been seen red is not
evidence of anything.

## 5. ⛔ OPEN — a TREASURY item, not a code item. Owner's to rule.

`Assets/Resources/Data/Canonical/wallets.json:41`, written and verified on-chain earlier the same
day (`TREASURY_VERIFY_OK`), says in its own words:

> **⭐ RESOLVED 2026-08-24 — THRESHOLD IS 2-of-3, timeLock 0, "production-shaped".** Re-read from
> chain with `tools/treasury-verify.mjs --multisig`. The 1-of-1 text this block used to quote was
> **STALE**: the owner raised the threshold, and because doing so re-authors no address and no code,
> nothing in the repo was forced to notice. **This was the last red on go-live.**

The vault itself is sound — off-curve Squads PDA `9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj`,
SKR ATA present, official mint, **decimals 6 read from chain**, linkage proven from multisig
`BcHLoNCsnGD6oegywkP19PALKMQYoFeQWTvmPLmp22no`. The only red is the threshold: **one key controls
all revenue with no co-signer**, so key loss or compromise is total loss with nothing to recover
against.

This blocks no code and no build. Raising the threshold changes **no address and no code**.
Surfaced to the owner 2026-08-23; it is hers to rule, and it is the one thing standing between
"tested on device" and "public sales".


### ⭐ PROPOSED, NOT DONE — make the ship chain COMPUTE the threshold (owner call)

The 2026-08-24 sweep found the threshold cached in **nine** files while the chain said something
else for a day. The nine are corrected, but correcting copies does not stop the next copy. **The
structural fix is the §16 lesson applied here: call the one file, do not restate it.**

`tools/r2-ship.ps1` already proves this shape works for bundles. The equivalent would be a
`treasury-verify.mjs <vault> --multisig <ms>` call in the ship chain, marker-judged on
`TREASURY_VERIFY_OK`, so a build that reaches a device has *computed* the threshold rather than
trusted a sentence.

⚠ **THE TRADEOFF IS REAL AND IT IS THE OWNER'S CALL:** this puts a **mainnet RPC round-trip on the
ship path**. Public RPC is rate-limited and occasionally down, so a green build could be blocked by
someone else's outage. Options: (a) block, like R2 parity; (b) `-WarnOnly`, like the sideload path;
(c) leave it manual and re-run the verifier at each go-live decision. **Not wired either way** —
adding a network dependency to the ship chain is not a change to make unilaterally.

⛔ Whatever is chosen: **always pass `--multisig`.** Without it the tool returns
`TREASURY_VERIFY_OK` having read no threshold at all — a green that proves the vault, not its safety.

## 6. What still needs the owner's hands (felt-test)

1. **The quote matches** — the SKR figure on the pack card must be the SKR figure the wallet
   prompts you to sign. The tests prove the server and chain agree; only eyes prove the two
   surfaces render the same number.
2. **The prompt count.** Expect **2 visible prompts on the first purchase of a session** (session
   mint, then the transfer) and **1 on every purchase after, within 15 minutes**. The session is
   minted lazily on the first authed call, not at connect — see WO-1157. If one prompt on the
   first buy is wanted, minting at connect is a small contained change.
3. **A real pack, not the canary.** The canaries answer `pinned: true` with **no quote row**, so
   they prove the rail and do **not** exercise the quote path. Verifying the quote needs a real
   ladder SKU.

## 7. Files touched

- `Assets/_Modules/Wallet/SolanaWalletProvider.cs` — the ruled mainnet condition
- `Assets/_Modules/Core/FeatureFlags.cs` — `defaultOn: true` + the four-step record
- `Assets/Editor/Regression/MonetizationActivationRegression.cs` — re-pointed + matched-pair pin
- `Assets/Editor/Regression/WalletProviderSelectionRegression.cs` — re-pointed pin
- `CLI_LANES_WO_NUMBERS.md` — minted 1159, bumped 1159 → 1160 in the same edit
