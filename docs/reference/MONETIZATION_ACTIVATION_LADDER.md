# MONETIZATION ACTIVATION LADDER — the INTENDED state, and how it turns on

**Status: KNOWN DICTIONARY / DECLARED-INTENTIONAL.** Owner ruling 2026-08-08, restated and sequenced
2026-08-09.

> ## ⛔ READ THIS BEFORE FILING A MONETIZATION FINDING
> **Everything on this page is OFF ON PURPOSE.** The disabled monetization surface is a deliberate
> business decision — taken to clear store review and to get the Terms of Use exactly right *before*
> anything can take money.
>
> **A static sweep cannot tell "never wired" from "deliberately unwired" — both read as zero
> consumers.** That is exactly how the 2026-08-09 reverse audit mis-filed four monetization systems as
> ORPHAN in a document framed around wasted investment. **This page exists so that cannot happen
> again.** If an audit, oracle, or agent flags any item in §2 as dead weight, waste, or a risk to fix —
> **the finding is wrong, and this page is the answer.**
>
> The genuine risks are in §4. They are reasons the OFF state must HOLD — never reasons to turn
> anything on.

---

## 1. THE LADDER — five rungs, and only the last one can take money

There are **TWO independent switches**, not one. Collapsing them is the most common misunderstanding:

| Switch | Moves | Lives in |
|---|---|---|
| **`SOLANA_SDK` compile define** | `StubWalletProvider` (mock, no chain) ⇄ `SolanaWalletProvider` (real chain) | `#if SOLANA_SDK` guards, isolated inside `SolanaWalletProvider.cs` |
| **`WalletNetwork`** | **Devnet ⇄ Mainnet** | taken from `WalletService` — *"Devnet in the v2 foundation"* (`SolanaWalletProvider.cs:24`) |

```
  RUNG 1  TODAY ......... no SDK -> StubWalletProvider (mock). RealmStorePurchase OFF + locked.
          Proves the UI flow end to end. Proves NOTHING about payment.
             |
  RUNG 2  DEFINE SOLANA_SDK -> SolanaWalletProvider on DEVNET.
          ** The first rung where "confirm monetization works" means anything. **
          Real signing, real RPC, real failure modes, free test SOL.
             |
  RUNG 3  DISCLAIMERS / TERMS FINALISED (attorney redlines land on the live page).
             |
  RUNG 4  FLIP WalletNetwork -> MAINNET. Real money reachable, but nothing purchasable
          while the flag is off. Owner ruling: mainnet testing is SAFE provided no
          purchase is made — no purchase, no money risk.
             |
  RUNG 5  TURN RealmStorePurchase ON.  <-- the ONLY step that can take money.
          BLOCKED until WO-931 lands (see §3).
```

**The stub is not "on devnet" — it is BELOW devnet.** Nothing reaches a chain until `SOLANA_SDK` is
defined. `StubWalletProvider.SendPayment` always succeeds, so no amount of stub testing validates a
payment path.

---

## 2. WHAT IS DELIBERATELY OFF (do not "clean up", do not re-wire)

| Surface | Why it reads as dead to a sweep |
|---|---|
| `ad-placements.json`, `ad-creatives.json` | only reader is `AdPlacementCovenantRegression` — the covenant is **guarding a disabled surface, which is its job** |
| `Core/Ads/IAdService.cs` + `NullAdService` + result enums | `RewardedAdManager` does not route through the seam **because nothing should show an ad** |
| `skr_store.json` | second-currency storefront, no runtime loader |
| `PackStore` purchase path | gated by `FeatureFlags.RealmStorePurchase`, re-gated `defaultOn:false` **and locked** in `576ef012`/`576601e3` |
| Crystal top-up | same gate |
| `StubWalletProvider` selected on SDK-less builds | **intended fallback**, see §3 |

**Corroborating history:** commit `576601e3` — *"re-gate real-money purchases OFF, and lock the default
so it cannot silently flip back"* — and the 08-07 ad-placement purge, which removed a placement paying
**150 crystals** for an ad view, because crystals are the SKR on-ramp and that convertibility is
prohibited by AdMob/Unity.

