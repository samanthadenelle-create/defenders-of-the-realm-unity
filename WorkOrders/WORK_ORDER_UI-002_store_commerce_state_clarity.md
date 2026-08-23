# WORK ORDER UI-002 — Store commerce-state clarity and truthful actions

**Series:** UI (intentionally separate from numbered BOARD work)
**Status:** PARTIAL 2026-08-22 - the card/string work landed with UI-001 (StorePackCard carries state as words not hue; three player-facing strings moved into both canon-strings copies, retiring the hardcoded English at the old PackStore.cs:1331/:1333). ⛔ §4 REMAINS UNSATISFIABLE AS WRITTEN and was deliberately left alone: the BalanceState.Unavailable branch has already DROPPED the bound address and there is no retry control, so "Balance unavailable - retry" would render a button with nothing to tap. That is a commerce decision (PurchaseGate is the sole authority), not a layout one - needs an owner ruling before anyone implements it.
**Owner lane:** UI / Commerce presentation (security authorities preserved)
**Date:** 2026-08-22
**Tracking:** Not tracked on `BOARD.html`. Do not edit or rebuild the board for this document.

**Design reference (interactive wireframe, SME-folded):** https://claude.ai/code/artifact/5b8dc821-9290-4936-b202-6e3974854955
- drive the UI-002 commerce states live, toggle thumb zones and greyscale; the badge moves and
  the single bottom band are drawn in. The wireframe is the visual acceptance target.

## OWNER RULINGS 2026-08-22 (verbal, this session)

1. **"this is the money screen"** — truthful commerce state is not polish here; it is the product.
   Every state this document defines must be implemented, none deferred.
2. **"GET SME team on this"** — a four-lane SME review was convened 2026-08-22 (merchandising, Unity
   kit feasibility, crypto payment UX, accessibility/ergonomics). Findings are folded in below;
   sections marked **[SME]** carry that provenance.
3. The **full-screen ruling** on UI-001 (*"maximize whole screen"*) applies to every layout reference
   in this document too.

## Outcome

Make every Night Market state truthful and immediately understandable: which wallet is bound, whether a live wallet session exists, what rail and exact price will be used, whether an action is available, and what is happening after approval. The UI must explain commerce authority without duplicating or weakening it.

## Device evidence

- Screenshot: `dev/tmp/seeker_store_skr_offer.png`.
- Companion screenshot: `dev/tmp/seeker_store_pre_purchase.png`.
- Trace: `Logs/device/mon-skr-store-open-2026-08-22.log`.
- The trace records `BuildSpotlightCta 'hearth-spark': Buy REFUSED by PurchaseGate` with face `Coming soon` and `actionable=False` because `ff.realmstorepurchase` was off.
- The screenshot simultaneously says `Connect a wallet to see your balance.` even though the app has a bound Seeker identity elsewhere in the tested flow. Bound identity, live authorization/session, and readable balance are distinct states and must not be collapsed into one misleading sentence.

## Binding authority rule

**Location note (corrected 2026-08-22):** `PurchaseGate` moved 2026-08-21 to `Assets/_Modules/Wallet/PurchaseGate.cs` — its header records why (the charge path in `DeNelle.Wallet` could not reach it in `Village/Monetization`). Cite the Wallet path.

`PurchaseGate` remains the sole client authority for whether a purchase action may proceed. Server catalog/verification remains authoritative for SKU, rail, recipient, mint, amount, finality, and entitlement. UI code may render those decisions and request an action; it may not infer, override, or recreate them.

## Required state model

Render the following dimensions independently, then compose them into plain-language UI:

### Wallet identity/session

1. **No wallet bound** — no known paying identity; offer `Connect wallet` where allowed.
2. **Wallet bound, no live session** — show shortened bound address and `Authorize to purchase`; do not claim no wallet exists.
3. **Wallet connected/authorized** — show shortened address, network, and refreshable balance.
4. **Balance unavailable** — retain identity state and say `Balance unavailable — retry`; do not revert to `Connect wallet`.
5. **Wrong network/account changed** — block purchase with a specific recovery action.
6. **Wallet did not respond [SME]** — the 30s MWA association timeout (`WalletService.Connect`, `:475`)
   is NOT a cancel, and the code cannot distinguish "no wallet app installed" from an unanswered
   handshake — so the copy covers both honestly: `Wallet didn't respond - check it is installed, then retry.`
