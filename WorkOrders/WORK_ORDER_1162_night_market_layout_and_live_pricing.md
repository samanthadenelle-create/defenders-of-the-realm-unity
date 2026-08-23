# WORK ORDER 1162 — Night Market: responsive layout proof, and replace `low_24h` with live server pricing

**Status:** READY TO IMPLEMENT — **for the Codex seat.** ⛔ **§2 (pricing policy) is BLOCKED ON AN OWNER RULING and must not be implemented until it lands.** §1 (layout) is unblocked and can start immediately.

**Minted:** 2026-08-23 (CLI), banner bumped 1162 → 1163 in the same edit.
**Provenance:** read-only deep dive by the **Codex seat**, handed over by the owner 2026-08-23.
**CLI gatekeeper pass:** every load-bearing claim below was **re-verified against the tree** before this WO was written (memory `cli-gatekeeper-agent-role-model` — agent output is a proposal, never truth). Verdicts are recorded inline: **CONFIRMED**, **CORRECTED**, or **UNVERIFIED**.

---

## 0. What Codex got right, verified at source — and the half it missed

**CONFIRMED — the pricing policy is the trailing 24-hour low.** `api/_lib/purchase-catalog.js`:

```js
RATE_URL    = '…/coins/markets?vs_currency=usd&ids=seeker'
RATE_SOURCE = 'coingecko:seeker:low_24h'          // :113
RATE_CACHE_MS = 120_000                            // :114
const low   = Number(rows[0].low_24h);             // :235
const skr   = Math.ceil(usd / usdPerSkr);          // :196
```

A lower denominator yields MORE SKR, so the player is charged against the most merchant-favourable price seen in the trailing window. Codex's reading is correct.

**⚠ CORRECTED — this is a DECLARED, OWNER-OWED RULING, not an undiscovered defect.** The file says so itself, above the function:

> *"…rule on and that ruling is still owed. **Whoever changes it changes a price.** The rate used is the 24h LOW, which compounds the same direction: a low denominator yields MORE SKR. **Both halves of that are deliberate and both are on the same ruling.**"*

Write the ticket accordingly: this is a **policy change requiring an owner decision**, not a bug fix. Implementing it silently would be changing a price without a ruling.

**⚠ CORRECTED — Codex caught ONE half of two.** `ceil()` at `:196` rounds **up to a whole SKR**, which pushes the same direction as the 24h low. On a $2.99 pack the rounding alone can be a material premium. **Both halves are on the same ruling** and must be presented to the owner together, or she rules on half the problem.

**⛔ NEW CONTEXT CODEX DID NOT HAVE — THE PAY PATH WENT LIVE TODAY (WO-1159).** `FeatureFlags.RealmStorePurchase` now defaults ON, `WalletService.DefaultNetwork` is Mainnet, and the mainnet payment refusal is replaced by the ruled condition. When Codex ran its dive, this pricing policy was theoretical. **It is now the price real players pay.** That raises §2 from a design question to a live-revenue question.

**⛔ AND §2 IS CURRENTLY UNREACHABLE ANYWAY — see WO-1160.** `POST /api/purchases/quote` returns **Vercel `NOT_FOUND` (404)** on the domain the client hardcodes; the endpoint was never deployed to production. So every store card currently reads **"Price unavailable"** (the client correctly refusing to invent a price). **Any pricing work is unverifiable in prod until WO-1160's deploy lands.** Sequence the deploy first.

**⚠ CORRECTED — a current capture DOES exist.** Codex reported *"no current Realm Store capture in the workspace"*. One was taken from the owner's device on 2026-08-23 at **2670×1200**, the Seeker's real surface, showing the live three-column Night Market. Use it as the baseline rather than starting from zero. It also independently confirms the "Price unavailable" state above.

**CONFIRMED (sweep completed after handoff) — and Codex UNDERSTATED it.** The pair is real and on disk at `Assets/_Modules/Wallet/UI/PackStore.uxml` + `.uss`, with **four** referencing sites:

- `Assets/Editor/VillageSceneBuilder.cs:223` — a `const string PackStoreUxmlPath` pointing at it. ⚠ That file is the §9 **serialization bottleneck**: only ONE agent/branch touches it at a time.
- `Assets/_Modules/Wallet/StorePackCard.cs:49` — already names the hazard in a comment: *"the PackStore.uxml/.uss pair still on disk is a **TRAP, not a starting point**"*.
- `Assets/_Modules/Village/Buildings/MarketplaceInteractor.cs:14` — ⛔ **the dangerous one**: a how-to comment instructing a future developer to *"Add UIDocument to it; assign PackStore.uxml as the Source Asset."* That is live guidance to wire up a path that **does not render in player builds at all** (CLAUDE.md §8). The trap does not merely sit there — it **re-arms itself** by telling the next reader to use it.
- `Assets/Editor/VillageSceneBuilder.Characters.cs`

