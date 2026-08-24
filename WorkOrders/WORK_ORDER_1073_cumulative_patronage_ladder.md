# WORK ORDER 1073 — The Patronage ladder: cumulative lifetime support, visible status, zero combat stats

**Status:** SPEC — thresholds and unlock list need owner sign-off; architecture is implementable.
**Minted:** 2026-08-24 (UI seat), banner header bumped with the 1069–1074 block.
**Provenance:** the external review the owner ADOPTED 2026-08-24 (*"Create a Patronage system based
on cumulative support, with zero combat stats … Whales generally don't need 900,000 stone. They want
something that says: I have supported this world more than almost anybody else."*).

---

## 1. Why this is the missing whale mechanic (RCA of the gap)

After $49.99 there is nowhere to go — WO-1165 §4 showed the top rung actively deterring the
highest-intent buyer, and nothing in the lineup accumulates. Every purchase today is an island.
A **cumulative** ladder means every ordinary purchase (a $4.99 Ledger included) feeds the long-term
track, so a light spender who later becomes a heavy one is never "starting over" — and the whale's
destination is **visible status**, which the covenant permits without limit, rather than power,
which it forbids.

## 2. The system

- **Source of truth: server-side lifetime USD**, summed from `purchase_entitlements` per wallet
  (the table already records every settled purchase — no new bookkeeping, one aggregate query).
  The client renders; it never computes.
- **Thresholds and unlocks are DATA** (one authored table; illustrative from the adopted review —
  owner tunes): $25 profile frame · $50 banner · $100 exclusive settlement cosmetic · $250 animated
  Heart-of-Elarion cosmetic · $500 named patron monument + title · $1,000 rarest visual treatment.
- **Unlocks are cosmetic/status ONLY.** No resources, no crystals, no tempo, no slots — a Patronage
  tier that granted anything spendable would let the ladder compound with itself.
- Milestone unlocks are **granted, never purchased** — reaching the threshold flips the entitlement
  server-side; the client is told, and celebrates.
- **Not tradable, initially wallet-verifiable later** (adopted ruling: *"I would not make them
  freely tradable initially. Otherwise you're suddenly balancing a secondary market"*). No SPL mint,
  no transferability in v1; the wallet-attestation door stays open by keying everything to
  `BoundWallet`.

## 3. ⛔ Constraints

1. **Zero combat, zero tempo — pinned by oracle.** The WO-1165 §1 ruling survives on the ad-skip
   caps; Patronage must not add a third door. A regression asserts the Patronage unlock table
   contains no resource/currency/timer grant — the `battle_monthly.json:3` ZERO-COMBAT-POWER build
   gate pattern, applied here.
2. **Renders on the cosmetic rail or not at all.** Frames/banners/monuments depend on the cosmetic
   render work (WO-1176 §4 companion is the pathfinder; WO-1074 is the program). A threshold whose
   cosmetic cannot render yet is not authored yet — never a dead unlock.
3. The $500 "named patron monument" and WO-1070 §4's "named on the Heart" decision are **the same
   surface** — one owner ruling, one implementation, two tickets consume it.
4. Refund/chargeback semantics: an SPL transfer cannot reverse, so lifetime totals only ever grow —
   state this so nobody builds clawback logic for a rail that cannot claw back.

## 4. Where it surfaces (v1)

Profile screen (frame, title, total-agnostic tier emblem — show the TIER, never the dollar figure
publicly), the kingdom view (monument at $500+), leaderboard card chrome. The four-lane store
(WO-1165 §12) gives it the fourth lane: **👑 Patronage**, where the Vow and future prestige bundles
live alongside the milestone track's progress display.

## 5. Acceptance

- [ ] Lifetime total computed server-side from settled purchases; client displays only
- [ ] Every authored purchase (packs, ledgers, Vow) increments it — asserted across the catalog
- [ ] Unlock table contains zero spendable/tempo grants — oracle-enforced
- [ ] Tier entitlements survive reinstall (wallet-keyed) and are never client-grantable
- [ ] Public surfaces show tier, never dollars
- [ ] Owner sign-off recorded on thresholds + unlock list before implementation