7. **Session revoked mid-flow [SME]** — `SolanaWalletProvider:478` clears the MWA session on "grant
   revoked, expired, or the wallet did not answer". A player who switched wallet apps mid-purchase
   gets a RE-AUTHORIZE recovery state, never a generic failure.

The persistent bound address is identity evidence, not proof of a current signing session. A cached balance is not proof of current spendability. Copy and visual state must preserve those distinctions.

### Offer/action

1. **Rail globally closed** — non-actionable state sourced from `PurchaseGate`; explain that purchasing is unavailable without presenting a button-shaped false promise.
2. **Offer unavailable** — SKU/catalog/availability-specific state, distinct from the rail being closed.
3. **Actionable** — CTA includes verb + exact price + currency, e.g. `Buy - 36 SKR`.
4. **Insufficient balance** — show required amount, available balance, and valid recovery path; never start wallet approval.
5. **Wallet authorization required** — CTA says `Authorize wallet`, not `Buy`.
6. **Retryable service failure** — explicit `Retry` plus durable transaction-status guidance.
7. **Rail not configured [SME]** — `WalletEndpoints.SkrMint` ships EMPTY on both networks today and
   `PurchaseGate` refuses at the gate (`PurchaseGate.cs:150-155`). This is NOT a retryable failure:
   a `Retry` that can never succeed is a lie. Distinct state and copy from transient RPC failure.

`Coming soon` is permitted only when the owner/catalog genuinely rules future availability. It must not be used as a generic rendering of every disabled reason. A disabled button must never look identical to an actionable button.

### Transaction lifecycle

Provide explicit, mutually exclusive states:

1. `Ready`
2. `Opening wallet`
3. `Awaiting your approval` — no claim that payment has occurred
4. `Submitted` — show shortened signature when available
5. `Verifying on Devnet/Mainnet`
6. `Confirmed - delivering contents` — **[SME]** the chain check runs at `Commitment.Confirmed`, NOT finalized. Do not print `Verified` on Mainnet unless the verifier checks finalized; `Confirmed` is the honest word
7. `Fulfilled` — name the granted pack/content; prevent another accidental activation
8. `Cancelled - no payment was sent` — **[SME]** the app signs THEN self-submits, so a signed tx exists before submit; `nothing charged` is only provable pre-signature or post-expiry. Say what is provable
9. `Failed before submission - no payment was sent`
10. `Submitted, confirmation delayed` — preserve transaction/signature and provide safe retry/reconcile guidance; never invite a second payment blindly
11. `Expired - not charged; retry is safe` — **[SME]** after blockhash expiry (~60-90s past
    `lastValidBlockHeight`) with no signature found, the transaction PROVABLY cannot land; crypto
    players know this state and expect the safe retry. State 10's "never invite a second payment"
    is correct only while the tx MAY still land — treating provably-dead as forever-unsafe reads as
    broken to this audience. ⚠ Named dependency: `ConfirmTransaction` (`WalletService.cs:940`)
    polls to timeout and never checks block-height expiry, so the verifier cannot yet distinguish
    10 from 11.

Pending/verifying state must survive modal close, app background/foreground, and process restart through the existing pending-purchase authority. The UI must not clear, grant, or synthesize pending state itself.

### The danger window — the promise, and FOUR named code dependencies [SME: crypto lane, cited at source]

The UI must be able to promise, in plain words: **"Your payment is recorded on this device. You will
not be asked to pay again. The item arrives, or support restores it from the signature below."**
That promise requires a durable record at-or-before signing. Four gaps in current code make it
unimplementable from the presentation layer — so they are **NAMED DEPENDENCIES** of this WO,
Core/Wallet work that lands first and is never absorbed into a presentation pass:

1. **The pending row is written only on `result.Ok`** (`PackStore.cs:~1508`). The exception path —
   the one that logs *"outcome indeterminate; if a charge settled the entitlement may be lost"* —
   writes NO pending row. A crash after sign-and-submit leaves nothing to reconcile on restart.
2. **`PaymentResult.Failure` DROPS `TxSignature`** — the did-not-confirm path buries the signature
   inside the error string (`SolanaWalletProvider.cs:~698`). "Preserve the signature" cannot be
   rendered from a result that discarded it.
3. **`GetBalance` returns ZEROS on failure** (`:~538`) — the "no false zero" regression row below is
   unsatisfiable without an API change. This trips this WO's own first stop condition; route it to
   the commerce owner, do not work around it.