So the cleanup is worth doing, and its real deliverable is **deleting the instruction**, not just the files. Sequence it last (§3.5) and keep it off the same branch-moment as any other `VillageSceneBuilder` work.

---

## §1 — Night Market responsive layout (UNBLOCKED, start here)

Codex's structural findings, accepted as the starting map (re-verify each at source before editing):

- The live UI is **runtime-built uGUI** at `Assets/_Modules/Wallet/PackStore.cs:360` — not the UXML.
- The shipped screen is **full-bleed, three columns** (spotlight · shelf · commerce), which **supersedes the original WO's narrow two-column modal draft**.
- Overlap protections already in source: explicit **344 px / 228 px** card heights, matched row/card heights, two cards per row, **112 px** minimum touch targets authored before clamping, one status surface, one bottom band, safe-area handling.
- The layout is hard-coded around a **978 px reference height** and landscape.

**The gap is proof, not protection.** Those guards are structurally sensible and nothing currently demonstrates they hold.

**Deliverables:**
1. A **layout oracle** covering sibling overlap, safe-area intrusion, touch floors (`ElarionUiKit.MinTouchPx`), and text clipping. ⚠ Author it to the WO-1138 taxonomy: **fixture-absent → FAIL** naming the path; **harness-capability-absent → a VISIBLE stand-down that can never read as a pass**. A hollow pass here would assert a layout is safe while proving nothing.
2. **Captures at 1920×1080, 2340×1080, 2670×1200, and a constrained/notched safe area**, plus worst-case content: long pack names, server errors, wallet addresses, large SKR figures.
3. **OPEN THE PNGs.** `UI_CAPTURE_OK` proves a panel rendered, never that it looks right (CLAUDE.md §8; two broken panels reached the owner behind green markers).
4. Adjust column widths, card typography, wrapping and commerce/status allocation **from the captures**, not from reasoning about the code.

⚠ **The owner is red/green colourblind.** No fix may encode meaning in hue; value/shape contrast only, and the greyscale check is the gate.

## §2 — Pricing policy (⛔ BLOCKED ON OWNER RULING)

**Do not implement until the owner rules.** The decision she owes, both halves together:

| Knob | Today | Codex's recommendation |
|---|---|---|
| Rate source | `low_24h` (trailing 24h low) | **current / executable** price (or a Jupiter executable quote) |
| Rate cache | 120 s | 30–60 s |
| Rounding | `ceil()` to whole SKR | (unstated — **must be ruled in the same breath**) |
| Volatility cover | the 24h low, implicitly | a **small, disclosed** buffer or short TWAP |

Codex's recommendation, which the CLI endorses on the merits: **do not switch to a daily low**; use a server-authoritative current/executable price, cached briefly, wrapped in a binding single-use quote (2–5 min), display the exact SKR and its expiry, refresh before wallet approval if expired, and **fail closed** when pricing is unavailable.

**⭐ The existing architecture already does the hard part and must not be rebuilt.** WO-1158 shipped exactly the server-issued-quote spine Codex is describing: the server decides the number, the client transports and pays it verbatim, `/verify` re-derives its contract **from the quote row it issued** and reads no amount from the body, and expiry is judged against the transaction's own `blockTime` + a 180 s settlement grace. **This is a change to the POLICY INSIDE the quote — one function — not a re-architecture.** Touch `api/_lib/purchase-catalog.js`; leave `api/purchases/quote.js` and `PurchaseQuoteService.cs` structurally alone.

**Also in scope once ruled:** unify the header's approximate fiat figure with the **same server rate**, so Jupiter and CoinGecko can never show two different values on one screen (WO-1158 §5: *two prices on one screen is worse than a stale one*).

**Pricing regressions required with the change:** freshness, quote expiry, rounding, source identity, fail-closed. ⚠ And pin the RULED policy the way WO-1159 pinned the go-live flag — a policy that can be silently reverted is not ruled.

## §3 — Sequencing (do not reorder)

1. **WO-1160** — deploy `api/` to production (owner's call; Vercel CLI not installed on this box). Nothing in §2 is verifiable until this lands.
2. **§1 layout** — unblocked, independent of the deploy.
3. **Owner ruling on §2** — rate source + rounding, together.
4. **§2 implementation + regressions.**
5. **§1.7 UXML quarantine** — separate, safe cleanup, only after verifying the scene reference.

## §4 — House rules for the Codex seat

- **CLI is the sole committer.** Edit only; do not gate, do not commit, do not push (CLAUDE.md §11).
- **Judge by MARKERS on a fresh log**, never exit codes — the runners exit 0 on refusals and FAILs.
- **Instrument before editing** any non-trivial defect (§12): a captured line proves the cause; static reading only locates candidates.
- **Never soften an oracle to make a change fit.** Pin the exception with its reason (the 2026-08-21 lesson).
- **Money-path edits get a red-then-green proof** — a pin that has never been seen red is not evidence (WO-1159 precedent).
