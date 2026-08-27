# RESULT — WO-1157 Fail bounce (Title / dungeon MintSession sheets)

**Date:** 2026-08-27  **Seat:** CLI implementer
**WO Status:** left unchanged (READY TO IMPLEMENT) per instruction.
**Commit / Unity / LoginPanel:** not touched.

## Captured Fail (device 2026-08-27)

Every extra wallet sheet was `MintSessionAsync` (`SignMessage via targeted MWA association`) on Title after CONTINUE, and again ~15 min later walking into a dungeon (`auth_sessions` TTL). It was not the Unity login panel.

Those callers are cloud SAVE (`GameStateService.SendCurrentSnapshot` → `TryAttachAsync`), not a purchase. Lazy mint-on-any-authed-call is what popped MWA while the player was continuing / walking.

## How Title / dungeon mint is prevented

`BackendRequestSigner.TryAttachAsync` now mints only for `/api/purchases/*`.

- Title CONTINUE and dungeon-enter save attach a **live** in-memory session if one exists.
- If the session is missing or expired, they **wait** (return false, caller aborts/requeues). They do **not** call `MintSessionAsync` and they do **not** fall through to per-request `SignMessageBase58`.
- `WarmUpSessionAsync` stays deferred (`first authenticated action will mint; boot/connect never signs`) so auto-resume/boot cannot re-open WO-1211.
- Purchase (`PurchaseQuoteService` / `PurchaseEntitlementVerifier`): live session → transfer only. Cold session → mint then transfer. Per-request signature remains as a purchase-only fallback if the session endpoint is down.

## Instrumentation (permanent)

`MintSessionAsync` logs `FlowTrace.Step` **before** the wallet sheet: `why` (`missing` / `expired` / `explicit-connect`), `scene`, `caller` (external frame + path). Query strings are stripped so a wallet never lands in a log. The wait path logs the same why/scene/caller when it refuses to sign.

Public seam `MintSessionForExplicitConnectAsync` exists for an explicit wallet-connect handshake. Auto-resume must keep calling `WarmUpSessionAsync`. LoginPanel is owned by WO-1249 and was not edited.

## Unchanged (constraints held)

- Transfer prompt always kept; no settlement-rail rewrite.
- Session TTL not extended; token still memory-only (never plaintext persist).
- `WALLET_MISMATCH` still refused client-side before attach (`wallet != playerId`) and server-side unchanged.
- Single-use nonces still burned on mint.
- No `TESTER_BUILD` bypass.
- `api/` not touched (no new Vercel deps; existing issue/reuse/expiry/mismatch coverage stays on `api/_lib/wallet-auth.js`).

## Files

- `Assets/_Modules/Core/Web3/BackendRequestSigner.cs` — brace-balanced (124), NUL=0.
- this RESULT.

Owner felt-test still required to close: CONTINUE and dungeon-enter show no SignMessage sheet; a purchase with a live session is transfer-only; a cold-session first purchase may still mint then transfer.