4. **`ConfirmTransaction` never checks `lastValidBlockHeight`** (`:~940`) — lifecycle state 11
   cannot be distinguished from state 10 until it does.

The durable surface: a restart-surviving **Pending purchases** entry (the PlayerPrefs pending row
exists; its only display today is the transient `_statusBanner`), showing the shortened signature
and a reconcile action.

### Honest timing per state [SME: crypto lane] — crypto users abandon at unexplained waits

| State | Typical | Escalate |
|---|---|---|
| Opening wallet | under 2s | warn at 5s; fail to `Wallet didn't respond` at 30s (the MWA timeout) |
| Awaiting your approval | unbounded — NO countdown; the clock pauses while the app is backgrounded | never auto-fail a human decision |
| Submitted | ~1s | — |
| Confirming | Devnet 1-2s; Mainnet 2-5s at Confirmed (finalized up to ~30s) — say `usually a few seconds` | `Confirmation delayed` at 60s; `Expired - retry is safe` at ~90s with no signature found |
| Delivering | under 1s | — |

## Presentation requirements

- Wallet summary belongs in one consistent header/status component: shortened address, `Devnet`/`Mainnet`, rail (`SKR`), and balance/status.
- The focused offer must always show exact rail and price before wallet handoff.
- Devnet test builds must display an unmistakable `DEVNET - TEST TOKEN` marker near the price and in verification status. Production must never display Devnet state.
- Use icon + text + shape for state. Never rely on green/red/gray alone.
- Disabled actions must explain why nearby, with an appropriate recovery action if one exists.
- Do not render a disabled button that invites repeated tapping without response.
- Only one primary action may be visually dominant for the focused product.
- Prevent rapid repeat activation while opening wallet, pending, verifying, or delivering.
- State is carried by icon + text + shape (never hue alone); contrast 4.5:1 body / 3:1 large;
  touch floors per `MinTouchPx = 112` with the clamp a no-op (WO-1060).
- **[SME] The earlier "announce state changes accessibly" requirement is STRUCK:** zero
  `UnityEngine.Accessibility` usage exists under `Assets/_Modules/` (verified by grep), and Unity's
  accessibility module targets UI Toolkit — this project is code-built uGUI, so a screen-reader
  requirement fails on every player build. If TalkBack support is wanted, it is its own WO.
- Never display seed phrases, private keys, full sensitive payloads, or raw backend errors.

## Suggested copy matrix

| Condition | Primary presentation | Supporting copy/action |
|---|---|---|
| No wallet bound | `Connect wallet` | `Connect a wallet to view its SKR balance and purchase.` |
| Bound, session closed | `Authorize wallet` | `Wallet CHKK...sfkC is bound. Authorization is required to sign.` |
| Connected, actionable | `Buy - 36 SKR` | `Devnet - Test token` or ruled production network |
| Rail disabled | `Purchases unavailable` (non-button state) | Render the exact owner-approved gate reason; do not say `Coming soon` unless true. |
| Insufficient SKR | `36 SKR required` | `Balance: 12 SKR` plus valid acquisition guidance |
| Awaiting wallet | `Awaiting approval...` | `Approve or cancel in your wallet. Do not close the wallet app.` |
| Submitted/verifying | `Confirming...` | Network + shortened signature; no second Buy action |
| Delayed | `Confirmation delayed` | `Your transaction was submitted. We will reconcile it; do not pay again.` |
| Fulfilled | `Added to your account` | Name the pack and exact delivered contents |
| Cancelled | `Purchase cancelled` | `Nothing was charged.` |

**[SME] ASCII note:** the matrix above previously used em dashes, U+2026 ellipses and U+2022
bullets — TMP renders non-ASCII as tofu, so all are replaced with `-`, `...`, `*`. And **no SKU
costs 20 SKR**: the live rungs are 25 / 36 / 60 / 120 / 240 / 600 (`packs.json` skrPeg); every
example now uses a real rung.

Final copy lives in **`canon-strings.json`** (flat camelCase keys, ASCII-only, dual copy —
`Assets/Resources/Data/Canonical/` + `Assets/StreamingAssets/Data/Canonical/`, byte-identical) —
not in a new mapping class, not in scattered SKU strings.

## Strict implementation scope

