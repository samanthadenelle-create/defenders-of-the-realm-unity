# WORK ORDER 1115 — Redeem codes: player promotions, and a dev grant that survives a release build

**Status:** SPEC — READY TO IMPLEMENT once §3 is ruled
**Minted:** 2026-08-17 (CLI seat, main line — banner bumped 1115 → 1116 in this same edit)
**Lane:** Monetization / live-ops. Touches the payment path — read §4 before writing a line.
**Provenance:** owner, 2026-08-17, verbatim: *"we could add a code option for promotions like 50% but
dev code would allow grant free resources or set to 1 skr"* — raised while trying to test the crafting
economy on a **published release APK**, where `DeNelle.DevTools` is stripped (`asmdef defineConstraints:
UNITY_EDITOR || DEVELOPMENT_BUILD`) so no in-game grant surface exists.

---

## 1. Why this is the right shape, and better than the alternative

The immediate need was "get crafting items into my inventory on the Seeker." The obvious answer is a
**development APK** — but that is the wrong tool:

- `AndroidBuild` ships `BuildOptions.None`, so a dev variant means changing the builder.
- A development build is **not the binary that ships**. Testing the economy on it proves the economy
  works *in a build nobody will ever download*.
- It has a watermark, different stripping and different perf.

A redeem code runs on **the release APK the players actually have**. Same binary, same code paths,
same stripping. It also happens to be a real live-ops feature rather than test scaffolding: launch
promotions, influencer codes, apology grants after an outage, and Solana-community drops all use the
same machinery.

**One system, two audiences.** That is the whole design.

---

## 2. ⛔ THE SECURITY CONSTRAINT THAT DECIDES THE ARCHITECTURE

> **The game is PUBLISHED. The binary is public. Anything the client can decide, an attacker can decide.**

A code validated **in the client** — any string compare, any hash check, any embedded table — is
extracted the first time somebody decompiles the APK. And the specific codes requested here are the
worst possible ones to leak: *"grant free resources"* and *"set the pack to 1 SKR"* are a money
exploit and a price exploit respectively. A shared dev code would let anyone mint resources or buy a
$5 pack for a cent.

**So: codes are VALIDATED SERVER-SIDE. Always. No exceptions, no offline fallback for grant codes.**

⚠ **And an offline fallback is not a kindness here — it is the exploit.** If the client grants when
the server is unreachable, the attack is "turn on airplane mode." A redeem that cannot reach the
server must FAIL and say so, not degrade to trusting the player.

**We already have the rail, and the owner proved it working today (2026-08-17):** the wallet signs
backend requests (`SignMessageBase58` → `BackendRequestSigner.cs:154` / `GameStateService.cs:1568`),
the backend verifies the ed25519 signature, and the save round-trips keyed to `BoundWallet`. A redeem
request rides that exact path. **Do not invent a second auth mechanism.**

Consequence worth stating: a leaked DEV code is harmless, because the server binds it to the
**owner's wallet address**. Anyone else redeeming it gets a refusal.

---

## 3. ⛔ OWNER RULINGS REQUIRED before implementation

