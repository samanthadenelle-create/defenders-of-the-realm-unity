**Status:** READY - PARTIAL. The `auth_sessions` rail landed (`e526e013f`), but the ticket's DEFINING PROMISE is unmet: ⚠ **the first purchase still produces TWO wallet prompts** - its own body says "PARTIAL AGAINST ITS OWN WORDING".  *(Bucket corrected 2026-08-24: the line led with FIXED while its own text said work remained. Prior line preserved below.)*
>  PRIOR: **Status:** FIXED 2026-08-23 (`e526e013f`) — AWAITING OWNER FELT-TEST TO CLOSE. The `auth_sessions` rail landed: `POST /api/auth/session` runs the SAME `verifyWallet` and burns a real nonce (the existing proof cached, not a login shortcut), wired into `verifyWallet` so every protected route inherits it; 15-minute window, bound to one wallet, in memory only, additive and fail-closed. ⚠ **PARTIAL AGAINST ITS OWN WORDING — read before felt-testing.** The ticket says *"only the transfer should ever be seen"*; the session is minted **lazily on the first authed call** (`BackendRequestSigner.cs:230`), not at connect, so the FIRST purchase of a session shows **TWO** prompts (session mint, then transfer) and every purchase after shows **ONE**. That is 3 → 2 → 1, not 3 → 1. Minting at connect would make it one throughout — small, contained, not done. Prior status: "IN PROGRESS — CLI-owned (owner directive 2026-08-23: *"i want you to directly own it"*, *"not an agent"*)" — the work landed and the line never moved, which is the board-lies-about-tickets class (KEY_FACTS 08-14, 13 tickets).

# WORK ORDER 1157 — A purchase asks the wallet THREE times. It should ask once.

**Minted:** 2026-08-23 (CLI, banner bumped 1157 -> 1158 in this SAME edit)
**Lane:** Wallet / backend auth. **Class:** THE MONEY PATH FEELS BROKEN.
**Found by:** the owner, during the live MON002 mainnet canary, 2026-08-23 — *"i had to verify with wallet 3 times… cant it roll into one transaction like every other site?"*

---

## 1. WHAT ACTUALLY HAPPENS — three prompts, three different things

Read from the code, not inferred:

| # | Prompt | Where | Should it prompt? |
|---|---|---|---|
| 1 | MWA connect / authorize | `SolanaWalletProvider:320` (cached in `MwaSessionStore:171`) | **Once per install.** Already cached — should not recur. |
| 2 | Message signature for backend auth | `BackendRequestSigner.cs:154` | ⛔ **NO — this is the defect.** |
| 3 | `SignAndSendTransaction` (the transfer) | `SolanaWalletProvider:699` | ⭐ **YES, always. Keep it.** |

**Why #2 fires on every call:** each authenticated request fetches a **fresh single-use nonce**, builds
`dotr-save:v1:<wallet>:<nonce>:<sha256(body)>`, and asks the wallet to sign it. The signature is bound
to that exact body, so it is **structurally unreusable**. A purchase calls `/verify` and then
`/fulfill` — two calls, two signatures, two prompts.

## 2. ⭐ THE PROMPT WE ARE KEEPING, AND WHY

The transfer prompt stays. **A payment that does not ask is a payment you cannot refuse**, and this is
real money on mainnet. The sites this is being compared to behave the same way: they cache the
**session**, never the **purchase consent**. Anyone "fixing" this by suppressing prompt #3 has
misunderstood the ticket.

## 3. THE FIX — a short-lived session, not a permanent login

Sign **once**, exchange that signature for a bearer token with an expiry, attach the token to
subsequent calls. The plumbing is already half-built: the nonce response carries `expiresAt` and
`ttlSeconds` (`BackendRequestSigner.cs:217`) — the shape is there, it is simply never reused.

- `auth_sessions` table: token, wallet, expires_at, revoked.
- `POST /api/auth/session` — takes the SAME nonce + signature proof that exists today, returns a token.
- `x-session` header accepted **alongside** the existing `x-wallet`/`x-nonce`/`x-signature` path.
- Client obtains a session lazily, caches it, and re-signs **once** on expiry.

## 4. ⛔ CONSTRAINTS — the ones that make this safe rather than merely shorter

- **BACKWARD COMPATIBLE, FAIL-CLOSED.** The per-request signature path stays and keeps working. A
  missing/expired/unknown session must fall back or refuse — never silently authenticate.
- **THE TRADEOFF IS REAL AND MUST BE STATED, NOT HIDDEN.** A body-bound signature cannot be replayed
  against a different request; **a bearer token can, until it expires.** That is a genuine security
  reduction. It is acceptable ONLY with a short TTL and a wallet-bound token. Do not extend the TTL
  for convenience, and never make it a permanent login.
- **The wallet-vs-player check survives.** A session for wallet A must never act on player B
  (`AuthCode.WALLET_MISMATCH`).
- **Single-use nonces stay single-use.** The session is issued FROM one burned nonce; it does not
  make nonces reusable.
- ⛔ **Never persist a session token in plaintext.** `MwaSessionStore` already seals its token with
  AES-GCM precisely because PlayerPrefs on Android is readable — follow that precedent, or keep the
  session in memory only.
- The backend `package.json` is the **Vercel** deployment. Do not add runtime dependencies for this.

## 5. ACCEPTANCE

- [ ] A purchase prompts the wallet **exactly once** — the transfer
- [ ] The transfer prompt still appears, every time, and declining still cancels
- [ ] An expired session re-signs once and continues, with no user-visible failure
- [ ] A session for wallet A cannot act for wallet B
- [ ] The old signature path still authenticates (no forced migration)
- [ ] `node --test api/test/` stays green, with new cases for issue / reuse / expiry / mismatch
