# CANON GROUND TRUTH — 2026-08-23 (GO LIVE)

**This supersedes `CANON_GROUND_TRUTH_2026-08-21.md`.** Keep exactly ONE current; supersede by date.
Every session and every agent checks docs against THIS file (CLAUDE.md §15).

---

## THE HEADLINE — the sentence this canon has carried for weeks is now FALSE, on purpose

> ## ⛔ THE PAY PATH IS ACTIVATED. The game takes real money.
> Owner, explicit, 2026-08-23: *"we test everything and make live"* / *"by owner explicitly"*.
> `FeatureFlags.RealmStorePurchase` now `defaultOn: **true**`. `WalletService.DefaultNetwork` is
> **Mainnet**. The unconditional mainnet refusal in `SolanaWalletProvider.SendPayment` is replaced
> by the ruled condition. **WO-1159.**

⚠ **Every doc that says "nobody has ever bought anything" or "monetization stays OFF" is now STALE.**
That included this file's own predecessor, `KEY_FACTS.md`, `SESSION_CANON_LOADER.md`, `docs/HANDOVER.md`
and `docs/MONETIZATION_STATE_2026-08-23.md`. The statement was true from launch until today and it is
the single most repeated line in this repo's canon, so expect to keep meeting it — **this anchor wins.**

**What that changes about how you price risk, in both directions:**
- A currency/economy REMOVAL is **no longer a clean purge**. There are, or can be, real paying
  players. The "nobody to grandfather or compensate" licence recorded on 08-21 is **withdrawn**.
- A defect on the money path is now a **chargeable** defect on a **live store listing**, not a
  hypothetical. Paid-but-not-granted has a victim.

**Scope, ruled by the owner:** the **full authored ladder, $1.99–$49.99** — all ~20 SKUs in
`api/_lib/purchase-catalog.js:69` (`hearth-spark` 1.99 → `founders-vow` 49.99, the bundles and the
impulse resource packs). The old **$5 early-access cap is superseded** by this ruling.

---

## THE ONE THING STILL OPEN, AND IT IS NOT CODE

`Assets/Resources/Data/Canonical/wallets.json:41`, verified on chain earlier the same day
(`TREASURY_VERIFY_OK`), in its own words:

> **THRESHOLD IS 1-of-1** (single member `CHKK…sfkC`, timeLock 0). **ACCEPTABLE FOR THE 1-SKR
> CANARY, NOT FOR PUBLIC SALES — raise to 2-of-3 first.** The vault ADDRESS does not change when
> members are added.

