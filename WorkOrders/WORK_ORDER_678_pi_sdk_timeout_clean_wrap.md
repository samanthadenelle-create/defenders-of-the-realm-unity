**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK ORDER 678 — Wrap the Pi SDK 120s promise timeout cleanly (mobile web)

**Status: READY TO IMPLEMENT** (owner report 2026-07-12: mobile devices show a raw JS error
"promise with id 0 timed out after 120000ms" — "the error is ok saying they timed out but we
should wrap it cleanly").
**Lane:** Platform / WebGL template + Pi bridge. **Type:** EXISTING (cosmetic/robustness — the
timeout is expected behavior; the raw surface is the defect).

## Read-only RCA (verified from source 2026-07-12)

1. **The message is NOT ours.** No "120000" / promise-id tracking exists anywhere in our code
   (repo-wide grep: zero hits in `.cs`/`.jslib`/template). It is the **Pi SDK's internal
   postMessage bridge** (`https://sdk.minepi.com/pi-sdk.js`, loaded in
   `Assets/WebGLTemplates/Pi/index.html:11`): the SDK tracks host-channel requests by promise id
   with a 120s timeout. Outside Pi Browser (any normal mobile browser) `window.Pi` EXISTS (the
   script loads anywhere) but the HOST channel never answers → every `Pi.init` call spawns a
   doomed promise that rejects at exactly 120s with this message.
2. **Our C# already times out cleanly long before** (`PiSignInController.cs:125` init 20s,
   `:140` authenticate 30s → retryable button, FlowTrace.Warn — good). So by the time the SDK's
   own 120s rejection fires, our flow has moved on; the rejection is a zombie.
3. **Why the player sees it:** two leak paths, both unowned today:
   a. The SDK's internal rejection can fire as a **global `unhandledrejection`** (it happens
      inside pi-sdk.js's own machinery, upstream of our `.catch`es in `PiBridge.jslib` — our
      catches only wrap the promises WE call). `index.html` (56 lines, read in full) installs
      **no `unhandledrejection`/`onerror` handler and no `config.showBanner`** — so the Unity
      loader's DEFAULT error surface (alert/banner) and/or the browser shows the raw text.
   b. When our `.catch` DOES get it (PiBridge.jslib:43/68 forward `{type:'error', message}` to
      C#), it arrives ~90-100s AFTER our C# timeout already settled/abandoned its TCS — verify
      what `WebGLPiPlatform.HandleCallback` does with a late/unmatched 'error' (if it
      `Debug.LogError`s, it lands in the break recorder + dev overlay as noise).
4. **Aggravator:** `PiIsAvailable` (PiBridge.jslib:26) returns true when `window.Pi` exists —
   which is TRUE in any browser once pi-sdk.js loads. So the auto-sign-in
   (`WaitForPiThenAutoSignIn`, PiSignInController.cs:~95) fires `Pi.init` on every normal mobile
   browser load → guarantees one doomed 120s promise per session even though sign-in correctly
   gave up at 20s.

**§12 proving line for CLI:** reproduce once on a normal mobile browser (or desktop devtools
mobile emulation) — capture the console `unhandledrejection` with the "Promise with id 0 timed
out after 120000 ms" text ~120s after load, alongside our earlier
`[Flow:Pi] Pi.init timed out after 20s` line. That pair proves the zombie-rejection chain.

## The fix (bounded — three small lanes)

**Lane A — own the global error surface (template).** In `Assets/WebGLTemplates/Pi/index.html`:
- Add `window.addEventListener('unhandledrejection', handler)` + `window.onerror`: if the
  message matches the known-benign signature (`/promise with id \d+ timed out/i`, plus the
  generic "not in Pi Browser" class), `preventDefault()`, log ONE quiet
  `console.info('[Pi] SDK host-channel timeout (expected outside Pi Browser) — suppressed')`,
  and forward to the Unity side if the instance exists (`SendMessage('PiBridge','OnPiCallback',
  …type:'error', where:'sdk-global'…)`) so it lands in OUR telemetry, not the player's face.
  Unknown errors still pass through (never blanket-swallow — §5 no-silent-failure).
- Provide `config.showBanner` that routes to console + (for genuine loader errors only) the
  existing `#unity-loading-bar` element — never a raw `alert()`.
- Mirror the same handler block into any sibling WebGL template used by the non-Pi build
  (check `Assets/WebGLTemplates/*` at implementation time).

**Lane B — quiet the late callback (C#).** `WebGLPiPlatform.HandleCallback`: a callback arriving
for an already-settled/abandoned TCS logs `FlowTrace.Warn("Pi", "late SDK callback after local
timeout — ignored (expected outside Pi Browser)")` — Warn, never Fail/LogError (keeps it out of
break-log.jsonl + the F8 recorder; this is expected noise, not a break).

**Lane C — stop creating the doomed promise where we can.** Gate the AUTO sign-in on a real
Pi-Browser environment check, not `window.Pi` presence: add `PiIsPiBrowser()` to the jslib
(UA contains "PiBrowser" — verify the exact token against the Pi docs at implementation — OR
a successful `Pi.init` race within our 20s window). Outside Pi Browser: skip auto-init, keep the
manual "Sign in with Pi" button (pressing it may still spawn one doomed promise — Lane A absorbs
it). This kills the guaranteed-per-session zombie without removing any capability.

## Acceptance

- [ ] Normal mobile browser: load → play 3+ minutes → NO visible error dialog/banner at any
      point; console shows the single suppressed-info line; sign-in button reads
      "Sign in with Pi" (manual, retryable).
- [ ] Pi Browser (owner/prod path): sign-in flow UNCHANGED — init/auth/verify all work; no new
      suppression swallows a real auth error (a genuine auth failure still reaches the UI).
- [ ] Late SDK callbacks log Warn-level `[Flow:Pi]` lines only — zero new entries in
      `break-log.jsonl` from this class.
- [ ] Unknown/unmatched JS errors still surface (throw a test error in console → visible).
- [ ] Proving lines quoted in the RESULT (pre-fix reproduction + post-fix clean run).
- [ ] `COMPILE_GATE_OK`; WebGL preview build deployed for owner mobile felt-pass (PO closes).

## What NOT to touch

- The 20s/30s C# timeouts (`PiSignInController`) — they are the correct primary guard (07-01 fix).
- `PiBridge.jslib` promise `.catch` forwarding — keep; Lane B only changes the C# reception.
- No blanket `try/catch` around the Unity loader; no swallowing of non-matching errors.
- Production Vercel stays untouched — preview only until the owner promotes (standing rule).

*Cross-refs:* `PiSignInController.cs` (:117-146 timeout design + root-cause comment) ·
`PiBridge.jslib` · `Assets/WebGLTemplates/Pi/index.html` · WO-603 (identity binding — unaffected) ·
`docs/TICKET_PIPELINE.md` rule 0.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
