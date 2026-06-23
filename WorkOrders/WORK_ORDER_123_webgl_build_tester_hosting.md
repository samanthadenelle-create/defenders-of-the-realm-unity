# WORK ORDER 123 — WebGL Build + Tester Hosting

**Status:** READY — overnight CLI (build) + script-relay (hosting)
**Priority:** High — **unblocks testers** (the build-distribution bottleneck "all along"). Sequenced by
owner *after* zones + WO-104 castle for the *polished* host, but a **first testable build can come sooner**.
**Lanes:** WebGL build = CLI (here). Hosting/deploy = script-relay (owner runs in Vercel/itch, relays output).

---

## Why
Get a **hosted link** into testers' hands so they can run the `docs/qa` test cases without exe distribution
or the editor. One URL → anyone, any device, instant. Pairs with the **dev portal** (F1: set-level, +10k XP,
jump-to-state) = a complete tester toolkit. Also doubles as the **Pi pitch demo link** + public web channel.

## Reconciliation
`WORK_ORDER_09_webgl_build` is RESULT'd — **WebGL build infra exists** (`Assets/Editor/WebGLBuild.cs`). This
WO is *build it on the current green tree + host it*, not from scratch.

## Scope

### A. WebGL build (CLI, here)
- Build via the WebGL target (batchmode). Output = static `index.html` + Build/`.wasm`/`.data`/`.framework.js`.
- **Verify:** does it compile for WebGL (separate target — won't touch the desktop build), produce output,
  and what's the **payload size** (the gating metric)?
- **Known risks to surface:** Solana/wallet SDK generally **doesn't run on WebGL** → the web build is the
  **no-crypto / Stripe variant** (compile crypto OUT via the modular asmdefs, per NORTH_STAR 3-build plan);
  UI Toolkit / perf to verify; large `.data` payload may need a Brotli compression pass.

### B. Hosting (script-relay)
- **A SEPARATE Vercel project** from the backend one (separate concern, separate URL). Owner stands it up;
  CLI provides:
  - `vercel.json` with the **Unity WebGL headers** (`Content-Encoding` for compressed `.wasm`/`.data`,
    correct MIME types) — without these the browser won't load the build.
  - Deploy steps (push to the Vercel-watched repo/branch → auto-deploy).
- **Faster alt for testers NOW:** **itch.io** (purpose-built for WebGL; upload zip → instant link, no header
  config). Recommend itch for immediate tester access, Vercel as the polished long-term host. Can do both.

### C. Tester toolkit (already built)
Dev portal (F1) lets testers jump to any state for `docs/qa` cases. No new work — just document the URL + the
F1 shortcuts for testers.

## Acceptance
- [ ] WebGL build compiles + produces a loadable browser build (desktop build unaffected).
- [ ] Payload size measured; compression/slim pass if needed.
- [ ] Hosted at a link (itch and/or Vercel); `vercel.json` headers correct.
- [ ] Testers can reach the link + use the dev portal to run `docs/qa` cases.

## Sequencing
Owner: *after zones + WO-104 castle* for the polished host. **Flag:** a first "runs in a browser" build can
ship to testers **sooner** (parallel) to unblock them — owner's call on order.

🤖 Drafted by the build-connected CLI.
