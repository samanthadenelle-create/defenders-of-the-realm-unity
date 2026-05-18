# Monetization v2 — SKR Packs + Seasonal Pass

**Status:** Buildable spec. Supersedes the recommendation in `docs/monetization-design.md` §0 ("primary model: one-time supporter unlock + cosmetic-only secondary"). Owner-locked 2026-05-17 evening with explicit awareness that this doc **compromises one piece of the original covenant** — see §2.

**Audience:** Claude Code (build), owner (tuning + pricing calibration), Solana ecosystem reviewers (the grant story this doc engineers).

**Lives alongside:** `docs/monetization-design.md` (the philosophy + the constraints C1-C7), `docs/cosmetic-shop-spec.md` (the existing cosmetic shop UI + Glimmer currency), `docs/economy-design.md` (in-game currency rates), `docs/persistence-onchain-spec.md` (wallet identity), `src/modules/wallet/` (post-refactor wallet module), `src/services/api-client.ts` (backend boundary).

**One-line:** Players can buy themed packs in four currency rails (SKR / SOL / USDC / Stripe USD). Packs contain cosmetics + economy top-ups + convenience power (no combat-stat advantage). A generous seasonal pass (cosmetic track, permanent, no FOMO) backs the catalog. SKR exists as the Solana-native showcase rail — primary positioning is the Solana Foundation grant story, not primary revenue.

---

## 1. The four locked decisions (owner 2026-05-17)

| #   | Decision        | Locked value                                                                                                                                                      |
| --- | --------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Pack contents   | **Cosmetic + economy top-ups + convenience power.** Explicit divergence from cosmetic-only — see §2.                                                              |
| 2   | Currency rails  | **SKR + SOL + USDC + Stripe.** Maximum optionality. Wallet-connected players pick any token; non-wallet players use Stripe.                                       |
| 3   | SKR positioning | **SKR is the grant-credibility vector**, not the primary revenue path. SKR-priced packs exist to demonstrate Solana-native integration for the Solana Foundation. |
| 4   | Cadence         | **One-time packs + one generous seasonal pass.** No recurring subscriptions. No daily/weekly deals.                                                               |

---

## 2. The covenant amendment — the part future-you will second-guess

`docs/monetization-design.md` §1 lists seven constraints (C1–C7). This spec keeps six of them intact. The one it bends:

> **C1 — "Payment shortens time or sells expression — never gates progress, never sells power."**

This spec sells **convenience power** — instant-build, instant-repair, XP boosters, harvest auto-collect — that COMPRESSES time (already permitted) but also REDUCES player effort (newly permitted). It does not sell combat stats. Walls are not stronger when bought; towers do not fire farther; the hero does not deal more damage.

**Owner's stated reasoning:** the original covenant was written before the dApp Store + grant strategy was on the table. Convenience-power packs are necessary for the model to support development sustainably, and the line at "combat power" remains absolute. The mantra rewrites cleanly:

> _"You are never required to spend anything. Ever. And when you do, you cannot buy victory — only time and beauty."_

**The covenant rules still in force:**

- C2 — never required to spend, ever ✅ — every pack item is also earnable through gameplay.
- C3 — no loot boxes, no gacha, no randomized purchases ✅ — every pack shows its full contents pre-purchase.
- C4 — no energy systems, no FOMO countdowns, no dark patterns ✅ — packs don't expire; the seasonal pass is permanent.
- C5 — no gameplay interruption ✅ — store is player-initiated; no pop-ups.
- C6 — generosity over extraction ✅ — gift mechanic from `social-spec.md` §2 wraps every pack (giftable to other players).
- C7 — cozy tone ✅ — packs are themed in narrative-bible voice ("The Folk send a small thanks for tending the Heart").

**The covenant rule explicitly bent:**

- C1 (revised) — payment may shorten time and reduce effort, never alters combat stats. The boundary moves from "sells expression" to "sells expression + convenience." Combat stats remain sacrosanct.

If this divergence is too much in playtest or community feedback, the convenience-power layer (§5.3) can be ripped out as a single removable section without affecting the cosmetic + economy layers.

**Convenience power is treated as a removable experiment.** If it compromises the cozy feel in playtest — or if Solana Foundation reviewers, community feedback, or the owner's own judgment finds it predatory — it will be excised entirely per §11.1. The covenant rewrite is provisional, not permanent.

---

## 3. SKR — the grant-credibility vector

SKR is the Solana Seeker phone's native token. Using SKR as an in-game currency demonstrates first-party Solana Mobile alignment in a way that USDC or Stripe never can. The strategic value isn't the SKR revenue (which will be small — most players will pay via Stripe). The strategic value is:

- **The Solana Foundation grant pitch.** "Defenders of the Realm uses SKR as a first-class in-game currency" is a meaningfully different sentence from "uses Solana wallets for sign-in." It demonstrates the kind of native ecosystem integration grant committees fund.
- **The Solana Mobile dApp Store featured-app pitch.** Apps that use SKR for actual in-game purchases (not just gate-of-entry) are differentiated. The dApp Store editorial team can credibly feature a game that exercises the Seeker token; they can't feature one that doesn't.
- **The "Genesis Token" alignment** if/when SKR rewards programs touch dApp Store apps. Being SKR-native at launch means we're in the inflow when those programs activate.

**Practical implication on the spec:** every pack price has a SKR amount alongside SOL / USDC / Stripe-USD. SKR doesn't need to be the cheapest or most-promoted rail. It just needs to be a real, working purchase path. The UI flow must work for a real SKR-holding player end-to-end on first play.

---

## 4. Pricing — the scaling tier ladder

Pricing follows mobile-game industry-standard tier psychology (Starter $1.99 / Value $4.99 / Trader $9.99 / Patron $19.99 / Founder $49.99), with per-currency parity at launch.

| Tier | Pack name                 | Stripe (USD) | USDC       | SOL @ launch\* | SKR @ launch\* | Theme                                                                                            |
| ---- | ------------------------- | ------------ | ---------- | -------------- | -------------- | ------------------------------------------------------------------------------------------------ |
| 1    | **Hearth Spark**          | $1.99        | 1.99 USDC  | 0.018 SOL      | 25 SKR         | Tiny welcome — beginner cosmetic + small economy bump                                            |
| 2    | **Lanternlight**          | $4.99        | 4.99 USDC  | 0.045 SOL      | 60 SKR         | The "value" tier — what most players who buy something will buy                                  |
| 3    | **Folk's Thanks**         | $9.99        | 9.99 USDC  | 0.09 SOL       | 120 SKR        | The supporter-edition slot — themed cosmetic set + named seasonal recognition                    |
| 4    | **Patron of Elarion** | $19.99       | 19.99 USDC | 0.18 SOL       | 240 SKR        | Heavyweight cosmetic suite + meaningful economy headstart                                        |
| 5    | **Founder's Vow**         | $49.99       | 49.99 USDC | 0.45 SOL       | 600 SKR        | One-time only, available v1 launch only — special founder cosmetic + permanent in-village banner |

`* @ launch` = prices set ONCE at v1 launch and held until v1.1 review. SOL and SKR prices are fixed amounts; their USD-equivalent fluctuates with the market. This is the **SKR-fixed pricing strategy** by default, with USD reference shown for transparency (see §4.2).

