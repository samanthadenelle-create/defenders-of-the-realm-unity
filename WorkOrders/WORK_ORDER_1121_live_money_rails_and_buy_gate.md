**Status:** DONE 2026-08-22 (owner) - the live money rail is proven end to end. SKR mints are populated for BOTH networks (WalletEndpoints.cs:54/:56) and decimals are network-parameterised (:61 devnet 9, :62 mainnet 6). Idempotent grant confirmed at PurchaseGate.cs:285 TryClaimGrant (claims BEFORE granting, true exactly once); paid-but-not-granted has a loud path at :317. The stub/free-purchase hole (WO-931) was closed 2026-08-10. Mainnet stays blocked two ways: the canary+owner allowlist at SolanaWalletProvider.cs:582 then the blanket block at :591.

> ⚠ CARRIED FORWARD, not part of this closure: the ticket body's "Known gaps" table is ~60% STALE and its footer cites dead line numbers (FeatureFlags.cs:659/:651 - the real ones are :681 and SolanaWalletProvider.cs:591). Also worth amending §2: "SKR mint set for the network you claim to support" should read "mint AND decimals" - as written, that checklist would have passed the build that authorised 1,000 SKR instead of 1.

> **Status reconciliation 2026-08-26:** the stale `READY TO IMPLEMENT` line formerly here was
> removed because it contradicted the owner-closed `DONE` status above. The carried-forward stale
> documentation cleanup remains non-blocking and is not a reopened implementation ticket.
**Minted:** 2026-08-17 (CLI seat) — program WO-1117  
**Lane:** Wallet / PackStore / FeatureFlags  
**Depends on:** WO-1118 honest shelf; WO-915; WO-931 (never ship free grants); **PROD-003** storefront  
**Related:** WO-1037 shortfall offer (display-only until this is green)

---

## 0. One-line truth

**A live store that cannot take money, or takes money and grants nothing, is worse than no store.**  
Impulse SKUs are ready to sell; the rails are not. This WO is the **ship hygiene + payment completion** path so Buy-ON is earned.

---

## 1. Known gaps (do not re-discover)

| Gap | Effect |
|---|---|
| `FeatureFlags.RealmStorePurchase` must be **OFF** on public until checklist | Buy CTA must refuse honestly |
| Mainnet payment hard-block in `SolanaWalletProvider` | Real players cannot pay |
| `SkrMintDevnet` / mainnet mint empty | SKR rail dead |
| StubWallet / free `ApplyPackContents` class of bugs (WO-931) | **Never** ship free grant on Buy |
| Post-pay grant via fragile service resolution | Charged + empty inventory risk |
| `skr_store.json` acquisition packs 2.9× SKR arbitrage | Do not activate |

---

## 2. Ship checklist (all green before Buy default ON)

- [ ] **Honest shelf** (WO-1118) — live SKUs only deliverable grants  
- [ ] **SKR mint** set for the network you claim to support; one successful transfer test  
- [ ] **Mainnet policy** deliberate (unblock under release flag or stay OFF)  
- [ ] **USDC and/or SOL** rail proven if SKR not ready (document which rail is primary)  
- [ ] **Idempotent grant** by paymentId — retry never double-grants  
- [ ] **ApplyPackContents** failure after charge: loud FlowTrace + support path; ideally server entitlement  
- [ ] **Realm Store** permanent door (PROD-003) or Coppin first-option still works  
- [ ] **Shortfall offer** (1037) can open pack detail; Buy still gated by same flag  
- [ ] **No StubWallet** in release player builds  
- [ ] Owner device: pay → grant → save → relaunch entitlement present  

---

## 3. Implementation scope

1. Re-gate `RealmStorePurchase` per owner R5 (recommended: default OFF on release; PlayerPrefs force-ON for tester).  
2. Complete one real payment path end-to-end (pick primary rail in RESULT).  
3. Harden grant path: reduce reflection/AppDomain fragility; Fail loud if grant misses after pay.  
4. Dual-copy packs + price peg documented; refuse loading skr_store acquisition ladder if it would arbitrage.  
5. "Coming soon" / zero-crypto honesty when gate OFF — never silent dead button.  
6. Broke-case Finish-Now when Buy OFF: honest toast and/or ad path (1120), never silent no-op.

---

## 4. Acceptance

1. Release build with flag OFF: store browsable, Buy refused, no free grant.  
2. Tester force-ON + working rail: one paid impulse pack lands resources.  
3. Regression: purchase gate tests; no double-grant on retry.  
4. Checklist section filled in RESULT with dates.  
5. `COMPILE_GATE_OK`.

## 5. Not in scope

- Ads SDK (1120), harvest engine (1119), season pass (1122), cosmetic art.  

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `FeatureFlags.cs:659 RealmStorePurchase off; :651 mainnet block` — buy gate open. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
