# WORK ORDER 1317 — The published Pi app authenticated against TESTNET, and showed $SKR instead of Pi

**Status:** DONE
**Silo:** Web / Pi
**Minted:** 2026-09-02 (CLI) from the owner's report, diagnosed from captured telemetry.
**Severity:** P0 — authentication broken on a published, mainnet app.

## Owner report, verbatim

> **"it did before but now i cant get the authentication to work"**
> **"can you make sure the market lists as pi not as SKR"**
> Portal environment, confirmed by the owner: **Mainnet / production**.

## The captured evidence — CLAUDE.md sec.12, diagnosed from data, not from reading

Pulled from the live `analytics_events` `web_trace` sink via `/api/admin/db?view=traces`.
Three separate sessions on 2026-09-01 (10:37-10:42), all served from
`echoes-of-elarion.vercel.app`, build `2026.08.30.347462`:

```
[Title] log: [Flow:Pi] Signed in as samanthadenelle (uid bound to session).
[Title] log: [Flow:Pi]     PiInit(sandbox=True)
[Title] log: [Flow:Pi]     PiAuthenticate(scopes=username)
```

**Authentication succeeded, with `sandbox=True`.** `PiBridge.jslib` states what that means:
`sandbox != 0 -> Testnet sandbox`.

⚠ Also established from the same query, and it cleared an innocent suspect: **the newest trace
session in the entire sink is 2026-09-01 10:57.** There are ZERO sessions from 2026-09-02, so the
owner has not yet loaded the rotation build shipped that morning (WO-1312) — **its input shim cannot
be the cause of this regression**, which was the first theory and was wrong.

## Root cause 1 — a `[SerializeField]` that nothing serializes

`PiSignInController.cs:51` was `[SerializeField] private bool sandbox = true;` with the tooltip
*"Develop entirely on Testnet/Sandbox; flip off for mainnet go-live."*

**Nothing carries a serialized override.** `PiSignInController` appears in **no scene and no prefab**
(grepped `Assets/Scenes`, `Assets/Prefabs`, `Assets/Resources`; `Title.unity` has zero references) —
the component is added at runtime, so **the field initializer is what ships**. The `[SerializeField]`
attribute made it *look* configurable and hid the fact that the hardcoded `true` was production
behaviour.

Sandbox and mainnet are different Pi environments. Auth worked while the portal app was a sandbox
app, and broke the moment it moved to mainnet — while the client kept asking for testnet. The
tooltip had named the required action since the field was written; nothing enforced it.

**Fix:** build-driven initializer. Editor and `DEVELOPMENT_BUILD` keep sandbox so testnet stays
testable with no code edit; a ship build is mainnet. Deliberately NOT a runtime flag or PlayerPrefs —
the environment must be decided by the artifact, not by state a device can carry across builds.

## Root cause 2 — the currency skin rides the same broken signal

`CurrencySkinResolver.cs:274-289` routes the ENTIRE currency skin off one boolean
(WO-787 Part C, owner: *"if not Pi-facing should always be SKR"*): real Pi Browser = Pi skin,
anything else = SKR. That boolean is `WebGLPiPlatform.IsPiBrowserEnvironment` ->
`PiBridge.jslib`'s `PiIsPiBrowser()`, which tested **only** `/pibrowser/i` against the user agent.

Its own comment concedes the failure mode: *"Conservative by design — an unrecognised UA returns 0."*
An unrecognised Pi WebView therefore shows the player **$SKR inside Pi** — the owner's exact report.
⚠ SKR is Solana Mobile's governance token; it is not ours and must never front a Pi storefront.

**Fix:** add the HOST as a second signal. This repo already treats it as load-bearing fact in FIVE
files — `api/pi/verify.js`, `api/trace.js`, `api/events/track.js`, `api/bug-report.js`,
`api/game/save.js` all set CORS for exactly `<app>.pinet.com`, Pi's proxy. If we are served from
pinet.com we ARE the Pi deployment, whatever the WebView calls itself.