- Store wallet/status header, PurchaseGate-reason presentation, CTA state styling/copy, transaction progress/recovery presentation, accessibility semantics, and commerce-state regression coverage.
- Read current wallet/purchase state through existing services and immutable view models/events.
- Add presentation-only adapters if needed; they must not become an alternative purchase authority.

## Non-goals

- No change to PurchaseGate decisions or feature-flag defaults.
- No wallet signing, transaction construction, recipient/mint/amount, verifier, reconciliation, fulfillment, database, or API changes.
- No price, catalog, reward, or economic changes.
- No auto-connect, auto-sign, auto-retry payment, or background submission.
- No manual `BOARD.html` edit.
- No claim that a bound wallet is connected, or that a submitted payment is fulfilled.

## Regression matrix

Automated tests must derive expected presentation from independent state inputs; they must not merely assert constants against themselves.

| Case | Required proof |
|---|---|
| No wallet bound | Connect CTA; no balance fiction; Buy unavailable |
| Bound/no session | Correct shortened address; Authorize CTA; not `Connect wallet` |
| Connected/balance loaded | Address, network, SKR balance, exact offer price |
| Balance fetch failure | Identity preserved; retry shown; no false zero **(BLOCKED by danger-window dependency 3 until `GetBalance` distinguishes failure from zero)** |
| PurchaseGate off | Non-actionable; exact gate reason; no purchase call |
| Offer unavailable | Distinct from rail-off reason |
| Insufficient balance | No wallet handoff; exact required/available amounts |
| Actionable | One tap produces one handoff; `Buy - 36 SKR` visible |
| Rapid repeated taps | One presentation/transaction only |
| Wallet cancel | Ready state restored; nothing charged/granted |
| Failure before signature | Nothing charged/granted; retry safe |
| Signature submitted | Buy suppressed; signature persisted |
| Verification delayed | No second charge invitation; reconcile path visible |
| Verified, delivery pending | No duplicate grant/payment |
| Fulfilled | Exact contents shown; pending record clears only through authority |
| App restart at each pending state | State rehydrates and reconciles truthfully |
| Devnet build | `DEVNET - TEST TOKEN` visible |
| Mainnet build | No Devnet copy; mainnet mint/network gates still authoritative |

## Device matrix and evidence

On Seeker at 2340×1080, capture full-screen evidence for:

1. Bound wallet with session closed.
2. Connected wallet and loaded 100-test-SKR balance.
3. PurchaseGate disabled.
4. Actionable `Buy - 36 SKR` Devnet offer.
5. Awaiting wallet approval.
6. User cancellation.
7. Submitted/verifying.
8. Delayed/reconcile-safe state (simulated test environment is acceptable).
9. Fulfilled once, showing exact delivered contents.
10. Restart/reconcile without a second grant or second payment prompt.

Pair screenshots with FlowTrace/API/database evidence where relevant. Redact only secrets; public wallet addresses and transaction signatures may be shortened in screenshots.

## Acceptance criteria

- A bound wallet is never described as nonexistent merely because no live session/balance is available.
- The player always sees currency and exact price before signing.
- Disabled, actionable, pending, verifying, and fulfilled controls are visually and semantically distinct.
- No state encourages a second payment while a submitted transaction may still settle.
- Devnet cannot be mistaken for a real-token Mainnet purchase.
- Cancel/failure clearly says whether anything was charged.
- Fulfillment names what was delivered and occurs exactly once through existing authority.
- Numeric contrast, touch floors, and a GREYSCALE capture of disabled-vs-actionable pass; every state is legible with hue removed. *(The screen-reader/announcement line is struck — see Presentation requirements.)*
- Existing PurchaseGate, wallet, verifier, reconciliation, and fulfillment regressions remain green.

## Stop conditions

Stop and return to the commerce/security owner if:

- Required UI truth cannot be obtained from an existing authoritative service/event without guessing.
- Two authorities disagree on wallet, network, amount, signature, verification, or fulfillment state.
- The proposed design requires bypassing PurchaseGate or enabling a production flag.
- A failure case cannot distinguish `not submitted` from `submitted but unconfirmed`.
- The implementation would store secrets or private wallet material in UI state/logs.
- Copy implies a guaranteed price, completion, refund, or entitlement that the backend does not guarantee.

## Handoff evidence

Implementation result must enumerate exact changed files, state-table/oracle results, device/build identity, FlowTrace excerpts, and screenshot paths. Stage only the explicit lane. Do not commit unrelated dirty-tree files.
