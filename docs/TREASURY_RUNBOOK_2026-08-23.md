# Treasury runbook — creating the Squads vault for SKR revenue

**Status:** ready to execute. Every on-chain step is signed by the owner; the CLI verifies.
**Verifier:** `node tools/treasury-verify.mjs <vaultPubkey>` — read-only, holds no key.

---

## ⛔ THE ONE THING THIS RUNBOOK REFUSES

A circulating boilerplate (`manage-game-vault.ts`) initialises the agent with
`process.env.SOLANA_PRIVATE_KEY` and exposes `payoutGameRewards(...)`, which proposes transfers
out of the treasury. **That makes the agent a signer on the money.** It is not built here.

This is not caution for its own sake. The failure it prevents is specific: an agent that can sign
can move funds from a mistaken premise, at 3am, with no human in the loop — and the most expensive
error this project has already had (`decimals: 9` against a 6-decimal mint, a **1000x** overcharge)
came from a confidently-worded document, not from malice. Had a signing agent acted on it, the money
would have been gone before any check ran, because `/verify` runs **after** settlement.

**The division that holds:** the owner signs; the agent prepares and verifies. Anything an agent
would sign, the owner signs in the Squads UI instead.

---

## ⚠ ERRORS IN THE CIRCULATING BOILERPLATE — do not paste it

| # | The snippet says | Why it is wrong |
|---|---|---|
| 1 | *"your game's custom SKR Mint Address"* | ⛔ **SKR IS NOT OURS.** It is Solana Mobile's governance token (`SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3`). We never minted it and never hold it. Following this literally leads to minting a *fake* SKR and taking real money into a worthless token. |
| 2 | `RPC_URL \|\| "https://solana.com"` | That is a marketing website, not an RPC endpoint. Every call fails. |
| 3 | `createMultisigProposal(...)` | Used but **never imported** — the file cannot run as written. |
| 4 | `transferFromMultisigTreasury` | Imported and never used. |
| 5 | `threshold: 2` over 2 owners | A **2-of-2**: lose either key and the treasury is **frozen forever**, with no recovery path. Use **2-of-3** so one lost key is survivable. |
| 6 | `transactionIndex: 1` hardcoded | The index must be **read from the multisig account and incremented**. Fixed at 1, the second proposal always fails. |
| 7 | `amount: number` | Token amounts are **base units** and must be integers (`BigInt`), with `decimals` read from the mint. A float here is how 1 SKR becomes 1000. |

---

## THE SEQUENCE

### 1. Owner — create the multisig (Squads UI, her wallet)

At **https://app.squads.so**, connected with her wallet:

- **Threshold 2-of-3.** Third key is the survivable-loss margin, not ceremony.
- **Members:** her primary wallet, a second key she controls on different hardware, and a third
  held separately (a hardware wallet, or a co-signer she trusts).
- **Config authority:** immutable (`null`) unless there is a reason to keep it mutable.
- **Time lock:** consider a non-zero delay. It is the difference between noticing a bad withdrawal
  and reading about it afterwards.

⚠ **Send funds to the VAULT (index 0), never to the multisig account itself.** The Squads UI shows
both, and they are different addresses. Money sent to the multisig account is not in the treasury.

### 2. CLI — verify before anything is authored

```
node tools/treasury-verify.mjs <vaultPubkey>
```

It checks four things and refuses on any of them:

1. **Mint decimals, read from chain** — currently proven `6`. Never taken from a document.
2. **Off-curve** — a Squads vault is program-derived and therefore off the ed25519 curve. A plain
   wallet is on it. *This is the check that catches the mistake we actually made.*
3. **Owner program** — Squads V4, or System-owned-and-off-curve for a SOL-holding vault.
4. **The SKR ATA** — exists, holds the official mint, authority is the vault.

Judge the **marker** (`TREASURY_VERIFY_OK` / `TREASURY_VERIFY_FAIL`), never the exit code.

**Proven against the address the project was carrying, 2026-08-23:**

```
FAIL  vault is ON the ed25519 curve — it is a PLAIN WALLET, not a program-derived Squads vault.
FAIL  SKR ATA ApxAy5uqivjcfxd1E5XDtubY7b4SACfTPAKfuSdVrpAy does not exist yet.
TREASURY_VERIFY_FAIL 2 problem(s) — do NOT author this address anywhere
```

That is the verifier working. It was run against the bad address **first**, on purpose: a check
nobody has seen fail is not yet a check.

### 3. Owner — create the SKR token account deliberately

⚠ **Do not let the first transfer create it.** Create and fund it as its own step, then re-run the
verifier and require `TREASURY_VERIFY_OK`.

### 4. CLI — author the recipient in BOTH places

`wallets.json` (both byte-identical canonical mirrors: `Assets/Resources/Data/Canonical/` and
`Assets/StreamingAssets/Data/Canonical/`) **and** the server environment
(`SOLANA_MAINNET_PURCHASE_RECIPIENT`, `..._ATA`, `SOLANA_MAINNET_RPC_URL`). Client and backend
disagreeing is a paid-but-not-granted purchase.

### 5. The canary — one transaction, rehearsed

1. `MAINNET_CANARY_ENABLED=true` for the window only.
2. **Cancel-first rehearsal:** decline at the wallet prompt, prove nothing moved.
3. Fire **exactly one** 1 SKR purchase.
4. Confirm chain, backend row, wallet, SKU, mint, recipient, amount, wood delta, receipt, relaunch,
   retry and reinstall all join on the **same signature**.
5. `MAINNET_CANARY_ENABLED=false`, keep reconciliation, file the artifact.

---

## What the CLI will build, and what it will not

**Will:** read-only vault status and balance (`treasury-verify.mjs` already reports the SKR balance
once the ATA exists); proposal **preparation** that emits an unsigned transaction for the owner to
review and sign; verification of every result on chain.

**Will not:** hold a key, set `SOLANA_PRIVATE_KEY`, or execute a transfer. If a future task seems to
need it, that is the moment to stop and ask — not to add the variable.
