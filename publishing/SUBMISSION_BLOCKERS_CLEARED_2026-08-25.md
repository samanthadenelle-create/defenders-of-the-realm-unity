# Submission blockers - BOTH CLEARED 2026-08-25

**Read this beside `SUBMISSION_READY_2026-08-22.md`.** That packet opens with two owner-gated
blockers. Both are resolved. This file records HOW, verified at source and on chain rather than
asserted, so the next submission does not re-litigate them.

---

## BLOCKER 1 - "the build is pinned to Devnet by a compile-time constant" - CLEARED

`Assets/_Modules/Wallet/WalletService.cs:242`

```csharp
public const WalletNetwork DefaultNetwork = WalletNetwork.Mainnet;
```

Flipped 2026-08-23 on the owner's explicit ruling (WO-1159), in the sanctioned four-step order:
mainnet decision + lift the payment block, `DefaultNetwork` off Devnet, a real signed transaction
SETTLES, and only THEN the purchase flag.

⚠ **The comment above that line said the opposite until tonight and was corrected in this session.**
It read *"TEMPORARILY MAINNET ... REVERT TO Devnet THE MOMENT THE CANARY IS DONE"* and described
safety as a canary-only allowlist of one SKU and one wallet. Every clause of that became false when
the owner ruled the full ladder live. **A seat obeying it would have reverted a live, paying game to
devnet while believing it was following canon.** The line now records the ruled permanent state and
the matched pair that governs it.

## BLOCKER 2 - "a compliant production build has the purchase rail switched OFF" - CLEARED

`Assets/_Modules/Core/FeatureFlags.cs:721`

```csharp
public static bool RealmStorePurchase => Get("realmstorepurchase", defaultOn: true);
```

⛔ **THE MATCHED PAIR:** this flag is safe at `true` ONLY while `DefaultNetwork` is Mainnet. On Devnet
the tokens are free test tokens and the purchase chain COMPLETES - real packs granted for worthless
SKR. `MonetizationActivationRegression` pins BOTH; moving either alone turns the suite red.

## The listing may now describe purchases, because a reviewer can reach them

The 08-22 packet warned that if purchases stayed off, the purchase sentence had to come OUT of
`new_in_version`, `long_description` and `testing_instructions` - otherwise the listing declares a
feature the reviewer cannot reach. **That condition no longer applies.** Verified live on
2026-08-25 against production:

```
POST /api/purchases/quote  {"network":"mainnet-beta"}   (no wallet, no signature, no playerId)
-> mode=list, 27 rows, $1.99-$49.99, every row sellable
   mint      SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3   (official SKR)
   recipient 9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj   (the 2-of-3 treasury vault)
   decimals  6   (read from chain - a doc carrying devnet's 9 turns 1 SKR into 1,000)
```

And the loop is proven in BOTH directions, on chain, not in theory:

| when | amount | what |
|---|---|---|
| 2026-08-23 18:14 | +1 SKR | mainnet canary |
| 2026-08-24 17:41 | +391 SKR | real ladder purchase |
| 2026-08-25 02:45 | +391 SKR | real ladder purchase |
| 2026-08-26 01:13 | **-783 SKR** | owner withdrawal under 2-of-3 multisig |

Treasury re-verified the same day: `TREASURY_VERIFY_OK`, multisig `BcHLoNCsnGD6oegywkP19PALKMQYoFeQWTvmPLmp22no`
is **2-of-3, timeLock 0, production-shaped**, and derives vault[0] == the recipient above (linkage
PROVEN, not asserted).

## What is still OWNER-OWNED before you submit

1. **The dApp Store CLI runs with YOUR wallet.** `dapp-store --apk-file ... --whats-new ...` against
   the EXISTING listing - this is an UPDATE, and the app + App NFT already exist. No agent can sign it.
2. **`new_in_version` is drafted** in `config.yaml` and describes only things a reviewer can reach:
   Stone and the Quarry, SKR in the store, multi-side later waves, the Defense Report, and the rebuilt
   quest/Rumor Board/palette surfaces. ⛔ It deliberately does NOT claim gear max-level abilities -
   that machinery ships deliberately SILENT until the ability names are authored (WO-814), so
   advertising it would describe something the reviewer cannot see.
3. **Media/screenshots** in `publishing/media/` are from the previous packet - confirm they still
   represent the build before submitting.
