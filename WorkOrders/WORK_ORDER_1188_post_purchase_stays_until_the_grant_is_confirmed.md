# WORK ORDER 1188 - the purchase screen must stay until the grant is CONFIRMED, then say what arrived

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739.
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1188 -> 1189 in the same edit)
**Silo:** Monetization / store UX
**Origin:** owner, 2026-08-25, verbatim: *"after the purchase is complete I think that we need to
leave it on a processing screen until we keep calling back calling back calling back and it confirms
that the redemption happened and then when that does happen, then it should tell them X was received,
and deposited, and close out gracefully."*

---

## The gap, verified at source

**The polling primitive already exists and nothing polls with it.**
`PurchaseEntitlementVerifier.ReconcileAsync(pack, wallet)` asks the DURABLE authority - the server -
whether an entitlement exists for this wallet and SKU. It is called in exactly ONE place,
`PackStore.cs:2161`, and that call is **BEFORE** a purchase (the reinstall / new-device restore path,
so a returning player is not charged twice). ⛔ Nothing calls it AFTER a purchase.

So today, when a purchase does not resolve on the first `/verify` answer, the player gets a terminal
sentence and is sent away:

- `EntitlementVerificationState.Pending` -> *"Payment submitted; verification is pending. Reopen the
  store to resume - do not pay again."* (`PackStore.cs:2315`)
- `Fulfilled`-but-restore-pending -> *"Payment verified, but delivery is pending. Reopen the store to
  retry."*

⚠ **The copy is honest and the safety is correct** - it never says rejected, and it tells the player
not to pay again. **It is the SHAPE that is wrong.** The player just spent real money and is handed a
chore: come back later and reopen a screen. The system knows how to answer the question and does not
ask it.

⭐ **And this became more valuable the same morning.** `/api/purchases/verify` now answers **HTTP 503
`state: 'record_failed'`** when the transfer settled but a post-settlement DB write failed - an
explicitly RETRYABLE state carrying `stage` and `ref`. A poll loop resolves exactly that case with no
player action at all. Without one, the most expensive failure this system can produce is handed to the
player as homework.

## What to build

1. **A terminal PROCESSING state that does not dismiss itself.** After the transfer is submitted, the
   store stays on a processing surface until the grant is CONFIRMED or the loop gives up. ⛔ No "reopen
   the store" instruction while the loop is still running - that is what this ticket removes.
2. **Poll the durable authority.** Re-ask `ReconcileAsync` (and/or `/verify` for a known signature) on
   a backoff. Suggested shape, tune with data: fast early, slowing out - e.g. 2s, 4s, 8s, 15s, 30s -
   with a stated overall ceiling.
3. **Confirm by what ARRIVED, not by what was requested.** ⛔ This is the load-bearing line. Report the
   **measured** post-grant balances - the amount actually CREDITED - never the pack's advertised
   contents. Canon: `docs/INSTRUMENTATION_STANDARD.md` section 1.4b (assert outcomes, not intent), and
   WO-978, where four economy callers logged the amount REQUESTED as though it were the amount
   CREDITED. A confirmation screen that reads the pack definition instead of the wallet would restate
   that bug on the one screen where the player is checking whether they got what they paid for.
   ⚠ Interacts with the owner's capped-resource ruling (`OWNER_RULINGS_OWED_2.md` ruling 5): a capped
   resource pays what FITS and discloses the shortfall in words. So the confirmation must be able to
   say *"Storage full - 240 of 500 collected"*, not just a success tick.
4. **Close gracefully.** On confirmation: name what was received and where it went, then return the
   player to the store in a clean state - purchase cleared, no stale pending marker.
5. **A bounded, honest give-up.** If the ceiling is reached the screen must NOT read as failure - the
   money moved. It states that the payment is recorded, that nothing will be charged again, that the
   grant will complete, and it surfaces the `ref` when the server supplied one.
   ⛔ Never a bare spinner that ends in silence, and never a "failed" word on a settled payment.

## Acceptance criteria

1. A purchase whose first `/verify` returns `pending` resolves **with no further player action** when
   the server later confirms - proven by a captured trace showing repeated polls and the resolving one.
2. A purchase answered **503 `record_failed`** stays on the processing screen and recovers when the
   record lands; it is never rendered as a rejection.
3. The confirmation names **measured** credited amounts. A test where a capped resource overflows must
   show the shortfall in WORDS.
4. ⛔ No path re-prompts the wallet or re-submits a transfer while polling. One payment, one transfer.
5. ⛔ Nothing in the screen carries meaning by colour alone - the owner is red/green colourblind. Words
   and shape.
6. ASCII-only strings (non-ASCII renders as tofu in TMP on device).
7. The loop must not spin forever in a lost app-focus / backgrounded state - state the behaviour.

## What NOT to touch

- ⛔ `api/_lib/purchase-catalog.js` - it is under the mirror law and is contended.
- ⛔ Do not add client price arithmetic or a client SKU allowlist. The server quote is the sole
  authority on what is sellable and at what amount; the client fails CLOSED.
- ⛔ Do not weaken `PurchaseEntitlementVerifier`'s existing double-charge protections. `Remember` /
  `HasPending` exist so a crash between verification and grant reopens onto the SAME signature rather
  than inviting a second charge.
