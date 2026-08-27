# WORK ORDER 1175 — Community first: a Discord, and SKR as the REWARDED path

**Status:** CLOSED AS MOOT 2026-08-27 (owner ruling). **The premise cannot be true.** This ticket
rewards a player for CHOOSING SKR, and SKR is the only currency anyone can pay in: `purchase_quotes`
carries `CHECK (currency IN ('SKR'))` — a constraint, not a default — and WO-1174 (dual-currency
USDC) is **PARKED**. There is nothing to choose against, so a "rewarded path" cannot discriminate.

⛔ **TWO CLAIMS IN THE BODY BELOW ARE FALSE. Do not carry them forward.**
1. *"`purchase_entitlements.currency` records SKR vs USDC per settled purchase — SKR buyers are
   identifiable."* No USDC row can ever exist, so `WHERE currency='SKR'` selects **every** buyer.
2. *"WO-1165 found 9 of 13 SKUs hidden for exactly one reason — cosmetics do not render."* WO-1165 §8
   gives **several distinct** reasons (duplicate clones, dominated pricing, tokens with no redeemer,
   inert buffs) and names cosmetics only as one SKU's unauthored differentiator.

The surviving ideas were **re-minted rather than salvaged here**, so nothing builds on a false
premise: **WO-1246** (SKU visibility) and **WO-1247** (the patron covenant reward).

Phase 2 (Discord) is partly overtaken by events: a server and **both** directions already exist
(`tools/status-post.mjs`, `tools/discord-inbox.mjs`, both landed 2026-08-26 — *after* this ticket was
minted). What remains of it is an owner action outside the repo.

