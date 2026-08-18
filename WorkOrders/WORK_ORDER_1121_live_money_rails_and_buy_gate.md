# WO-1121 — Live money rails: Buy gate, mainnet/SKR checklist, post-pay reliability

**Status:** READY TO IMPLEMENT after owner R5 (WO-1117) + WO-915 rulings  
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
