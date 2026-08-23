# WORK ORDER 1160 — The money path's two new endpoints were never deployed to production

**Status:** BLOCKED ON OWNER — the fix is a **production deploy of `api/`**, and web promotion is explicitly the owner's call (START_HERE §4: *"Web deploys stay preview-only … NEVER `--prod` — promotion is the owner's"*). No code change is required. ⚠ The Vercel CLI is **not installed on this machine** (`npm i -g vercel`).

**Minted:** 2026-08-23 (CLI) — banner bumped 1160 → 1161 in the same edit
**Priority:** ⛔ **P0 — it is the only thing between the go-live build and a working sale.**
**Provenance:** owner felt-test of the WO-1159 go-live APK: *"look at screenshot i dont see that"* — device screencap shows every Night Market card reading **"Price unavailable"**.

---

## 1. The proving data — a probe, not a theory

The game hardcodes `BackendRequestSigner.BackendBase = "https://defenders-of-the-realm-v2.vercel.app"`
(`Assets/_Modules/Core/Web3/BackendRequestSigner.cs:50`). Probed directly:

| endpoint | production | verdict |
|---|---|---|
| `POST /api/purchases/quote` | **404** — body `NOT_FOUND`, Vercel edge (`cle1::v66fk-…`) | **route does not exist** |
| `POST /api/auth/session` | **404** — same | **route does not exist** |
| `POST /api/auth/nonce` | 400 (rejects empty body) | exists |
| `POST /api/purchases/verify` | 400 (rejects empty body) | exists |

GET returns 404 on both new routes too, so this is a **missing route, not a method rejection**.
The two endpoints that answer are the OLD ones. **The two added by WO-1158 and WO-1157 today are
absent from production.** This matches the long-standing canon note that `api/` has been deployed to
**PREVIEW only** while the client hardcodes the prod domain.

## 2. What it causes — one visible symptom, one INVISIBLE one

**(a) Visible, and correct behaviour.** `PurchaseQuoteService` gets a 404, so no price is issued;
`PackDef.AmountFor(Skr)` returns **zero**; the card renders the words **"Price unavailable"** beside
the local USD anchor (`~ $2.99`), and the right rail reads *"Price unavailable right now. Nothing has
been charged; reopen the store to retry."* **This is WO-1158's fail-closed path working exactly as
designed** — the client refuses to invent a number rather than charge a made-up one. Nothing is
broken on the client. **No purchase is possible, so the quote can never be verified on this build.**

**(b) INVISIBLE, and the one nobody would have caught.** `/api/auth/session` is also 404, so
`BackendRequestSigner.TryAttachSession` fails and falls back to **signing every call individually** —
by design (*"FALLBACK, NOT REPLACEMENT"*, `BackendRequestSigner.cs:180`). So **WO-1157's one-prompt
fix is silently inert in production**: the wallet still asks three times. Had the quote endpoint been
deployed and the session endpoint not, the three prompts would have read as *"WO-1157 failed"* and
sent someone to debug working client code.

## 3. ⭐ WHY THE CANARY DID NOT CATCH THIS — the transferable lesson

The owner ran the mainnet 1-SKR canary **twice, successfully**, before this build. Both canaries
answer **`pinned: true` with no quote row and no rate**, and they settle against `/verify` — **which
exists in production.** So the canary exercised the transport, the wallet, the mint, the recipient,
the decimals and the entitlement seam, and **never once touched `/api/purchases/quote`.**

> **A proof-of-rail cannot prove a path it structurally bypasses.** This was written into WO-1158 and
> stated before the test — *"the canaries do not test the quote"* — and here is the material proof:
> the quote endpoint did not exist at all, and both canaries still passed.

The same shape as CLAUDE.md §16 (an unpushed R2 bundle installs, launches and plays) and WO-1138 (a
gate that reports success while asserting nothing). **Success on an adjacent path is not evidence.**

## 4. The fix

**Deploy `api/` to production.** No code change. `api/` is git-tracked in this repo, so a production
deploy from the repo root ships all four routes together.

⚠ Two constraints, both real:
1. **Promotion is the owner's call**, never the agent's (START_HERE §4). Preview deploys are the
   agent's ceiling.
2. **The Vercel CLI is not installed on this machine** — `npm i -g vercel` first.

⚠ And one trap recorded in canon: **deploy from the REPO ROOT.** `Builds/WebGL/` carries its own
`.vercel/project.json` pointing at a DIFFERENT Vercel project (`defenders-webgl`), so a deploy run
from there lands in the wrong project.

## 5. Acceptance — how to know it actually worked

Re-run the probe. **Judge by the HTTP code, not by the deploy log's own success claim** (this repo's
standing rule: markers and measurements, never a runner's self-report):

- `POST /api/purchases/quote` must stop returning 404. A **400/401 is SUCCESS here** — it means the
  route exists and is rejecting an unsigned empty body, which is correct.
- `POST /api/auth/session` likewise.
- Then, on device: the Night Market cards must show **real SKR digits** instead of "Price
  unavailable", and the first purchase of a session must show **two** wallet prompts, not three.

## 6. Related, found in the same screenshot — NOT part of this ticket

The store's legal band prints **`REWARDS DISTRIBUTOR 2JRm…nmNi`** directly beneath the claim
**"0% STORE FEE — EVERY PAYMENT REACHES THE REALM"**. `WalletRegistry.cs:16-18` states that wallet is
*"shown for transparency. **NOT a payment destination**"* — payments land in the Squads treasury vault
`9wbHbKuirtKai5e3ajvdpzdRYVpuxpAH4DUnERkVtBzj`. So a storefront that now takes **real money** is
printing a payment-adjacent claim next to the **wrong wallet**. Harmless while nothing could be
bought; a truthfulness problem now. **Owner ruling owed** (drop it, or swap it for the real
recipient). Rendered at `PackStore.cs:604` via `StoreLegalFooter.Build`.
