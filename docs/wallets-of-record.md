# Wallets of Record — DeNelle Studios

**Purpose:** Canonical, single-source-of-truth record of every wallet associated with Defenders of the Realm. Other docs (whitepaper, dApp Store submission, grant application, monetization spec, anti-cheat spec) reference this file rather than duplicating addresses.

**Last updated:** 2026-05-18 by owner — Seeker hardware-backed publisher wallet provisioned; Seeker hardware-backed Rewards Distributor wallet provisioned; prior Solflare desktop wallet demoted to dev/staging. Owner picked **Option A** for stake disclosure — 1M SKR stake stays in owner's private source wallet (off-record); yield periodically flows to the Rewards Distributor (§2) for player payouts.

**Status:** Publisher wallet locked (hardware-backed, §1). Rewards Distributor locked (hardware-backed, §2). 1M SKR stake source stays private (Option A). Revenue treasury wallets pending Squads multisig setup (§4).

---

## 1. Publisher / Studio Wallet — hardware-backed

The owner's primary identity wallet on Solana. Generated **fresh inside the Seeker phone's Seed Vault** on 2026-05-18 — the seed phrase never touched a software environment. Used for **studio identity and grant receipt**. The 1M SKR stake lives in a separate private wallet (see §1.1). **NOT a revenue treasury** — pack purchases never land here.

| Field             | Value                                                  |
| ----------------- | ------------------------------------------------------ |
| Address           | `C5ummRoS1bB73gnBC57VqpGfD9QjM9g1iv3vc7cDbgYQ`         |
| Wallet provider   | Solana Seeker — Seed Vault (hardware secure element)   |
| Holder            | Samantha Denelle / DeNelle Studios                     |
| Purpose           | Studio identity + grant receipt                        |
| Public visibility | Public on-chain (Solscan lookup-safe)                  |
| Security tier     | **Hardware-backed at birth** — strongest non-multisig profile |

### What this wallet is used for

- **dApp Store publisher identity** — the wallet that signs as the publisher of record in the Solana Mobile dApp Store submission. See `docs/solana-dapp-store-submission.md` §4.1.
- **Solana Foundation grant receipt** — the wallet the Foundation disburses to if the Builder-track grant is approved. See `docs/launch-materials/solana-foundation-application.md` §7 checklist line 173.
- **White paper attribution** — listed in `docs/whitepaper.md` header as the publisher/lead-developer wallet of record.
- **Future treasury multisig — hardware signer** — when the SOL/USDC/SKR treasury wallets are provisioned as Squads 2-of-2 multisigs, this Seeker wallet serves as signer B (the hardware-backed key). This eliminates the need for a separate Ledger purchase.

### 1.1 1M SKR stake — Option A path, owner-locked 2026-05-18

Owner picked **Option A: stake stays private.** The 1M SKR stake remains in the owner's private source wallet (off-record, not disclosed in this file or any public doc). The wallet `2JRmE…` is repurposed as the **Rewards Distributor** (§2) — it does NOT hold the stake; it receives periodic yield transfers from the private source wallet and pays out small SKR rewards to players.

**Why Option A is fine for the grant pitch:**
- Foundation grant credibility relies on owner attestation + verifiable yield flow (rewards distributor pays player wallets at a cadence consistent with 1M SKR earning ~5% APY yield).
- If a Foundation reviewer specifically requests the stake address for verification, the owner can provide it on a need-to-know basis without it becoming part of the public documentation.
- Personal privacy preserved — the owner's source wallet doesn't get linked publicly to studio operations.
- Cleanest separation of concerns: principal is personal; yield is studio.

**Decision recorded:**

- [x] **Stake disclosure decided** _(2026-05-18 — owner: SD; choice: A — keep private)_
- [x] **Rewards Distributor provisioned** _(2026-05-18 — `2JRmE…` on Seeker Seed Vault — see §2)_
- [ ] **Yield-flow protocol documented** — owner-side procedure for periodic SKR-yield transfers from private stake source → Rewards Distributor → player wallets. Coordinate with `docs/monetization-v2-spec.md` §13 once Streams A/B/C launch.

### What this wallet is NOT used for

