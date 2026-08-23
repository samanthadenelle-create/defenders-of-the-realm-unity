# Monetization state — 2026-08-23

**Owner ruling framing:** *"all monetization is completed to this point minus 1 SKR pack live test"*.
This document records exactly what that means, so nobody has to reconstruct it.

> ### ⭐ THE HEADLINE
> **Every monetization deliverable is CODE-COMPLETE and gated. The only thing not done is the
> LIVE MAINNET 1-SKR TEST**, which is blocked on configuration the repo cannot supply itself, not
> on engineering.

---

## What is DONE

| Ticket | State | Proof |
|---|---|---|
| **WO-1147** purchasing / verified entitlement | **DONE — OWNER-PROVEN END TO END** | A live **Devnet** SKR purchase completed 2026-08-22: wallet authenticated, signature backend-verified, entitlement claimed **exactly once**, Hearth Spark delivered, `/fulfill` acknowledged durably *before* the receipt, and the wallet balance unchanged afterwards (no second transfer). |
| **WO-1149** stop the world during a transaction | **DONE** | `WorldHold` is the **single writer** of `Time.timeScale`, acquired as a `using` declaration and the **first statement** of `PackStore.Purchase`, so every exit releases by construction — including the exception path. Survived the live purchase. |
| **WO-1146** rewarded ads | **CODE DONE — ACTIVATION HELD** | Three placement-specific LevelPlay units, consent-before-init, main-thread ILRD forwarding, server-anchored placement accounting, earned-callback-only grants, duplicate/cross-unit callback refusal, permanent refusal of the synchronous bypass. `[monetization-activation]` green. |
| **WO-1121** live money rails / buy gate | **DONE** | Both SKR mints populated; decimals network-parameterised; idempotent grant confirmed (`PurchaseGate.cs:285` claims **before** granting); paid-but-not-granted has a loud path; the stub/free-purchase hole closed 2026-08-10. |
| **MON002** mainnet 1 SKR canary | **CODE DONE — FAIL-CLOSED** | Mainnet contract, official mint, owner allowlist, isolated canary SKU, exact 1 SKR -> 1 wood, network-bound recovery, independent regressions. `[mainnet-canary]` green. |

**Backend:** `/verify`, `/reconcile`, `/fulfill` are live with an exactly-once entitlement seam;
`node --test test/purchases.verify.test.js` passes **13/13**, including *"wrong decimals are rejected"*.

---

## What is NOT done — and it is one thing

### The live Mainnet 1-SKR test has never been run.

It is **fail-closed by design**: no mainnet recipient, ATA or RPC is configured and there is **no
fallback**, so the canary **refuses before wallet approval**. That is correct behaviour, not a bug.

**To run it, these must exist first — do not guess any of them:**

| Required | State |
|---|---|
| `SOLANA_MAINNET_RPC_URL` | not configured |
| `SOLANA_MAINNET_PURCHASE_RECIPIENT` | ⚠ see below |
| `SOLANA_MAINNET_PURCHASE_RECIPIENT_ATA` | does not exist on chain yet |
| `MAINNET_CANARY_ENABLED=true` | off (enable for the window only, then remove) |

⚠ **The recipient supplied so far is a PLAIN WALLET, not a Squads vault.** Verified on-chain:
`2VePaneS3xX2EdzSbe4JdiovRffboLJV4yNVmVTkeuCg` is **on the ed25519 curve** and System-Program-owned;
a multisig vault is program-derived and therefore **off**-curve. Acceptable for a 1-SKR canary because
it proves the *rail*; ⛔ **never acceptable as the production treasury.**

⚠ **Its SKR token account does not exist.** Neither derived ATA is present on mainnet, so the
treasury has never held SKR. Create it as a deliberate, funded step — do not let a transfer create it
incidentally.

Derived, for the classic `TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA` program (**not** Token-2022):
`ApxAy5uqivjcfxd1E5XDtubY7b4SACfTPAKfuSdVrpAy`.

---

## ⛔ THE ERROR THAT ALMOST SHIPPED — read before touching any amount

The MON002 handoff specified **`decimals: 9`** and **`1_000_000_000` base units**, and the server
catalog carried it verbatim.

**Mainnet SKR has SIX decimals.** Read off-chain and confirmed by the owner from the explorer (mint
authority `FMNn5sorEBbEoGQGrh7y3xSbYGt116F12FpL2VTsohiw`). At 6 decimals, `1_000_000_000` base units
is **1,000 SKR** — on a row whose entire purpose is to move exactly 1.

**Where the 9 came from:** our own **Devnet test mint** (`3BwWSAUZ…AB77N`) genuinely IS 9 decimals.
That figure was carried into the Mainnet spec as though it described the real token.
⚠ **The Devnet path legitimately uses 9 and must never be "corrected".**

| | mint | decimals | 1 SKR = |
|---|---|---|---|
| Devnet (ours, test) | `3BwWSAUZ…AB77N` | **9** | `1_000_000_000` |
| Mainnet (Solana Mobile's, real) | `SKRbvo6Gf7…NPGZhW3` | **6** | `1_000_000` |

> ### ⛔ THE VERIFIER CANNOT PROTECT THE FUNDS.
> `/verify` runs **after** the transfer settles. A 9-vs-6 mismatch fails the check with the money
> already gone: **1,000 SKR transferred, no entitlement granted.** Exact-equality guards protect
> correctness, never funds.

**Standing rule:** any figure that decides an on-chain AMOUNT is read from the mint, before the first
transaction — never from a document, and never from a sibling network.

---

## Public activation posture — still OFF, deliberately

- `FeatureFlags.RealmStorePurchase` -> `Get("realmstorepurchase", defaultOn: false)`
- `FeatureFlags.RewardedAdSkip` -> `Get("rewardedadskip", defaultOn: false)`
- `WalletService.DefaultNetwork` is a compile-time `const` pinned to **Devnet**. Flipping it is an
  **owner-gated one-line source edit that an agent never makes**.
- The only ON paths are command-line-only scripting symbols (`STORE_RAIL_LOCAL_TEST`,
  `MONETIZATION_LOCAL_TEST`, `MAINNET_CANARY_TEST`) that **no file defines**.

⛔ **MON002 succeeding does NOT authorise public sales.** It proves one rail with one wallet.

---

## The sequence to go live

1. Provision the Squads multisig treasury; get its **owner pubkey**.
2. Derive its SKR ATA from that owner + the official mint; **verify on chain** that the account
   exists, holds the official mint, and its authority is the intended owner.
3. Author the recipient in both canonical `wallets.json` mirrors **and** the server environment, so
   client and backend agree.
4. Set `MAINNET_CANARY_ENABLED=true` for the window only.
5. **Cancel-first rehearsal** — decline at the wallet prompt and prove nothing moved.
6. Fire **exactly one** 1-SKR transaction.
7. Confirm chain, backend row, wallet, SKU, mint, recipient, amount, wood delta, receipt, relaunch,
   retry and reinstall all join on the **same signature**.
8. Set `MAINNET_CANARY_ENABLED=false`, retain reconciliation, produce a clean artifact.

**Owner's stated intent after that:** turn monetization on and buy a real pack, to watch the flow
from the statistical/reporting side.

⚠ A production build must carry **no owner-test defines** — tonight's APK carries
`STORE_RAIL_LOCAL_TEST;MONETIZATION_LOCAL_TEST` and is a **canary, never a submission build**.
And because bundle names are content-hashed, **every content build needs its own R2 push.**
