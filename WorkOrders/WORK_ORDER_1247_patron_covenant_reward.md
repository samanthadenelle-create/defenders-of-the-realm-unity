# WORK ORDER 1247 - The patron covenant: thank the people who paid, on a premise that is actually true

**Status:** SPEC - needs one owner ruling before it can be built
**Silo:** Backend (`api/`) + a client surface
**Severity:** P3. Nothing is broken. This is a retention/goodwill feature.
**Origin:** Owner ruling 2026-08-27 - re-minted out of WO-1175, which was CLOSED AS MOOT. The
covenant *reasoning* in that ticket was worth keeping; its premise was not.

---

## What died, and what survived

WO-1175 asked us to reward a player for **CHOOSING SKR over another currency**. That cannot be built,
and the reason is structural rather than a matter of timing:

- `api/schema.sql` — `purchase_quotes.currency TEXT NOT NULL CHECK (currency IN ('SKR'))`. **A
  constraint, not a default.** One value.
- `api/_lib/purchase-catalog.js` — every real pack is priced in USD and **paid in SKR**.
- WO-1174 (dual-currency USDC) is **PARKED** by the owner.

So there is no choice to reward, and `WHERE currency='SKR'` selects **every** buyer. WO-1175 also
claimed `purchase_entitlements.currency` distinguishes SKR from USDC buyers — it cannot, because no
USDC row can ever exist.

**What survived is the idea underneath:** the people who paid early, while the game was rough, are
doing something different from a player who buys later, and it is worth marking.

## ⛔ THE OWNER RULING THIS NEEDS BEFORE ANY CODE

**Who is thanked, and with what?** The honest options are not equivalent, and the fairness shape
differs:

| Option | Who qualifies | Risk |
|---|---|---|
| **Every purchaser** | anyone with a settled entitlement | thanks a whale and a $1.99 buyer identically |
| **Early purchasers** | settled before a cutoff date | needs a defensible cutoff, and it is arbitrary |
| **Cumulative spend tier** | already built — see below | overlaps WO-1073; may be the whole answer |

⭐ **Check WO-1073 first: the cumulative patronage ladder already SHIPPED** ($50 / $150 / $500, the
Benefactors wall, per-patron monuments, `patronage_benefactors`). It may already be the covenant, in
which case this ticket is a small addition to it rather than a new system - **and that is the
outcome to prefer.** Do not build a second recognition system beside the one that exists.

## ⛔ Constraints that bind whatever is chosen

- **Money is real.** A reward that alters balance can devalue something a player paid for.
- **SKR is Solana Mobile's governance token.** We did not mint it, we hold none, and we cannot pay it
  out. Any design implying a treasury disbursement of SKR is wrong — flag it, do not build it.
- ⛔ **Never render or log a wallet address, an email, or a real name.** The chosen patron name and a
  player id are the entire public surface (this is already how WO-1073 works — follow it).
- The reward must not be a resource grant that inflates the economy. Prefer **permanence and
  prestige**, which is the owner's standing position on whale-tier value.
- ASCII-only strings; no meaning by colour alone.

## Acceptance (once the ruling exists)

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs.
2. A regression proving the qualifying rule selects exactly who it should and nobody else — prove RED
   first (WO-1138), including a non-qualifier who must NOT receive it.
3. Owner felt-verifies on device.

## What NOT to touch

- ⛔ The purchase/settlement rail.
- ⛔ WO-1073's shipped ladder semantics — extend it, do not fork it.
- ⛔ Do not revive WO-1175's two false claims (see its status block).