**Founder's Vow is intentionally distinct:** it's a one-time launch-window pack. The "permanent in-village banner" (a vertical pennant near the Heart with the player's chosen 8-letter inscription) is the only item in the entire catalog that's time-locked to the launch window. After v1.1 ships, the banner is no longer purchasable — but everyone who bought it keeps it forever. This creates a single, ethical scarcity moment for ecosystem early adopters without breaking C4.

### 4.1 USD reference + price oracle

Wallet-purchase rails (SOL, USDC, SKR) display their USD reference at purchase time using a price oracle. Players see: `60 SKR ≈ $4.99 USD as of 2026-05-17 22:34 UTC`. The disclaimer "_token price moves with the market_" is permanent UI text on the pack purchase modal.

For SKR specifically: at launch, target ~$0.083/SKR (placeholder — confirm current price at v1 ship). If SKR price moves more than 20% in either direction over a sustained 14-day window, owner re-prices the SKR amounts in a v1.x patch. **Do not** automatically re-price — fluctuation IS the crypto-native UX expectation. Manual repricing only on sustained, significant moves.

### 4.2 Stripe + USDC pricing fixed in USD

Stripe and USDC are stable; their prices are set in dollars and held. No reprice cadence needed.

### 4.3 Regional pricing

**v1: USD-only across all rails.** Region-specific pricing tiers (€, ¥, etc.) deferred to v1.2 — Stripe handles fiat currency conversion at the payment layer for non-USD payment methods, so players see their local currency automatically without needing custom price points.

---

## 5. Pack contents — what's actually in the bag

Each pack contains items across three layers. **Every item must also be earnable through gameplay** (per C2). The pack's value proposition is that buying gets you all of them at once, with one cosmetic that is unique to that pack.

### 5.1 Cosmetic layer (the heart of every pack)

Every pack includes 1–4 cosmetic items themed to the pack. Cosmetics are _pure visual reskins_ — no stat changes (C1 original). Reuses the cosmetic-shop-spec.md SKU system; pack purchases simply grant the SKUs directly.

Cosmetic items by tier:

| Tier               | Cosmetic count       | Slot type                                                                                                                             |
| ------------------ | -------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Hearth Spark (T1)  | 1                    | Single pet skin variant OR a single building palette swap                                                                             |
| Lanternlight (T2)  | 2                    | One hero outfit + one pet skin                                                                                                        |
| Folk's Thanks (T3) | 3                    | One hero outfit + one pet skin + one village ambient theme (lantern color, ember palette)                                             |
| Patron (T4)        | 4                    | Two hero outfits + two pet skins (themed pair)                                                                                        |
| Founder's Vow (T5) | 4 + permanent banner | Founder-exclusive hero outfit + founder pet skin + founder building palette + the permanent in-village banner with 8-char inscription |

The **one cosmetic item unique to each pack** is the "pack-exclusive" — it's purchasable in-pack only, and not available via the regular cosmetic shop. The other items in the pack also exist in the regular shop (purchasable individually with Glimmer or earned).

### 5.2 Economy layer (the "value" perception)

Each pack includes a top-up of in-game currencies. Amounts are scaled so that the pack always represents better value-per-dollar than the equivalent in raw currency purchases — but never enough that the pack is the _only_ sensible path.

| Tier          | Glimmer | Crystals | Food  | Coins  |
| ------------- | ------- | -------- | ----- | ------ |
| Hearth Spark  | 25      | 200      | 50    | 100    |
| Lanternlight  | 75      | 700      | 200   | 400    |
| Folk's Thanks | 175     | 1,800    | 500   | 1,000  |
| Patron        | 400     | 5,000    | 1,500 | 3,000  |
| Founder's Vow | 1,000   | 15,000   | 5,000 | 10,000 |

These represent ~30–60 minutes of average gameplay-earning per dollar. The pack is a _time-saver_, not a _gate-bypass_ — a player who never spends a dollar accumulates the same amounts over time.

### 5.3 Convenience-power layer (the bent covenant — see §2)

Convenience power is **time-saving and effort-reducing**, never combat-power. The line:

✅ **Allowed:**

- Instant-build (skip the X-second build animation for one building)
- Instant-repair (skip the repair queue for one building)
- 2× XP weekend (24-hour buff to XP gain — does NOT raise XP cap or unlock content faster than possible)
- Harvest auto-collect (24-hour buff: resources auto-tick into inventory instead of requiring tap)

🚫 **Forbidden (combat or close-to-combat impact):**

- More damage per shot
- Longer tower range
- Higher hero HP
- Stronger walls
- Higher resource caps (this gates content; would be a sell-progress)
- Skipping prep timer (this is a deliberate gameplay rhythm; skipping breaks the loop)
- **Tower fire-rate pre-charge** — REMOVED per Grok + ChatGPT review 2026-05-17. Even temporary fire-rate buffs read as buying combat performance. The convenience layer stays clear of live combat.
- **Any permanent passive trait** on a paid pack. REMOVED per ChatGPT review — permanent passives, even "half-rate" ones, read as paid superiority and would let some players permanently skip an interaction other players have to perform.

Convenience-power items by tier (revised post-review):

| Tier          | Convenience items                                                                              |
| ------------- | ---------------------------------------------------------------------------------------------- |
| Hearth Spark  | 1× instant-build token                                                                         |
| Lanternlight  | 3× instant-build tokens, 1× harvest auto-collect 1-hour                                        |
| Folk's Thanks | 5× instant-build, 5× instant-repair, 1× harvest auto-collect 24-hour                           |
| Patron        | 10× instant-build, 10× instant-repair, 2× harvest auto-collect 24-hour, 1× 2×-XP weekend (24h) |
| Founder's Vow | 25× instant-build, 25× instant-repair, 5× harvest auto-collect 24-hour, 3× 2×-XP weekends      |

**Founder's Vow no longer carries a permanent passive trait.** It's distinguished by:

- Founder-exclusive hero outfit + pet skin + building palette (cosmetic identity)
- Permanent inscribed in-village banner (social/identity proof — see §4)
- A larger one-time bundle of the same convenience tokens every tier gets

No paid passive runs forever. Every convenience benefit comes from a finite, consumed token. Future-you (and players) get to compare like-to-like: a Founder has more tokens at the start, but every token they spend brings them to the same state a non-Founder is already in.

---

## 6. The Seasonal Pass — Keeper's Almanac

Per existing `monetization-design.md` §0.3 — the _generous_ variant. Reaffirmed here as the model.

- **One-time purchase per season:** $9.99 / 9.99 USDC / 0.09 SOL / 120 SKR.
- **Permanent unlock.** No expiry. Once bought, the season's 30-tier track is unlockable at the player's pace, forever. No FOMO. No "this season ends Friday."
- **Cosmetic-only track.** No economy, no convenience power — the pass is pure cosmetic expression. (C1 original holds for the pass.)
- **30 tiers.** Each tier unlocks at a fixed gameplay milestone (waves cleared, dungeons completed, bond levels gained). Player can complete tier 30 in their first weekend or their fifth year — same outcome.
- **Free track parallel.** Every season also has a 10-tier _free_ track for non-pass players (matches the existing free-vs-pass cosmetic shop pattern in `cosmetic-shop-spec.md` §2.2). Pass owners get the 10 free + 20 additional pass-exclusive tiers.
- **Seasonal cadence:** 90 days. Three seasons per year. Each season has a distinct narrative theme drawn from the narrative bible (Season 1: _The Lanternkeeper's Spring_; Season 2: _The Hollow's Summer_; etc.).

The seasonal pass is the spine of long-term monetization. The packs in §4 are the entry point; the pass is the retention.

---

## 7. UI flow — the player journey

### 7.1 Discovery

- **From the village HUD:** a small `🛍` glyph in the top-right opens the store. Non-intrusive; only player-initiated (C5).
- **From the post-wave Damage Report modal:** an optional "Quick Repair Pack" CTA shows IF the player's wall damage is above 60% — points them at the Hearth Spark pack as a self-evident value moment. The CTA is dismissible and never auto-opens.
- **From the Heart altar (in 3D village):** a "Found a coin pouch" interactable event spawns randomly once per session at <1% probability when the player is near the Heart; tapping opens the store. Cozy framing, no FOMO. (This is the only "discovery" mechanic; it's a moment, not a prompt.)

### 7.2 Pack detail page

Single screen, modal over the store. Shows:

- Pack name + narrative-bible-voice tagline ("_The Folk send a small thanks for tending the Heart._")
- Full contents list — every cosmetic with preview render, every economy amount, every convenience item with description
- Four currency rail tabs — SKR / SOL / USDC / Stripe USD. Tapping a tab shows the price in that rail + the live USD reference.
- "Gift this pack" button (per C6, social-spec.md §2) — recipient gets the cosmetics; sender keeps a small token of recognition.
- "Buy" button → currency-specific flow (§7.3-§7.6).

### 7.3 Stripe flow

1. Tap Buy. If not logged in via Stripe yet, a single email-collection step appears (used for receipt + entitlement).
2. Stripe Checkout opens (existing `@stripe/stripe-js` integration).
3. Payment → success webhook to backend → entitlement written to Vercel Postgres keyed by email.
4. Backend pushes entitlement to client via API poll OR (better) via WebSocket if the partyserver-replacement supports it.
5. Pack contents materialize in the player's inventory. Cosmetics unlock; economy tops up; convenience items appear in their tray.

### 7.4 USDC / SOL flow (wallet-connected)

1. Player must have wallet connected. If not, prompt to connect (skipping if already connected).
2. Tap Buy. Modal shows: _"You will sign a transaction sending X USDC (or SOL) to the Defenders treasury."_
3. Wallet signs → tx submits → wait for confirmation (Solana finality is ~1s; show a progress indicator).
4. Backend listens for the on-chain transaction targeting the treasury address; verifies tx hash + amount + sender; writes entitlement keyed by sender's wallet address.
5. Pack contents materialize (same as 7.3).

### 7.5 SKR flow

Same as 7.4, but the SPL token program path:

1. Player needs an SKR-holding wallet. If they don't have any, surface a "How do I get SKR?" link to the official Solana docs / Jupiter swap UI. Do not silently fail.
2. SKR transfer tx → backend verifies → entitlement granted (same shape as USDC).

### 7.6 Identity merge

If a player who first used Stripe later connects a wallet, their entitlements merge: the Stripe-email entitlements transfer to the wallet identity. The wallet becomes the canonical identity going forward.

If a player who used a wallet later wants to receive entitlements via Stripe (different device), they can request a "transfer to email" via a one-time email-link — wallet entitlements copy to the email; both identities then point to the same entitlement set.

This is mildly complex. Implementation defers to **v1.1** if it bleeds time — at v1 launch, identity is single-rail (wallet OR Stripe email, whichever was first used).

---

## 8. Architecture — implementation shape

### 8.1 Entitlement storage

**Vercel Postgres** (already in deps). Schema:

```sql
CREATE TABLE entitlements (
  id BIGSERIAL PRIMARY KEY,
  identity_kind TEXT NOT NULL CHECK (identity_kind IN ('wallet', 'email')),
  identity_value TEXT NOT NULL,
  pack_sku TEXT NOT NULL,           -- e.g. 'hearth-spark', 'lanternlight'
  purchase_rail TEXT NOT NULL CHECK (purchase_rail IN ('stripe', 'usdc', 'sol', 'skr')),
  tx_hash TEXT,                     -- non-null for crypto rails
  stripe_session_id TEXT,           -- non-null for Stripe rail
  amount_native TEXT NOT NULL,      -- amount in the purchased rail's native units (string for precision)
  amount_usd_at_purchase NUMERIC(10,2) NOT NULL,
  granted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  fulfilled_at TIMESTAMPTZ,         -- when the client confirmed it received the items
  UNIQUE (identity_kind, identity_value, tx_hash, stripe_session_id)
);
```

### 8.2 Treasury wallets

Three separate Solana wallets, one each for SOL / USDC / SKR. Multi-sig or treasury-controlled. Public addresses in `src/services/treasury.ts` constants. The owner's existing publisher wallet IS NOT the same wallet as the treasury — keep these strictly separated for accounting + security.

### 8.3 Transaction verification

Backend `services/payment-verifier.ts` does:

- For Stripe: validates the Stripe webhook signature (standard pattern).
- For crypto rails: takes a tx hash from the client, fetches it from a Solana RPC, verifies (a) the destination is the treasury address, (b) the token mint is correct (USDC or SKR; SOL is native so different), (c) the amount matches the expected pack price (within a 1% tolerance for slippage), (d) the tx is finalized.
- On success, writes the entitlement row and returns success to the client.
- Idempotent: same tx hash → already-recorded → success, no double-grant.

### 8.4 Pack catalog

Static config at `src/content/packs.ts`:

```ts
export interface PackDef {
  sku: string;
  tier: 1 | 2 | 3 | 4 | 5;
  name: string;
  tagline: string;
  pricing: {
    usd: number;
    usdc: number;
    sol: number;
    skr: number;
  };
  contents: {
    cosmetics: CosmeticSku[];
    economy: { glimmer?: number; crystals?: number; food?: number; coins?: number };
    convenience: ConvenienceItemDef[];
  };
  founderOnly?: boolean;
  packExclusiveCosmetic: CosmeticSku;
}
```

Catalog is content; modifiable without code changes for tuning.

### 8.5 Client-side store module

`src/modules/store/` (new, post-refactor). Owns: pack catalog UI, pack detail modals, currency-rail tabs, the four purchase flows. Reads catalog from content; writes via `services/payment-verifier`; observes entitlements via `state/entitlementsSlice`. OWNERSHIP.md per refactor spec §1.4:

```
modules/store owns: pack catalog UI, purchase flows, entitlement display
may consume: state/entitlementsSlice (read-only fulfillment), services/payment-verifier,
             services/treasury, ui/SkillNode, contracts/identity
may NOT: directly mutate cosmetics/economy/convenience state (those flow via entitlement fulfillment),
         import from modules/wallet runtime (uses contracts/identity instead)
```

### 8.6 State

New Zustand slice: `state/slices/entitlementsSlice.ts`. Tracks: list of owned SKUs, fulfillment state per SKU, last-sync timestamp. Persists to localStorage AND syncs with backend on wallet/email change. On app boot, fetches latest from backend to catch entitlements purchased on another device.

### 8.7 Save schema additions

`state/saveSchema.ts` gains:

```ts
entitlements: {
  ownedPacks: string[];          // pack SKUs
  ownedSeasons: number[];        // season indices
  founderBannerInscription?: string;  // 8-char player-set, only for Founder's Vow owners
}
```

---

## 9. Acceptance criteria

The system ships when ALL of these are true:

- [ ] All 5 packs purchasable via all 4 currency rails (20 paths total) end-to-end on a real device.
- [ ] Pack contents materialize in inventory within 10 seconds of payment confirmation.
- [ ] Stripe webhook validated; webhook secret in env not in code.
- [ ] SKR / USDC / SOL transactions verified server-side against the Solana mainnet RPC.
- [ ] Idempotency: replaying the same tx hash returns success without double-grant.
- [ ] USD reference price displays correctly and updates with the price oracle.
- [ ] Seasonal pass purchasable; track UI reflects unlock state correctly; free vs pass tracks differentiated.
- [ ] Founder's Vow available only during launch window (gate by `Date.now() < FOUNDER_WINDOW_END`); banner inscription persists in save.
- [ ] Convenience items consume on use; instant-build skips a build timer; auto-collect ticks resources.
- [ ] Gift flow works: player A gifts pack to player B; B receives cosmetics; A keeps a recognition token.
- [ ] All packs visible from store; pack-exclusive cosmetic distinct from regular shop SKU.
- [ ] No regression to the cozy covenant — playtester reads the store and feels "this is fair," not "this is a casino."
- [ ] No combat-stat-changing item appears anywhere in any pack (validated by code review of `packs.ts`).
- [ ] No permanent passive trait grantable via purchase (validated by code review of `packs.ts`).
- [ ] **SKR launch-pricing checkpoint passed** — see §9.1.
- [ ] **Closed playtest complete** — see §9.2.

### 9.1 SKR launch-pricing checkpoint (pre-enable blocker)

Before enabling live SKR purchases, ALL of:

- [ ] Current SKR/USD price confirmed from at least two independent sources (Jupiter API + CoinGecko, or equivalent) within 24 hours of enable.
- [ ] SKR pack amounts recalibrated against actual market price — if SKR has moved more than 15% from the $0.083 placeholder in §4, pack SKR amounts are adjusted to maintain USD parity per tier.
- [ ] Recalibrated amounts committed to `src/content/packs.ts` and reflected in the store UI.
- [ ] Price oracle integration verified live: `60 SKR ≈ $X.XX USD as of HH:MM UTC` renders on every wallet-rail pack purchase modal.
- [ ] Owner sign-off on the calibrated SKR amounts in a single commit, message: `Pricing: SKR launch calibration — anchor $0.0XX/SKR confirmed YYYY-MM-DD`.

This checkpoint executes within 48 hours of the v1 launch event and must be re-run if SKR price moves >20% sustained over 14 days post-launch (per §4.1 reprice rule).

### 9.2 Closed playtest acceptance gate (pre-launch blocker)

Before any pack purchase is enabled in production, run a closed playtest:

- [ ] **≥20 playtesters** recruited from outside the owner's immediate circle (so feedback isn't friend-filtered).
- [ ] Each playtester completes ~30 minutes of gameplay reaching at least the first breach + first dungeon entry.
- [ ] Each playtester is shown the store at least once during their session.
- [ ] Post-session questionnaire measures:
  - **(a) Did you feel any pressure to purchase?** Scale 1–5. Target: average ≤ 2.
  - **(b) Did the store feel fair / cozy / predatory / aggressive?** Free-text + multi-select. Target: zero playtesters describe it as "predatory" or "aggressive"; ≥75 % describe it as "fair" or "cozy".
  - **(c) Would you purchase a pack at the listed prices?** % yes — track but do not gate on. This is the conversion-rate signal.
  - **(d) Did you finish the playtest session because you wanted to keep playing, or because you felt obligated?** Target: ≥80% chose "wanted to keep playing."
  - **(e) Free-text: what would you change about the store?**
- [ ] Results written to `docs/playtest-results-monetization-<date>.md` and reviewed before flipping the production enable flag.
- [ ] If question (b) yields any "predatory" response: trigger §11.1 rip-out before launch.

---

## 10. Out of scope (v1)

- **Refund flow.** Crypto transactions are irreversible. Stripe disputes route to a support email; no automated refund UI in v1.
- **Cross-device entitlement sync via QR code or short-link.** Use the wallet-connect path or the Stripe-email path; explicit cross-device merge is v1.1.
- **Gifted-pack tracking (recipient-side display).** "I received a gift from [player]" UI is v1.1.
- **Region-specific pricing tiers.** v1 ships USD/SKR/SOL/USDC flat across regions; Stripe handles fiat conversion for non-USD payment methods.
- **Discount codes / promotional pricing.** No coupon system. The pricing ladder is the ladder.
- **Subscription / recurring payments.** Explicitly out per Decision 4.
- **Bundled multi-pack offers ("buy 2 packs, get 10% off").** No deals. The packs are the packs.
- **In-game pop-up offers.** Forbidden per C5. Store is always player-initiated.

---

## 11. Risks + mitigations

| Risk                                               | Mitigation                                                                                                                                                                                                             |
| -------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| SKR rail goes unused (most players pay via Stripe) | OK — SKR's value is the grant story (§3), not revenue. As long as it works end-to-end for the demo, it's done its job. Real revenue mix can be 90/10 Stripe/SKR.                                                       |
| Convenience power feels predatory in playtest      | The §2 covenant amendment is monitored. If five+ playtesters describe the store as predatory, rip the convenience-power layer in a v1.1 patch. Packs become cosmetic-only. Sketch the rip-out path in §11.1 below.     |
| Price oracle goes down                             | Cache the last-known good price. Display "$X.XX as of HH:MM" rather than failing. Refresh in background.                                                                                                               |
| SKR price moves 50% overnight                      | Manual reprice within 72 hours. Communicate to players via in-game toast: _"The Folk recount the realm's coin — pack prices have steadied to today's value."_ (cozy framing for what is otherwise just a price update) |
| Treasury wallet compromise                         | Multi-sig from day one. Owner + a hardware-key co-signer. Never deploy a hot key in production.                                                                                                                        |
| Stripe chargeback waterfall                        | Track chargeback rate; if it exceeds 1%, review the convenience-power layer (often the source of buyer's-remorse refunds).                                                                                             |
| Crypto tax / accounting                            | Bookkeeping for crypto receipts is the owner's responsibility. Document received-amount + USD-at-receipt in the entitlements table for tax basis.                                                                      |

### 11.1 If convenience-power needs to come out (the rip-out path)

If playtest reveals the convenience-power layer is the wrong call:

1. Set every pack's `contents.convenience` to `[]` in `packs.ts`.
2. Add a single line to each pack's tagline: "_… and a small gift of crystals for your trouble._"
3. Up the economy amounts in §5.2 by ~30% to compensate the perceived value.
4. The cosmetic + economy structure stays intact. Players who bought the convenience items keep them.

The rip-out is one config-file change. Build path is unaffected.

---

## 12. Yield-funded player rewards — the SKR economy comes full circle

**Owner state 2026-05-17:** 1 million SKR currently staked. Conservative ~5% APY = ~50,000 SKR/year of yield. This yield, if directed at player rewards, becomes a **self-sustaining contest + drop budget that never touches principal**.

This single mechanic transforms the spec's grant pitch (§13) from "the game accepts SKR" to "the game _operates on_ the SKR yield curve." That is a categorically different story to a grant committee. It also creates a genuine flywheel: yield → player rewards → engaged players → some spend back into packs → revenue → reinforces the staked position. Closed loop.

### 12.1 The budget math (placeholders — confirm at v1 launch)

| Variable          | Conservative | Base      | Bullish   |
| ----------------- | ------------ | --------- | --------- |
| SKR staked        | 1,000,000    | 1,000,000 | 1,000,000 |
| Annual APY        | 4%           | 5.5%      | 7%        |
| Annual SKR yield  | 40,000       | 55,000    | 70,000    |
| Monthly SKR yield | ~3,330       | ~4,580    | ~5,830    |
| Weekly SKR yield  | ~770         | ~1,060    | ~1,350    |

At $0.083/SKR launch reference: monthly yield is ~$275–$485 USD-equivalent. Modest as a dollar budget; meaningful as a player-facing reward stream (especially for small SKR drops that read as "tens of dollars worth" to recipients with relatively cheap entry-tier wallet holdings).

**Critically: yield is NOT guaranteed.** Staking APY varies with validator performance, network conditions, and protocol changes. The spec must treat yield as a _target budget_ and re-tune monthly. The principal funds the contests when yield underperforms; surpluses build a reserve for tournaments.

### 12.2 The reward catalog — three streams

Yield gets split across three distinct reward streams, each tuned to a different player-engagement goal.

#### 12.2.1 Stream A — Achievement drops (~40% of yield)

First-time achievements pay a small SKR drop. Player wallet required to receive (wallet-less players accumulate the drops as pending; claim on wallet connect). Examples:

| Achievement                                            | First-time SKR drop |
| ------------------------------------------------------ | ------------------- |
| First wave cleared                                     | 0.5 SKR             |
| First breach survived                                  | 1 SKR               |
| First dungeon completed                                | 2 SKR               |
| First questline completed                              | 3 SKR               |
| First boss defeated                                    | 5 SKR               |
| Hero level 10                                          | 5 SKR               |
| Pet bond rank max (per pet, 3 total)                   | 5 SKR each          |
| Wave 30 cleared                                        | 10 SKR              |
| All three dungeons completed                           | 10 SKR              |
| First Founder's Vow purchase (matches the buyer's USD) | 25 SKR              |
| Heart fully repaired post-Wave-20 breach               | 5 SKR               |
| Letter-to-the-next written (endgame conversation)      | 10 SKR              |
| New Game+ entry                                        | 10 SKR              |

**These are one-time per save, per achievement.** Total reachable per save ≈ 100 SKR per dedicated player. With ~500 dedicated players in v1 = 50,000 SKR — about 1 year's yield. Sustainable as long as Acquisition velocity ≤ yield velocity, which is exactly the right shape: rewards naturally throttle as the player base grows. The cost-per-acquisition story is "we paid 100 SKR (~$8) to a player who finished the entire game and may have spent $20 on packs in the process" — astonishingly good unit economics.

#### 12.2.2 Stream B — Weekly leaderboard contest (~40% of yield)

A single recurring contest: **The Watcher's Roll** — weekly leaderboard, multiple categories.

| Category                        | Prize pool (weekly) | Top 1 | Top 2-3 | Top 4-10  |
| ------------------------------- | ------------------- | ----- | ------- | --------- |
| Highest wave reached this week  | 100 SKR             | 40    | 20 each | 5 each    |
| Fastest dungeon clear (any)     | 50 SKR              | 25    | 10 each | 1.25 each |
| Most pets bonded this week      | 50 SKR              | 25    | 10 each | 1.25 each |
| Most repairs paid for this week | 50 SKR              | 25    | 10 each | 1.25 each |
| Top weekly **Total**            | **~250 SKR/week**   | ~14%  | ~10%    | ~2%       |

That's ~1,000 SKR/month — within base-case yield budget. Adjust prizes if yield runs low/high; UI displays _current week's prize pool_ dynamically (no FOMO, just transparency about how the yield is being spent).

**Leaderboard requires:**

- Server-side score recording (we now have an empty `services/` boundary; this is its first real customer post-Poof-removal)
- Anti-gaming: rate limit per wallet, minimum playtime threshold, anomaly detection (perfect runs on a brand-new wallet flag for review)
- Wallet-only entry. Non-wallet players see the leaderboard but cannot win prizes — they're told this transparently on the leaderboard screen, framed cozily: _"Watcher prizes are sent to bonded wallets. Connect a wallet to compete for the Roll."_

#### 12.2.3 Stream C — Seasonal tournament (~20% of yield)

Once per 90-day season, a single tournament with a larger purse drawn from the accumulated 20%. ~3,000–4,000 SKR per tournament. Format TBD — could be:

- Highest single-day wave score during a 7-day tournament window
- Bracket-style 1v1 dungeon speedruns (introduces async PvP without real combat)
- Cooperative goal — community-pooled progress unlocks a celebratory drop for everyone who participated

Seasonal tournaments are the marketing moment — Twitter clips, partnerships with Solana Mobile-aligned creators, narrative beats in the game world tied to the tournament's storyline (the Keeper's Tournament hosted by Sir Bram, etc.).

