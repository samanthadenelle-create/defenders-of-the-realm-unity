# WORK ORDER 687 — Web-trace read / triage surface (admin read path + pre-boot loader-error beacon)

**Status: READY TO IMPLEMENT.** **Lane:** Persistence/Backend (Lane 7) + Platform/WebGL template (Lane
10). **Type:** EXISTING read surface (extend) + a small NEW pre-boot beacon. **Silo:** `api/admin/` +
`tools/db-viewer/` + `Assets/WebGLTemplates/Pi/index.html` (file-disjoint from Unity C# lanes).

**Numbering note:** minted **687** from the `CLI_LANES_WO_NUMBERS.md` banner (part of the 685/686/687
web-trace lifecycle trio; next-free bumped to 688 in the same edit).

## Symptom

Two gaps at the READ / ingress ends of the web-trace lifecycle:
1. **Blind spot before Unity boots.** WebTrace only exists once the Unity runtime is alive
   (`WebTrace.cs:108` `[RuntimeInitializeOnLoadMethod]`). Any failure **before** that — a WASM/framework
   load failure, an OOM, a CDN/COEP fault, a black-screen loader hang — produces **zero telemetry**. The
   most demo-killing class of web failure is exactly the one the pipe can't see.
2. **The read/triage surface is built but undocumented as the canonical triage path.** `api/admin/db.js`
   already exposes a `traces` view + `tools/db-viewer/` renders it, but there is no WO tying the
   web-trace lifecycle's READ end to it, and no beacon feeding it the pre-boot failures.

## RCA / evidence (cited)

- **`WebTrace.cs:108-127`** — `Install()` runs `RuntimeInitializeLoadType.BeforeSceneLoad` — i.e. only
  after the Unity player has loaded and started. Nothing captures a failure *before* that point.
- **`WebTrace.cs:259` (`#if UNITY_WEBGL && !UNITY_EDITOR`)** — the remote POST only exists inside the
  running player; the loader shell (`index.html`) is outside it entirely.
- **`api/admin/db.js:23-25, 170-201`** — the `traces` view is ALREADY BUILT: `?view=traces` (latest
  web_trace sessions summary) and `?view=traces&session=<id>` (that session's lines). Admin-key gated
  (`:57-64`), read-only, parameterized. This is the read path WO-687 formalizes as the triage surface.
- **`api/admin/db.js:128-168`** — the `metrics` view already computes `web_trace` error-line counts per
  day (`trace_error_lines_per_day`) — the triage dashboard's aggregate.
- **`tools/db-viewer/`** — the local HTML the owner double-clicks (per `api/admin/db.js:4`) renders
  these views; it is the human surface.
- **`api/trace.js:22-25`** — the endpoint already accepts a raw `lines` array / `{lines:[...]}` shape
  and stores it as a `web_trace` row — so a tiny JS beacon can POST to the SAME endpoint with no server
  change (the loader beacon reuses the existing sink).
- **`docs/SECURITY_AUDIT_2026-07-12.md:12`** — the retention cutoff (WO-685) prunes at 7 days; the read
  surface's 7-day windows (`db.js:136,147,157,196`) already align to that TTL.

## Exact steps

**Lane A — formalize + verify the admin read path (mostly built):**
1. Confirm `api/admin/db.js` `?view=traces` and `?view=traces&session=<id>` + `?view=metrics` return
   the web_trace read/aggregate as documented; add any missing field the triage view needs (e.g. a
   signal-only line filter matching `api/trace.js:64`'s `isSignal` regex so triage sees error lines
   first). Keep it **read-only, admin-key-gated, parameterized, 200|400|500**.
2. Document `tools/db-viewer/` "Traces" tab as the canonical web-trace triage surface in the RESULT (the
   §14 F8 live-triage loop reads captured lines; this is the persisted-DB counterpart).

**Lane B — pre-boot loader-error beacon (the new gap):**
3. In `Assets/WebGLTemplates/Pi/index.html`, add a tiny inline JS beacon (no bundler, no import) that,
   BEFORE/AROUND the Unity loader, installs `window.onerror` + `window.onunhandledrejection` +
   the Unity loader's `onError`/`onProgress`-timeout hook, and on a pre-boot failure does ONE
   `fetch('https://…/api/trace', { method:'POST', keepalive:true, body: JSON.stringify({ session, build,
   lines:[ '[loader] ' + msg ] }) })` — fire-and-forget, reusing the existing `/api/trace` sink shape
   (`api/trace.js` already accepts `{lines:[...]}`; NO server change).
   - Guard it so the beacon can never itself throw or block the loader; dedupe (fire once per failure
     class); include a `loader`/pre-boot tag so triage can distinguish it from in-runtime traces.
   - It rides the WO-685 retention + WO-686 caps automatically (same table, same endpoint).
4. Keep the WO-678 `showBanner` wrapper (`Pi/index.html`) as the single **player-visible** surface — the
   beacon is telemetry-only, it does not add a second on-screen error surface (respects WO-682).

**Lane C — regression / verify:**
5. Simulate a pre-boot failure (e.g. force the loader `onError`) on the preview build and confirm a
   `[loader]` web_trace row lands and shows in the db-viewer Traces tab. Browser check on the Vercel
   preview per memory `mobile-tickets-verify-on-preview-device` (Windows won't repro web-load faults).

## Acceptance

- [ ] `api/admin/db.js` `?view=traces` / `&session=<id>` / `?view=metrics` verified returning web_trace
      reads + the per-day error-line aggregate; any triage-filter field added is admin-gated + read-only.
- [ ] `tools/db-viewer/` Traces tab documented as the canonical persisted web-trace triage surface.
- [ ] A forced pre-boot loader error produces exactly ONE `[loader]`-tagged `web_trace` row via
      `/api/trace` (no server change), visible in the db-viewer; the loader never hangs or double-fires.
- [ ] No new player-visible error surface (WO-678 banner stays the only one; WO-682 quiet-catch honored).
- [ ] Verified on a Vercel preview in a browser (not Windows editor).

## What NOT to touch

- `api/trace.js` server logic (the beacon reuses the existing `{lines:[...]}` shape — no endpoint change).
- The WO-678 Pi-timeout / `showBanner` player-visible wrapper (extend around it; do not add a 2nd surface).
- The WebTrace.cs in-runtime sink (this WO adds the PRE-runtime beacon; the runtime pipe is unchanged).
- Admin-key gating / read-only construction of `api/admin/db.js` (never widen it to a write or unauth path).

*Proof source: `WebTrace.cs`, `api/admin/db.js`, `api/trace.js`, `tools/db-viewer/`,
`docs/SECURITY_AUDIT_2026-07-12.md`, `Assets/WebGLTemplates/Pi/index.html` as cited.*