- **NOT a revenue treasury.** Pack purchases (SOL / USDC / SKR) NEVER land here. See §4 below for the treasury wallets.
- **NOT a player wallet.** The publisher wallet is excluded from any flagged-player / anti-bot heuristic — it is owner-attested and on the founder allow-list. See `docs/anti-cheat-spec.md` §5 (developer wallet exclusion). This exclusion applies to `C5umm…`, `3Eeww…`, AND the Studio Stake Wallet `2JRmE…`.
- **NOT a contest payout source.** Player rewards are paid from the rewards distributor service (§5), which draws on yield from the Studio Stake Wallet (§2), not principal.
- **NOT the SKR stake holder.** The 1M SKR stake stays in the owner's private source wallet (Option A, see §1.1).

## 2. Rewards Distributor Wallet — hardware-backed

The dedicated wallet that pays out SKR rewards to player wallets — Stream A achievement drops, Stream B leaderboard payouts, Stream C tournament prizes. Receives periodic SKR yield transfers from the owner's private stake source wallet; **drains as paid out** — never holds large balances. Generated fresh inside the Seeker phone's Seed Vault on 2026-05-18; separately-keyed from the publisher wallet `C5umm…`.

| Field             | Value                                                       |
| ----------------- | ----------------------------------------------------------- |
| Address           | `2JRmEmrqUbhTiHX3u5bes5kHYZeZkJ2V1cMWubxwnmNi`              |
| Wallet provider   | Solana Seeker — Seed Vault (hardware secure element)        |
| Holder            | Samantha Denelle / DeNelle Studios                          |
| Purpose           | SKR rewards distribution to players (Streams A/B/C)         |
| Public visibility | Public on-chain (Solscan lookup-safe)                       |
| Security tier     | **Hardware-backed at birth** — strongest non-multisig profile |
| Typical balance   | Low — funded periodically with yield, drained as players are paid |

### What this wallet is used for

- **Stream A — Achievement drops.** First-time milestone completions trigger small SKR payouts (10–100 SKR per drop). This wallet signs and sends those transactions.
- **Stream B — Leaderboard payouts.** Weekly skill-based leaderboard winners receive SKR. This wallet pays them.
- **Stream C — Tournament prizes.** Seasonal tournament winners. Larger payouts. Same wallet.
- **Verifiable yield flow for the Foundation pitch.** Solscan can show the outflow cadence from this wallet to player wallets — evidence that the player-rewards economy is real, even without disclosing the principal stake's source.

### What this wallet is NOT used for

- **NOT a publisher wallet.** Grant disbursement and dApp Store identity belong to `C5umm…` (§1), not this one.
- **NOT a treasury wallet.** Pack revenue does NOT land here.
- **NOT the stake principal.** The 1M SKR principal stays in the owner's private source wallet (Option A — see §1.1).
- **NOT funded by pack revenue.** Stream A/B/C payouts come from stake yield, NEVER from purchase revenue. This is a structural rule per `docs/monetization-v2-spec.md` §12 — yield only, principal preserved, revenue strictly separated.

### Security notes

- **Recovery phrase stored separately** from the publisher wallet's recovery phrase. Two different physical locations. Same paper-storage discipline applies.
- **Excluded from anti-cheat player-bot heuristic** (`docs/anti-cheat-spec.md` §5) — owner-attested founder wallet allow-list.
- **Outbound transactions are programmatic** (via `partyserver/services/rewards-distributor.ts` once built) — payouts to players run through a server signed with this wallet's key. Owner manually signs only when funding the wallet (periodic yield deposits from the private source) or when adjusting the schedule.
- **Pre-launch gate from hard rule §6:** no outbound real-mainnet SKR payouts from this wallet until the cyber audit AND external pentest both close green. Currently the rewards-distributor service has its boolean `enabled` flag set to `false`. Flip is manual.

## 3. Dev / Staging Wallet — `3Eeww2hyBUhiLi7AS2xsjZbfZQ2fmPFq8yh53vNzgaHe`

The original Solflare desktop wallet, demoted to dev/staging on 2026-05-18 when the hardware-backed Seeker wallet (§1) was provisioned. Holds **only a small SOL gas balance** for test transactions; no SKR stake, no production assets.

| Field             | Value                                                  |
| ----------------- | ------------------------------------------------------ |
| Address           | `3Eeww2hyBUhiLi7AS2xsjZbfZQ2fmPFq8yh53vNzgaHe`         |
| Wallet provider   | Solflare (desktop software wallet)                     |
| Holder            | Samantha Denelle / DeNelle Studios                     |
| Purpose           | Dev / staging — testing, signing in development        |
| Security tier     | Software wallet — **do not place high-value assets**   |
| Current balance   | Gas-only (small SOL for transaction fees)              |