### 12.3 Anti-gaming policy

A 100% on-chain payout system is also a 100% attractive attack surface. Mitigations:

- **Minimum account age** — wallet must have been used for at least one non-prize tx before being eligible for any prize over 5 SKR. Filters out farm wallets created the moment a contest starts.
- **Single-claim guard per achievement** — each achievement pays once per save, even if the save is reset.
- **Rate-limit suspicious patterns** — a wallet that wins 3 weekly leaderboards consecutively + has < $50 of pack purchases in its history gets manually reviewed before the 4th payout.
- **Soft KYC for prizes > 50 SKR** — for the seasonal tournament tier specifically, winners email an attestation that they're a real human, not an org running automated farms.
- **Public payouts log** — every payout tx hash listed publicly at `/treasury/payouts`. Players can audit; treasury accountability is visible.
- **Treasury watch-window** — if accumulated unclaimed prize debt exceeds the treasury's available balance, halt new contests until the backlog clears. Never write checks the treasury can't cash.

### 12.4 Legal flag — talk to a lawyer

Crypto contests with monetary prizes intersect with gambling, sweepstakes, and money-transmission law in many jurisdictions. This spec describes a **skill-based contest model** (high-wave, fast-clear, leaderboard) which is generally distinguishable from sweepstakes / lotteries — but the legal line is jurisdiction-specific.