**R1 — What may a PLAYER code do?** My recommendation in brackets.
- *(a)* Percentage discount on a pack (the owner's "50%") **[yes — this is the ask]**
- *(b)* Grant resources outright (an apology/launch gift) **[yes, but see R3]**
- *(c)* Grant a cosmetic / Echo / gear item **[defer — item grants touch the catalog and rarity]**
- *(d)* Free pack outright **[defer — a 100% discount is (a) with a 100 value; no separate path]**

**R2 — Single-use, per-wallet, or campaign-wide?** Recommendation: **per-wallet single-use with a
campaign-wide cap and an expiry**, all three enforced server-side. A code with no cap and no expiry is
the one that ends up on a deal site. The schema should carry `maxRedemptions`, `perWallet`, `expiresAt`
from day one even if the first campaign leaves them generous — adding them later means migrating live
codes.

**R3 — Do granted resources respect the storage cap?** ⚠ This matters more than it looks and it is the
same trap as the impulse packs (WO-1037/947 §12): `IEconomy.Grant` returns the **APPLIED** basket, not
the requested one, precisely so a silent clamp cannot hide. A player who redeems "5000 wood" into a
2000 store and silently loses 3000 has been lied to by a promotion. Recommendation: **report what
actually landed**, and refuse-with-explanation rather than partially granting, when the overflow is
large.

**R4 — Price-setting dev codes ("set to 1 SKR").** Recommendation: **the server returns a PRICE
OVERRIDE for that wallet's session; the client never sets a price.** A client that can set its own
price is a client that can set it to zero. The override must also be visibly labelled in the UI (see
§5) so a screenshot of a discounted store can never be mistaken for the real price list.

**R5 — Where does the code get entered?** Recommendation: **Settings → Redeem Code**, not the store.
Keeps a text field off the purchase surface, and works even when `realmstorepurchase` is off — which
is exactly the state the owner needs for testing today.

---

## 4. What this must NOT touch

The three payment-refusal layers are deliberate and are **not** in scope:
1. `FeatureFlags.RealmStorePurchase` (`defaultOn: false`)
2. `WalletService.Pay` / `PayFlat` unconditional refusal (WO-931 — closed the stub free-grant hole)
3. `SolanaWalletProvider.SendPayment`'s `WalletNetwork.Mainnet` hard-block (`:429`)

⚠ **A redeem code must never become a fourth way to acquire something the payment path refuses.** If a
code can grant what a purchase cannot, the refusals are decorative. A GRANT code is fine (it is a
gift, no money moves); a code that completes a PURCHASE is not — that must still route through
`WalletService.Pay` with every layer intact.

Also: the WO-947 §12 purchase-boundary guardrails apply unchanged — **exactly one economy key per
grant**, never structures, never levels, never queue completions.

---

## 5. Honesty requirements (these are not polish)

- A discounted price must **show the original and the discount**, never just a low number. The player
  should always be able to see what they saved and what the real price is.
- A dev/test override must be **visibly marked as a test price** in the UI. A screenshot of a 1-SKR
  store must be self-evidently not a real offer.
- A failed redeem must say **why** — expired, already used, wrong wallet, no connection — never a bare
  "invalid code". §12 discipline: no silent failures, and here a vague failure also reads as a scam.
- ⛔ **Never log a code value.** F8 captures get shared; a live promo code in a capture is a leak. Log
  the OUTCOME (redeemed / expired / already-used), never the string.

---

## 6. Shape of the work

**Backend** (`api/`, the existing Vercel + Neon project in this repo — memory `api-backend-in-repo`;
read it, do not greenfield):
- `promo_codes` table: `code`, `kind` (discount / grant / price_override), `payload`, `maxRedemptions`,
  `perWallet`, `expiresAt`, `boundWallet` (nullable — set for dev codes), `active`.
- `redemptions` table: `code`, `wallet`, `redeemedAt` — the per-wallet single-use enforcement.
- One endpoint, **signature-verified like the save path**, returning the applied result.
- Admin write path reuses the existing admin-db route; no new auth surface.

**Client**:
- Settings → Redeem Code entry, routed through the existing signed-request seam.
- Applies the server's answer. **Decides nothing itself.**
- `FlowTrace`/`Guard` on every branch; no code value in any trace.

**Oracles**: a client-side grant path cannot exist (source lint: no local code table, no offline grant
fallback); a redeem never routes around `WalletService.Pay` for purchases; one economy key per grant;
no code string reaches a log. Register like a neighbour in `Assets/Editor/Regression/`.

---

## 7. Acceptance

1. A dev code bound to the owner's wallet grants on **her** device and is **refused** on any other.
2. With the network disabled, a redeem **fails with a reason** and grants nothing.
3. A discount shows original price, discount, and final price.
4. A test price override is visibly labelled as such.
5. Granting into a full store reports what actually landed (R3).
6. No code string appears in any log, trace or F8 capture.
7. `COMPILE_GATE_OK` + `REGRESSION_OK`.

---

## 8. Immediate unblock for the owner, ahead of all of this

She needs crafting items on the Seeker **now**, to test the polish economy. This WO is the right
long-term answer but is not a ten-minute job. Faster options, in order of preference:

1. **Grant server-side directly** against her `BoundWallet` save row — no client change at all, and the
   save already round-trips (proven today). Needs her wallet address and a one-off admin write.
2. **Editor playtest** — DevTools is present in-Editor; the economy is identical. Loses device-fidelity
   for touch/perf, keeps it for the crafting logic she is actually testing.
3. A one-off development APK — **least preferred**, for the §1 reasons.
