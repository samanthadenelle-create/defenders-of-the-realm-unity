# Pi Network auth — what's built + the morning checklist

**Status (2026-06-28, overnight):** all CODE for Pi auth is written + compile-targeted. What
remains is the perf gate, your Pi App Studio registration, and hosting the auth build on a
controlled domain. itch is for the PERF test only.

## What's implemented (this repo)
- `Assets/Plugins/WebGL/PiBridge.jslib` — Unity↔Pi-JS bridge. `Pi.init` awaited as a Promise
  before `authenticate`; wraps `authenticate` / `createPayment` / `Ads`; returns via
  `SendMessage("PiBridge","OnPiCallback", json)`.
- `Assets/_Modules/Core/Platform/` — `IPiPlatform` seam, `WebGLPiPlatform` (real) /
  `EditorPiPlatform` (stub) / `PiPlatform` resolver, and `PiSignInController`:
  **auto-triggers on load inside Pi Browser + shows a "Sign in with Pi" button**; flow =
  `Init → Authenticate(['username']) → POST /api/pi/verify → session`.
- `api/pi/verify.js` — Vercel function: validates the access token via
  `GET https://api.minepi.com/v2/me` (Bearer, **no API key**) before establishing a session.
  Returns `{ success, uid, username }`.
- `Assets/WebGLTemplates/Pi/` — WebGL template that loads `pi-sdk.js` (so `window.Pi` exists)
  + `validation-key.txt` (placeholder).

## The sequence
1. **Perf gate (itch is fine):** open the itch WebGL build in **Pi Browser** on your phone.
   Loads + plays at acceptable speed? → proceed. (If it chokes at ~186 MB → land WO-545
   Addressables shrink, re-test. itch sandbox is OK here — no Pi validation needed for perf.)
2. **Register in Pi App Studio (`develop.pi`):** create the app, declare the hosting domain,
   copy the **validation key**.
3. **Paste the key** into `Assets/WebGLTemplates/Pi/validation-key.txt` (replace the
   placeholder). It is copied to the build root on build.
4. **Build with the Pi template:** Build Settings → Player → Resolution and Presentation →
   WebGL Template → **Pi**. Build WebGL.
5. **Host on a domain you control** — ⚠️ **NOT itch** (its sandbox subdomain can't serve
   `validation-key.txt` at the domain root Pi checks). Use **Vercel**
   (`defenders-of-the-realm-v2.vercel.app`, where `/api/pi/verify` already lives) or your own domain.
6. **Test:** open the hosted URL in Pi Browser → auto sign-in (or tap the button) →
   `Pi.init → authenticate → /verify → "Pi: <username>"`.

## Notes
- `sandbox=true` on `PiSignInController` (Testnet) until mainnet go-live — flip when ready.
- Payments (`createPayment`) ride the same bridge but use the `pi-backend/` Cloudflare Worker
  (`/approve` + `/complete`, which DO need `PI_API_KEY`). Auth (`/verify`) needs no key.
- Off Pi Browser everything is an inert stub — desktop/itch/Editor play is unchanged.