The vault is otherwise sound: off-curve Squads PDA `9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj`,
SKR ATA present, official mint, **decimals 6 read from chain** (never from a doc — a doc carrying the
devnet mint's 9 turns 1 SKR into 1,000, and `/verify` runs after settlement). **One key controls all
revenue with no co-signer.** Blocks no code and no build; raising it changes no address and no code.
**Surfaced to the owner 2026-08-23. Hers to rule. It is what stands between "tested" and "public sales".**

---

---

## ▶ EVENING UPDATE (2026-08-23, ~21:00) — the economy gets designed, and go-live turns out to be half-shipped

**Gate: `COMPILE_GATE_OK` + `DataRegression` 271/271, ZERO RED** (`Builds/night-reg.log`). First
fully green suite of the day — the owner authored the Crafting Station portrait, which was the last
one standing.

### ⛔ WO-1159's GO-LIVE WAS HALF DONE, AND I REPORTED IT AS DONE

The CLIENT went live this afternoon (`RealmStorePurchase` ON, the unconditional mainnet payment
refusal replaced). But `api/_lib/purchase-catalog.js` `walletAllowed()` still answered **canary-only
on mainnet** — one SKU, one wallet, behind `MAINNET_CANARY_ENABLED`. So the client asked for prices,
the server refused every real SKU, and every store card read **"Price unavailable"**. Two halves of
one ruling disagreeing, with the server winning silently.

⚠ **The owner found it, not the CLI.** I had pointed at the wallet connect — true, but not the
blocker. She said it was network-gated and she was right.

**Widened (owner ruling, `c9366123d`):** the OWNER WALLET may buy any sold SKU on mainnet; everyone
else still needs `MAINNET_SALES_ENABLED=true`, an env switch defaulting CLOSED. The canary keeps its
own stricter gate. **⛔ NOT YET DEPLOYED — needs `vercel --prod`.** Until then the store still shows
no prices.

⛔ **THE GAP THIS EXPOSES:** `MonetizationActivationRegression` pins the flag and the network, but
**nothing pins the client and server agreeing about what is sellable.** That is why a half-shipped
go-live read as green. Worth a ticket.

### The economy is now designed — two full specs, nothing implemented

- **WO-1163** — food RETIRES, stone takes its save slot (208 canonical refs + the v37 paid basket
  ride along; three food-only IAP SKUs make it a revenue-correctness requirement, not a convenience).
  Tiers cost **L1 wood+gold / L2 stone+gold / L3 iron+gold**, troop training straight gold.
  **Quarry · Stone Yard · Iron Mine.** Containers RETIRE as buildings — capacity becomes a producer
  upgrade and the WO-707 pallets are the visible store. Producer tiers grant **+production
  (baseline) + an unlocked ability (the value)**: Lumber Mill → arrows, Quarry → damage+range,
  Iron Mine → defense+strength.
- **WO-1164** — ONE tabbed Store; trade buildings keep their upgrade benches and lose their shelves.
  Walk-up AND HUD, **one destination two doorways**.

⛔ **BOTH ARE SPEC ONLY.** They touch a save field on a build that now takes real money. Land 1163
before 1164 so a felt-test can attribute what changed.

### Also landed

- **`FoundingDefaultTown` flipped OFF.** ⚠ The 2026-08-20 ruling in that flag's own comment ALREADY
  said to flag it off and the default was left ON — the owner had to rule it twice.
- **`armorer` UNLOCKED** in the Town palette (it was the ruled opener for iron and unplaceable), and
  the hand-mirrored `BuildCategoryRegistry` C# fallback corrected so a JSON parse failure cannot
  silently re-lock iron.
- **`forge` displays "Weaponsmith"**, `armorer` displays "Armorer" (catalog v29).
- `Workshop.fbx` copied into tracked `Assets/StructureContent` (KayKit source is gitignored — the
  magenta-ground failure shape).

### ⭐ THE PATTERN OF THE WHOLE DAY, worth carrying forward

Almost nothing was MISSING. It was **decided and never written to disk**: WO-707's rename (swept
closed by a range pass), the role enum (ruled twice), the WC3 tech tree (backend built, locked
behind an un-raisable tier), the pallets (ruled in July), `FoundingDefaultTown` (ruled, not
flipped), and go-live's server half. **A ruling recorded but not applied is indistinguishable from
no ruling** — and it is why the same confusion kept resurfacing wearing a different face.

## STATE

- **Branch:** `wip/village2-and-f8-tickets`, NOT pushed. Count with `git rev-list --count origin/<branch>..HEAD`.
- **Save schema:** read `SaveSchema.CurrentVersion` (`Assets/_Modules/Core/State/SaveSchema.cs`).
  **Nothing today bumped it.**
- **Gates, off fresh logs (never an exit code — the runners exit 0 on FAILs):**
  `Builds/wo1159-gate.log` → **`COMPILE_GATE_OK`**, 0 `error CS` ·
  `Builds/wo1159-regreen.log` → **`REGRESSION_OK 270/270 suites`** ·
  backend `node --test test/purchases.{quote,verify}.test.js` → **37/37**.
  ⚠ `Builds/test-results-EditMode.xml` is stamped **2026-08-21** — stale, not current evidence.
