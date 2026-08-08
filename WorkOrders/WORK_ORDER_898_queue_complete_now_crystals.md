> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: `crystalsPerBracket` = 0 hits repo-wide (VERIFIED at source) - the headline data-driven pricing mechanic was never built; there is no impulse dialog and no Jupiter fallback. Only the progress bars shipped. Note also that the monetization premise MOVED when commit ef40c0e7 purged premium-currency ad rewards.
> The previous Status line read "SPEC - READY (grounding in sec.3)" and was wrong.

# WORK ORDER 898 — Queue: progress bars + "Complete now" with crystals (any item, any channel)

**Status:** PARTIAL (reconciled 2026-08-08) · **Silo:** Queue / economy / monetization / UI · **For:** CLAUDE CLI · **Date:** 2026-08-05
**PO:** Samantha (owner) · **Author:** UI seat
**Owner rulings:** *"show progress bars for items and offer an option to complete now, purchase with crystal to complete"* · *"can choose ANY item in the queue and complete now (X crystals)"* · *"if an invasion is starting they might opt to spend for it."*

## 0. The feature (and why it earns money)
Every timed job in the Obsidian queue (Builder / Train / Research — any channel) shows:
1. **A progress bar** — how far along it is (filled = elapsed, tied to `RemainingSec` / total).
2. **A "Complete now · X crystals" action** on **ANY item — active OR still queued** — that finishes it instantly for crystals.

**The monetization driver (owner):** an invasion/raid is imminent, a wall/tower/troop batch is still cooking → the player spends crystals to have it ready in time. That pressure is the sell; the button just has to be there, on everything, the moment they feel it.

## 1. Behavior
- **Progress bar:** on every active job (elapsed/total). Queued-but-not-started items show an empty bar + "queued" (they start when a slot frees).
- **Complete now (active item):** spend `X` crystals → the job finishes instantly, its result is granted, the slot frees, the next queued job starts.
- **Complete now (queued item):** spend `X` crystals → that item is completed instantly and granted **regardless of queue position** (it does not need to reach the front first). Owner: "choose anything in the queue and complete now."
- **Crystal cost `X` (owner formula, 2026-08-05):** directly tied to **remaining** time, **stepped in 5-minute increments** — the cost drops one step each time the remaining time crosses a 5-minute boundary (more time left = more crystals). **Below 5 minutes remaining → a FLAT crystal cost** (a fixed floor). So:
  - remaining ≥ 5 min: `X = crystalsPerBracket × ceil(remainingMinutes / 5)` (e.g. 20 min → 4 brackets, 12 min → 3, 6 min → 2).
  - remaining < 5 min: `X = flatUnder5` (a single fixed cost, no scaling).
  - `crystalsPerBracket` and `flatUnder5` are **data-driven** (owner-tuned economy data), never hardcoded.
  - A **queued** (not-started) item costs against its FULL duration (top bracket). The displayed cost **updates live** as the timer crosses each 5-minute boundary (steps down).
- **Confirm the spend:** tapping Complete now shows a one-line confirm ("Finish <name> now for X crystals?") before the crystals are spent — no accidental crystal burn. Unaffordable → the button reads "Need X crystals" (informative, routes to the crystal store if one exists), never a dead tap.
- **Invasion nudge (optional, recommended):** when a raid/invasion is imminent (the wave/raid system knows), surface/emphasize the Complete-now affordances on defensive items so the player sees the option exactly when it matters. Keep it a nudge, not a modal spam.

## 1b. Out of crystals → THE IMPULSE BUTTON (quick-buy crystals with SKR, realtime)
Owner (2026-08-05): *"a quick buy option… purchase with SKR in realtime… if out of crystals… that's the impulse button."*

When the player taps **Complete now** but lacks the crystals, **do NOT dead-end.** Surface the impulse button:
- **`Quick buy <N> crystals · <Y> SKR`** — the smallest crystal pack (from `packs.json`) that covers the shortfall.
- Tapping it opens the **existing SKR purchase flow in realtime** (Solana / Seeker — fast finality, 0% fees per the wallet notes) → on success the crystals land → **the complete-now action auto-proceeds** (the job finishes). The player never leaves the invasion moment. This is the impulse conversion.
- **Reuse the EXISTING payment path — do NOT build a new one:** `CryptoPaymentManager`, `PackStoreVM`, `packs.json` (crystal-pack SKUs), wallet / `BoundWallet` (memory: Solana/SKR store; Seeker primary; Solana Mobile SDK, WO-766). The impulse button is a new ENTRY POINT into that store, not a second store.
- **Real-money purchase → explicit confirmation** (amount of SKR + crystals received) before any spend; the wallet handles the actual auth. Never a one-tap silent charge. On cancel, return to the complete-now dialog unchanged.
- **The peak-pressure surface:** an invasion is incoming, they're one tap from finishing that wall, and they're short — this is where the SKR buy button belongs. (Do NOT nag it elsewhere; it appears on the out-of-crystals path, not on every affordable item.)

