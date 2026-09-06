# WO-1440: LIVE PROMO BLOCKER - FIRSTWATCH is public on X and nobody can redeem it

**Status:** DONE - shipped to production 2026-09-06 (`dpl_27KRA2P93P76Nx9fcmsbWChT2W5z`,
commit `08ae66de0`). A guest redeem of FIRSTWATCH was captured succeeding against the real
prod endpoint. See `WORK_ORDER_1440_firstwatch_guest_redeem_live_promo_blocker.RESULT.md`.
A cap gap was found and reported mid-ticket (`max_redemptions` was NULL, so the "500" was a
tier boundary, not a cap) and was **closed by someone during the session** - the live row now
reads `max_redemptions = 500`, and this change is what makes that value hold atomically.
RESULT section 6 keeps the record.
**Silo:** `api/promo/redeem.js` + `api/_lib/wallet-auth.js`. **SERVER-SIDE ONLY** - see section 2, this is
the whole constraint.
**Source:** owner, 2026-09-06. The campaign is already posted from `@EchoesOfElarion`.

---

## 1. THE SITUATION

The X post advertises **500 free crystals, code FIRSTWATCH, for the first 500 players**, with the
instructions: navigate to the Solana dApp Store -> download Defenders of the Realm -> enter the code in
the in-game Night Store.

**Every one of those players will fail.** Owner's own device, captured this session:
```
[Flow:Wallet] authed call has no live session; waiting without SignMessage. why=missing
```
and the in-game modal reads *"We could not verify your account, so the code was NOT used."*

**The promo row itself is CORRECT and needs no change.** Verified in the owner's Neon console:
`active=TRUE`, `bound_wallet=NULL` (any wallet), `per_player_limit=1`, `tier1_limit=500`,
`expires_at=2026-10-01`. **Do not "fix" the data. The data is right.**

## 2. ⛔ THE CONSTRAINT THAT DECIDES THE WHOLE DESIGN

**The post sends people to the SOLANA dAPP STORE - the PUBLISHED build.** A client fix does not reach
them without a store submission and review. **Therefore the fix MUST be server-side and must work with
whatever identity headers the ALREADY-SHIPPED build sends.**

**FIRST TASK, before any design: determine what the published build actually sends.** Do not assume it
matches HEAD (memory: `diagnose-the-build-under-test`). If it sends only `X-Guest-Id`, that is what you
have to work with. State what you found, with evidence.

## 3. THE RULING BEING REVERSED, AND WHY - RECORD THIS, DO NOT ERASE IT

`api/promo/redeem.js:30-33` currently reads, in its own header:
> *"NOW: this route calls authenticateGranting() - WALLET RAIL ONLY. A guest is refused with
> AUTH_WALLET_REQUIRED. Redeeming grants value... ⛔ Do not 'restore guest redeem for convenience'."*

That reasoning is **sound and remains true**: `X-Guest-Id` carries no proof, the id is minted by the
client, so every fresh `guest-local-<64 hex>` is a brand-new "player" and one actor can mint unlimited
identities.

**OWNER RULING 2026-09-06, made with that risk stated to her explicitly: guests may redeem.** Her
reasoning, and it is correct: an acquisition promo that refuses everyone it is meant to acquire has zero
value, while the exposure is **bounded by the 500 cap** - the worst case is one actor farming 500
crystals, not an unbounded drain.

**Update the header comment to record the reversal, its date, its reasoning AND the residual risk.** Do
not delete the original paragraph - the next seat must be able to see that this was a considered
trade-off and not an oversight. CLAUDE.md section 15.

## 4. WHAT TO BUILD

Guest redemption, with abuse controls that **do not depend on a client-chosen id**:

1. **The global cap is the real bound.** Verify `tier1_limit` (500) is enforced server-side, atomically,
   under concurrency. **A race here is the actual risk** - 500 simultaneous redeems must not yield 501.
   Prove it, do not assume the existing path is safe.
2. **`per_player_limit=1` per guest id** stays - it stops the accidental double-tap, which is most of the
   real traffic. It does not stop a determined farmer and must not be described as though it does.
3. **Add IP-based rate limiting**, the one signal the client cannot choose. Raises farming cost without
   needing anything from the shipped build. Pick a limit that a shared NAT (a household, a campus, a
   conference) does not trip - a family should not lose the promo because a sibling redeemed. State the
   number you chose and why.
4. **Keep the wallet rail intact and preferred.** A wallet holder must still redeem as a wallet holder;
   the guest path is additive, never a replacement.
5. **Every guest redemption is logged and attributable** - enough to spot a farming pattern after the
   fact and, if needed, claw back.

⛔ **Do NOT weaken any other endpoint.** The wallet-only rule stands everywhere else - purchases, saves,
entitlements. This reversal is scoped to promo redeem, on the owner's ruling, and nothing else.

## 5. THE SECOND DEFECT, PROBABLY INDEPENDENT - DO NOT LET IT HIDE

**A real wallet is ALSO failing.** The owner holds a wallet and still could not redeem:
```
[Flow:Wallet] authed call has no live session; waiting without SignMessage. why=missing
[Sync] Wallet cloud SAVE aborted - shared authentication unavailable (fail-closed). Delta re-queued offline.
```
**Her cloud saves are being refused too** - this is broader than the promo, and it means a wallet
player's progress is local-only. Related open ticket: WO-1420 (silent reauthorize REFUSED in 0.1 s while
`WalletService` reported a 30 s TIMEOUT, F8 device capture seq 4683).

**Investigate and report, but do NOT expand this ticket into it** - the guest path is what unblocks the
live campaign. If the wallet-session defect turns out to be the same root cause, say so. If it is
separate, hand it back and it gets its own WO.

## 6. ACCEPTANCE

- [ ] A guest on the PUBLISHED build can redeem FIRSTWATCH. Proven against the real prod endpoint with a
      captured request/response, **not** by reading the code.
- [ ] The 500 cap holds under concurrent load. Proven by MEASUREMENT - a race that yields 501 is a
      material loss.
- [ ] A wallet holder still redeems via the wallet rail, unchanged.
- [ ] The reversal, its reasoning and the residual risk are recorded in the file header.
- [ ] Section 5 reported with evidence, and handed back rather than absorbed.
- [ ] No other endpoint's identity requirements changed.

## 7. URGENCY, HONESTLY SCOPED
The post currently shows **7 views, 1 like, 1 repost**. The blast radius is still tiny, which is why this
is fixable rather than a reputational problem. **It grows every hour the post is up.**
