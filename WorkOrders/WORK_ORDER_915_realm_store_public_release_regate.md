> ## RECONCILED 2026-08-08 - true status is NEEDS-OWNER-RULING
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: `FeatureFlags.cs:581` still has `RealmStorePurchase` at `defaultOn: true` - the flag is STILL ON and has not been re-gated. Nothing in this WO has been implemented; it is waiting on the owner, not on a CLI seat.
> The previous Status line read "READY FOR OWNER RULING on sec.2; READY TO IMPLEMENT after rulings" and was wrong - the "READY TO IMPLEMENT" half reads as pickup-able work when the ruling is still outstanding.

# WORK ORDER 915 — Realm Store: public-release re-gate + complete the payment path

**Status:** BLOCKED — needs an owner ruling on §2; nothing implemented (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-07 (CLI / Grok — residual of audit finding #1 / WO-911 Q9)  
**Silo:** Monetization / Wallet / FeatureFlags (isolated lane)  
**Roles:** Owner rules open questions; CLI implements after rulings  
**Depends on:** `f329c8d5` flipped `FeatureFlags.RealmStorePurchase` default **ON** for sole-tester / devnet; flag comment already records the ship blocker  
**Related:** WO-911 (timer speed-ups / crystal faucet), WO-912 (ad revenue free path — separate), PackStore / SolanaWalletProvider

---

## 0. One-line truth

Buy is **ON for the only tester** so broke-case Finish-Now no longer dead-ends on “Coming soon.” A **public store release must not ship** that state: mainnet payments are hard-blocked and the SKR devnet mint is empty, so a real purchase still cannot complete for players. This WO is the **ship hygiene** to re-gate or finish payment before public.

---

## 1. Proven gaps (verified at source — do not re-guess)

| Gap | Where | Effect |
|-----|--------|--------|
| `RealmStorePurchase` default **true** | `FeatureFlags.cs` (~L581) | Buy CTA offered on release builds too |
| `SendPayment` hard-blocks Mainnet | `SolanaWalletProvider` | Even with flag ON, mainnet cannot take money |
| `WalletEndpoints.SkrMintDevnet == ""` | wallet endpoints | Default currency SKR cannot resolve a mint on devnet |
| USDC/SOL may partially work | pack currency rails | Only path with a chance today; not the default SKR story |
| “Coming soon” branch still exists | `PackStore` when gate OFF | Correct zero-crypto honesty when re-gated |

Owner ruling 2026-08-07 (Q9): sole tester + devnet → **ON is correct for now.**  
Ship comment on the flag: **re-gate or complete payment before public store release.**

---

## 2. Open owner rulings (block implementation of the wrong path)

Answer before CLI codes a permanent default:

1. **Public APK zero-crypto honesty** — On first public build, should Buy be:
   - **(A)** OFF by default (`defaultOn: false` / IsDevBuild-only) + cosmetic cards + “Coming soon” / “Get crystals later”, **or**  
   - **(B)** ON only when a real payment path is verified (SKR mint + mainnet unblocked under a deliberate release flag)?  
   **Recommended: (A) until (B) is proven green.**

2. **Crystal faucet without crypto** — Broke-case Finish-Now must still route somewhere useful when Buy is OFF:
   - Rewarded ad (blocked until WO-912 + real SDK — see `FeatureFlags` ad gate), and/or  
   - Dev/editor crystal grant, and/or  
   - Honest toast: “Crystals unavailable until Store opens” (never a silent no-op).

3. **SKR mint + network** — When do we fill `SkrMintDevnet` and allow mainnet? Separate backend WO acceptable; this WO must **list the checklist** and refuse to claim “store ready” without it.

---

## 3. Scope (after rulings)

### Phase A — Checklist (docs + flag comment stay true)

Add or update a short ship checklist (pick one home: `docs/SHIP_STORE_CHECKLIST.md` **or** a section in existing monetization canon — do not invent a third source of truth if one already exists):

- [ ] `RealmStorePurchase` default for RELEASE  
- [ ] `SkrMintDevnet` non-empty and verified transfer  
- [ ] Mainnet policy (keep hard-block vs deliberate enable)  
- [ ] PackStore dual-copy + price parity with any `skr_store.json` (known 2.9× bug — do not activate dead data)  
- [ ] Broke-case Finish-Now path when Buy OFF  
- [ ] Headless / Editor purchase stub still works for owner

### Phase B — Re-gate default for public (if ruling A)

```csharp
// Intent sketch only — match house FeatureFlags style
public static bool RealmStorePurchase => Get(
    "realmstorepurchase",
    defaultOn: IsDevBuild); // or false + PlayerPrefs override for tester
```

- Preserve PlayerPrefs override so the sole tester can force ON without a rebuild.  
- Keep Buy refusal FlowTrace when OFF.  
- Cosmetic pack cards remain visible (existing Path A design).

### Phase C — Payment path (if ruling B)

Only after owner supplies mint + network policy:

1. Wire `SkrMintDevnet` (or mainnet mint) from a single config source.  
2. Prove one full purchase: select pack → wallet → grant crystals → Instant Finish succeeds.  
3. Never claim green from “button visible.”

### Phase D — Explicitly out of scope

- WO-912 real ad SDK  
- Pack content redesign  
- Changing crystal Instant pricing (WO-911 / WO-898)

---

## 4. Files (likely)

| File | Action |
|------|--------|
| `Assets/_Modules/Core/FeatureFlags.cs` | Default / comments per ruling |
| `Assets/_Modules/Wallet/PackStore.cs` | Only if broke-case copy when OFF needs polish |
| `Assets/_Modules/Wallet/SolanaWalletProvider.cs` / endpoints | Phase C only |
| Ship checklist doc | Phase A |

---

## 5. Acceptance

- [ ] Owner rulings recorded in RESULT (A vs B + crystal path when Buy OFF).  
- [ ] Public-release default cannot offer a Buy that cannot complete (re-gate **or** proven payment).  
- [ ] Sole-tester path to exercise Buy still exists (PlayerPrefs / IsDevBuild).  
- [ ] Flag comment still names both payment gaps until closed.  
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` if code changes.  
- [ ] No mainnet money movement without explicit owner go.

---

## 6. RESULT

`WorkOrders/WORK_ORDER_915_realm_store_public_release_regate.RESULT.md`