Before launch:

- Consult a lawyer who handles crypto + gaming compliance (Solana ecosystem firms; there are several with this practice).
- Specifically clarify: is the contest model defensible as skill-based in the US, EU, and UK? Are there jurisdictions where it must be excluded outright?
- Geo-fence prize eligibility if needed. Players in excluded jurisdictions see the leaderboard but cannot win — same UX pattern as wallet-less players.
- Document the legal opinion in `docs/contests-legal-opinion.md` (placeholder created; lawyer fills).

**Status: legal review is a pre-launch blocker for Stream B + C.** Stream A (achievement drops) is structurally a "thank you for completing the game" reward; the risk profile is lower. Owner can ship Stream A in v1 and gate Streams B + C behind the legal review.

### 12.5 Architecture additions

Beyond §8:

- **Treasury yield wallet** — a fourth treasury (separate from packs SOL / USDC / SKR receive wallets). This wallet receives staking rewards and distributes prizes. Funded weekly from the staked principal's yield via a manual or scripted transfer.
- **Leaderboard service** — new `services/leaderboard.ts`. Records score events per wallet per category per week. Stores in Vercel Postgres. Reads on leaderboard screen.
- **Achievement event hooks** — every gameplay event that triggers an achievement (first wave, first dungeon, etc.) fires `services/achievements.ts → recordAchievement(walletOrPending, achievementId)`. If wallet-connected: triggers payout immediately. If wallet-less: stores as pending in localStorage; claims on connect.
- **Payout signer** — a backend signer wallet, hardware-key-secured, that signs SKR transfer txs. Triggered by a daily cron OR a manual approval flow for prizes over a threshold.
- **Public payouts page** — `/treasury/payouts` shows every payout with: timestamp, recipient wallet (truncated), amount, reason (achievement / leaderboard / tournament), Solscan link to the tx. Transparency by default.