### What this wallet is used for

- **Local development** — signing test transactions against a staging environment.
- **Pre-mainnet smoke tests** — verifying pack-purchase flow end-to-end on devnet before pointing at mainnet treasuries.

### What this wallet is NOT used for

- **NOT the publisher of record.** Anywhere a doc cites the publisher wallet, the answer is `C5umm…` (§1), not this one.
- **NOT a treasury wallet.** Pack revenue does not land here.
- **NOT a Foundation grant receipt destination** — see grant application checklist.
- **NOT the 1M SKR stake holder.** The stake will live at the Studio Stake Wallet `2JRmE…` (§2) once the transfer completes.

## 4. Revenue Treasury Wallets — pending Squads multisig setup

Three separate Solana wallets, one each for SOL / USDC / SKR pack-purchase revenue. **None are configured yet.** Per `docs/monetization-v2-spec.md` §8.2 + §16 checklist:

- Each treasury wallet must be a **Squads 2-of-2 multisig** with signer A (owner's day-to-day wallet, day-to-day operational) and signer B (hardware-backed key — **the Seeker wallet §1**).
- Public vault addresses go into `src/services/treasury.ts` constants once provisioned.
- Treasury wallets are **strictly separated** from the publisher wallet for accounting and security.

| Currency | Address                 | Status                            |
| -------- | ----------------------- | --------------------------------- |
| SOL      | _(not yet provisioned)_ | ⚠️ blocked on Squads setup        |
| USDC     | _(not yet provisioned)_ | ⚠️ blocked on Squads setup        |
| SKR      | _(not yet provisioned)_ | ⚠️ blocked on Squads setup        |

When provisioned, fill in addresses here AND mirror to `src/services/treasury.ts`.

## 5. Rewards Distributor Wallet — pending

A separate wallet that holds **only the SKR yield** distributed for player rewards (Streams A achievement drops, B leaderboard, C tournament). Receives small periodic transfers from the Studio Stake Wallet's (§2) staking yield; pays out to player wallets per anti-cheat-cleared distribution rules. **Drained-as-paid out**; never holds large balances.

| Field   | Value                                |
| ------- | ------------------------------------ |
| Address | _(not yet provisioned)_              |
| Status  | ⚠️ blocked on rewards architecture finalization |

When provisioned, fill in here AND mirror to `src/services/rewards-distributor.ts`.

## 6. Operational rules

- **Never commit private keys, seed phrases, or wallet passwords to this file, the repo, chat history, or any logged surface.** Public addresses only.
- **Seed-phrase storage:** the Seeker's Seed Vault holds the live keys for both §1 (publisher) AND §2 (Studio Stake Wallet) on the same physical device, but as **separately-keyed Seed Vault entries with separate recovery phrases**. Each recovery phrase is on paper, stored in a physical location distinct from the other. Never photographed, never typed into a computer, never spoken into a microphone.
- **OFAC sanctions screening** applies to every wallet that interacts with the game — `docs/cyber-audit-end-to-end-spec.md` line 336. The publisher (§1), Studio Stake Wallet (§2), and dev/staging (§3) wallets must remain off the SDN list at all times.
- **Wallet rotation procedure** — if any wallet is suspected compromised: pause services, move funds to a fresh wallet, update this file, update `src/services/treasury.ts` constants, post a clan-channel notice. Documented procedure pending.
- **Audit logs** — all writes to this file are part of git history; ledger-of-record. Material changes (new wallet, retired wallet, stake migration) get one-paragraph context in the commit message.
- **Yield-funding protocol** — periodic SKR transfers from the owner's private stake source → Rewards Distributor (§2). Verify the recipient address on the Seeker's screen each time. Frequency: monthly or as needed to keep §2 funded for upcoming Stream A/B/C payouts. Cadence + sizing tuned per `docs/monetization-v2-spec.md` §13.

## 7. Cross-references

- `docs/whitepaper.md` — publisher attribution
- `docs/solana-dapp-store-submission.md` — publisher identity
- `docs/launch-materials/solana-foundation-application.md` — grant receipt wallet
- `docs/monetization-v2-spec.md` §8.2 + §13 + §16 — treasury setup rules
- `docs/anti-cheat-spec.md` §5 + §6 — developer wallet exclusion (both `C5umm…` and `3Eeww…`)
- `docs/cyber-audit-end-to-end-spec.md` §11 — sanctions screening + wallet auditing

---

_Tend the Heart. Hold the keys._
