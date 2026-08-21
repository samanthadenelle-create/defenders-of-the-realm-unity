<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-341 — Backend: auth token refresh + expiry handling

**Status:** CLOSED — DEPRECATED, audit-verified obsolete (2026-08-21 backlog audit).

**Depends on:** WO-120 (backend spec done), WO-80 (Vercel + Neon set up)

**Lane:** 7 (Persistence/Backend)

---

## Summary

Authentication tokens (issued at login via WO-80) have a short lifetime (15min). This WO implements the refresh-token flow so players don't get booted mid-session. Includes expiry detection and silent re-auth.

---

## Files to edit

- `Assets/_Modules/Core/Backend/AuthTokenManager.cs` (new file)
  - Store `accessToken`, `refreshToken`, `expiresAt`
  - Method `RefreshToken()` → POST `/api/auth/refresh` with refreshToken, get new access token
  - Method `IsTokenExpired()` → check time vs. expiresAt
  - On refresh failure: log error, set auth state to "expired" (triggers re-login flow)
- `Assets/_Modules/Core/Backend/BackendService.cs`
  - Hook: before every API call, check `IsTokenExpired()` and refresh silently if needed
  - Pass Authorization header with current token
- `Assets/_Modules/Core/Persistence/GameState.cs`
  - Add `CurrentAuthToken` property (bound by AuthTokenManager)
  - Add `IsAuthenticated` property

---

## Acceptance criteria

- [ ] RefreshToken() POST succeeds and updates expiry timestamp
- [ ] Silent refresh happens before API calls (no visible player delay)
- [ ] Expired token triggers re-login (modal / boot to login screen)
- [ ] On refresh failure, error is logged + user is notified
- [ ] No plaintext token storage in PlayerPrefs (use secure storage or memory-only)
- [ ] Brace balance check passes
- [ ] No System.Reflection

---

## What NOT to do

- Do NOT implement social login yet (scope: token refresh only)
- Do NOT create leaderboard API calls (that's WO-129)
- Do NOT edit UI (errors logged only; modal triggering is separate)

---

## Notes

Refresh is handled server-side (Vercel endpoint). Client just calls it when needed. Tokens should NEVER be stored in plain PlayerPrefs.

> **AUDIT 2026-08-21 (agent fleet, read-only):** DEPRECATED. Evidence: `BackendAuthConfig.cs:1-25` — wallet-signed headers; no token flow exists. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