*(Superseded status: READY — ⚠ SPLIT-FLAGGED, the two phases are not one ticket.* **Phase 2 (Discord)** is an owner/ops action **outside the repo** and is the higher priority (see §0). **Phase 1 (the SKR reward)** is **BLOCKED behind cosmetics rendering**. ⭐ Recommend splitting into two tickets so neither half blocks the other. *(Status audit 2026-08-24: lead-verified bucket correction; body unchanged.)*

**Minted:** 2026-08-24 (CLI), banner bumped 1174 → 1176 in the same edit (with WO-1174).
**Provenance:** owner, 2026-08-24 — *"earned a title in the discord and monthly free code?"*, then
*"i need a discord"* and *"first we need players then a desire to buy, then a purchase."*

---

## 0. ⭐ THE ORDERING RULING THAT RE-RANKS THIS TICKET

> **Owner, 2026-08-24: "first we need players then a desire to buy, then a purchase."**

That sentence parked WO-1174 (dual currency) and promoted this one, and the reasoning is worth
keeping because the pull runs the other way. Today the purchase rail went from never-having-worked
to a **proven mainnet sale** — and the instinct after a win like that is to keep polishing the rail.

⛔ **But the rail now has ONE user.** A second currency widens the *last* step of a funnel whose
*first* step is empty. **Community is upstream work; checkout is downstream.**

So: the Discord half is no longer the deferred nicety it was drafted as this morning. It is the part
that produces the thing everything else depends on.

## 1. What this ticket is, in two halves

**Phase 2 — a place for players to exist.** A Discord server. Upstream, wanted now.
**Phase 1 — a reason to choose SKR.** A wallet-bound cosmetic reward. Downstream, and blocked (§3).

They are independent. Neither waits on the other.

## 2. PHASE 2 — the Discord. Three separate things, not one project.

⚠ **Owner: *"i dont even know how to create a discord and all that stuff."*** So the honest shape,
smallest first:

1. **A server** — minutes, no code, no integration. Create it, make a few channels, done. Roles are
   a settings screen. **This is the whole of what is needed to start.**
2. **Linking a wallet to a Discord account** — Discord OAuth plus a `wallet ↔ discord_user_id`
   mapping. This is the real engineering, and where the privacy questions live.
3. **Automatic role assignment** — a bot with permission to grant roles.

⛔ **DO NOT BUILD 2 OR 3 YET.** A title is a *social* reward: it is worth exactly as many people as
witness it. Building the automation before the room has anyone in it inverts the order this ticket
exists to respect. **Assign roles BY HAND** — at this scale it is minutes a month, needs no code,
and it proves someone wants the title before we build a pipeline to deliver one.

⚠ **What the game does NOT need for any of this:** nothing. No client change, no backend change, no
release. Step 1 is entirely outside the repo, which is why it can happen immediately and in parallel
with everything else.

## 3. PHASE 1 — the SKR reward. Buildable, but genuinely blocked.

⭐ **The attribution already exists.** `purchase_entitlements.currency` records SKR vs USDC per
settled purchase — SKR buyers are identifiable from data we already write. No new tracking.

⭐ **And the promo rail already has the hard part:**

```
bound_wallet      NULL = public; SET = ONLY this wallet may redeem   <- the key field
per_player_limit  cross-code cap
max_redemptions   global cap
expires_at        NULL = never
active            operator kill-switch
```

So the mint is a query plus a loop: read the period's SKR buyers, mint one wallet-bound expiring
code each, surface it through the store's existing **"Redeem a Code"**.

### ⛔ THE COVENANT DECIDES WHAT IT MAY GRANT

`packs.json`, verbatim: **"convenience and BEAUTY, never combat power."**

| Reward | Verdict |
|---|---|
| A title / badge / status | ✅ pure status, zero game effect |
| A cosmetic skin | ✅ the "beauty" half, verbatim |
| Crystals / coins / a pack | ⛔ **NO** |

⚠ **The distinction is RECURRING vs one-off.** A single purchase granting goods is a sale. A
*monthly* grant of goods attached to a currency choice is buying an advantage on an instalment plan
— the exact shape WO-1165 §1 flagged as the live risk once coins began buying troop tempo.

### ⚠ WHAT ACTUALLY BLOCKS IT

**A promo code cannot grant a cosmetic today.** `promo_codes` pays `reward_crystals`,
`reward_coins`, or `reward_pack_sku` — all three of which §2 forbids here. So Phase 1 needs
`reward_cosmetic_id`, honoured by `/api/promo/redeem` and granted through the ONE appearance owner
(`CosmeticApplier`), never a second grant path.

⛔ **And that lands on a known hole: WO-1165 found 9 of 13 non-incidental SKUs hidden for exactly one
reason — cosmetics do not render.** A cosmetic reward nobody can see is not a reward. **This phase is
blocked behind cosmetics actually rendering**, the same dependency as WO-1166's Echo wardrobe, and
the two should be sequenced together rather than discovering it twice.

## 4. Order of work

1. **Create the Discord server** (§2 step 1) — outside the repo, do it whenever, costs nothing.
2. Roles by hand. No bot.
3. ⏸ Phase 1 waits on cosmetics rendering — that is the real gate, not the code path.
4. `reward_cosmetic_id` → redeem → `CosmeticApplier`.
5. The monthly mint over `purchase_entitlements WHERE currency='SKR'`.
6. ⏸ OAuth + bot only if the room fills and hand-assignment starts to hurt.

## 5. Acceptance (Phase 1, when unblocked)

- [ ] A wallet-bound code grants a COSMETIC and redeems exactly once, for that wallet only
- [ ] `reward_crystals`, `reward_coins`, `reward_pack_sku` are ZERO/NULL on every code this mints —
      asserted by a test, so a later edit cannot quietly add goods
- [ ] Redemption visible in ops (`promo_redemptions` already records it)
- [ ] The granted cosmetic is VISIBLE on screen — proven by a screenshot, never a state flag
      (WO-992: equipping once changed a flag and nothing the player could see)

---

## OWNER STATEMENT 2026-08-27 - THE FUNNEL ARGUMENT IS OVERTAKEN

Owner: *"the SKR mainnet tests are live as well as monetization so this is moot."*

Section 0's ordering ruling (*"first we need players then a desire to buy, then a purchase"*) and
section 1's *"the rail now has ONE user"* were both written when SKR was not live. **SKR mainnet is
live now**, so Phase 1's deferral no longer rests on anything.

⚠ Still true and NOT overtaken: the covenant still forbids the rewarded path granting **crystals,
coins, or a pack** - a title is a SOCIAL reward. And WO-1165's hole stands: 9 of 13 non-incidental
SKUs are hidden. A rewarded path pointing at a hidden SKU rewards nobody.