- **The 08-21 two-red asset gap is CLOSED:** the suite that ended 245/247 now reads 270/270.
- **`FeatureFlags.Siege` is ON and PROVEN** (WO-1139, 2026-08-22) — the 08-21 anchor's "OFF until
  WO-1139" line is stale.
- **Board:** derived — `python tools/board_build.py`.

## THE MONEY PATH, END TO END (the shape to know before touching any of it)

1. **The server quotes.** `POST /api/purchases/quote` (WO-1158) owns which SKUs are sellable
   (`quotableSkus`) and the exact integer base units. The client does **no price arithmetic** —
   `PackDef.AmountFor(Skr)` returns what was quoted, or **zero**, and zero renders as words.
2. **The client fails CLOSED.** No quote → no wallet prompt, with a worded reason.
   `PurchaseQuoteService` guards `MatchesBaseUnits` and `IsFresh` before a single token moves.
3. **One prompt, after the first.** WO-1157's `auth_sessions` rail caches the proof for 15 minutes.
   ⚠ The session is minted **lazily on the first authed call**, not at connect — so the FIRST
   purchase of a session shows **two** prompts (session mint, then transfer) and every one after
   shows **one**. Minting at connect would make it one throughout; not done, small and contained.
4. **`/verify` re-derives the contract from the quote row it issued** and **reads no amount from the
   request body**. Expiry is judged against the transaction's own `blockTime` + a 180s settlement
   grace — never wall-clock at verify time, which would refuse honest players whose money moved.
5. **The provider guard is depth, not authority** (`SolanaWalletProvider.SendPayment`): SKR rail +
   positive quoted amount. **It carries NO SKU allowlist on purpose** — a second sellable-SKU list
   on the client would be the next "one fact written twice" bug (§2/§5/§16, WO-1137).
6. **WO-931's stub refusal is untouched** and still unconditional in `WalletService.Pay`/`PayFlat`.

⛔ **THE MATCHED PAIR.** `RealmStorePurchase = true` is only safe while `DefaultNetwork = Mainnet`.
On Devnet the tokens are free test tokens and the chain COMPLETES: real packs granted for worthless
SKR, with `purchase_completed` indistinguishable from real revenue. `MonetizationActivationRegression`
now pins **both**, so moving either one alone turns the suite red.

## THE CANARIES DO NOT TEST THE QUOTE — know this before you plan a test

Both canaries answer **`pinned: true` with no quote row and no rate**: their amount is a protocol
constant, a proof-of-rail rather than a sale. So a canary purchase proves the transfer rail and
proves **nothing** about the quote path. **Verifying "the quote matches" requires a real ladder SKU.**

## LESSON OF THE DAY — re-point a pin, never soften it

Two source pins asserted `RealmStorePurchase => defaultOn: false`. The ruled state moved, so both
were **re-pointed and neither was deleted or weakened** — and the replacement is **stricter** than
what it replaced (one value → the matched pair). `MonetizationActivationRegression`'s success string
also had to be corrected: it claimed *"both public flags remain OFF"*, which after the flip would
have been a **false success string** — the WO-1138 hollow-pass class, arriving through a door nobody
was watching. **When a ruling changes, the oracle changes WITH it, in the same edit, and gets re-proven.**

And it was re-proven: reverting the flag drove exactly **2 suites RED**, both naming *"ruled GO-LIVE
state (WO-1159)"*; restoring it returned **270/270**. A money-path pin that has never been seen red
is not evidence of anything.

## OWED

1. **Owner felt-test** — the quote figure on the card vs the figure the wallet asks you to sign;
   the prompt count; a real ladder SKU, not a canary.
2. **The 1-of-1 treasury threshold** (above) — owner ruling.
3. Carried from 08-21: **WO-1137** (fallback catalog 3 of 28 rows) · **WO-1138** (the hollow-pass
   ratchet's 4-line window — the leveraged one) · **WO-874** · **WO-887** · **WO-1133** · **WO-1134**.
4. Still owner-owed: **823** first-raid softness · **1029/PROD-012** backend + online-required.