---

## 3. `StubWalletProvider` — intended, well-built, and the one thing that must change before RUNG 5

**It exists for a real reason:** the Solana SDK is not installed in the v2 foundation, so `WalletService`
needs a provider that compiles and runs today.

**It is already hardened** (audit 2026-08-02, both fixes verified in the header):
1. The stub address carries a **`"stub-wallet-"` marker whose `-` is NOT in the base58 alphabet**, so it
   fails `GameStateService.IsCloudIdentityShaped` **by construction, not by policy**.
2. The RNG is **seeded per instance** — previously a constant seed meant every device minted the same
   address, so an Android build missing `SOLANA_SDK` would have put **every tester on one `player_data`
   row**.
3. Cloud-save keying additionally requires `WalletService.IsRealSigningWallet`, which this provider
   **can never satisfy**.

**The residual issue is NOT identity — it is purchase completion.** `GetBalance()` returns generous mock
balances *"so the store and all five packs are exercisable end to end"*, and `SendPayment()` simulates
finality, debits the mock, and returns a **fabricated tx signature on success**; `PackStore` then calls
`ApplyPackContents` and fires `purchase_completed` with it.

### ⚠ THE TRAP BETWEEN RUNG 4 AND RUNG 5
**Even on mainnet, any build without `SOLANA_SDK` still falls back to the stub — which grants packs for
free.** WebGL and desktop are exactly that case.

**Therefore WO-931 (build-guard or runtime refusal at the `WalletService` seam) MUST land BEFORE
RUNG 5, not after.** That is why it is *precondition 3 of 3* in the flag's DO-NOT-TURN-ON block.
⚠ **Do NOT delete `StubWalletProvider`** — editor, EditMode and offline flows depend on it. The fix is a
guard, not a removal. WO-931 deliberately leaves the choice (build-guard / runtime refusal / both)
unpicked as an architecture call.

---

## 4. THE GENUINE RISKS — reasons the OFF state must HOLD

These stand and are **not** contradicted by anything above. Cross-referenced to
`docs/reference/AUDIT_2026-08-09.md`:

| # | Risk |
|---|---|
| **F8** | `StubWalletProvider` compiles into every player (no `#if` guard) and `WalletService` auto-selects it on release desktop / WebGL / Android-without-SDK |
| **F9** | **The flag defense is defeasible per-device** — `FeatureFlags.Get` reads **PlayerPrefs FIRST**, so any device that ever stored `ff.realmstorepurchase=1` keeps a live Buy rail regardless of the shipped default. Tester devices are exactly that population |
| **F10** | `WalletService.PayFlat` is gated by **nothing** — dead only by scene-absence of its caller |
| **F28** | The ad covenant checks string **values** only; a reward authored as `{"economy":{"crystals":700}}` carries `crystals` as a **key** and passes clean. Today's file holds by authoring style, not by construction |

---

## 5. OPERATIONAL NOTES FOR RUNG 2

- The Solana SDK is a **git-URL package** that re-resolves into `Library/PackageCache`.
  **`tools/android/patch-solana-sdk.ps1` must run before ANY APK build** or it will not compile.
- Android stripping is at **Low**; WO-848 is open to restore Medium.
- Prod `/api/auth/nonce` currently has **no CORS and `OPTIONS` 400**, so a browser blocks the WebGL
  wallet rail regardless of client state. See the `api/`-promotion caveat in `AUDIT_2026-08-09.md` §5 —
  **do not promote `api/` to prod before F5/F6/F7 are fixed.**

---

## 6. FOR AUDITORS AND ORACLES

When sweeping for unused systems, the monetization cluster in §2 is **DECLARED-UNUSED**. Record it as
declared; do **not** rank it as wasted investment, and do **not** propose re-wiring it.

The correct question for this cluster is never *"why is this not wired?"* — it is
**"is the OFF state still airtight?"** §4 is the checklist for that.
