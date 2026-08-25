# WORK ORDER 1073 — The Patronage ladder: cumulative lifetime support, visible status, zero combat stats

**Status:** READY - ARCHITECTURE SLICE LANDED 2026-08-25 (`cb57b1a41`), **TICKET STILL OPEN**. Architecture approved by the owner 2026-08-24; thresholds **TENTATIVE** at $50 / $150 / $500, and no tier above $500 may be designed until real $500 patrons exist. **What landed is a NAMED SLICE ONLY:** `api/_lib/patronage.js` (server-side lifetime-USD aggregate + data-driven three-tier table, thresholds as INTEGER CENTS 5000 / 15000 / 50000) and its oracle `test/patronage.test.js`, which pins the $500 ceiling, monotonic resolution at every exact boundary, and that the module exports status only and cannot flip an entitlement. The cosmetics-only invariant is structurally true, not merely asserted: the module contains zero references to crystals, coins, currency, resources or timers (verified by grep at HEAD). Evidence: 56/56 backend tests pass with `DATABASE_URL` explicitly unset. **STILL OUTSTANDING - do not read this ticket as takeable-in-full or as closed:** the entitlement FLIP and its migration, the endpoints, every client surface, and cosmetic rendering (the ticket's own rule - a tier whose cosmetic cannot render is not authored yet).
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

---

## ⭐ OWNER RULING 2026-08-24

**Build the ARCHITECTURE now.** The server-side lifetime-USD aggregate, the data-driven threshold
table, the granted-not-purchased entitlement flip, and the cosmetic-only oracle are all approved and
implementable today. This ticket moves **SPEC → READY (architecture)**.

### Thresholds — ⚠ **TENTATIVE**, three tiers only

| Tier | Threshold (tentative) | Unlocks |
|---|---|---|
| **Patron** | **$50** | permanent Patron crest · profile border · banner component |
| **High Patron** | **$150** | exclusive kingdom decoration · animated heraldry · premium Heart aura |
| **Founder / Benefactor** | **$500** | permanent monument · player/house inscription · unique animated kingdom marker |

These **supersede** §2's illustrative six-rung list ($25/$50/$100/$250/$500/$1,000) as the shape to
build against. They are authored as **DATA** (§2 already requires it), so re-tuning a threshold is a
data edit, not a rebuild — which is precisely why "tentative" is safe to ship the architecture on.

### ⛔ NO WHALE LADDER ABOVE $500 — owner, verbatim:

> *"Do not design a $2,500 whale ladder before you know whether you have $500 whales."*

Higher tiers are authored **only after real $500 patrons exist** in `purchase_entitlements`. This is
an evidence gate, not a preference. ⚠ Do not pre-author placeholder rows above $500 — an unrendered
threshold is the dead unlock §3.2 forbids.

### The $500 monument — this is where "Named on the Heart" LANDS

§3.3 predicted that WO-1070 §4.2 and this tier are the same surface, and the owner ruled it that way:
the `packs.json` "Founders are named on the Heart" copy is **removed now** from the Vow, and the
capability re-appears here as the **$500 Patron Monument** with player/house inscription.

⭐ **OWNER'S SITING CONSTRAINT, BINDING ON THIS TIER:** the monument stands **NEAR the Heart and never
alters the Heart itself.** Verbatim: *"that protects your most important world object from becoming a
NASCAR hood covered in sponsor names."* No inscription on the Heart mesh, no per-patron decal, no name
list on the world tree; a **separate adjacent object**, bounded in scale and density however many
patrons accumulate.

### Still owed before implementation completes

- The unlock list above is the owner's sign-off for **v1**; §5's last acceptance box is satisfied for
  these three tiers and these three tiers only.
- §3.2 still governs: a tier whose cosmetic cannot render yet is **not authored yet**.