### 12.6 Acceptance criteria additions

- [ ] Treasury yield wallet exists and is separate from packs receive wallets.
- [ ] Stream A (achievement drops) functional end-to-end: completing a tracked achievement pays the wallet within 10 minutes or stores as pending if no wallet connected.
- [ ] `/treasury/payouts` page exists and lists every payout with verified Solscan links.
- [ ] Anti-gaming: minimum-account-age filter functional; suspicious-pattern review queue exists.
- [ ] Stream A wallet-less pending-claim mechanism: connect wallet → pending drops mint.
- [ ] Stream B + C gated behind legal sign-off (a single boolean flag in `services/contests.ts` config; default OFF until lawyer's opinion lands).

---

## 13. The grant pitch — Solana Foundation application strategy

**The runway math (owner-validated 2026-05-17):** A modest $10,000 USD development grant from the Solana Foundation funds approximately **6 months of focused innovation** on this project. That sets the target — small grants are realistic, not large ones, and they're more than sufficient runway given the project's solo / low-overhead operating cost. We are not pitching a $250K grant for 12 engineers; we are pitching a $10K grant for one developer to do another 6 months of high-velocity work that builds on the 60-commits-a-day cadence already demonstrated.

### 13.1 The pitch itself

> _"Defenders of the Realm is a cozy mobile-first tower defense game that integrates Solana payments as a first-class purchase rail alongside Stripe. Players can buy themed cosmetic and convenience packs in SKR, SOL, USDC, or USD; paying with their Seeker wallet is a native experience, not an afterthought._
>
> _Beyond accepting payments, the game **operates on the SKR yield curve**: 1 million SKR staked by the developer funds a self-sustaining player-rewards economy — achievement drops, weekly skill-based leaderboard contests, and seasonal tournaments. The economy never requires touching principal. Players earn SKR by engaging with the game; some of those players spend SKR back through packs. The loop is closed._
>
> _The game runs entirely without a wallet for players who don't want one, holding the cozy 'never required to spend, ever' covenant intact. A connected wallet unlocks the full payment ladder, the rewards economy, and a permanent on-chain entitlement the player owns across devices._
>
> _This is what first-party Solana Mobile ecosystem alignment looks like at the game-design level — not the integration level."_

### 13.2 The technical demo for grant review

A 5-minute live demo:

1. **Show the title screen + splash cinematic** — 5 seconds, demonstrates production polish.
2. **Walk a wave + breach + ATB Last Stand** — 60 seconds, demonstrates the core loop.
3. **Open the store, show the four-tab pack detail page** (SKR / SOL / USDC / Stripe) — 15 seconds.
4. **Buy a pack live with SKR** — wallet sign → on-chain → entitlement in hand — ~5 seconds end-to-end.
5. **Show the on-chain tx in Solscan** with the treasury wallet receiving SKR — 10 seconds.
6. **Show the game responding** — cosmetic unlocked, equipped, visible in the village — 5 seconds.
7. **Open `/treasury/payouts`** — show the public payouts log; click through a recent achievement drop tx in Solscan — 30 seconds.
8. **Show the leaderboard** with this week's prize pool funded from yield, with the public yield-balance dashboard — 30 seconds.

The whole demo runs in under 4 minutes. **No slides. Just live software.** Velocity + integration + transparency are the three things grant committees notice; this demo shows all three.

### 13.3 The concrete grant targets — apply to all of these

| Program                                                        | Typical size                              | Lead time        | Fit                                                                                   |
| -------------------------------------------------------------- | ----------------------------------------- | ---------------- | ------------------------------------------------------------------------------------- |
| **Solana Foundation Grants — Builder track**                   | $5K–$50K                                  | 2–6 weeks review | Direct fit. Apply once v1 ships and the demo above can run live.                      |
| **Solana Mobile dApp Store featured-app program**              | Marketing/promotion + sometimes prize SKR | TBD              | Direct fit. Apply during dApp Store submission review (Day 14 of the 2-week roadmap). |
| **Solana Foundation Game Day / hackathon prizes**              | $5K–$25K per category                     | Quarterly        | If timing aligns with a hackathon, enter retroactively with shipped product.          |
| **Helium / Render / IoT-token cross-ecosystem grants**         | Variable                                  | Variable         | Lower priority; only if there's a real integration story (unlikely for this game).    |
| **Solana ecosystem VCs (Multicoin, Foundation Capital, etc.)** | $50K–$500K seed                           | Months           | Defer until clear retention data. Grant before VC.                                    |

The order: **Solana Foundation Grants first** (most aligned, fastest, smallest commitment). **dApp Store featured-app program in parallel** (no downside to applying). VCs only if the metrics post-launch warrant a seed conversation.

### 13.4 Application timing — when to apply

- **Apply to Solana Foundation Grants ~Day 16 of the 2-week roadmap** — after the dApp Store submission is filed but before review returns. The application can cite "currently in dApp Store review" which is its own signal.
- **Apply to dApp Store featured-app slot Day 14**, simultaneously with the submission itself.
- **Continue building during the review period.** Grants and dApp Store reviews both take weeks; the v1.1 work (Tower-Sim engine, accessibility, CI, etc.) fills the wait productively.

### 13.5 Application materials checklist

Beyond the spec itself:

- [ ] **30-second gameplay clip** — title screen → village → wave → breach → ATB Last Stand → victory. No voiceover. Just gameplay + the in-game audio.
- [ ] **5-minute demo video** — the §13.2 demo above, recorded and uploaded unlisted to YouTube.
- [ ] **Commit history visualization** — a Git graph or a `gource`-rendered video of the 60-commits-a-day cadence. Velocity is a story; tell it visually.
- [ ] **The four key docs linked:** `monetization-v2-spec.md` (this file), `narrative-bible.md`, `refactor-feature-modules-spec.md`, `two-week-roadmap.md`. Demonstrates the studio-grade thinking, not just a vibe.
- [ ] **The owner's PM background story** — 2-3 paragraphs on running HP global projects, the discipline that comes with that, why this project applies that discipline to indie game dev.
- [ ] **The grant ask** — explicit: "$10,000 USD to fund 6 months of v1.1 / v1.2 development. Specific milestones: [Tower-Sim engine, accessibility pass, second dungeon questline, marketing trailer]."

### 13.6 What $10K of grant runway actually buys

Concrete budget for a 6-month $10K runway:

| Line item                                                                 | Cost                    | Why                                                         |
| ------------------------------------------------------------------------- | ----------------------- | ----------------------------------------------------------- |
| Hosting (Vercel + treasury infra)                                         | $30/month × 6 = $180    | Already running                                             |
| Domain renewal                                                            | $20                     | Annual                                                      |
| Claude API spend (build agents)                                           | $400/month × 6 = $2,400 | The actual cost of running this project's parallel agents   |
| One-off art commissions (icon polish, feature graphic, missing pet skins) | $1,500                  | Asset gaps; ship a more visually-finished v1.1              |
| Music commission (one new track for the Hollow Deep / endgame)            | $1,000                  | Single track from a Bandcamp-tier composer                  |
| SFX bundle license                                                        | $300                    | Polish layer for ATB / Tower-Sim / dungeon                  |
| Audio mixing pass                                                         | $500                    | Professional mix of the existing tracks for mobile playback |
| Lawyer (contests / sweepstakes opinion)                                   | $1,500                  | Pre-launch blocker for Streams B + C (§12.4)                |
| Solana mainnet tx fees (treasury operations, payout signing)              | $50                     | Negligible at SOL prices                                    |
| Marketing — paid X promotion + Discord boost                              | $1,000                  | Two 30-day campaigns around v1 launch + v1.1                |
| Contingency / unknown unknowns                                            | $1,550                  | 15% buffer                                                  |
| **TOTAL**                                                                 | **$10,000**             | 6 months of solid v1.x runway                               |

This budget is fundable, specific, and explainable. A grant reviewer reading it sees a developer who has thought about cost, not just code.

### 13.7 Why this pitch works for Solana Foundation specifically

The Foundation funds projects that move metrics they actually care about. For dApp Store / Mobile / Seeker, those metrics are:

- **dApp Store quality bar** — they need flagship apps. A polished, mobile-first game with native SKR integration raises the bar; the Foundation gets demo-able value.
- **SKR utility surface** — every game that exercises SKR strengthens the token's narrative. A game where 1M SKR funds an ongoing player-rewards economy is a new SKR utility pattern, not just another consumer.
- **Solana-native consumer onboarding** — a player who connects a wallet to claim a 5 SKR drop has just performed a non-trivial onboarding step (wallet creation OR import, signing a tx) for the cost of about a minute of game time. This is a high-quality consumer funnel.
- **Studio-grade execution from indie** — the velocity + architecture + spec discipline visible in this repo is exactly the kind of "indie scaling like a small studio" story that Foundation marketing wants to highlight.

We are not pitching against teams who are doing crypto features for crypto's sake. We are pitching as a competent game that happens to use crypto correctly. That's the differentiator.

---

_This spec gives the game a real revenue model that respects the cozy covenant 90% of the way and engineers the Solana Foundation grant story into the architecture. Convenience-power is the explicit compromise; the rip-out path in §11.1 is the safety valve. Combat power remains forever off the table._

---

## 14. Review history

| Date       | Reviewer           | Key feedback applied                                                                                                                         |
| ---------- | ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-05-17 | Grok (external)    | (a) Three locked changes below; (b) playtest plan added §9.2; (c) §2 removable-experiment line added.                                        |
| 2026-05-17 | ChatGPT (external) | (a) Tower fire-rate pre-charge removed from §5.3; (b) Founder's Vow perpetual passive removed; (c) SKR launch-pricing checkpoint added §9.1. |
| 2026-05-17 | Owner              | All three external-review locked changes applied; spec greenlit for build.                                                                   |

### 14.1 What was changed (review changelog)

- **§2** — added the "convenience power is a removable experiment" sentence so future Claude sessions and reviewers see the experimental framing.
- **§5.3 Allowed list** — removed "Tower fire-rate pre-charge." Convenience layer stays clear of live combat performance.
- **§5.3 Forbidden list** — explicitly added "tower fire-rate pre-charge" and "any permanent passive trait" so future-you doesn't try to slip them back in.
- **§5.3 Tier table** — Patron loses the pre-charge item. Founder's Vow loses the "perpetual auto-collect at half-rate" passive and gains a larger one-time bundle of the same finite tokens every tier gets. No paid passive runs forever.
- **§9** — three new acceptance criteria: no-permanent-passive review, SKR launch-pricing checkpoint, closed-playtest gate.
- **§9.1** — new section. Pre-enable blocker: SKR price reconfirmed within 24h of enable; pack amounts recalibrated if >15% drift; owner-signoff commit.
- **§9.2** — new section. ≥20 closed playtesters; 5-question feedback survey; any "predatory" response triggers §11.1 rip-out before launch.

### 14.2 What was deliberately NOT changed (Grok suggestions not taken)

- **Pricing ladder tweak ($2.99/$6.99/$12.99/$24.99/$49.99 indie variant)** — Grok suggested testing as alternative. Held the mobile-game ladder ($1.99/$4.99/$9.99/$19.99/$49.99) for v1 since the dApp Store target market is mobile-game-trained. Indie variant remains available as v1.1 A/B test if conversion data warrants.
- **"Founder's Vow" rename to "Launch Guardian Banner"** — Grok suggested softening the name. Held "Founder's Vow" because it carries narrative weight from `narrative-bible.md` and the dungeons storyline (the Keeper's vow language). The banner UX/copy is being softened ("a thank-you to early believers, not a flex") inside the pack detail page, not the SKU name.
- **Public multi-sig signer documentation** — Grok suggested. Deferred to the grant application materials (§13.5) rather than the spec, since spec exposure of treasury structure is a security tradeoff.

---

## 15. Treasury setup playbook

This section is the operations runbook for setting up the four Solana wallets the system depends on. **Owner-executed; Claude Code can verify but cannot perform key generation or multi-sig configuration.** Complete before the §9 acceptance criteria can be measured.

### 15.1 The four wallets

| Wallet                         | Purpose                                                  | Recommended security                                    |
| ------------------------------ | -------------------------------------------------------- | ------------------------------------------------------- |
| **Treasury — SOL receive**     | Inbound for SOL pack purchases                           | 2-of-2 multi-sig (owner hot key + hardware co-signer)   |
| **Treasury — USDC receive**    | Inbound for USDC pack purchases                          | 2-of-2 multi-sig (same signers as SOL receive)          |
| **Treasury — SKR receive**     | Inbound for SKR pack purchases                           | 2-of-2 multi-sig (same signers)                         |
| **Treasury — yield + payouts** | Receives staking rewards; signs prize payouts to players | 2-of-2 multi-sig + hot-key signer with daily rate limit |

The publisher wallet (the one that mints the dApp Store release NFTs) is a fifth, completely separate wallet — **never confused with the treasuries.** Cross-contamination of publisher and treasury is the single biggest security mistake to avoid.

### 15.2 Setup checklist — owner-executed

In order:

- [ ] Generate four fresh Solana keypairs offline (e.g. via `solana-keygen new` on an air-gapped machine, or use a hardware wallet like Ledger / Solflare). **Never paste these seed phrases into Claude, ChatGPT, or any browser tool.**
- [ ] Back up each seed phrase on physical paper, two copies, stored in geographically separated secure locations.
- [ ] For each wallet, use [Squads](https://squads.so/) to wrap the key as a multi-sig vault. Squads is the standard Solana multi-sig tool; well-audited; UI is straightforward.
- [ ] Each Squads vault: 2-of-2 with owner's day-to-day wallet as signer A, a hardware-backed key (Ledger) as signer B. Squads exposes a _vault address_ which is the address you publish.
- [ ] Fund each receive vault with ~0.01 SOL for rent + tx fees.
- [ ] Fund the payouts vault with the first month's prize budget transferred from the staking principal (the 1M SKR delegation reward stream — see §12.5).
- [ ] Record the four vault addresses (not the signer keys) in `src/services/treasury.ts` constants and commit. Public knowledge by design — every payout from these wallets is traceable on Solscan.

### 15.3 Owner-facing wallet hygiene

- The owner's day-to-day signer wallet **never** holds the treasury principal directly. It's only one of two signers on a vault.
- Daily payout signing happens via Squads' web UI or CLI; multi-sig means a single compromised key can't drain anything.
- The hardware key (Ledger) is the cold half. Plug in only to sign tournament-level payouts or to rotate signers.
- If the owner's hot key is compromised: rotate it via Squads (the hardware co-signer authorizes), and there is no theft because the attacker has only 1 of 2 signatures.

### 15.4 Operational cadence

| Cadence   | Action                                                                                                                                   |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| Daily     | Hot key signs Stream A achievement-drop payouts (auto-batched; manual approval)                                                          |
| Weekly    | Hot key signs The Watcher's Roll leaderboard payouts (Sunday 23:59 UTC cutoff → Monday batch)                                            |
| Quarterly | Hardware key + hot key co-sign seasonal tournament payouts (larger purse — warrants both signatures)                                     |
| Quarterly | Stake-yield sweep from delegated principal → payouts vault (manual or scripted)                                                          |
| Quarterly | Public `/treasury/payouts` reconciliation — owner posts a transparency thread on X showing yield received, prizes paid, treasury balance |

### 15.5 What Claude Code does

- Reads the four vault addresses from `src/services/treasury.ts` (owner commits these).
- Verifies inbound txs against these addresses in `services/payment-verifier.ts`.
- Constructs unsigned payout txs and posts them to a queue; the owner's signer tool (Squads UI or a CLI script) finalizes them.
- **Claude Code never holds a signing key.** Every key the system uses is owner-controlled.

### 15.6 Acceptance for §15

- [ ] Four Squads vaults created; addresses recorded in `src/services/treasury.ts`.
- [ ] Each vault funded with rent + tx fees.
- [ ] Payouts vault funded with one month's projected prize budget.
- [ ] First test tx (1 SKR to a test wallet) signed end-to-end via the multi-sig flow.
- [ ] Recovery dry-run: confirm the hardware key alone can re-add a signer if the hot key is lost. (Don't actually lose it — just verify the procedure.)

---

## 16. The five-minute grant pitch demo — turn-by-turn script

Recorded video, no slides, no voiceover during the demo itself. Single take if possible. Use OBS or QuickTime; export 1080p mp4.

A **separate voiceover track** (the owner's voice) overlays the gameplay with timed callouts. The demo is the proof; the voiceover is the framing. Total runtime target: **4 minutes 40 seconds**.

### 16.1 Pre-recording setup

- Defenders of the Realm running locally on `localhost:5173` against a clean save (delete localStorage first).
- Two browser windows: one for the game, one for Solscan + the public payouts page side-by-side.
- A test wallet pre-funded with: 60 SKR (Lanternlight tier), 1 SOL (gas), ~5 USDC (alternate rail demo if time).
- Audio: in-game music + SFX enabled at moderate volume; voiceover recorded separately and mixed in post.

### 16.2 Shot-by-shot script

| Time      | Shot                       | What happens on screen                                                                                                                                                                          | Voiceover                                                                                                                                                                                                                                    |
| --------- | -------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0:00–0:05 | Title screen               | Splash cinematic plays — embers, violet motes, "Defenders of the Realm" logo arrival                                                                                                            | _"Defenders of the Realm — a cozy mobile-first tower defense built for Solana."_                                                                                                                                                             |
| 0:05–0:20 | Onboarding flash           | Quick cut: hero pick → pet pick → village load                                                                                                                                                  | _"You're the Keeper, bound to the Heart of an old valley. The dark is coming for it."_                                                                                                                                                       |
| 0:20–1:20 | Wave 1 → first breach      | Walk a complete wave: enemies spawn, towers fire, pets deploy, hero abilities cast, an enemy breaches the wall, screen cuts to ATB Last Stand, hero + pets resolve the encounter, victory card. | _"Real-time tower defense with a turn-based fallback when defenses break. Three hero classes, three pet spirits, four hundred enemies across ten biomes."_                                                                                   |
| 1:20–1:35 | Open store                 | From the village HUD, tap the 🛍 glyph. Store opens — five packs visible, each themed.                                                                                                          | _"The game makes its money through cosmetic and convenience packs. Never combat power, never required to buy anything."_                                                                                                                     |
| 1:35–1:50 | Pack detail                | Tap Lanternlight. Modal opens with full contents — cosmetics with preview renders, economy amounts, convenience tokens. **Four currency rail tabs visible: SKR / SOL / USDC / Stripe USD.**     | _"Every pack is purchasable in four rails — SKR, SOL, USDC, or fiat through Stripe. Players pick their preferred currency."_                                                                                                                 |
| 1:50–2:00 | SKR tab selected           | Tap the SKR tab. Price shows "60 SKR ≈ $4.99 USD as of [timestamp]". USD reference is visible.                                                                                                  | _"Paying with SKR is a first-class experience, not an afterthought. The USD reference makes the price transparent."_                                                                                                                         |
| 2:00–2:15 | Buy with SKR — wallet sign | Tap Buy. Wallet popup opens (Phantom). Confirm signing. Tx submits. Loading spinner.                                                                                                            | _"The player signs once; their Seeker wallet handles the rest."_                                                                                                                                                                             |
| 2:15–2:25 | Game responds              | Loading spinner resolves to victory state. Cosmetics unlock, equip on hero. Confirmation toast in the village.                                                                                  | _"Within seconds, the entitlement is on-chain and the cosmetics are equipped."_                                                                                                                                                              |
| 2:25–2:50 | Solscan reveal             | Switch to the other browser window. Solscan shows the new tx targeting the SKR treasury vault. Click through to the multi-sig vault page — public address, recent inflows visible.              | _"The transaction is public, the treasury is public, every dollar a player spends is auditable on-chain."_                                                                                                                                   |
| 2:50–3:10 | Yield + payouts page       | Navigate to `/treasury/payouts`. Page lists recent payouts: timestamps, recipients (truncated), amounts, reasons (achievement drop, leaderboard prize). Each row links to Solscan.              | _"And here's the other half of the story — the developer has one million SKR staked. The yield, around five thousand SKR a month, funds player rewards. Achievement drops, weekly leaderboard prizes, seasonal tournaments."_                |
| 3:10–3:30 | Leaderboard                | Navigate back to the game. Open the Watcher's Roll leaderboard. Current week's prize pool shown — funded from yield. Top 10 players listed.                                                     | _"This is a skill-based leaderboard contest. Top players this week win small SKR prizes. Subject to legal sign-off in each jurisdiction — that's documented in the spec."_                                                                   |
| 3:30–3:45 | Achievement drop close-up  | Click an achievement entry in the payouts list. Solscan tx open shows a 5 SKR drop to a player wallet. Player wallet shown receiving it.                                                        | _"A player just finished their first dungeon. They got five SKR for it. We paid them about forty cents to engage them long enough to maybe spend twenty dollars on packs. The unit economics are extraordinary."_                            |
| 3:45–4:15 | The flywheel sentence      | Cut to a simple text card overlay (white text on the village background): "Yield → Players → Engagement → Packs → Treasury → reinforced stake. Closed loop."                                    | _"The game operates on the SKR yield curve. One million staked, fifty thousand SKR a year flowing to players, some of it flowing back through packs. Players hold SKR. The Foundation's token gets actual utility. The flywheel is closed."_ |
| 4:15–4:40 | The ask                    | Cut to a final text card: "Defenders of the Realm. $10,000 USD to fund 6 months of v1.1/v1.2."                                                                                                  | _"We're asking for ten thousand dollars to fund six months of innovation. The roadmap is in the spec. Thanks for watching."_                                                                                                                 |

### 16.3 Editing pass

- Cut tightly. Anywhere the demo lags (e.g. transaction confirmation taking longer than expected), speed-ramp 2× and overlay a clock so the viewer knows time passed.
- Lower-third callouts at key moments: "1M SKR staked" when the flywheel reveal lands; "$10K ask, 6 mo runway" on the final card.
- Match cuts between gameplay and Solscan should be brisk — no lingering on either.
- Background music: in-game audio only. Don't add corporate-pitch-deck music.

### 16.4 Acceptance for §16

- [ ] Demo recorded end-to-end in a single take if possible (one re-take per second budget acceptable).
- [ ] Voiceover recorded clean, mixed at -6dB below in-game audio.
- [ ] Final video uploaded **unlisted** to YouTube.
- [ ] Video link added to `docs/grant-application-materials/index.md`.
- [ ] Runtime ≤ 5:00 — if it runs longer, re-cut.

---

## 17. The 30-second gameplay clip storyboard

The companion piece to §16. Where the 5-min demo tells the _full story_, the 30-second clip exists to **hook in the first frame and never let go**. Used for X (Twitter), the Vercel landing page, the dApp Store listing's preview video field, and the top of any grant application.

**No voiceover. No text overlay. Just gameplay + in-game audio.** This is the moment the game has to speak for itself.

### 17.1 Pacing principle

A 30-second clip has roughly **6–8 shot beats**. Each beat ≤ 5 seconds. The cuts should accelerate slightly toward the end — slower setup, faster payoff.

### 17.2 Shot list

| Time      | Shot                   | What happens                                                                                                                                                 |
| --------- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 0:00–0:04 | **The Title**          | Splash cinematic. Embers ignite. Violet motes drift. "DEFENDERS OF THE REALM" arrives via the staged logo reveal. Hold one beat on the final logo.           |
| 0:04–0:08 | **The Village**        | Camera pans across the 3D village — Heart at center glowing violet, walls intact, three pets idling, hero standing near a building. Day-night cycle visible. |
| 0:08–0:13 | **The Wave Begins**    | "Wave 5 Incoming" notice. Enemies spawn at edges. Pets snap to attention. Hero turns to face. First tower fires — gold particles, satisfying SFX.            |
| 0:13–0:18 | **The Hero in Combat** | Tight shot on the hero casting Frost Nova / Meteor Strike. AoE explosion. Multiple enemies stagger. Floating damage numbers.                                 |
| 0:18–0:21 | **The Breach**         | An enemy crosses the wall ring. Red vignette pulses. Screen begins to dim.                                                                                   |
| 0:21–0:26 | **Last Stand**         | Cut to the ATB battle screen — hero + pets aligned bottom-left, enemies right. ATB bar fills. Hero acts; enemy hit; combo float.                             |
| 0:26–0:29 | **Victory**            | Victory card flashes briefly — Heart-violet glow, narrative-bible-voice tagline ("_The line holds. The Heart breathes again._").                             |
| 0:29–0:30 | **The Tagline**        | Single frame: black background, gold serif text: _Tend the Heart. Hold the dark._ Hold for one beat. End.                                                    |

### 17.3 Production notes

- **No UI clutter.** Hide the dev HUD, hide settings buttons, leave only the in-fiction HUD (Heart HP bar, mana, ability bar, pet bar).
- **No console.** Record on a clean save with a moderately-stocked wave 5 — not a tutorial wave (too slow), not a wave 30 (too chaotic).
- **Frame rate:** 60 fps capture if the perf work has hit its targets. 30 fps is acceptable if mobile perf optimization hasn't fully landed yet — note in the upload that the final v1 build will be 60.
- **Resolution:** 1920×1080 master; 1080×1920 portrait crop for the dApp Store listing preview (per `docs/solana-dapp-store-submission.md` §5.4 requirements).
- **Aspect ratio:** export both landscape and portrait masters. Portrait is canonical for the dApp Store and X mobile.
- **Audio:** in-game music + SFX. If the music is in transition between tracks during recording, re-record. The audio bed should feel continuous.

### 17.4 The 5-second test

Show the first 5 seconds to someone who has never seen the game. If they can't tell:

- It's a game (not a website / app demo)
- It's set in a magical fantasy world
- It looks polished

…re-cut. The first 5 seconds carry disproportionate weight on social platforms where autoplay+mute is the default. The splash cinematic + the village pan are deliberately ordered to make those facts unavoidable.

### 17.5 Acceptance for §17

- [ ] Clip recorded at 1920×1080 master + 1080×1920 portrait master.
- [ ] Runtime 28–32 seconds (target 30).
- [ ] Both masters uploaded to `docs/grant-application-materials/` and to the dApp Store submission packet.
- [ ] No text overlay, no voiceover, no UI dev clutter.
- [ ] Audio mixed: music + SFX at -3dB; no clipping.
- [ ] **5-second test passed** with at least 2 non-developers.

---

## 18. Priority for Claude Code (which to build first)

When this spec graduates from design to build queue, Claude Code prioritizes:

1. **§15 Treasury setup playbook** — owner-executed; Claude Code helps verify and commits the resulting addresses to `src/services/treasury.ts`. **Pre-requisite for everything else in this spec.** Cannot ship the pack store without treasury addresses.
2. **§8 architecture + Stripe rail** — the simplest payment integration to verify end-to-end. Stripe-only build, Stream A achievement drops, no contests. Ships a real revenue rail without crypto complexity.
3. **§7.4 USDC + SOL rails** — once Stripe works, add the two non-volatile crypto rails. They share verification logic with SKR (§7.5) but don't carry the launch-pricing reprice risk.
4. **§7.5 SKR rail + §9.1 launch-pricing checkpoint** — last crypto rail to enable. Gated by the calibration checkpoint.
5. **§9.2 closed playtest gate** — runs after rails 1-4 work but BEFORE production purchase flag flips on. Pre-launch blocker.
6. **§12 yield-funded rewards Stream A** — independent of pack purchase flows; ship alongside the store as the "we give too" half of the economy.
7. **§16 + §17 grant materials** — Claude Code can produce a draft cut of the 5-min demo and the 30-second clip using OBS-style screen capture; owner does the final edit + voiceover.
8. **§12 Streams B + C** — gated by the legal opinion (§12.4); ship as soon as the lawyer signs off, can be after v1 launch.

Anything in §13 (grant application) is owner-action, not build-action — Claude Code helps assemble materials but the submission itself is human-driven.