Matched as an exact host or a dotted suffix with the length derived from the literal — never a
substring (`indexOf('pinet.com')` would also match `pinet.com.evil.tld`) and never `endsWith` (ES6,
and this runs in whatever WebView Pi ships). Verified against 10 cases including that spoof and a
`notpinet.com` near-miss.

## What was NOT the cause — recorded so it is not re-theorised

- **The WO-1312 input shim.** No 09-02 sessions exist; the build was never loaded.
- **The backend.** `POST /api/pi/verify` with a junk token returns `{"success":false,"error":"pi /me
  returned 401"}` on BOTH production hosts — it is correctly forwarding to Pi and correctly refusing.
- **The validation key** (WO-1313), **R2/CORS** (WO-1314), **the landscape gate** (WO-1312 correction).

## Acceptance criteria

1. A ship WebGL build inits `sandbox=false`; an editor/dev build still inits `sandbox=true`.
2. Inside Pi (UA token OR a `*.pinet.com` host) the store shows **Pi**, never $SKR.
3. Outside Pi, WO-787 Part C still holds: non-Pi browsers resolve SKR.
4. **Proven from a captured `web_trace`, not from reasoning** — a post-deploy session must show
   `PiInit(sandbox=False)` followed by a successful `Signed in as ...`. Until that line exists this
   is unverified.

## What NOT to touch

- ⛔ Do not make the sandbox flag a runtime toggle, PlayerPrefs value or URL param. A device could
  then carry testnet into a mainnet session, which is this defect with extra steps.
- ⛔ Do not remove the UA check — keep BOTH signals. A Pi WebView served from a non-pinet host still
  needs to resolve as Pi.
- ⛔ Do not widen the host match to a substring. `pinet.com.evil.tld` must not match.
- ⛔ Do not change `CurrencySkinResolver`'s WO-787 routing itself; the routing is right, its input
  signal was too narrow.

---

# ✅ PROVEN 2026-09-02 — acceptance criterion 4 MET by a live capture

Session `wt-1454dc8bfaa4`, build `2026.09.02.352005@echoes-of-elarion.vercel.app`, in REAL Pi Browser
(`device='Unknown browser Unknown version'` — the same signature as the 09-01 Pi sessions):

```
[Flow:Skin] Pi Browser host detected - resolving the Pi skin.
[Flow:Skin] Currency skin resolved: 'pi' (auth=PiSdk, symbol=pi, identity=PiUid).
[Flow:Pi]   PiInit(sandbox=False)
[Flow:Pi]   PiAuthenticate(scopes=username)
[Flow:Pi]   Signed in as samanthadenelle (uid bound to session).
```

559 lines, **zero** Pi failures after sign-in. Both halves of this WO are now proven, not reasoned:
1. **The mainnet flip is CORRECT.** `PiInit(sandbox=False)` authenticated.
2. **The `.pinet.com` host signal works.** Skin resolved to `pi` / `PiUid`, not `$SKR`.

## ⛔ DO NOT "FIX" THIS BACK TO SANDBOX ON THE STRENGTH OF THE PORTAL BADGE

The Pi Developer Portal shows this app with a **`Testnet`** badge (owner screenshot, 2026-09-02,
alongside URL `https://echoes-of-elarion.vercel.app`, slug `echos-of-elarion-r9c5`, 8/10 steps).

**That badge and this capture disagree, and the CAPTURE WINS.** A mainnet init authenticated, live, in
Pi Browser, minutes after the badge was read. The badge evidently describes the app's LISTING /
ecosystem status, not the environment the SDK authenticates against.

I was one edit from reverting `PiEnvironment.Sandbox` to `true` on the strength of that label when
this capture landed. **Reverting it would have broken working authentication.** That is the whole
CLAUDE.md sec.12 lesson arriving on a label instead of a stack trace: a plausible-looking source of
truth is not evidence.

`PiEnvironment.Sandbox` stays build-driven (`false` in a ship build). **WO-1321's fallback is now
belt-and-braces, not the mechanism** — the first attempt wins, and the second never runs.

## What this closes and what it does not

CLOSED: Pi sign-in on the published build; the SKR-inside-Pi skin defect.
STILL OPEN: nothing in this WO. Payments (WO-1318) and ads (WO-1320) have their own proofs to earn.