### 1b.1 The impulse dialog — 3 options, approved in the wallet
Owner (2026-08-05): *"you are short X crystals, want to purchase a quick pack to complete? give 3 options and approve in wallet."*
- Dialog: **"You're short `<X>` crystals. Buy a quick pack to complete?"**
- **THREE pack options** (from `packs.json` crystal SKUs) — e.g. small / medium / large, each showing `<crystals> · <SKR price>`; the smallest that covers the shortfall is pre-highlighted; larger packs show their better value (bonus %).
- **Approval happens IN THE WALLET:** selecting a pack hands off to the wallet's own approval/sign UI — the player approves the SKR spend in the wallet. The game **never** collects wallet credentials / seed / private keys in-app (security + the wallet-auth pattern). On approve → crystals granted → the complete-now auto-proceeds. On reject/cancel → back to the complete-now dialog, nothing spent.

### 1b.2 Not enough SKR → instant Jupiter swap (reuse existing — verified in tree)
Owner (2026-08-05): *"if their wallet doesn't have enough SKR allow a Jupiter swap instantly — think that part is already implemented way back."* Confirmed present.
- If the wallet lacks enough SKR for the chosen pack, offer an **instant Jupiter swap** to acquire the needed SKR from another token, inline, then continue the purchase.
- **REUSE the existing implementation:** `DeNelle.Web3.JupiterSwapService` / `IJupiterService` (`CoreServices.Jupiter`) + the `JupiterSwapPanel`/host; USD→SKR via Jupiter (live quotes work). Do NOT build a new swap.
- ⚠ **HARD DEPENDENCY before this ships (GAP_AUDIT 2026-07-18 #18 + CC_MONETIZATION_RECONCILIATION):** swap **signing is stubbed** (`WalletBridgeStub`) pending the Solana Mobile SDK (**WO-766**); and the release-build swap **no-ops but returns `true` with no `onError`** — a swap that did NOT happen must never report success. The impulse money path requires a **real signed swap + honest success/failure** first. Do not ship the SKR quick-buy on top of the stub.

### 1b.3 Fee policy — RULED: ABSORB (owner 2026-08-05: "probably absorb since there's no fee to us")
**DECIDED: ABSORB — bake costs into the pack price; NO separate fee line.**
- Solana network + swap costs are negligible (sub-cent); and the model already skips the 30% app-store tax, so margin easily covers them.
- The impulse moment is conversion-critical — a visible fee line at "invasion incoming, quick buy" breaks the impulse and costs the sale.
- **Set `packs.json` crystal-pack SKR prices to already cover costs.** Quote the player **ONE all-in number** they approve in the wallet (slippage-protected on the swap). No "+ fee" line anywhere in the impulse flow.
- Optional revenue lever (invisible): a small **Jupiter platform-fee (bps) baked into the swap quote** — the player sees only the final total. Keep it small; never surface it as friction.

**Pricing — DYNAMIC, volume-discounted packs, HARD CAP $5.00 (owner ruling 2026-08-05).** (SKR ≈ $0.007.)
- **The 3 options are sized to the player's CURRENT need**, not a fixed ladder: option 1 = the smallest pack that covers *this* finish; options 2–3 scale up (more crystals — finish more / stock up). If they're completing something expensive, the packs shown are **larger**.
- **Larger quantity = discounted** — better crystals-per-dollar the bigger the pack (buy-more-save-more).
- **HARD CAP: $5.00 max per pack** (owner: "$5.00"). At $0.007/SKR that's ≈ **700 SKR** max. Prefer round SKR display where it fits (e.g. 200 / 500 / 700 SKR ≈ $1.40 / $3.50 / $4.90), but the exact amounts flex with the need + spot price and **never exceed $5**.
- Crystal grant per pack is the fixed game value; **SKR is the real settlement amount** the wallet transfers (not cosmetic). App-store rail mirrors the same grants at **round USD tiers** ($1.99 / $4.99, ≤ $5) — no SKR there.
- Display: SKR rail shows the round SKR amount (+ optional "≈ $x.xx" hint); app-store shows the USD price. Do NOT hardcode fixed SKR numbers — pack size is need-driven, capped at $5.

**TWO payment rails, one rule — ALWAYS bake in, never a separate fee line (owner 2026-08-05):**
- **Seeker / Solana (now):** SKR crypto, ~0% platform fee → pack SKR price ≈ target; Jupiter swap fallback (§1b.2).
- **Apple / Google Play (later):** MUST use the store's own IAP in **fiat / USDC — no crypto, no Jupiter** (store policy forbids crypto for digital goods), and the store takes **~30%**. Still bake it in: set the displayed price so net-after-cut hits the target (≈ `target / 0.70`), shown as ONE clean price — no fee line here either. USDC/fiat means the app-store build carries **no wallet/swap flow at all**; the store handles payment.
- The impulse dialog's payment path is **platform-selected**: wallet → SKR → Jupiter on Seeker; store IAP (USDC/fiat) on app stores. Same 3-pack UX, different rail underneath.
- Per-rail pack pricing lives in the store data (`packs.json` + store-readiness config); align with `docs/SOLANA_STORE_READINESS_2026-08-06.md` / `docs/MONETIZATION_SME_REVIEW_2026-08-06.md`.

## 2. Where it appears
Wherever a queue is shown — the **Manage screen** (WO-905, the three rails + any in-progress browse rows) AND any HUD/queue surface that survives. One shared component: a queue-item row that renders `{icon, name, progress, remaining, completeNowCost, state}` + the Complete-now button. Build once, reuse everywhere (mirror WO-864's reusable rail component).

## 3. Grounding for CLI (verify at source; reuse, don't fork)
- Queue state already publishes what's needed: `ObsidianQueueGate.WorkQueueStatus` exposes per-job `Label` / `RemainingSec` / `Queued` + per-channel busy/slots (cited in WO-905 §4). Progress = `1 - RemainingSec/TotalSec` (add `TotalSec` if not published).
- The **complete/finish** path: find how a job completes today (timer elapse → grant) and add a "complete immediately" entry that runs the SAME grant path — do NOT reimplement the grant.
- Crystal spend: the economy ledger (crystals = premium currency; memory: uncapped premium). Spend through the existing ledger API + confirm.
- Invasion/raid imminence signal: the wave/raid system (`WaveManager` / raid controllers).

## 4. Constraints
- **This is an economy + queue MUTATION** (distinct from WO-905's read-only browser). Route the spend + complete through existing APIs; add a regression that a Complete-now spends exactly `X` and completes exactly one job.
- Crystal cost is **data-driven** (no magic number in code).
- Colourblind: progress + affordability read by shape/text, not colour alone.
- MVVM strict; `UI_CAPTURE_OK` required.

## 5. Acceptance criteria
**Engineering:**
- [ ] Every in-progress job shows a progress bar tied to its real remaining time.
- [ ] **Any item — active or queued — has a Complete now · X crystals action**; completing a queued item grants it regardless of position.
- [ ] Cost `X` **steps down in 5-minute brackets** (`crystalsPerBracket × ceil(remainingMin/5)`) and is a **flat cost under 5 minutes**; both values data-driven (not hardcoded); queued items cost against full duration; the displayed cost updates live as the timer crosses each 5-min boundary.
- [ ] Spend is confirmed, routes through the economy ledger + the existing completion/grant path (no forked economy or grant).
- [ ] **Out of crystals → the impulse dialog (§1b):** "short X crystals" → **3 SKR pack options** (from `packs.json`) → **approval in the wallet** (no in-app credential entry) → crystals granted → complete-now auto-proceeds. Cancel spends nothing.
- [ ] **Not enough SKR → Jupiter swap:** reuses `JupiterSwapService`/`IJupiterService`; a swap that did not execute NEVER reports success (fixes the GAP_AUDIT #18 no-op-returns-true bug); real signed swap gated on WO-766 (Solana Mobile SDK) — do not ship on the stub.
- [ ] Regression: a Complete-now spends exactly X crystals and completes exactly one job; queue advances correctly.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` + `UI_CAPTURE_OK`.
**Felt (owner closes):**
- [ ] With a raid incoming and a wall mid-build, the player taps Complete now, confirms crystals, and the wall is instantly ready.
- [ ] Progress bars read at a glance across all three rails.
- [ ] Headless capture of a queue with progress bars + a Complete-now confirm — open the PNGs, attach to RESULT.

## 6. RESULT
`WorkOrders/WORK_ORDER_898_queue_complete_now_crystals.RESULT.md` — the crystal-cost curve used, the shared queue-item component, and the screenshots.
