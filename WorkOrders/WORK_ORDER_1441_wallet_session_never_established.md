# WO-1441: the wallet session is NEVER established - cloud saves are failing and the promo cannot be redeemed

**Status:** FIXED - ON THE SEEKER 2026.09.07.358574 - client landed in `32659c0f6`; device proof of mint/save/drain still owed (see RESULT)
**Silo:** `DeNelle.Core.Web3` (`BackendRequestSigner`) + `DeNelle.Wallet`
(`WalletService`, `NightMarketSharedCardSession`). Disjoint from WO-1440 (server-side `api/`) and from
the raid tickets.
**Source:** owner, 2026-09-06: *"get me the wallet session fixed too"*. Found while diagnosing why the
live FIRSTWATCH promo could not be redeemed.

**⚠ THIS TICKET OWNS THE WHOLE WALLET SESSION, INCLUDING WO-1420.** That ticket recorded the same
subsystem from a different angle (silent reauthorize REFUSED in 0.1 s while `WalletService` reported a
30 s TIMEOUT; F8 device capture seq 4683, build 2026.09.06.357453). **Read it first.** One lane, one
subsystem - do not fix half of this and leave the other half to a second seat.

---

## 1. THE CAPTURED EVIDENCE

`logs/debug/wallet-session-2026-09-06.log` (22 MB, pulled from the owner's device this session, pid 7170
still live). The failure repeats indefinitely:

```
13:26:55.668  [Flow:Wallet] authed call has no live session; waiting without SignMessage.
              why=missing scene=Main_Castle_Overworld caller=<TryAttachSession>d__20.MoveNext /api/g...
13:26:55.668  DeNelle.Core.Web3.<TryAttachSession>d__20:MoveNext()
13:26:55.668  DeNelle.Core.Web3.BackendRequestSigner:TryAttachSession(UnityWebRequest, String, Boolean)
13:26:55.677  [BREAK] error: [Sync] Wallet cloud SAVE aborted - shared authentication unavailable
              (fail-closed). Delta re-queued offline.
```

**`why=missing`. Not expired, not refused, not timed out - NEVER CREATED.** That single token is the
whole starting point: this is not a refresh bug, it is an establishment bug. **Do not begin by
investigating expiry or renewal.**

## 2. THE SECOND CLUE - the browser handoff

```
13:25:07.175  [WalletService] Using SolanaWalletProvider (Solana Unity SDK compiled in).
13:25:07.190  DeNelle.Wallet.NightMarketSharedCardSession:OpenBrowser()
13:25:20.950  DeNelle.Wallet.NightMarketSharedCardSession:OpenBrowser()
```

The owner opened the Night Market; the shared card session handed off to a browser **twice**, 14 seconds
apart. **Nothing in the log shows anything coming back.** A round trip that leaves the app and never
returns is the shape to chase - the return leg (deep link, custom scheme, intent filter, polled
exchange) is where to look first. **Instrument the return leg before theorising about it** (CLAUDE.md
section 12): if a deep link is never received, that absence must be a logged line, not a silence.

## 3. WHY THIS MATTERS BEYOND THE PROMO

- **Cloud saves are refused.** Every delta is `re-queued offline`. The owner has played all day and
  **her progress exists only on that handset.** The fail-closed behaviour is CORRECT - it refuses rather
  than writing something wrong - but the recovery path must actually recover.
- **The live FIRSTWATCH campaign cannot be redeemed by a wallet holder** (WO-1440 unblocks the GUEST
  rail server-side; this ticket is what makes the WALLET rail work).
- Anything else on the authed rail - entitlements, purchases - is equally dark.

## 4. WHAT TO ESTABLISH, IN ORDER

1. **What is supposed to CREATE the session, and did it ever run?** Follow `TryAttachSession`'s notion of
   a "live session" back to its writer. `why=missing` says the store was empty at read time - find out
   whether the write never happened, wrote elsewhere, or was cleared.
2. **Does the browser round trip complete?** Trace the return leg end to end. If it cannot complete on
   this device/OS configuration, that is the finding.
3. **Is `NightMarketSharedCardSession` the ONLY establishment path?** If the session can only be minted
   by opening a store screen, then a player who never opens the store never has one - and cloud save
   would be broken for them from first launch. **Establish whether that is the case; it would reframe
   this from a bug into a missing lifecycle.**
4. **Reconcile with WO-1420.** Same root cause, or two? Say which, with evidence.

## 5. WHAT NOT TO DO

⛔ **Do not make the save path fail OPEN.** Writing a save under unverified identity is how one player's
progress lands on another player's account. The fail-closed refusal is correct and stays.
⛔ **Do not weaken the auth requirement** to make the symptom disappear. WO-1440 reverses the guest rule
for the promo endpoint ONLY, on an explicit owner ruling with the risk stated. That reversal does not
extend here.
⛔ **Do not delete the offline queue.** Those deltas are the owner's day. Whatever else changes, they
must survive and drain once identity returns - **verify the drain actually works**, because a queue that
fills forever and never drains is the same data loss with extra steps.

## 6. ACCEPTANCE

- [ ] The root cause is proven from a captured trace, quoted. **An inferred cause is a guess**
      (memory: `never-inference-fix`).
- [ ] A wallet holder gets a live session without having to open the Night Market, proven on device.
- [ ] A cloud save SUCCEEDS, proven by a captured request/response - not by the absence of an error.
- [ ] The queued offline deltas DRAIN, proven by measurement.
- [ ] WO-1420 is answered: same cause or separate, with evidence.
- [ ] Instrumentation on the return leg is permanent, so the next failure is one read rather than a day.
- [ ] `REGRESSION_OK n/n`.
