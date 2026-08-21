<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER — SKR Staking Reward + Solana Seeker Ecosystem Pitch

**Type:** DESIGN + RESEARCH SPEC (ideas, data schema, phased plan, cited sources). **No `.cs` in this WO** — implementation is a follow-up WO once the owner greenlights a stage.
**Status:** CLOSED — ALREADY DONE (owner ruling 2026-08-21).
**Author lane:** Monetization/Backend + Distribution (§9 parallel lane — isolated from gameplay).
**Date:** 2026-06-28
**Supersedes nothing.** *Layers on top of* `WorkOrders/WORK_ORDER_skr_store_design.md` (held-SKR premium store, `ISkrLedger`), `WorkOrders/WORK_ORDER_pi_browser_integration.md` (parallel web rail), `docs/monetization-v2-spec.md` §12 (owner's yield-funded rewards), and `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` (staged local→cloud→Solana). Staking is a **small loyalty layer + a distribution wedge**, not a new economy.

---

## 0. The one-sentence shape

> **Lock (stake) held SKR for a chosen duration → earn a SMALL, capped, cosmetic-first loyalty reward** (a "Keeper" crown/flair + a modest convenience bump, and *optionally* a tiny SKR drip drawn **only** from the existing capped owner-yield pool — never new emission). It ships V1 as an **off-chain virtual lock** (a save-side balance hold behind `ISkrLedger`, no wallet), and later resolves against **real on-chain SKR staking** — which matters because **the game's SKR *is* the real Solana Mobile Seeker token**, and that single fact is the spine of the Seeker pitch (Part B).

### Why this exists
The owner asked for "not a massive bump, but something to make the Solana Seeker give more weight for consideration." This WO designs the *smallest healthy* staking reward that (a) gives a holder a reason to keep SKR locked, (b) cannot inflate or become pay-to-win, and (c) converts directly into a stronger Seeker / Solana Mobile ecosystem story.

---

## 1. GROUND TRUTH — read before designing (the load-bearing fact)

**The game's premium token `SKR` is the real Solana Mobile "Seeker" token.** This is not a coincidental ticker — `monetization-v2-spec.md` §12 already states the owner has **1,000,000 SKR staked** earning protocol yield. Research confirms what that token is:

- **SKR** = "the native asset of the Solana Mobile economy," an **SPL token, fixed supply 10,000,000,000**, launched **January 2026**. [Solana Mobile — SKR launches Jan 2026](https://blog.solanamobile.com/post/skr-launches-january-2026), [bitget academy](https://www.bitget.com/academy/what-is-solana-seeker-skr-and-how-does-it-work)
- **Native protocol staking already exists:** holders **delegate SKR to "Guardians"** (Anza, DoubleZero, Triton, Helius, Jito, Solana Mobile) to earn inflation rewards and govern. **Inflation starts ~10% APY at TGE, decaying 25%/yr to a 2% terminal rate by ~year 6.** Unstaking has a **2-day epoch / ~48-hour cooldown**; rewards auto-compound. [phemex academy](https://phemex.com/academy/what-is-seeker-skr), [bitget academy](https://www.bitget.com/academy/what-is-solana-seeker-skr-and-how-does-it-work), [stake.solanamobile.com](https://stake.solanamobile.com/)
- **Guardians use delegated SKR stake to *curate the dApp Store*** — verify device integrity and **review/approve dApp Store submissions**. Staking SKR is literally wired into app-store governance. [bitget academy](https://www.bitget.com/academy/what-is-solana-seeker-skr-and-how-does-it-work), [SKR launch blog](https://blog.solanamobile.com/post/skr-launches-january-2026)

**Two consequences that drive every decision below:**
1. **We must NOT mint or simulate SKR "yield."** The protocol already pays staking inflation to holders. If the game also paid an APY in SKR it would be (a) unsustainable, (b) a de-facto security, and (c) competing with the real token's economics. **The game's staking reward is therefore a LOYALTY layer — cosmetic/convenience perks for *demonstrated commitment* — not a yield product.** Any SKR component is a tiny *rebate* from a **hard-capped, pre-existing pool**, not interest.
2. **"Real on-chain staking" in our staging is not something we build — it's the protocol's existing Guardian-delegation.** Our V2 just *reads proof* that the player has SKR staked (or held) and unlocks the same cosmetic perks. The seam is "off-chain virtual lock" → "read on-chain staked balance," not "write a staking contract."

> ⚠ **OWNER DECISION TO CONFIRM (gates implementation):** Is the in-game premium SKR **the same mint** as Solana Mobile's SKR (the strongest, simplest story — premium currency = the Seeker token), or a **separate game token that shares the name** (cleaner legal separation, but a weaker pitch and a confusing duplicate ticker)? This WO is written for the **same-mint** reading (it is the better Seeker pitch and matches the §12 "1M SKR staked" framing). If it's a separate token, §A4 regulatory framing gets *more* important, not less.

---

# PART A — THE STAKING REWARD (small, sustainable, non-predatory)

## A1. Mechanic — reward TYPE options and the recommendation

Three candidate reward types. **Recommendation: ship (a) + a strictly-bounded (c); treat (b) as the optional, capped, later add-on.**

| Option | What it grants | Sustainability | Regulatory optics | Verdict |
|---|---|---|---|---|
| **(a) Non-token loyalty perks** — exclusive **"Keeper" crown/skin** + **profile flair/title**, a modest **offline echo-storage / passive-accrual convenience bump**, and a small **SKR-Store discount** while staked | **Infinitely sustainable** (cosmetics cost nothing to mint; convenience is time, not power) | **Clean** — consumptive utility, no profit promise | ✅ **PRIMARY** |
| **(b) Tiny SKR drip** — a *rebate* paid from the **existing capped owner-yield pool** (`monetization-v2-spec.md` §12, ~3,300–5,800 SKR/month), pro-rata to staked weight, **never exceeding the pool** | Sustainable **only because the pool is hard-capped** and pre-funded by the owner's *already-staked* principal's yield. **No new emission, ever.** | **Sensitive** — a token return reads "investment." Must be framed as a **loyalty rebate from a fixed budget**, capped, and *not advertised as APY* | ⚠ **OPTIONAL / LATER** (gate behind legal sign-off, §A4) |
| **(c) SKR-Store store-credit / discount** — staking grants a **% discount** (or a small fixed store-credit accrual) usable **only inside the SKR Store** on cosmetics/convenience | Sustainable (it's a price reduction on cosmetic SKUs, not a payout) | **Clean** — a loyalty discount, like any storefront | ✅ **SECONDARY** (bundle with (a)) |

**Why small + capped beats high-APY (the binding argument):**
- **Sustainability / anti-ponzi:** A high APY needs an ever-growing inflow to pay earlier stakers — the ponzi failure mode. Our reward budget is a **fixed pool** (the owner's existing staking *yield*, not principal) plus **zero-marginal-cost cosmetics**. It **cannot** promise more than it holds; the §12 "treasury watch-window" already halts payouts if unclaimed debt exceeds the pool.
- **Regulatory optics:** A modest, capped, cosmetic-first loyalty perk is **consumptive utility** — far from the Howey "expectation of profit from the efforts of others." A double-digit APY on a held token is the textbook *security* signature. Small + cosmetic keeps us in utility territory (§A4).
- **Covenant fit:** Cosmetics + convenience are exactly what the bent covenant permits; an APY race would pressure us toward pay-to-win to "justify" the yield. Small keeps us honest.
- **Player trust / non-predatory:** The reward is a *thank-you for believing in the project*, not a lure. No countdown, no "stake more to not miss out," transparent budget.

### A1.1 Concrete recommended reward (V1 starting numbers — all tunable JSON)
Three lock tiers, perks scale gently. Numbers are **illustrative defaults in `skr_staking.json`**, owner-tunable:

| Tier | Lock duration | Min staked | Perks (cosmetic-first) |
|---|---|---|---|
| **Kindled** | 30 days | 50 SKR | "Kindled Keeper" profile flair + 5% SKR-Store discount |
| **Tended** | 90 days | 200 SKR | + animated **Keeper crown** cosmetic + 10% discount + +1 echo offline-storage slot (convenience) |
| **Everburning** | 180 days | 500 SKR | + exclusive **"Everburning" weapon VFX** (expression) + 15% discount + a *small* SKR rebate share **iff** option (b) is enabled and within pool |

All perks are **cosmetic or convenience**; the discount and rebate are bounded. **None touch combat.**

## A2. Data model — thin interpreter, capped-pool accounting (owner's data-structure style)

Four tables; the runtime does a handful of verbs (`Stake`, `Unstake`, `AccruePerks`, `ClaimRebate`, `EvalTier`) and **never switches on a tier name** — tiers/perks are JSON rows. Full schema + sample: **`Assets/StreamingAssets/Data/Canonical/skr_staking.json`** (mirror to `Assets/Resources/...` per the `CanonicalJson.Read` WebGL pattern, exactly like `packs.json`/`skr_store.json`). Machine schema and a worked sample ship **with this WO** at that path.

**Table A — Stake record (player state, behind `ISkrLedger`):**
```
StakeRecord {
  id            : string     // stable id
  amountSkr     : number     // locked amount (debited from spendable SKR balance, held in a 'staked' sub-bucket)
  lockStartUtc  : string     // ISO-8601
  lockTierId    : string     // -> StakeTier table (duration + min)
  unlockUtc     : string     // computed: lockStart + tier.durationDays
  status        : enum { active, cooldown, released }
  cooldownEndUtc: string?     // set on early/normal unstake; mirrors protocol ~48h epoch
  source        : enum { virtual_local, virtual_cloud, onchain_proof }  // staging authority
  onchainRef    : string?     // V2: stake/delegation account or proof pointer (no keys, pointer only)
}
```

**Table B — Stake tier (config rows):**
```
StakeTier { id, displayName, durationDays:int, minStakeSkr:number, perks:[PerkGrant], earlyUnstakeForfeits:bool }
```

**Table C — Perk grant (what a tier delivers; reuses SKR-Store `Grant` shape):**
```
PerkGrant {
  kind        : enum { cosmetic_sku, profile_flair, store_discount_pct, convenience_bump, skr_rebate_share }
  cosmeticSku : string?                 // kind=cosmetic_sku  -> OwnedItemIds (reuse wardrobe SKU path)
  flairId     : string?                 // kind=profile_flair
  discountPct : number?                 // kind=store_discount_pct (applies in SKR Store while staked)
  bump        : { kind:string, amount:number }?   // kind=convenience_bump (e.g. echo_storage_slot +1)
  rebateWeight: number?                 // kind=skr_rebate_share -> relative weight in the capped-pool split
}
```

**Table D — Yield-source accounting (the anti-inflation firewall):**
```
RewardPool {
  poolId            : string        // e.g. "owner_yield_2026Q3"
  fundedSkr         : number        // SKR transferred in from the owner's *staking yield* (NOT principal, NOT minted)
  reservedSkr       : number        // sum of accrued-but-unclaimed rebates (debt)
  paidSkr           : number        // lifetime paid
  periodStartUtc    : string
  periodEndUtc      : string
  maxRebatePerUser  : number        // hard per-user cap (anti-whale, anti-security optics)
  haltIfDebtExceeds : bool          // §12 treasury watch-window: stop accruing rebate if reserved > funded - paid
}
```
**Invariant (regression-gated):** `paidSkr + reservedSkr <= fundedSkr` for every pool, always. A rebate can **never** be accrued that the pool can't cover — the runtime checks the pool *before* it credits, and `FlowTrace.Fail`s (never silently) if the pool is dry, degrading gracefully to perks-only. **This is the structural guarantee that the reward can't become a ponzi.**

### A2.1 The interpreter (design only — no code here)
```
Stake(amountSkr, tierId):
  Guard: Ledger.SpendableBalance >= amountSkr ; tier exists ; amount >= tier.minStakeSkr
  Ledger.MoveToStaked(amountSkr)            // ISkrLedger: spendable -> staked sub-bucket (no burn, no payout)
  write StakeRecord(active, unlockUtc = now + tier.durationDays)
  ApplyPerks(tier.perks)                    // cosmetics/flair/discount land immediately (the "thank-you")
  FlowTrace.Step("Skr","staked", amount, tierId)

AccrueRebate(pool, period):                 // only if option (b) enabled
  if pool.haltIfDebtExceeds and (pool.reserved > pool.funded - pool.paid): FlowTrace.Warn(...); return  // graceful, perks unaffected
  for each active onchain/virtual stake: share = weight / totalWeight ; amt = min(period.budget*share, pool.maxRebatePerUser - alreadyPaid)
  pool.reserved += amt ; record SkrDrop(pending)   // claimed later via the SKR-Store pendingClaims path

Unstake(id):
  set status=cooldown, cooldownEndUtc = now + cooldownHrs (≈48, mirrors protocol epoch)
  if early and tier.earlyUnstakeForfeits: revoke unclaimed rebate (NOT the cosmetics already granted — those are kept)
Release(id):  // after cooldown
  Ledger.MoveToSpendable(amount) ; status=released
```
Adding a new tier/perk = a JSON row. The interpreter never special-cases a SKU or tier.

## A3. Staging — explicit seam (V1 ships with NO Solana)

Maps onto the ratified data architecture and reuses the `ISkrLedger` seam already designed in `WORK_ORDER_skr_store_design.md` §6 — extended with a *staked sub-bucket* + the `RewardPool` accounting.

- **Stage 1 — V1, NOW: `LocalSkrLedger` virtual stake.** Staking moves SKR from the local *spendable* balance to a local *staked* sub-bucket; perks accrue from the JSON tier table; lock timers are save timestamps. **No wallet, no network, no Solana SDK.** Fully playable offline. `source = virtual_local`. (Option (b) SKR rebate can run here too, against a *locally-mirrored* pool figure, but **real SKR payout waits for Stage 3** — V1 rebate is shown as "pending, claimable when on-chain claims open," consistent with the SKR-Store `pendingClaims` design.)
- **Stage 2 — cloud: `CloudSkrLedger`.** Stake records + pool accounting reconcile from the cloud save DB (the first online dependency). `source = virtual_cloud`. Pool `funded/reserved/paid` becomes server-authoritative so rebate debt is honestly tracked. **Binary never in DB; cosmetics stream via Addressables** (pointer only).
- **Stage 3 — on-chain: `SolanaSkrLedger`, proof-of-stake-read (NOT a new staking contract).** The protocol *already* stakes SKR (Guardian delegation). Here the game **reads the player's real staked/held SKR** via `WalletService`/`SolanaWalletProvider` (or a backend RPC read, like the pack verifier) and unlocks the **same cosmetic tiers** — "you stake SKR with a Guardian → you wear the Keeper crown in-game." Any SKR rebate becomes a **real on-chain transfer from the §12 payouts vault**, server-verified, capped. `source = onchain_proof`. **Still optional** — a wallet-less player stays virtual forever and the loyalty perks still work on locally-held/earned SKR.

The seam means cloud and Solana light up **without touching the tier table, the perks, or the player.**

## A4. Guardrails (BINDING)

1. **Covenant firewall — rewards are NEVER combat power.** `PerkGrant.kind` is constrained to `{cosmetic_sku, profile_flair, store_discount_pct, convenience_bump, skr_rebate_share}` — there is **no stat/combat kind** and the regression (§A5) fails the build if one appears. Convenience bumps are limited to the already-sanctioned list (storage slot, passive-accrual time) — **no fire-rate, no cap raise, no permanent passive power.**
2. **No lock-you-out-of-spending trap.** Only the SKR the player **explicitly chooses to stake** is locked; the **spendable balance is untouched** and the SKR Store stays fully usable. Staking is opt-in, the locked amount is shown distinctly, and **early unstake is always allowed** (with only the *unclaimed rebate* forfeited — never the cosmetics, never the principal). Cooldown (~48h) mirrors the protocol so the UX matches real SKR.
3. **Lock-up clarity.** Pre-stake modal shows exact unlock date, exact perks, "you can unstake early (cosmetics kept, pending rebate forfeited)," and the staked amount remains the player's property throughout. No hidden auto-renew. No FOMO timer.
4. **Anti-inflation / capped-pool.** §A2 Table D invariant (`paid + reserved <= funded`) + per-user cap + the §12 halt-window. The reward budget is the owner's **existing staking yield**, never minted SKR, never principal.
5. **Regulatory framing (utility/cosmetic, avoid "security").** Language and design discipline:
   - **Frame as loyalty, not yield.** UI copy: "Keeper rewards for supporting Elarion" — **never** "earn X% APY," "passive income," or "returns." No APY number is ever displayed for the *game* reward (the *protocol's* native staking APY is Solana Mobile's, shown in their wallet, not ours).
   - **Cosmetic/convenience-first; the SKR rebate is a small, capped rebate from a fixed budget**, explicitly "while supplies last in the loyalty pool," not an interest rate.
   - **No promise of price appreciation or profit from others' efforts** (the Howey factors). Perks are consumptive utility delivered immediately.
   - **Keep the legal opinion gate:** option (b) (any SKR payout) ships **only after** the §12 sweepstakes/securities legal sign-off already budgeted in `monetization-v2-spec.md`. Options (a)+(c) (cosmetics + store discount) need no such gate and can ship first.
   - This is **not legal advice** — it is design discipline to *stay in the utility lane*; counsel reviews before any token payout goes live.

## A5. Regression invariants (headless build gate — the firewall)
A `SkrStakingRegression` (future WO) asserts:
1. Every `PerkGrant.kind` ∈ the allowed set — **a combat/stat kind fails the build** (pay-to-win firewall).
2. For every `RewardPool`: `paidSkr + reservedSkr <= fundedSkr` and `maxRebatePerUser > 0`.
3. Every tier has `durationDays > 0`, `minStakeSkr > 0`, ≥1 cosmetic/flair perk (the reward is real), and a non-empty unlock/forfeit disclosure string.
4. No perk grants soft currency or stats; `convenience_bump.kind` ∈ the sanctioned convenience list.
5. `cosmeticSku` resolves to a real SKU; `iconId`/asset refs are **pointer strings only** (no inlined binary).
6. Staking debits only the **staked sub-bucket**; spendable balance is never auto-locked.

## A6. What NOT to do (scope guard)
- **Do NOT** mint, emit, or simulate an SKR APY — the protocol pays staking inflation; the game pays *cosmetic loyalty* + an optional *capped rebate*.
- **Do NOT** lock the player's spendable SKR — only the opt-in staked amount.
- **Do NOT** grant any combat/stat/cap perk (regression gate).
- **Do NOT** advertise APY/returns/passive-income for the game reward (security optics).
- **Do NOT** ship option (b) SKR payout before legal sign-off; ship (a)+(c) first.
- **Do NOT** build a staking *contract* — V2 reads the protocol's existing stake; it does not reimplement it.
- **Do NOT** put binary in the catalog/ledger; pointer strings only.

---

# PART B — SOLANA SEEKER / SOLANA MOBILE PITCH

## B1. Current state of Solana Mobile / Seeker (researched, cited — June 2026)

**The Seeker phone is shipping.** Solana Mobile began shipping the second-gen **Seeker** smartphone to customers in **over 50 countries**, on **150,000+ preorders** (~$67.5M gross). [The Block](https://www.theblock.co/post/365600/solana-mobile-seeker-crypto-smartphone), [coinlaw](https://coinlaw.io/solana-seeker-phone-shipping/), [solanamobile.com/seeker](https://solanamobile.com/seeker)

**The SKR token launched January 2026** (see Part A §1): SPL, 10B fixed supply; native Guardian-delegation **staking** (10%→2% decaying inflation, ~48h/2-day-epoch unstake); SKR stake **curates the dApp Store**. [SKR launch blog](https://blog.solanamobile.com/post/skr-launches-january-2026), [phemex](https://phemex.com/academy/what-is-seeker-skr), [bitget](https://www.bitget.com/academy/what-is-solana-seeker-skr-and-how-does-it-work)

**The dApp Store is live and large, zero-fee.** Solana dApp Store 2.0 surfaces **2,500+ dApps** (265+ specifically engaged on Seeker), with **zero commission** — developers keep 100% of revenue (vs. Apple/Google 30%). Season 1 drove ~$2.6B volume / 9M txns. [coinlaw](https://coinlaw.io/solana-seeker-phone-shipping/), [helius](https://www.helius.dev/blog/publishing-solana-mobile-apps)

**There ARE developer incentives.** Solana Mobile **distributed 141M SKR to 188 developers** who shipped qualifying dApp Store apps in **Season 1 — ~750,000 SKR per qualifying team** (plus 1.8B SKR to ~100,000 users), claim window Jan 21–Apr 20 2026; plus ongoing **hackathons and builder grants.** [coinlaw](https://coinlaw.io/solana-seeker-phone-shipping/), [crypto.news](https://crypto.news/solana-mobile-confirms-1-8-billion-skr-token-airdrop-for-seeker-phone-users/)

**dApp Store listing — concrete requirements** ([Solana Mobile Docs — publishing overview](https://docs.solanamobile.com/dapp-publishing/overview), [prepare](https://docs.solanamobile.com/dapp-publishing/prepare), [submit-new-app](https://docs.solanamobile.com/dapp-publishing/submit-new-app), [Blueshift course](https://learn.blueshift.gg/en/courses/dapp-store-publishing/solana-dapp-store), [helius](https://www.helius.dev/blog/publishing-solana-mobile-apps)):
- Publish via the **Publisher Portal** (`publish.solanamobile.com`, recommended) or the **`dapp-store` CLI** (CI/CD).
- **Both Android apps AND web apps are accepted** — a **PWA can be converted to an APK** and published. (This is the bridge to the Pi WebGL workstream — §B4.)
- **Publisher account:** profile + **KYC/KYB**, connect a **Solana publisher wallet** (Phantom/Solflare/Backpack).
- **APK must be signed with a NEW signing key** used *solely* for the dApp Store — a key already used on Google Play is **rejected**.
- **Assets:** 512×512 icon, 1200×600 banner, **≥4 screenshots/videos** (1080p recommended).
- **Cost:** publisher wallet needs **~0.2 SOL** for tx + ArDrive (Arweave) upload fees. Releases are minted as **on-chain NFTs**.
- **Review:** ~**2–5 business days**, email notice of approval/rejection. Must comply with the **Publisher Policy**.

**Seed Vault & wallet integration** ([Seed Vault docs](https://docs.solanamobile.com/developers/seed-vault), [seed-vault-sdk](https://github.com/solana-mobile/seed-vault-sdk), [Unity SDK docs](https://docs.solanamobile.com/unity/unity_sdk), [Solana.Unity-SDK](https://github.com/magicblock-labs/Solana.Unity-SDK)):
- **Seed Vault** is the Seeker's hardware-backed key store (keys never leave the secure element / TEE). The **Seed Vault SDK is for *wallet* apps** — a **game/dApp should NOT integrate Seed Vault directly.**
- A game integrates via **Mobile Wallet Adapter (MWA)** — connect to any on-device wallet (incl. the Seeker's Seed Vault Wallet by Solflare), request authorization, sign + send transactions. **MWA has a Unity SDK** (also React Native/Flutter/Unreal/Godot), and the **Magicblock Solana.Unity-SDK** is a Unity Asset Store Verified Solution. So **our existing `WalletService`/`SolanaWalletProvider` seam targets MWA**, and Seed Vault is reached *through the user's wallet*, not by us.

## B2. Why THIS project is a stronger Seeker candidate (what to emphasize)

The pitch is uncommonly strong because of one alignment most applicants don't have: **this game already uses SKR — the Seeker's own token — as its premium currency, and now rewards staking it.** Emphasize, in order:

1. **"A real, shipping game that runs on the Seeker's own token."** Not a token bolted onto a demo — a playable single-Knight RPG whose **premium economy IS SKR**, whose **store spends SKR**, and whose **loyalty layer rewards staking SKR** (Part A). That is precisely the "drive SKR utility + staking" behavior Solana Mobile is subsidizing.
2. **Staking SKR = in-game prestige.** The Keeper crown/flair turns *protocol staking* into *visible game identity*. This is exactly the flywheel Solana Mobile wants: more reasons to hold & stake SKR. Lead the pitch with it.
3. **The closed economic loop (already documented).** `monetization-v2-spec.md` §12: the owner's **1M staked SKR** funds capped player rewards → engaged players → some spend back through packs → reinforces the position. The game "**operates on the SKR yield curve**," and the new staking-loyalty layer extends that loop to *every* player who stakes. A grant/listing committee sees genuine, sustained token utility, not a one-off integration.
4. **Ethical by construction.** Cosmetic/convenience-only covenant, capped non-inflationary pool, transparent payouts (§12 public `/treasury/payouts`), no pay-to-win — a *good-citizen* dApp the Guardian curators can approve confidently.
5. **Mobile-ready, two distribution rails.** Single-Knight V1 is deliberately small (the reason for Addressables-remote streaming) → fits both a **native Android APK** for the dApp Store **and** the parallel **Pi/WebGL** web rail (`WORK_ORDER_pi_browser_integration.md`). One small build, two ecosystems.

## B3. What boxes a dApp-Store listing wants ticked (our checklist)

| Box | Status for us | Action |
|---|---|---|
| Real, working app | ✅ V1 loop holds (polish phase) | Keep stable; cut a signed release build |
| Solana utility on-device | ✅ SKR currency + staking-loyalty + (staged) MWA wallet | Land Stage 3 wallet-read (MWA) before listing for full credit |
| **Mobile Wallet Adapter** wallet connect | ⏳ seam exists (`WalletService`), MWA target pending | Implement MWA via Unity SDK (follow-up WO) |
| New dedicated **signing key** (not Play key) | ☐ | Generate dApp-Store-only keystore |
| KYC/KYB publisher + **publisher wallet** | ☐ | Owner completes portal KYC; **separate from treasuries** (§12.15 — five-wallet discipline) |
| Assets: 512² icon, 1200×600 banner, ≥4 screens | ◑ have art | Produce to spec |
| ~0.2 SOL for mint/ArDrive | ☐ | Fund publisher wallet |
| Publisher-Policy compliance | ✅ ethical, no P2W, no gambling-by-default | Confirm contests (§12 streams) legal posture before enabling those |
| 512² etc. + listing copy | ◑ | Narrative-bible voice listing page |

## B4. Realistic phased path to a listing (owner-gated)

- **Phase 0 — Wallet-read spike (MWA).** Wire **Mobile Wallet Adapter** through the existing `SolanaWalletProvider` seam on an Android build; connect a wallet, read the player's **SKR balance / staked amount**, unlock the Keeper cosmetic from on-chain proof (Part A Stage 3, read-only). **Gate:** does MWA connect + read on a real device? Low risk, no payments.
- **Phase 1 — Publisher onboarding (parallel, non-code).** Owner: Publisher Portal account + **KYC/KYB**, **new signing keystore**, fund **~0.2 SOL** publisher wallet (kept separate from the five treasuries). Prepare assets (icon/banner/4 screens) + listing copy.
- **Phase 2 — Signed release + submit.** Cut the signed dApp-Store APK (single-Knight V1, Addressables-remote to keep size small), submit via Portal/CLI, pass the 2–5 day review. List with SKR utility + staking-loyalty front-and-center.
- **Phase 3 — Apply for Season/builder incentives.** With a live listing demonstrating real SKR utility + staking, apply for the **dApp Store Season grants / builder grants / hackathons** (Season 1 precedent: ~750k SKR/qualifying team). The staking-loyalty layer is a direct "we drive SKR staking" qualifier.
- **Phase 4 — Payments rail (optional, after legal).** Add on-chain SKR spend (the pack verifier pattern) and, post legal sign-off, the capped SKR rebate (Part A option b). Mirror the parallel Pi rail (PWA→APK is also dApp-Store-eligible — one build can serve both).

**Parallelism:** Phases 0–1 are isolated (wallet seam + owner onboarding) → Monetization/Backend/Distribution lane, no gameplay or Unity-gate contention (§9).

---

## C. Deliverables shipped with this WO
- **This design + research doc.**
- **`Assets/StreamingAssets/Data/Canonical/skr_staking.json`** — the §A2 tables (config, stake tiers, perks, reward-pool accounting) + a worked sample ledger and a worked pool-invariant example. Mirror to `Assets/Resources/Data/Canonical/skr_staking.json` at implementation (the `CanonicalJson.Read` WebGL pattern).

## D. Open questions for the owner (route before IMPLEMENT)
1. **Same-mint vs separate token** (§1 ⚠) — is in-game SKR the real Solana Mobile SKR mint? (Recommend YES — best pitch, matches §12.)
2. **Enable option (b) SKR rebate at all,** or ship cosmetics + store-discount only? (Recommend: ship (a)+(c) first; (b) only post-legal.)
3. **Tier numbers** — durations (30/90/180) and minimums (50/200/500 SKR) are placeholders; owner tunes.
4. **Reward-pool source** — confirm rebates draw from the §12 *yield* wallet only (never principal), and the per-user cap.
5. **Name** — "Keeper rewards" / "Keeper's Vow" working title (narrative-bible voice), to match the cozy framing and avoid finance language.

## E. Sources
**Solana Mobile / Seeker / SKR:**
- Seeker shipping / preorders / Season 1 dev rewards — [coinlaw](https://coinlaw.io/solana-seeker-phone-shipping/), [The Block](https://www.theblock.co/post/365600/solana-mobile-seeker-crypto-smartphone), [solanamobile.com/seeker](https://solanamobile.com/seeker)
- SKR token (supply, staking, Guardians, dApp-Store curation, inflation schedule) — [SKR launches Jan 2026](https://blog.solanamobile.com/post/skr-launches-january-2026), [bitget academy](https://www.bitget.com/academy/what-is-solana-seeker-skr-and-how-does-it-work), [phemex academy](https://phemex.com/academy/what-is-seeker-skr), [Stake SKR](https://stake.solanamobile.com/)
- 1.8B SKR airdrop / 141M to 188 devs — [crypto.news](https://crypto.news/solana-mobile-confirms-1-8-billion-skr-token-airdrop-for-seeker-phone-users/)
- dApp Store publishing (portal, KYC, signing key, assets, 0.2 SOL, 2–5 day review, web+Android) — [Solana Mobile Docs: overview](https://docs.solanamobile.com/dapp-publishing/overview), [prepare](https://docs.solanamobile.com/dapp-publishing/prepare), [submit-new-app](https://docs.solanamobile.com/dapp-publishing/submit-new-app), [CLI](https://docs.solanamobile.com/dapp-store/publishing-cli), [Blueshift course](https://learn.blueshift.gg/en/courses/dapp-store-publishing/solana-dapp-store), [helius](https://www.helius.dev/blog/publishing-solana-mobile-apps)
- Seed Vault (wallet-app SDK; games use MWA) — [Seed Vault docs](https://docs.solanamobile.com/developers/seed-vault), [seed-vault-sdk](https://github.com/solana-mobile/seed-vault-sdk)
- MWA + Unity — [Unity SDK docs](https://docs.solanamobile.com/unity/unity_sdk), [Solana.Unity-SDK](https://github.com/magicblock-labs/Solana.Unity-SDK), [Verified Solution](https://solana.com/news/solana-sdk-for-unity-by-magicblock)

**Internal canon referenced (do NOT duplicate):**
- `WorkOrders/WORK_ORDER_skr_store_design.md` — held-SKR premium store, `ISkrLedger`, PackStore on-ramp, covenant + validator firewall, `Grant` shape (reused by `PerkGrant`).
- `WorkOrders/WORK_ORDER_pi_browser_integration.md` — parallel web rail; PWA→APK convergence with the dApp Store.
- `docs/monetization-v2-spec.md` §12 — owner's 1M-SKR-staked yield-funded rewards, capped pool, treasury watch-window, public payouts, legal gate (the budget + accounting this staking reward draws on).
- `docs/DATA_ARCHITECTURE_DECISION_2026-06-27.md` — staged local→cloud→Solana; Addressables-remote (the size lever for a dApp-Store/WebGL build).
- Memory `data-architecture-hybrid-db-direction`, `combat-pivot-single-hero-northstar`, `owner-thinks-in-data-structures`, `wardrobe-dressable-capability`.
</content>
</invoke>

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner states the SKR staking reward + Seeker ecosystem pitch has already been done. Closed on her word. Related shipped state: StakeRewardsResolver / StakeRewardsPanel exist read-only.
