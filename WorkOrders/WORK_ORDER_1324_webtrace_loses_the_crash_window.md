# WORK ORDER 1324 — WebTrace discards the last 5 seconds, which is exactly the crash

**Status:** READY TO IMPLEMENT
**Silo:** Diagnostics / Web
**Minted:** 2026-09-02 (CLI) after an owner crash that the trace could not explain.
**Severity:** P1 diagnostic — every web crash is currently unexplainable by construction.

## The event that exposed it

> **"I was in and building a town and it was landscape. When I tried adding archer tower it crashed
> and unity restarted"**

What the sink DID capture, in `wt-53818b5604ca`:
```
[Flow:VfxPerfGate] HITCH 128792.0ms (budget 16.7ms) at loop occupancy 0/24, occupancy delta +0.
  The loop count did not rise - a stall from outside the VFX pool.
```
**A 128-second frozen frame.** On a mobile WebView that is the browser killing the tab, which the
player experiences as "it restarted".

What the sink did NOT capture: **the crash itself, or anything in the ~5 s before it.**

## Root cause of the diagnostic gap

`Assets/_Modules/Core/Diagnostics/WebTrace.cs:81-83`
```csharp
private const int   FlushThreshold = 50;    // flush early once this many queue
private const float FlushSeconds   = 5f;    // ...otherwise flush on this cadence
private const int   MaxBatch       = 200;
```
and `:33` — *"On failure the batch is DROPPED (no retry)."*

So up to 5 seconds of lines sit in a RAM ring when the tab dies, and they die with it. **The window
we most need is the only window guaranteed to be lost.** A 128 s stall then a kill means the ring
holds the entire interesting sequence and posts none of it.

⚠ This is the same class as the gates that report success without proving it: an instrument whose
blind spot is exactly the event it exists to record.

## What to build

1. **Flush on the way out.** The Pi template already listens for `pagehide` and `visibilitychange`
   (`Assets/WebGLTemplates/Pi/index.html`) — hang a final flush off those, and off `beforeunload`.
   Use `navigator.sendBeacon` for that last post: it is the only send that survives teardown; a normal
   fetch/XHR is cancelled. `api/trace.js` already accepts a plain JSON POST, so the beacon payload
   needs no new endpoint — verify the content-type it will send is acceptable.
2. **Flush on a hitch.** `VfxPerfGate` already detects and reports the stall. When a frame exceeds a
   large threshold, force a flush immediately rather than waiting out the cadence — a 128 s frame is
   25 missed flush windows.
3. **Do NOT simply lower `FlushSeconds` globally.** That multiplies POST volume for every player to
   serve a rare event, and the owner's sessions already produce thousands of lines. Flush on the two
   EVENTS instead.

## Acceptance criteria

1. Killing the tab mid-session lands the final batch in `analytics_events`. Prove it by killing a tab
   and reading the sink — not by reasoning about `sendBeacon`.
2. A frame over the hitch threshold forces a flush, and the lines around it survive.
3. Normal-session POST volume does not measurably rise.
4. A dropped batch is still reported rather than silently swallowed (`:33` currently drops with no retry).

## What NOT to touch

- ⛔ Do not raise `MaxBatch` to compensate; a bigger lost batch is a bigger loss.
- ⛔ Do not add a blocking send anywhere on the frame path.
- ⛔ Do not change the trace ENDPOINT. It is absolute on purpose: under Pi the app is served through
  `<app>.pinet.com`, so a relative URL posts to Pi's proxy instead of Vercel.
- ⛔ Do not weaken or strip `VfxPerfGate`'s hitch reporting. It is the only reason we know a 128 s
  stall happened at all.

## Follow-on, NOT this ticket

Once the window is captured, the archer-tower crash gets its own RCA. Do not guess at it here.
Ruled out already, with data: the watchtower content is present and correct on WebGL (all 4 objects
the catalog names verified on R2, 2.35/0.96/0.96/0.06 MB), R2 parity is green, and the bundles are
small. The leading untested hypothesis remains WO-1314 (a 512 MB WebGL heap), which that ticket
requires be PROVEN by capture rather than assumed.
